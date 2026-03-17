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

            var (request, requestError) = MotelySearchRequestFactory.FromOptions(
                options,
                threadCount,
                options.BatchCharCount.Value
            );
            if (requestError != null || request == null)
                return Task.FromResult(
                    ErrorJson(requestError ?? "Search request could not be created.")
                );

            var (plan, filterId, prepareError) = MotelySearchOrchestrator.PrepareSearch(
                config,
                request
            );
            if (prepareError != null || plan == null || filterId == null)
                return Task.FromResult(
                    ErrorJson(prepareError ?? "Search could not be prepared.")
                );

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
