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
    private static readonly ConcurrentDictionary<string, IMotelySearchContext> _activeSearches = new();

    // Cached immutable values (computed once, reused forever)
    private static string? _cachedVersion;
    private static string[]? _cachedFeatures;

    // ──────────────────────────────── JS Push Callbacks ([JSImport]) ────────────────────────────────
    // C# calls these to push progress/results to JS without polling.
    // JS registers the actual handlers on globalThis before starting a search.
    // Native primitive marshaling -- no JSON on the hot path.

    [JSImport("globalThis.__motelyOnProgress")]
    static partial void JsPushProgress(
        string searchId,
        double totalSeedsSearched,
        double matchingSeeds,
        double elapsedMs,
        int resultCount);

    [JSImport("globalThis.__motelyOnResult")]
    static partial void JsPushResult(
        string searchId,
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
                Threads = options?.ThreadCount ?? Math.Max(1, Environment.ProcessorCount - 1),
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

            var context = MotelySearchOrchestrator.LaunchWithContext(
                config, parameters, useInMemoryStorage: true);

            var searchId = context.SearchId;
            _activeSearches[searchId] = context;

            // Start search on background web workers
            var worker = MotelySearchPlatform.CreateWorker(() =>
            {
                try { context.Start(); }
                catch (Exception ex) { Console.Error.WriteLine($"Search {searchId} failed: {ex.Message}"); }
            });
            worker.Start();

            // Push progress to JS until search completes.
            // await Task.Delay yields the main thread back to the JS event loop (maps to setTimeout).
            int lastResultCount = 0;
            while (context.Status == MotelySearchStatus.Running)
            {
                await Task.Delay(500);

                // Push progress with native primitive marshaling (no JSON)
                try
                {
                    JsPushProgress(
                        searchId,
                        context.TotalSeedsSearched,
                        context.MatchingSeeds,
                        context.ElapsedTime.TotalMilliseconds,
                        context.ResultCount);

                    // Push new individual results since last update
                    int currentCount = context.ResultCount;
                    if (currentCount > lastResultCount)
                    {
                        var newResults = context.GetResults(lastResultCount, currentCount - lastResultCount);
                        foreach (var r in newResults)
                            JsPushResult(searchId, r.Seed, r.Score);
                        lastResultCount = currentCount;
                    }
                }
                catch
                {
                    // JS callback may not be registered; continue search regardless
                }
            }

            // Return final status JSON when Promise resolves
            return BuildStatusJson(searchId, context);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    /// <summary>
    /// Get status + top results for an active search. Returns JSON.
    /// Available for on-demand queries (e.g. user clicks "show results").
    /// </summary>
    public static string GetSearchStatus(string searchId, int resultLimit)
    {
        if (!_activeSearches.TryGetValue(searchId, out var context))
            return ErrorJson($"Unknown search: {searchId}");

        try
        {
            return BuildStatusJson(searchId, context, resultLimit);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }

    [JSExport]
    public static Task<string> GetSearchStatusAsync(string searchId, int resultLimit) =>
        Task.FromResult(GetSearchStatus(searchId, resultLimit));

    /// <summary>
    /// Stop a running search. Non-blocking (sets cancellation flag).
    /// </summary>
    public static void StopSearch(string searchId)
    {
        if (_activeSearches.TryGetValue(searchId, out var context))
        {
            context.Cancel();
        }
    }

    [JSExport]
    public static Task StopSearchAsync(string searchId)
    {
        StopSearch(searchId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dispose a completed/stopped search and free memory.
    /// Returns Task (Promise on JS side) that resolves when cleanup is done.
    /// </summary>
    [JSExport]
    public static async Task DisposeSearch(string searchId)
    {
        if (_activeSearches.TryRemove(searchId, out var context))
        {
            context.Cancel();
            await context.WaitForCompletionAsync();
            context.Dispose();
        }
    }

    // ──────────────────────────────── Helpers ────────────────────────────────

    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(new ErrorDto { Error = message },
            MotelyAotJsonContext.Default.ErrorDto);

    private static string BuildStatusJson(string searchId, IMotelySearchContext context, int resultLimit = 50)
    {
        var limit = resultLimit > 0 ? resultLimit : 50;
        var results = context.GetTopResults(limit);

        var dto = new SearchStatusDto
        {
            SearchId = searchId,
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
