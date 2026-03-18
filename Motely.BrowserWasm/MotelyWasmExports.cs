using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using CapabilitiesDto = global::Motely.CapabilitiesDto;
using ErrorDto = global::Motely.ErrorDto;
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
    private static readonly ConcurrentQueue<SearchHitDto> _resultQueue = new();

    // Cached immutable values (computed once, reused forever)
    private static string? _cachedVersion;

    // ──────────────────────────────── Version / Capabilities ────────────────────────────────

    [JSExport]
    public static string GetVersion() => GetCachedVersion();

    [JSExport]
    public static string GetRuntime() => "browser-wasm";

    [JSExport]
    public static bool IsSimdEnabled() => MotelyRuntime.IsSimdEnabled();

    [JSExport]
    public static bool IsThreadingEnabled() => GetAvailableThreadCount() > 1;

    [JSExport]
    public static int GetAvailableThreadCount() => Environment.ProcessorCount;

    [JSExport]
    public static int GetProcessorCount() => Environment.ProcessorCount;

    // ──────────────────────────────── Analyzer ────────────────────────────────

    /// <summary>
    /// Analyze a single seed. Returns JSON SeedAnalysisDto.
    /// </summary>
    public static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            var dto = MotelyRuntime.AnalyzeSeed(seed, deck, stake);
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

    // ──────────────────────────────── Streaming (Cursor) ────────────────────────────────

    /// <summary>
    /// Stream Lucky Money results with cursor pattern.
    /// state = -1 → start fresh. state = savedDouble → resume.
    /// Returns JSON { results: bool[], nextState: double }.
    /// </summary>
    [JSExport]
    public static Task<string> StreamLuckyMoneyAsync(
        string seed, string deck, string stake,
        double state, int take, double baseLuck)
    {
        try
        {
            if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
                return Task.FromResult(JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown deck: {deck}" },
                    MotelyJsonContext.Default.ErrorDto));

            if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                return Task.FromResult(JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown stake: {stake}" },
                    MotelyJsonContext.Default.ErrorDto));

            // state == -1 means fresh start (JS can't pass null for double)
            double? cursorState = state < 0 ? null : state;

            var (results, nextState) = MotelySeedStreamer.StreamLuckyMoney(
                seed, deckEnum, stakeEnum, cursorState, take, baseLuck);

            var dto = new LuckyMoneyStreamDto
            {
                Results = results,
                NextState = nextState,
            };
            return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.LuckyMoneyStreamDto));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message },
                MotelyJsonContext.Default.ErrorDto));
        }
    }

    [JSExport]
    public static Task<string> StreamLuckyMultAsync(
        string seed, string deck, string stake,
        double state, int take, double baseLuck)
    {
        return StreamBoolEvent(seed, deck, stake, state, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamLuckyMult(s, d, st, cs, t, baseLuck));
    }

    [JSExport]
    public static Task<string> StreamMisprintAsync(
        string seed, string deck, string stake,
        double state, int take)
    {
        try
        {
            if (!TryParseEnums(deck, stake, out var deckEnum, out var stakeEnum, out var error))
                return Task.FromResult(error);

            double? cursorState = state < 0 ? null : state;
            var (results, nextState) = MotelySeedStreamer.StreamMisprint(
                seed, deckEnum, stakeEnum, cursorState, take);

            var dto = new IntStreamDto { Results = results, NextState = nextState };
            return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.IntStreamDto));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message }, MotelyJsonContext.Default.ErrorDto));
        }
    }

    [JSExport]
    public static Task<string> StreamCavendishAsync(
        string seed, string deck, string stake,
        double state, int take, double baseLuck)
    {
        return StreamBoolEvent(seed, deck, stake, state, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamCavendish(s, d, st, cs, t, baseLuck));
    }

    [JSExport]
    public static Task<string> StreamGrosMichelAsync(
        string seed, string deck, string stake,
        double state, int take, double baseLuck)
    {
        return StreamBoolEvent(seed, deck, stake, state, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamGrosMichel(s, d, st, cs, t, baseLuck));
    }

    [JSExport]
    public static Task<string> StreamErraticDeckAsync(
        string seed, string deck, string stake,
        double state, int take)
    {
        return StreamStringEvent(seed, deck, stake, state, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamErraticDeck(s, d, st, cs, t));
    }

    [JSExport]
    public static Task<string> StreamWheelOfFortuneAsync(
        string seed, string deck, string stake,
        double state, int take, double baseLuck)
    {
        return StreamStringEvent(seed, deck, stake, state, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamWheelOfFortune(s, d, st, cs, t, baseLuck));
    }

    // ── Tier 2: Per-ante streams ──

    [JSExport]
    public static Task<string> StreamTagsAsync(
        string seed, string deck, string stake,
        int ante, double state, int take)
    {
        return StreamStringEvent(seed, deck, stake, state, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamTags(s, d, st, ante, cs, t));
    }

    [JSExport]
    public static Task<string> StreamBoosterPacksAsync(
        string seed, string deck, string stake,
        int ante, double state, bool generatedFirstPack, int take)
    {
        try
        {
            if (!TryParseEnums(deck, stake, out var deckEnum, out var stakeEnum, out var error))
                return Task.FromResult(error);

            double? cursorState = state < 0 ? null : state;
            var (results, nextState, nextGenerated) = MotelySeedStreamer.StreamBoosterPacks(
                seed, deckEnum, stakeEnum, ante, cursorState, generatedFirstPack, take);

            var dto = new PackStreamDto
            {
                Results = results,
                NextState = nextState,
                GeneratedFirstPack = nextGenerated,
            };
            return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.PackStreamDto));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message }, MotelyJsonContext.Default.ErrorDto));
        }
    }

    [JSExport]
    public static Task<string> StreamVouchersAsync(
        string seed, string deck, string stake,
        int ante, int voucherBitfield, double state, int take)
    {
        return StreamStringEvent(seed, deck, stake, state, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamVouchers(s, d, st, ante, voucherBitfield, cs, t));
    }

    // ── Stream helpers ──

    private static bool TryParseEnums(string deck, string stake,
        out MotelyDeck deckEnum, out MotelyStake stakeEnum, out string error)
    {
        if (!Enum.TryParse<MotelyDeck>(deck, true, out deckEnum))
        {
            error = JsonSerializer.Serialize(
                new ErrorDto { Error = $"Unknown deck: {deck}" }, MotelyJsonContext.Default.ErrorDto);
            stakeEnum = default;
            return false;
        }
        if (!Enum.TryParse<MotelyStake>(stake, true, out stakeEnum))
        {
            error = JsonSerializer.Serialize(
                new ErrorDto { Error = $"Unknown stake: {stake}" }, MotelyJsonContext.Default.ErrorDto);
            return false;
        }
        error = "";
        return true;
    }

    private static Task<string> StreamBoolEvent(
        string seed, string deck, string stake, double state, int take,
        Func<string, MotelyDeck, MotelyStake, double?, int, (bool[] Results, double NextState)> streamer)
    {
        try
        {
            if (!TryParseEnums(deck, stake, out var deckEnum, out var stakeEnum, out var error))
                return Task.FromResult(error);

            double? cursorState = state < 0 ? null : state;
            var (results, nextState) = streamer(seed, deckEnum, stakeEnum, cursorState, take);

            var dto = new LuckyMoneyStreamDto { Results = results, NextState = nextState };
            return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.LuckyMoneyStreamDto));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message }, MotelyJsonContext.Default.ErrorDto));
        }
    }

    private static Task<string> StreamStringEvent(
        string seed, string deck, string stake, double state, int take,
        Func<string, MotelyDeck, MotelyStake, double?, int, (string[] Results, double NextState)> streamer)
    {
        try
        {
            if (!TryParseEnums(deck, stake, out var deckEnum, out var stakeEnum, out var error))
                return Task.FromResult(error);

            double? cursorState = state < 0 ? null : state;
            var (results, nextState) = streamer(seed, deckEnum, stakeEnum, cursorState, take);

            var dto = new StringStreamDto { Results = results, NextState = nextState };
            return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.StringStreamDto));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message }, MotelyJsonContext.Default.ErrorDto));
        }
    }

    // ── Tier 3: Multi-cursor streams ──

    [JSExport]
    public static Task<string> StreamTarotAsync(
        string seed, string deck, string stake,
        int ante, string source, string stateJson, int take)
    {
        return StreamItemEvent(seed, deck, stake, stateJson, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamTarot(s, d, st, ante, source, cs, t));
    }

    [JSExport]
    public static Task<string> StreamPlanetAsync(
        string seed, string deck, string stake,
        int ante, string source, string stateJson, int take)
    {
        return StreamItemEvent(seed, deck, stake, stateJson, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamPlanet(s, d, st, ante, source, cs, t));
    }

    [JSExport]
    public static Task<string> StreamSpectralAsync(
        string seed, string deck, string stake,
        int ante, string source, string stateJson, int take)
    {
        return StreamItemEvent(seed, deck, stake, stateJson, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamSpectral(s, d, st, ante, source, cs, t));
    }

    [JSExport]
    public static Task<string> StreamStandardCardsAsync(
        string seed, string deck, string stake,
        int ante, int flags, string stateJson, int take)
    {
        return StreamItemEvent(seed, deck, stake, stateJson, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamStandardCards(s, d, st, ante, flags, cs, t));
    }

    [JSExport]
    public static Task<string> StreamJokersAsync(
        string seed, string deck, string stake,
        int ante, string source, int flags, string stateJson, int take)
    {
        return StreamItemEvent(seed, deck, stake, stateJson, take,
            (s, d, st, cs, t) => MotelySeedStreamer.StreamJokers(s, d, st, ante, source, flags, cs, t));
    }

    // ── Stream helpers ── (multi-cursor)

    private static Task<string> StreamItemEvent(
        string seed, string deck, string stake, string stateJson, int take,
        Func<string, MotelyDeck, MotelyStake, double[]?, int, (string[] Results, double[] NextState)> streamer)
    {
        try
        {
            if (!TryParseEnums(deck, stake, out var deckEnum, out var stakeEnum, out var error))
                return Task.FromResult(error);

            double[]? cursorState = string.IsNullOrEmpty(stateJson) || stateJson == "null"
                ? null
                : JsonSerializer.Deserialize(stateJson, MotelyJsonContext.Default.DoubleArray);

            var (results, nextState) = streamer(seed, deckEnum, stakeEnum, cursorState, take);

            var dto = new ItemStreamDto { Results = results, NextState = nextState };
            return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.ItemStreamDto));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message }, MotelyJsonContext.Default.ErrorDto));
        }
    }

    // ──────────────────────────────── JAML Validation ────────────────────────────────

    /// <summary>
    /// Validate a JAML string. Returns JSON ValidateResultDto.
    /// </summary>
    public static string ValidateJaml(string jamlContent)
    {
        var dto = MotelyRuntime.ValidateJaml(jamlContent);
        return JsonSerializer.Serialize(dto, global::Motely.MotelyJsonContext.Default.ValidateResultDto);
    }

    [JSExport]
    public static Task<string> ValidateJamlAsync(string jamlContent) =>
        Task.FromResult(ValidateJaml(jamlContent));

    // ──────────────────────────────── Search (non-blocking, JS polls status) ────────────────────────────────

    [JSExport]
    public static Task<string> StartJamlSearch(
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
                        Keywords = parsedOptions.Keywords,
                        Padding = parsedOptions.Padding,
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

            int threadCount = 1;
            if (options.ThreadCount.Value > 1 && IsThreadingEnabled())
                threadCount = Math.Min(options.ThreadCount.Value, Environment.ProcessorCount);

            if (threadCount > Environment.ProcessorCount)
            {
                return Task.FromResult(ErrorJson(
                    $"Invalid options.threadCount. Requested {threadCount}, but runtime reports {Environment.ProcessorCount} available processor(s)."
                ));
            }

            // Normalize seeds and keywords inline
            var normalizedSeeds = new List<string>();
            var normalizedKeywords = new List<string>();

            if (options.SpecificSeed != null)
            {
                if (string.IsNullOrWhiteSpace(options.SpecificSeed))
                    return Task.FromResult(ErrorJson("specificSeed cannot be empty."));
                normalizedSeeds.Add(options.SpecificSeed.Trim().ToUpperInvariant());
            }
            if (options.Seeds != null)
            {
                if (options.Seeds.Length == 0)
                    return Task.FromResult(ErrorJson("seeds must contain at least one seed."));
                foreach (var s in options.Seeds)
                {
                    if (string.IsNullOrWhiteSpace(s))
                        return Task.FromResult(ErrorJson("seeds entry cannot be empty."));
                    normalizedSeeds.Add(s.Trim().ToUpperInvariant());
                }
            }
            if (options.Keyword != null)
            {
                if (string.IsNullOrWhiteSpace(options.Keyword))
                    return Task.FromResult(ErrorJson("keyword cannot be empty."));
                var kw = options.Keyword.Trim().ToUpperInvariant();
                if (kw.Length > MotelyCore.MaxSeedLength)
                    return Task.FromResult(ErrorJson($"keyword is too long (max {MotelyCore.MaxSeedLength} chars)."));
                normalizedKeywords.Add(kw);
            }
            if (options.Keywords != null)
            {
                if (options.Keywords.Length == 0)
                    return Task.FromResult(ErrorJson("keywords must contain at least one keyword."));
                foreach (var kw in options.Keywords)
                {
                    if (string.IsNullOrWhiteSpace(kw))
                        return Task.FromResult(ErrorJson("keywords entry cannot be empty."));
                    var norm = kw.Trim().ToUpperInvariant();
                    if (norm.Length > MotelyCore.MaxSeedLength)
                        return Task.FromResult(ErrorJson($"keyword '{norm}' is too long (max {MotelyCore.MaxSeedLength} chars)."));
                    normalizedKeywords.Add(norm);
                }
            }

            // Cancel any existing search first (only one at a time)
            StopSearch();
            _resultQueue.Clear();

            // Build plan directly from config
            var plan = JamlSearchBuilder.CreatePlan(config);
            var filterId = config.FilterId;
            var settings = plan.Settings
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(threadCount)
                .WithBatchCharacterCount(options.BatchCharCount.Value);

            if (options.StartBatch.HasValue)
                settings = settings.WithStartBatchIndex(options.StartBatch.Value);
            if (options.EndBatch.HasValue)
                settings = settings.WithEndBatchIndex(options.EndBatch.Value);

            var modeOpts = new SearchOptionsDto
            {
                Seeds = normalizedSeeds.Count > 0 ? normalizedSeeds.ToArray() : null,
                Keywords = normalizedKeywords.Count > 0 ? normalizedKeywords.ToArray() : null,
                Padding = string.IsNullOrWhiteSpace(options.Padding) ? null : options.Padding.Trim().ToUpperInvariant(),
                RandomSeeds = options.RandomSeeds,
                Palindrome = options.Palindrome == true ? true : null,
            };
            var (_, modeError) = settings.ApplySearchMode(modeOpts);
            if (modeError != null)
                return Task.FromResult(ErrorJson(modeError));

            settings = settings.WithProgressCallback(prog =>
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
                    var hit = CreateSearchHit(tally);
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

            return Task.FromResult(finalStatus);
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

    private static string BuildStatusJson(IMotelySearch search, string? filterId = null)
    {
        var results = _resultQueue.ToArray();

        var dto = new global::Motely.SearchStatusDto
        {
            FilterId = filterId ?? _currentFilterId ?? string.Empty,
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

    private static SearchHitDto CreateSearchHit(MotelySeedScoreTally tally)
    {
        return new SearchHitDto
        {
            Seed = tally.Seed,
            Score = tally.Score,
            Tallies = BuildTallies(tally),
        };
    }

    private static int[] BuildTallies(MotelySeedScoreTally tally)
    {
        if (tally.TallyCount == 0)
            return [];

        var tallies = new int[tally.TallyCount];
        for (int i = 0; i < tally.TallyCount; i++)
            tallies[i] = tally.GetTally(i);
        return tallies;
    }

    private static string GetCachedVersion() =>
        _cachedVersion ??= MotelyRuntime.GetVersion(typeof(MotelyWasmExports).Assembly);

    [JSExport]
    private static string[] GetFeatureList() =>
        MotelyRuntime.GetFeatureList("browser-wasm", GetAvailableThreadCount());
}
