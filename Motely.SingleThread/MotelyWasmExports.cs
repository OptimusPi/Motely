using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using CapabilitiesDto = global::Motely.CapabilitiesDto;
using ErrorDto = global::Motely.ErrorDto;
using SearchHitDto = global::Motely.SearchHitDto;
using SearchOptionsDto = global::Motely.SearchOptionsDto;
using SearchStatusDto = global::Motely.SearchStatusDto;
using ValidateResultDto = global::Motely.ValidateResultDto;
using VersionDto = global::Motely.VersionDto;

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
    private static readonly ConcurrentQueue<SearchHitDto> _resultQueue = new();

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
        return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.VersionDto));
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
            JsonSerializer.Serialize(dto, MotelyJsonContext.Default.CapabilitiesDto)
        );
    }

    public static bool IsSimdEnabled() => System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;

    public static bool IsThreadingEnabled() => false; // SingleThread: threading disabled by design (WasmEnableThreads=false)

    public static int GetAvailableThreadCount() => 1;

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
                    MotelyJsonContext.Default.ErrorDto
                );

            if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown stake: {stake}" },
                    MotelyJsonContext.Default.ErrorDto
                );

            var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(cfg);

            if (!string.IsNullOrEmpty(analysis.Error))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = analysis.Error },
                    MotelyJsonContext.Default.ErrorDto
                );

            var dto = MapAnalysisToDto(analysis, seed, deckEnum, stakeEnum);
            return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message },
                MotelyJsonContext.Default.ErrorDto
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
                    MotelyJsonContext.Default.ValidateResultDto
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
                MotelyJsonContext.Default.ValidateResultDto
            );
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ValidateResultDto { Valid = false, Error = ex.Message },
                MotelyJsonContext.Default.ValidateResultDto
            );
        }
    }

    [JSExport]
    public static Task<string> ValidateJamlAsync(string jamlContent) =>
        Task.FromResult(ValidateJaml(jamlContent));

    // ──────────────────────────────── Search (non-blocking, JS polls status) ────────────────────────────────

    [JSExport]
    public static async Task<string> StartJamlSearch(
        string jamlContent,
        string optionsJson,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>]
        Action<long, long, long> onProgress,
        [JSMarshalAs<JSType.Function<JSType.String, JSType.Number>>]
        Action<string, int> onResult)
    {
        try
        {
            if (
                !JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null
            )
                return ErrorJson(parseError ?? "Failed to parse JAML filter");

            SearchOptionsDto? options = null;
            if (!string.IsNullOrEmpty(optionsJson) && optionsJson != "{}")
            {
                options = JsonSerializer.Deserialize(
                    optionsJson,
                    MotelyJsonContext.Default.SearchOptionsDto
                );
            }

            if (options == null)
                return ErrorJson("Search options are required. Provide threadCount and batchCharCount.");

            if (!options.ThreadCount.HasValue || options.ThreadCount.Value < 1)
                return ErrorJson("Invalid options.threadCount. Provide an integer >= 1.");

            if (
                !options.BatchCharCount.HasValue
                || options.BatchCharCount.Value < 1
                || options.BatchCharCount.Value > 7
            )
                return ErrorJson("Invalid options.batchCharCount. Provide an integer in range 1..7.");

            if (options.ThreadCount.Value > 1 && !IsThreadingEnabled())
            {
                return ErrorJson(
                    "This is a single-threaded WASM build. threadCount must be 1."
                );
            }

            if (options.ThreadCount.Value > Environment.ProcessorCount)
            {
                return ErrorJson(
                    $"Invalid options.threadCount. Requested {options.ThreadCount.Value}, but runtime reports {Environment.ProcessorCount} available processor(s)."
                );
            }

            // Cancel any existing search first (only one at a time)
            StopSearch();

            _drainedResults.Clear();
            _resultQueue.Clear();

            var (request, requestError) = MotelySearchRequestFactory.FromOptions(
                options,
                options.ThreadCount.Value,
                options.BatchCharCount.Value
            );
            if (requestError != null || request == null)
                return ErrorJson(requestError ?? "Search request could not be created.");

            var (plan, filterId, prepareError) = MotelySearchOrchestrator.PrepareSearch(
                config,
                request
            );
            if (prepareError != null || plan == null || filterId == null)
                return ErrorJson(prepareError ?? "Search could not be prepared.");

            var settings = plan.Settings.WithProgressCallback(prog =>
            {
                onProgress(
                    prog.SeedsSearched,
                    prog.MatchingSeeds,
                    (long)prog.ElapsedTime.TotalMilliseconds
                );
            });

            if (plan.ShouldClauseCount > 0)
            {
                settings = settings.WithScoredResultCallback(tally =>
                {
                    var hit = CreateSearchHit(plan.ShouldLabels, tally);
                    onResult(hit.Seed, hit.Score);
                    _resultQueue.Enqueue(hit);
                });
            }
            else
            {
                settings = settings.WithSeedMatchCallback(seed =>
                {
                    var hit = new SearchHitDto
                    {
                        Seed = seed,
                        Score = 0,
                        Tallies = [],
                    };
                    onResult(hit.Seed, hit.Score);
                    _resultQueue.Enqueue(hit);
                });
            }

            var cts = new CancellationTokenSource();
            var search = settings.CreateSearch();

            lock (_searchLock)
            {
                _currentSearch = search;
                _currentCts = cts;
                _currentFilterId = filterId;
            }

            string finalStatus;
            try
            {
                search.Start(cts.Token);
                DrainResultQueue();
                finalStatus = BuildStatusJson(search, filterId);
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

            return finalStatus;
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
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
    private static readonly List<SearchHitDto> _drainedResults = new();

    private static void DrainResultQueue()
    {
        while (_resultQueue.TryDequeue(out var r))
        {
            _drainedResults.Add(r);
        }
    }

    // ──────────────────────────────── Helpers ────────────────────────────────

    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(
            new ErrorDto { Error = message },
            MotelyJsonContext.Default.ErrorDto
        );

    private static string BuildStatusJson(IMotelySearch search, string? filterId = null)
    {
        var results = _drainedResults.ToArray();

        var dto = new SearchStatusDto
        {
            FilterId = filterId ?? _currentFilterId ?? string.Empty,
            Status = search.IsCompleted ? "Completed" : "Running",
            IsRunning = !search.IsCompleted,
            TotalSeedsSearched = search.TotalSeedsSearched,
            MatchingSeeds = search.MatchingSeeds,
            ElapsedMs = (long)search.ElapsedTime.TotalMilliseconds,
            ResultCount = results.Length,
            Results = results,
        };

        return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.SearchStatusDto);
    }

    private static SearchHitDto CreateSearchHit(
        string[] shouldLabels,
        MotelySeedScoreTally tally
    )
    {
        return new SearchHitDto
        {
            Seed = tally.Seed,
            Score = tally.Score,
            Tallies = BuildTallies(shouldLabels, tally),
        };
    }

    private static string[] BuildTallies(
        string[] shouldLabels,
        MotelySeedScoreTally tally
    )
    {
        if (shouldLabels.Length == 0 || tally.TallyCount == 0)
            return [];

        int count = Math.Min(shouldLabels.Length, tally.TallyCount);
        var tallies = new string[count];
        for (int i = 0; i < count; i++)
            tallies[i] = $"{shouldLabels[i]}={tally.GetTally(i)}";

        return tallies;
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
        if (IsThreadingEnabled())
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
