using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.BrowserWasm;

/// <summary>
/// All [JSExport] methods for the Motely WASM npm package.
/// Async push-based: StartJamlSearch returns Task (Promise), progress pushed via [JSImport].
/// Uses MotelyAotJsonContext for AOT-safe serialization.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class MotelyWasmExports
{
    // SINGLE search only - no dictionary needed for Blueprint
    private static IMotelySearchContext? _currentSearch;
    private static readonly object _searchLock = new object();
    private static readonly ConcurrentQueue<(string Seed, int Score)> _resultQueue = new();

    // Cached immutable values (computed once, reused forever)
    private static string? _cachedVersion;
    private static string[]? _cachedFeatures;

    // ──────────────────────────────── JS Push Callbacks ([JSImport]) ────────────────────────────────
    // C# calls these to push progress/results to JS without polling.
    // JS registers the actual handlers on globalThis before starting a search.
    // Native primitive marshaling -- no JSON on the hot path.

    [JSImport("globalThis.__motelyOnProgress")]
    [return: JSMarshalAs<JSType.Discard>]
    static partial void JsPushProgress(
        double totalSeedsSearched,
        double matchingSeeds,
        double elapsedMs,
        int resultCount);

    [JSImport("globalThis.__motelyOnResult")]
    [return: JSMarshalAs<JSType.Discard>]
    static partial void JsPushResult(
        string seed,
        int score);

    // ──────────────────────────────── Version / Capabilities ────────────────────────────────
    // Async exports required: with threading enabled, JS cannot call synchronous C# methods.

    [JSExport]
    public static Task<string> GetVersionAsync()
    {
        var dto = new VersionDto
        {
            Version = GetCachedVersion(),
            Runtime = "browser-wasm",
            Features = GetFeatureList(),
        };
        return Task.FromResult(JsonSerializer.Serialize(dto, MotelyAotJsonContext.Default.VersionDto));
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

    public static bool IsThreadingEnabled() => Thread.CurrentThread.ManagedThreadId >= 0
        && Environment.ProcessorCount > 0; // Will be > 1 if threads are enabled

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
                    MotelyAotJsonContext.Default.ErrorDto);

            if (!Enum.TryParse<MotelyStake>(stake, ignoreCase: true, out var stakeEnum))
                return JsonSerializer.Serialize(new ErrorDto { Error = $"Unknown stake: {stake}" },
                    MotelyAotJsonContext.Default.ErrorDto);

            var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(cfg);

            if (!string.IsNullOrEmpty(analysis.Error))
                return JsonSerializer.Serialize(new ErrorDto { Error = analysis.Error },
                    MotelyAotJsonContext.Default.ErrorDto);

            var dto = MapAnalysisToDto(analysis, seed, deck, stake);
            return JsonSerializer.Serialize(dto, MotelyAotJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorDto { Error = ex.Message },
                MotelyAotJsonContext.Default.ErrorDto);
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
            var config = ConfigFormatConverter.LoadFromJamlString(jamlContent);
            if (config == null)
            {
                return JsonSerializer.Serialize(
                    new ValidateResultDto { Valid = false, Error = "Failed to parse JAML" },
                    MotelyAotJsonContext.Default.ValidateResultDto);
            }

            return JsonSerializer.Serialize(
                new ValidateResultDto
                {
                    Valid = true,
                    Name = config.Name,
                    Deck = config.Deck,
                    Stake = config.Stake,
                },
                MotelyAotJsonContext.Default.ValidateResultDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ValidateResultDto { Valid = false, Error = ex.Message },
                MotelyAotJsonContext.Default.ValidateResultDto);
        }
    }

    [JSExport]
    public static Task<string> ValidateJamlAsync(string jamlContent) =>
        Task.FromResult(ValidateJaml(jamlContent));

    // ──────────────────────────────── Search (async push-based) ────────────────────────────────

    /// <summary>
    /// Start a JAML search. Returns Task&lt;string&gt; (Promise on JS side).
    /// C# pushes progress to JS via [JSImport] callbacks. Promise resolves with final status JSON.
    /// </summary>
    [JSExport]
    public static async Task<string> StartJamlSearch(string jamlContent, string optionsJson)
    {
        try
        {
            var config = ConfigFormatConverter.LoadFromJamlString(jamlContent);
            if (config == null)
                return ErrorJson("Failed to parse JAML filter");

            // Parse optional search options
            SearchOptionsDto? options = null;
            if (!string.IsNullOrEmpty(optionsJson) && optionsJson != "{}")
            {
                options = JsonSerializer.Deserialize(optionsJson,
                    MotelyAotJsonContext.Default.SearchOptionsDto);
            }

            var parameters = new JsonSearchParams
            {
                // Threads are enabled via WasmEnableThreads in .csproj and threads: "on" in loadMotely.
                // We use the requested thread count from options, defaulting to 4.
                Threads = options?.ThreadCount ?? 4,
                BatchSize = options?.BatchSize ?? 4,
                Cutoff = options?.Cutoff != null ? int.Parse(options.Cutoff) : 0,
                Quiet = true,
                NoFancy = true,
            };

            if (options?.StartBatch.HasValue == true)
                parameters.StartBatch = (ulong)options.StartBatch.Value;
            if (options?.EndBatch.HasValue == true)
                parameters.EndBatch = (ulong)options.EndBatch.Value;
            if (options?.SpecificSeed != null)
                parameters.SpecificSeed = options.SpecificSeed;
            if (options?.RandomSeeds.HasValue == true)
                parameters.RandomSeeds = options.RandomSeeds.Value;
            if (options?.Palindrome == true)
                parameters.PalindromeSeeds = true;

            if (!string.IsNullOrEmpty(config.Deck))
                parameters.Deck = config.Deck;
            if (!string.IsNullOrEmpty(config.Stake))
                parameters.Stake = config.Stake;

            // Cancel any existing search first (only one at a time)
            StopSearch();

            parameters.ResultCallback = result =>
            {
                _resultQueue.Enqueue((result.Seed, (int)result.Score));
            };

            var context = MotelySearchOrchestrator.LaunchWithContext(
                config, parameters, useInMemoryStorage: true);

            lock (_searchLock)
            {
                _currentSearch = context;
            }

            // Start search directly — internal threads are already launched by the constructor.
            // Do NOT wrap in another Task.Run; that's redundant double-indirection.
            context.Start();

            // Main-thread drain loop: read shared memory counters + drain result queue.
            // Workers never call [JSImport] — they only write to ConcurrentQueue and atomic counters.
            // 15ms cadence (~66 FPS) — fast progress updates without starving browser event loop.
            var completionTask = context.WaitForCompletionAsync();
            while (!completionTask.IsCompleted)
            {
                DrainResultQueue();
                try { JsPushProgress(context.TotalSeedsSearched, context.MatchingSeeds, context.ElapsedTime.TotalMilliseconds, context.ResultCount); }
                catch { }
                await Task.Delay(15);
            }
            // Final drain + progress push
            DrainResultQueue();
            try { JsPushProgress(context.TotalSeedsSearched, context.MatchingSeeds, context.ElapsedTime.TotalMilliseconds, context.ResultCount); }
            catch { }

            return BuildStatusJson(context);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
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
            _currentSearch?.Cancel();
        }
    }

    /// <summary>
    /// Dispose the current search and free memory.
    /// No searchId needed - only one search runs at a time.
    /// </summary>
    [JSExport]
    public static async Task DisposeSearch()
    {
        IMotelySearchContext? context;
        lock (_searchLock)
        {
            context = _currentSearch;
            _currentSearch = null;
        }

        if (context != null)
        {
            context.Cancel();
            await context.WaitForCompletionAsync();
            context.Dispose();
        }
    }

    // ──────────────────────────────── Result Queue Drain ────────────────────────────────

    /// <summary>
    /// Drain queued results from worker threads and push to JS.
    /// Called from main thread only — safe to call [JSImport].
    /// </summary>
    private static void DrainResultQueue()
    {
        while (_resultQueue.TryDequeue(out var r))
        {
            try { JsPushResult(r.Seed, r.Score); }
            catch { }
        }
    }

    // ──────────────────────────────── Helpers ────────────────────────────────

    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(new ErrorDto { Error = message },
            MotelyAotJsonContext.Default.ErrorDto);

    private static string BuildStatusJson(IMotelySearchContext context)
    {
        var results = context.GetTopResults(50);

        var dto = new SearchStatusDto
        {
            Status = context.Status.ToString(),
            IsRunning = context.Status == MotelySearchStatus.Running,
            TotalSeedsSearched = context.TotalSeedsSearched,
            MatchingSeeds = context.MatchingSeeds,
            ResultCount = context.ResultCount,
            ElapsedMs = (long)context.ElapsedTime.TotalMilliseconds,
            Results = results.Select(r => new SearchHitDto
            {
                Seed = r.Seed,
                Score = r.Score,
                Tallies = r.Tallies?.ToArray(),
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
