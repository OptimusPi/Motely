using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Filters;

namespace Motely.BrowserWasm;

/// <summary>
/// All [JSExport] methods for the Motely WASM npm package.
/// Async push-based: StartJamlSearch returns Task (Promise), progress pushed via [JSImport].
/// Uses WasmJsonContext for AOT-safe serialization.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    // SINGLE search only - no dictionary needed for Blueprint
    private static IMotelySearch? _currentSearch;
    private static CancellationTokenSource? _currentCts;
    private static readonly object _searchLock = new object();
    private static readonly ConcurrentQueue<(string Seed, int Score)> _resultQueue = new();

    // Cached immutable values (computed once, reused forever)
    private static string? _cachedVersion;
    private static string[]? _cachedFeatures;

    // ──────────────────────────────── Version / Capabilities ────────────────────────────────

    [JSExport]
    public static Task<string> GetVersionAsync()
    {
        var dto = new VersionDto
        {
            Version = GetCachedVersion(),
            Runtime = "browser-wasm",
            Features = GetFeatureList(),
        };
        return Task.FromResult(JsonSerializer.Serialize(dto, WasmJsonContext.Default.VersionDto));
    }

    [JSExport]
    public static Task<string> GetCapabilitiesAsync()
    {
        var dto = new CapabilitiesDto
        {
            Simd = IsSimdEnabled(),
            Threads = IsThreadingEnabled(),
            ProcessorCount = GetProcessorCount(),
            Runtime = "browser-wasm",
            Version = GetCachedVersion(),
            Timestamp = DateTime.UtcNow.ToString("O"),
        };
        return Task.FromResult(JsonSerializer.Serialize(dto, WasmJsonContext.Default.CapabilitiesDto));
    }

    public static bool IsSimdEnabled()
    {
#if NET10_0_OR_GREATER
        return System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;
#else
        return false;
#endif
    }

    public static bool IsThreadingEnabled() => Environment.ProcessorCount > 1;

    public static int GetProcessorCount() => Environment.ProcessorCount;

    // ──────────────────────────────── Analyzer ────────────────────────────────

    /// <summary>
    /// Analyze a single seed. Returns JSON SeedAnalysisDto.
    /// </summary>
    public static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            if (!Enum.TryParse<MotelyDeck>(deck, ignoreCase: true, out var deckEnum))
                return JsonSerializer.Serialize(new ErrorDto { Error = $"Unknown deck: {deck}" },
                    WasmJsonContext.Default.ErrorDto);

            if (!Enum.TryParse<MotelyStake>(stake, ignoreCase: true, out var stakeEnum))
                return JsonSerializer.Serialize(new ErrorDto { Error = $"Unknown stake: {stake}" },
                    WasmJsonContext.Default.ErrorDto);

            var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(cfg);

            if (!string.IsNullOrEmpty(analysis.Error))
                return JsonSerializer.Serialize(new ErrorDto { Error = analysis.Error },
                    WasmJsonContext.Default.ErrorDto);

            var dto = MapAnalysisToDto(analysis, seed, deck, stake);
            return JsonSerializer.Serialize(dto, WasmJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorDto { Error = ex.Message },
                WasmJsonContext.Default.ErrorDto);
        }
    }

    [JSExport]
    public static Task<string> AnalyzeSeedAsync(string seed, string deck, string stake) =>
        Task.FromResult(AnalyzeSeed(seed, deck, stake));

    // ──────────────────────────────── JAML Validation ────────────────────────────────

    /// <summary>
    /// Validate a JAML string. Returns JSON ValidateResultDto.
    /// </summary>
    public static string ValidateJaml(string jamlContent)
    {
        try
        {
            if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null)
            {
                return JsonSerializer.Serialize(
                    new ValidateResultDto { Valid = false, Error = parseError ?? "Failed to parse JAML" },
                    WasmJsonContext.Default.ValidateResultDto);
            }

            return JsonSerializer.Serialize(
                new ValidateResultDto
                {
                    Valid = true,
                    Name = config.Name,
                    Deck = config.Deck.ToString(),
                    Stake = config.Stake.ToString(),
                },
                WasmJsonContext.Default.ValidateResultDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ValidateResultDto { Valid = false, Error = ex.Message },
                WasmJsonContext.Default.ValidateResultDto);
        }
    }

    [JSExport]
    public static Task<string> ValidateJamlAsync(string jamlContent) =>
        Task.FromResult(ValidateJaml(jamlContent));

    // ──────────────────────────────── Search (non-blocking, JS polls status) ────────────────────────────────

    /// <summary>
    /// Start a JAML search. Returns immediately with initial status JSON.
    /// JS polls progress via GetSearchStatus(). No blocking drain loop.
    /// </summary>
    [JSExport]
    public static Task<string> StartJamlSearch(string jamlContent, string optionsJson)
    {
        try
        {
            if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null)
                return Task.FromResult(ErrorJson(parseError ?? "Failed to parse JAML filter"));

            // Parse search options (required for explicit runtime behavior under AOT)
            SearchOptionsDto? options = null;
            if (!string.IsNullOrEmpty(optionsJson) && optionsJson != "{}")
            {
                options = JsonSerializer.Deserialize(optionsJson,
                    WasmJsonContext.Default.SearchOptionsDto);
            }

            if (options == null)
                return Task.FromResult(ErrorJson("Search options are required. Provide threadCount and batchSize."));

            if (!options.ThreadCount.HasValue || options.ThreadCount.Value < 1)
                return Task.FromResult(ErrorJson("Invalid options.threadCount. Provide an integer >= 1."));

            if (!options.BatchSize.HasValue || options.BatchSize.Value < 1 || options.BatchSize.Value > 7)
                return Task.FromResult(ErrorJson("Invalid options.batchSize. Provide an integer in range 1..7."));

            if (options.ThreadCount.Value > 1 && !IsThreadingEnabled())
            {
                return Task.FromResult(ErrorJson(
                    "WASM threading is not active at runtime. Ensure COOP/COEP headers are set and the loader enables threads."
                ));
            }

            if (options.ThreadCount.Value > Environment.ProcessorCount)
            {
                return Task.FromResult(ErrorJson(
                    $"Invalid options.threadCount. Requested {options.ThreadCount.Value}, but runtime reports {Environment.ProcessorCount} available processor(s)."
                ));
            }

            // Cancel any existing search first (only one at a time)
            StopSearch();

            var settings = JamlSearchBuilder.CreateSettings(config)
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(options.ThreadCount.Value)
                .WithBatchCharacterCount(options.BatchSize.Value)
                .WithSeedMatchCallback(seed => _resultQueue.Enqueue((seed, 0)));

            if (options.StartBatch.HasValue)
                settings = settings.WithStartBatchIndex(options.StartBatch.Value);
            if (options.EndBatch.HasValue)
                settings = settings.WithEndBatchIndex(options.EndBatch.Value);

            if (options.SpecificSeed != null)
                settings = settings.WithListSearch([options.SpecificSeed], 1);
            else if (options.RandomSeeds.HasValue)
                settings = settings.WithRandomSearch(options.RandomSeeds.Value);
            else if (options.Palindrome == true)
                settings = settings.WithPalindromeSearch();
            else
                settings = settings.WithSequentialSearch();

            var cts = new CancellationTokenSource();
            var search = settings.Start(cts.Token);

            lock (_searchLock)
            {
                _currentSearch = search;
                _currentCts = cts;
            }

            // Return initial status — JS will poll GetSearchStatus() for updates.
            return Task.FromResult(BuildStatusJson(search));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorJson(ex.Message));
        }
    }

    /// <summary>
    /// Poll current search progress. Drains result queue and returns status JSON.
    /// JS calls this on setInterval (e.g. every 1s). No JSInterop on the hot path.
    /// </summary>
    [JSExport]
    public static Task<string> GetSearchStatus()
    {
        IMotelySearch? search;
        lock (_searchLock)
        {
            search = _currentSearch;
        }

        if (search == null)
            return Task.FromResult(ErrorJson("No active search"));

        // Drain any queued results into the status response
        DrainResultQueue();
        return Task.FromResult(BuildStatusJson(search));
    }

    /// <summary>
    /// Stop the current running search. Non-blocking (sets cancellation flag).
    /// No searchId needed - only one search runs at a time.
    /// </summary>
    [JSExport]
    public static void StopSearch()
    {
        lock (_searchLock)
        {
            _currentCts?.Cancel();
        }
    }

    /// <summary>
    /// Dispose the current search and free memory.
    /// No searchId needed - only one search runs at a time.
    /// </summary>
    [JSExport]
    public static async Task DisposeSearch()
    {
        IMotelySearch? search;
        CancellationTokenSource? cts;
        lock (_searchLock)
        {
            search = _currentSearch;
            cts = _currentCts;
            _currentSearch = null;
            _currentCts = null;
        }

        if (search != null)
        {
            cts?.Cancel();
            await search.WaitForCompletionAsync();
            search.Dispose();
        }
    }

    // ──────────────────────────────── Result Queue ────────────────────────────────

    /// <summary>
    /// Drain queued results from worker threads into _drainedResults for inclusion in status JSON.
    /// </summary>
    private static readonly List<(string Seed, int Score)> _drainedResults = new();

    private static void DrainResultQueue()
    {
        while (_resultQueue.TryDequeue(out var r))
        {
            _drainedResults.Add(r);
        }
    }

    // ──────────────────────────────── Helpers ────────────────────────────────

    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(new ErrorDto { Error = message },
            WasmJsonContext.Default.ErrorDto);

    private static string BuildStatusJson(IMotelySearch search)
    {
        var dto = new SearchStatusDto
        {
            Status = search.IsCompleted ? "Completed" : "Running",
            IsRunning = !search.IsCompleted,
            TotalSeedsSearched = search.TotalSeedsSearched,
            MatchingSeeds = search.MatchingSeeds,
            ResultCount = _drainedResults.Count,
            ElapsedMs = (long)search.ElapsedTime.TotalMilliseconds,
            Results = _drainedResults.Select(r => new SearchHitDto
            {
                Seed = r.Seed,
                Score = r.Score,
                Tallies = [],
            }).ToArray(),
        };

        return JsonSerializer.Serialize(dto, WasmJsonContext.Default.SearchStatusDto);
    }

    private static string GetCachedVersion() =>
        _cachedVersion ??= typeof(MotelyWasmExports).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private static string[] GetFeatureList()
    {
        if (_cachedFeatures is not null) return _cachedFeatures;
        var features = new List<string> { "analyzer", "jaml-search", "jaml-validate" };
        if (IsSimdEnabled()) features.Add("simd");
        if (IsThreadingEnabled()) features.Add("threads");
        _cachedFeatures = features.ToArray();
        return _cachedFeatures;
    }

    private static SeedAnalysisDto MapAnalysisToDto(
        MotelySeedAnalysis analysis, string seed, string deck, string stake)
    {
        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck,
            Stake = stake,
            Error = analysis.Error,
            ErraticDeckComposition = analysis.ErraticDeckComposition?.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
            Antes = analysis.Antes.Select(a => new AnteAnalysisDto
            {
                Ante = a.Ante,
                Boss = a.Boss.ToString(),
                Voucher = a.Voucher.ToString(),
                SmallBlindTag = a.SmallBlindTag.ToString(),
                BigBlindTag = a.BigBlindTag.ToString(),
                DrawOrder = a.DrawOrder ?? "",
                ShopQueue = a.ShopQueue.Select(item => new ShopItemDto
                {
                    Id = item.Type.ToString(),
                    Name = item.ToString(),
                }).ToArray(),
                Packs = a.Packs.Select(p => new PackDto
                {
                    Type = p.Type.ToString(),
                    Items = p.Items.Select(i => i.ToString()).ToArray(),
                }).ToArray(),
            }).ToArray(),
        };
    }
}
