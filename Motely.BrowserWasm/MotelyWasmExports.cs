using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using CapabilitiesDto = global::Motely.CapabilitiesDto;
using ErrorDto = global::Motely.ErrorDto;
using ProgressCallbackDto = global::Motely.ProgressCallbackDto;
using SearchHitDto = global::Motely.SearchHitDto;
using ValidateResultDto = global::Motely.ValidateResultDto;
using VersionDto = global::Motely.VersionDto;
using SearchOptionsDto = global::Motely.SearchOptionsDto;
using SearchStatusDto = global::Motely.SearchStatusDto;

namespace Motely.BrowserWasm;

/// <summary>
/// All [JSExport] methods for the Motely WASM npm package.
/// Async push-based: StartJamlSearch returns Task (Promise), progress pushed via [JSImport].
/// Uses MotelyJsonContext for AOT-safe serialization.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    // SINGLE search only - no dictionary needed for Blueprint
    private static IMotelySearch? _currentSearch;
    private static CancellationTokenSource? _currentCts;
    private static string? _currentFilterId;
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
        return Task.FromResult(JsonSerializer.Serialize(dto, global::Motely.MotelyJsonContext.Default.VersionDto));
    }

    [JSExport]
    public static Task<string> GetCapabilitiesAsync()
    {
        var dto = new CapabilitiesDto
        {
            Simd = IsSimdEnabled(),
            Threads = IsThreadingEnabled(),
            AvailableThreadCount = GetAvailableThreadCount(),
            ProcessorCount = GetProcessorCount(),
            Runtime = "browser-wasm",
            Version = GetCachedVersion(),
            Timestamp = DateTime.UtcNow.ToString("O"),
        };
        return Task.FromResult(
            JsonSerializer.Serialize(dto, global::Motely.MotelyJsonContext.Default.CapabilitiesDto)
        );
    }

    public static bool IsSimdEnabled() => System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;

    public static bool IsThreadingEnabled() => GetAvailableThreadCount() > 1;

    public static int GetAvailableThreadCount() => Environment.ProcessorCount;

    public static int GetProcessorCount() => Environment.ProcessorCount;

    // ──────────────────────────────── Analyzer ────────────────────────────────

    /// <summary>
    /// Analyze a single seed. Returns JSON SeedAnalysisDto.
    /// </summary>
    public static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown deck: {deck}" },
                    global::Motely.MotelyJsonContext.Default.ErrorDto
                );

            if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown stake: {stake}" },
                    global::Motely.MotelyJsonContext.Default.ErrorDto
                );

            var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(cfg);

            if (!string.IsNullOrEmpty(analysis.Error))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = analysis.Error },
                    global::Motely.MotelyJsonContext.Default.ErrorDto
                );

            var dto = MapAnalysisToDto(analysis, seed, deckEnum, stakeEnum);
            return JsonSerializer.Serialize(dto, global::Motely.MotelyJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message },
                global::Motely.MotelyJsonContext.Default.ErrorDto
            );
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
            if (
                !JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null
            )
            {
                return JsonSerializer.Serialize(
                    new ValidateResultDto
                    {
                        Valid = false,
                        Error = parseError ?? "Failed to parse JAML",
                    },
                    global::Motely.MotelyJsonContext.Default.ValidateResultDto
                );
            }

            return JsonSerializer.Serialize(
                new ValidateResultDto
                {
                    Valid = true,
                    Name = config.Name,
                    Deck = config.Deck.ToString(),
                    Stake = config.Stake.ToString(),
                },
                global::Motely.MotelyJsonContext.Default.ValidateResultDto
            );
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ValidateResultDto { Valid = false, Error = ex.Message },
                global::Motely.MotelyJsonContext.Default.ValidateResultDto
            );
        }
    }

    [JSExport]
    public static Task<string> ValidateJamlAsync(string jamlContent) =>
        Task.FromResult(ValidateJaml(jamlContent));

    // ──────────────────────────────── Search (non-blocking, JS polls status) ────────────────────────────────

    [JSExport]
    public static Task<string> StartJamlSearch(
        string jamlContent,
        string optionsJson,
        [JSMarshalAs<JSType.Function<JSType.String>>]
        Action<string> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>]
        Action<string, int> onResult)
    {
        try
        {
            if (
                !JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null
            )
                return Task.FromResult(ErrorJson(parseError ?? "Failed to parse JAML filter"));

            // Parse search options (required for explicit runtime behavior under AOT)
            global::Motely.SearchOptionsDto? options = null;
            if (!string.IsNullOrEmpty(optionsJson) && optionsJson != "{}")
            {
                var parsedOptions = JsonSerializer.Deserialize(
                    optionsJson,
                    global::Motely.MotelyJsonContext.Default.SearchOptionsDto
                );

                if (parsedOptions != null)
                {
                    options = new global::Motely.SearchOptionsDto
                    {
                        ThreadCount = parsedOptions.ThreadCount,
                        BatchCharCount = parsedOptions.BatchCharCount,
                        Cutoff = parsedOptions.Cutoff,
                        StartBatch = parsedOptions.StartBatch,
                        EndBatch = parsedOptions.EndBatch,
                        SpecificSeed = parsedOptions.SpecificSeed,
                        Seeds = parsedOptions.Seeds,
                        Keyword = parsedOptions.Keyword,
                        RandomSeeds = parsedOptions.RandomSeeds,
                        Palindrome = parsedOptions.Palindrome,
                    };
                }
            }

            if (options == null)
                return Task.FromResult(ErrorJson("Search options are required. Provide threadCount and batchCharCount."));

            if (!options.ThreadCount.HasValue || options.ThreadCount.Value < 1)
                return Task.FromResult(ErrorJson("Invalid options.threadCount. Provide an integer >= 1."));

            if (
                !options.BatchCharCount.HasValue
                || options.BatchCharCount.Value < 1
                || options.BatchCharCount.Value > 7
            )
                return Task.FromResult(ErrorJson("Invalid options.batchCharCount. Provide an integer in range 1..7."));

            // Browser WASM: force single-thread (no thread pool in browser by default)
            int threadCount = 1;
            if (options.ThreadCount.Value > 1 && IsThreadingEnabled())
                threadCount = Math.Min(options.ThreadCount.Value, Environment.ProcessorCount);

            if (threadCount > Environment.ProcessorCount)
            {
                return Task.FromResult(ErrorJson(
                    $"Invalid options.threadCount. Requested {threadCount}, but runtime reports {Environment.ProcessorCount} available processor(s)."
                ));
            }

            // Cancel any existing search first (only one at a time)
            StopSearch();

            _resultQueue.Clear();

            var settings = JamlSearchBuilder
                .CreateSettings(config)
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(options.ThreadCount.Value)
                .WithBatchCharacterCount(options.BatchCharCount.Value)
                .WithSeedMatchCallback(seed =>
                {
                    onResult(seed, 0);
                    _resultQueue.Enqueue((seed, 0));
                })
                .WithProgressCallback(prog =>
                {
                    onProgress(JsonSerializer.Serialize(new ProgressCallbackDto
                    {
                        SeedsSearched = prog.SeedsSearched,
                        MatchingSeeds = prog.MatchingSeeds,
                        ElapsedMs = (long)prog.ElapsedTime.TotalMilliseconds,
                        ResultCount = _resultQueue.Count,
                    }, global::Motely.MotelyJsonContext.Default.ProgressCallbackDto));
                });

            if (options.StartBatch.HasValue)
                settings = settings.WithStartBatchIndex(options.StartBatch.Value);
            if (options.EndBatch.HasValue)
                settings = settings.WithEndBatchIndex(options.EndBatch.Value);

            if (options.SpecificSeed != null)
                settings = settings.WithListSearch([options.SpecificSeed], 1);
            else if (options.Seeds is { Length: > 0 })
                settings = settings.WithListSearch(options.Seeds);
            else if (!string.IsNullOrEmpty(options.Keyword))
            {
                string kw = options.Keyword.ToUpperInvariant();
                int padLen = MotelyCore.MaxSeedLength - kw.Length;
                if (padLen < 0)
                    return Task.FromResult(ErrorJson($"Keyword '{kw}' is too long (max {MotelyCore.MaxSeedLength} chars)."));
                settings = settings.WithListSearch(MotelyCore.GeneratePaddedSeeds(kw, padLen));
            }
            else if (options.RandomSeeds.HasValue)
                settings = settings.WithRandomSearch(options.RandomSeeds.Value);
            else if (options.Palindrome == true)
                settings = settings.WithPalindromeSearch();
            else
                settings = settings.WithSequentialSearch();

            var cts = new CancellationTokenSource();
            var search = settings.CreateSearch();
            var filterId = MotelyRuntimeIds.GenerateFilterId(config);

            lock (_searchLock)
            {
                _currentSearch = search;
                _currentCts = cts;
                _currentFilterId = filterId;
            }

            try
            {
                search.Start(cts.Token);
            }
            finally
            {
                lock (_searchLock)
                {
                    _currentSearch = null;
                    _currentCts = null;
                    _currentFilterId = null;
                }
            }

            return Task.FromResult(BuildStatusJson(search));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ErrorJson(ex.Message));
        }
    }

    /// <summary>
    /// Poll current search progress. Returns status JSON.
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
            try
            {
                // Wait for graceful shutdown with timeout
                await Task.WhenAny(
                    search.WaitForCompletionAsync(),
                    Task.Delay(TimeSpan.FromSeconds(5))
                );
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation works
            }
            catch
            {
                // Ignore other errors during shutdown
            }
            search.Dispose();
        }
    }

    // ──────────────────────────────── Helpers ────────────────────────────────

    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(
            new ErrorDto { Error = message },
            global::Motely.MotelyJsonContext.Default.ErrorDto
        );

    private static string BuildStatusJson(IMotelySearch search)
    {
        var results = _resultQueue
            .Select(r => new global::Motely.SearchHitDto
            {
                Seed = r.Seed,
                Score = r.Score,
                Tallies = [],
            })
            .ToArray();

        var dto = new global::Motely.SearchStatusDto
        {
            FilterId = _currentFilterId ?? string.Empty,
            Status = search.IsCompleted ? "Completed" : "Running",
            IsRunning = !search.IsCompleted,
            TotalSeedsSearched = search.TotalSeedsSearched,
            MatchingSeeds = search.MatchingSeeds,
            ResultCount = results.Length,
            ElapsedMs = (long)search.ElapsedTime.TotalMilliseconds,
            Results = results,
        };

        return JsonSerializer.Serialize(dto, global::Motely.MotelyJsonContext.Default.SearchStatusDto);
    }

    private static string GetCachedVersion() =>
        _cachedVersion ??=
            MotelyBuildVersion.For(typeof(MotelyWasmExports).Assembly);

    private static string[] GetFeatureList()
    {
        if (_cachedFeatures is not null)
            return _cachedFeatures;
        var features = new List<string> { "analyzer", "jaml-search", "jaml-validate" };
        if (IsSimdEnabled())
            features.Add("simd");
        if (GetAvailableThreadCount() > 1)
            features.Add("threads");
        _cachedFeatures = features.ToArray();
        return _cachedFeatures;
    }

    private static SeedAnalysisDto MapAnalysisToDto(
        MotelySeedAnalysis analysis,
        string seed,
        MotelyDeck deck,
        MotelyStake stake
    )
    {
        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck.ToString(),
            Stake = stake.ToString(),
            Error = analysis.Error,
            ErraticDeckComposition =
                analysis.ErraticDeckComposition?.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ) ?? [],
            Antes = analysis
                .Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = a.Boss.ToString(),
                    Voucher = a.Voucher.ToString(),
                    SmallBlindTag = a.SmallBlindTag.ToString(),
                    BigBlindTag = a.BigBlindTag.ToString(),
                    DrawOrder = a.DrawOrder ?? "",
                    ShopQueue = a
                        .ShopQueue.Select(item => new ShopItemDto
                        {
                            Id = item.Type.ToString(),
                            Name = item.ToString(),
                        })
                        .ToArray(),
                    Packs = a
                        .Packs.Select(p => new PackDto
                        {
                            Type = p.Type.ToString(),
                            Items = p.Items.Select(i => i.ToString()).ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}
