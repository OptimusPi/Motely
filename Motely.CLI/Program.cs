using System.Runtime.InteropServices;
using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;
using Motely;
using Motely.Analysis;
using Motely.CLI;
using Motely.DataLake;
using Motely.Enums;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Native;
using Motely.SeedProviders;

partial class Program
{
    private static readonly CancellationTokenSource _cts = new();
    private const int DefaultBatchCharCount = 4;

    private static List<string> BuildKeywordInputs(
        CommandOption<string> keywordOption,
        CommandOption<string> keywordsOption
    )
    {
        var keywordInputs = new List<string>();
        if (keywordOption.HasValue())
            keywordInputs.Add(keywordOption.ParsedValue.Trim().ToUpperInvariant());

        if (keywordsOption.HasValue())
        {
            keywordInputs.AddRange(
                keywordsOption
                    .ParsedValue.Split(
                        ',',
                        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
                    )
                    .Select(static k => k.Trim().ToUpperInvariant())
            );
        }

        return keywordInputs;
    }

    static bool TryParseSeedOptions(
        CommandOption<string> startSeedOption,
        CommandOption<string> stopSeedOption,
        out long? startIndex,
        out long? stopIndex,
        out string? error
    )
    {
        startIndex = null;
        stopIndex = null;
        error = null;

        if (startSeedOption.HasValue())
        {
            if (!TryParseSeedString(startSeedOption.ParsedValue, out var idx, out var err))
            {
                error = $"Error: --startSeed: {err}";
                return false;
            }
            startIndex = idx;
        }
        if (stopSeedOption.HasValue())
        {
            if (!TryParseSeedString(stopSeedOption.ParsedValue, out var idx, out var err))
            {
                error = $"Error: --stopSeed: {err}";
                return false;
            }
            stopIndex = idx;
        }
        return true;
    }

    static bool TryParseSeedString(string input, out long index, out string? error)
    {
        index = 0;
        error = null;
        var seed = MotelyGlobals.NormalizeSeed(input);
        // Motely's sequential search ranges over full 8-char seeds (11111111 → ZZZZZZZZ),
        // so --startSeed/--stopSeed must be exactly 8 chars. No padding: a short seed
        // would silently map to a different point than the user typed.
        if (seed.Length != MotelyGlobals.MaxSeedLength)
        {
            error =
                $"'{input}' must be exactly {MotelyGlobals.MaxSeedLength} characters (1-9, A-Z).";
            return false;
        }
        foreach (char c in seed)
        {
            if (!MotelyGlobals.SeedDigits.Contains(c))
            {
                // '0' was already normalized to 'O' above, so it can never reach here —
                // don't tell the user "no 0" for a char that can't be 0.
                error = $"'{input}' contains invalid character '{c}'. Valid: 1-9, A-Z.";
                return false;
            }
        }
        index = SeedMath.SeedToSearchIndex(seed);
        return true;
    }

    static void RequestTermination()
    {
        _cts.Cancel();
    }

    static void OnTermination(PosixSignalContext _)
    {
        RequestTermination();
    }

    static int Main(string[] args)
    {
        // .NET 10: runtime no longer provides default SIGTERM/SIGINT handlers (see
        // https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/10.0/sigterm-signal-handler).
        // Register handlers so Ctrl+C and termination signals cancel the search gracefully.
        using var _sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnTermination);
        using var _sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnTermination);
        using var _sighup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnTermination);

        Console.CancelKeyPress += (ctx, e) =>
        {
            e.Cancel = true;
            RequestTermination();
        };

        var escCts = new CancellationTokenSource();
        var escThread = new Thread(() =>
            ConsoleKeyMonitor.Run(RequestTermination, PrintLatestProgressOnDemand, escCts.Token)
        )
        {
            IsBackground = true,
            Name = "Console Key Listener",
        };
        escThread.Start();

        var app = new CommandLineApplication
        {
            Name = "Motely",
            Description = "Motely - Balatro Seed Searcher",
            OptionsComparison = StringComparison.OrdinalIgnoreCase,
        };
        app.HelpOption("-?|-h|--help");

        var jamlOption = app.Option<string>(
            "--jaml <JAML>",
            "JAML (Jimbo's Ante Markup Language) config file",
            CommandOptionType.SingleValue
        );
        var analyzeOption = app.Option<string>(
            "--analyze <SEED[,SEED...]>",
            "Analyze one or more seeds (comma-separated) as human-readable text, using the "
                + "legacy text-block analyzer (NOT JAMLyzer — see --glossary).",
            CommandOptionType.SingleValue
        );
        var glossaryOption = app.Option(
            "--glossary",
            "Print what JAML and JAMLyzer mean, then exit.",
            CommandOptionType.NoValue
        );
        var deckOption = app.Option<string>(
            "--deck <NAME>",
            "Deck name (Red, Blue, Yellow, Green, Black, Magic, Nebula, Checkered, Zodiac, Painted, Anaglyph, Plasma, Erratic)",
            CommandOptionType.SingleValue
        );
        var stakeOption = app.Option<string>(
            "--stake <STAKE>",
            "Stake name for analysis/search (default: White)",
            CommandOptionType.SingleValue
        );
        var threadsOption = app.Option<int>(
            "--threads <N>",
            "Thread count",
            CommandOptionType.SingleValue
        );
        var batchCharCountOption = app.Option<int>(
            "--batchCharCount <N>",
            "Sequential default search only (1–7, default 4). Ignored for --keyword/--random/--aesthetic/--source list modes.",
            CommandOptionType.SingleValue
        );
        var startBatchOption = app.Option<long>(
            "--startBatch <N>",
            "Starting batch index",
            CommandOptionType.SingleValue
        );
        var endBatchOption = app.Option<long>(
            "--endBatch <N>",
            "Ending batch index",
            CommandOptionType.SingleValue
        );
        var startPercentOption = app.Option<double>(
            "--startPercent <PCT>",
            "Sequential search: start at this percent of batch space (0–100). Ignored if --startBatch is set.",
            CommandOptionType.SingleValue
        );
        var startSeedOption = app.Option<string>(
            "--startSeed <SEED>",
            "Sequential: first seed to search (e.g. 11111111 … ZZZZZZZZ). Mutually exclusive with --startBatch/--endBatch/--startPercent.",
            CommandOptionType.SingleValue
        );
        var stopSeedOption = app.Option<string>(
            "--stopSeed <SEED>",
            "Sequential: last seed to search (inclusive, e.g. ZZZZZZZZ). Omit for full range after --startSeed.",
            CommandOptionType.SingleValue
        );
        var randomOption = app.Option<int>(
            "--random <N>",
            "Random seed count",
            CommandOptionType.SingleValue
        );
        var aestheticOption = app.Option<string>(
            "--aesthetic <NAME>",
            $"Search seeds from an aesthetic provider ({JamlAestheticParser.KnownJamlStringsDescription()})",
            CommandOptionType.SingleValue
        );
        var collectOption = app.Option<long>(
            "--collect <N>",
            $"Collect up to N matching seeds and stop (SIMD batches may deliver a few over). Sweeps every aesthetic first ({JamlAestheticParser.KnownJamlStringsDescription()}), then sequential if still short. Replaces --findone (use --collect 1).",
            CommandOptionType.SingleValue
        );
        var sourceOption = app.Option<string>(
            "--source <NAME_OR_PATH>",
            "Seed source: .csv/.txt/.parquet/.duckdb/.db file (DuckDB reads them all), local or http(s)/s3. Seeds ride the first column.",
            CommandOptionType.SingleValue
        );
        var drownOption = app.Option(
            "--makeitrain",
            "Make it rain: re-search every seed saved for this JAML filter, read straight from its DuckLake catalog (<results-path>/<filterId>.catalog).",
            CommandOptionType.NoValue
        );
        var resultsPathOption = app.Option<string>(
            "--results-path <PATH>",
            "Root folder of the seed lake (default: .seeds; env MOTELY_DATALAKE_PATH).",
            CommandOptionType.SingleValue
        );
        var seedsOption = app.Option<string>(
            "--seeds <LIST>",
            "Inline comma-separated seeds",
            CommandOptionType.SingleValue
        );
        var cutoffOption = app.Option<string>(
            "--cutoff <VALUE>",
            "Minimum score to print, or 'auto' for running maximum (every seed at or above the best score so far, ties included)",
            CommandOptionType.SingleValue
        );
        var keywordOption = app.Option<string>(
            "--keyword <WORD>",
            "Search seeds containing this keyword (pads to 8 chars with all valid chars)",
            CommandOptionType.SingleValue
        );
        var keywordsOption = app.Option<string>(
            "--keywords <WORDS>",
            "Comma-separated keywords, each padded to 8 chars (e.g. \"OW,OH,BOOB\")",
            CommandOptionType.SingleValue
        );
        var paddingOption = app.Option<string>(
            "--padding <CHARS>",
            "Restrict free-slot / pad chars for --keyword/--keywords and --aesthetic (e.g. \"123456789\" digits-only — words stay visible). Collect's aesthetic prepass defaults to 123456789 when this flag is omitted.",
            CommandOptionType.SingleValue
        );
        var nativeOption = app.Option<string>(
            "--native <NAME>",
            "Run a native C# filter by name (e.g. PerkeoObservatory, Observatory, Trickeoglyph, NaturalNegatives, ...). Seed-input flags match JAML: --source, --seeds, --keyword(s), --random, --aesthetic, or default sequential (--startBatch/--endBatch/--startPercent or --startSeed/--stopSeed).",
            CommandOptionType.SingleValue
        );
        var nativeRandomCountOption = app.Option<int>(
            "--native-random <N>",
            "Random seed count for --native mode",
            CommandOptionType.SingleValue
        );
        var quietOption = app.Option(
            "-q|--quiet|--no-progress",
            "Suppress per-batch progress lines and the startup preamble on stderr (stdout results unaffected).",
            CommandOptionType.NoValue
        );
        threadsOption.DefaultValue = Environment.ProcessorCount;
        // No DefaultValue here (unlike threadsOption above): CommandOption.HasValue() reports
        // true forever once a DefaultValue is set, so it could never again distinguish "user
        // typed --batchCharCount" from "didn't" — which is exactly the signal this needs to
        // decide whether to override a JAML's saved seeds: list. Default of 4 is applied at
        // each read site instead (DefaultBatchCharCount below).

        app.OnExecuteAsync(async _ =>
        {
            if (glossaryOption.HasValue())
            {
                Console.WriteLine(MotelyGlossary.Render());
                return 0;
            }

            if (args.Length == 0)
            {
                app.ShowHelp();
                return 0;
            }

            // --analyze mode — supports single seed or comma-separated batch.
            if (analyzeOption.HasValue())
            {
                var seedTokens = analyzeOption.ParsedValue.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                );

                var analyzeDeck = deckOption.HasValue() ? deckOption.ParsedValue : "Erratic";
                var analyzeStake = stakeOption.HasValue() ? stakeOption.ParsedValue : "White";

                if (seedTokens.Length == 1)
                    return ExecuteAnalyze(seedTokens[0], analyzeDeck, analyzeStake);

                return ExecuteAnalyzeBatch(seedTokens, analyzeDeck, analyzeStake);
            }

            // --native mode — run a hardcoded C# filter by name
            if (nativeOption.HasValue())
                return await RunNativeMode();

            return await RunJamlMode();

            // A local function, not a method: it captures the CommandOption objects themselves, so
            // HasValue() keeps meaning "the user typed this" (see the DefaultValue note above).
            // Reading values into a parameter list would quietly destroy that distinction.
            async Task<int> RunNativeMode()
            {
                // --drown needs a normalized filterId from a JAML file.
                if (drownOption.HasValue())
                {
                    Console.Error.WriteLine(
                        "Error: --makeitrain currently requires --jaml so the CLI can resolve the normalized filterId."
                    );
                    return 1;
                }

                var nDeck = deckOption.HasValue()
                    ? Enum.Parse<MotelyDeck>(deckOption.ParsedValue, true)
                    : MotelyDeck.Red;
                var nStake = stakeOption.HasValue()
                    ? Enum.Parse<MotelyStake>(stakeOption.ParsedValue, true)
                    : MotelyStake.White;
                int nThreads = threadsOption.HasValue()
                    ? threadsOption.ParsedValue
                    : Environment.ProcessorCount;
                int nBatch = batchCharCountOption.HasValue()
                    ? batchCharCountOption.ParsedValue
                    : DefaultBatchCharCount;

                if (
                    !MotelyNativeFilterNames.TryParse(
                        nativeOption.ParsedValue,
                        out var nativeFilter
                    )
                )
                {
                    Console.Error.WriteLine(
                        $"Error: unknown native filter '{nativeOption.ParsedValue}'. Known: {string.Join(", ", MotelyNativeFilterNames.DisplayNames)}"
                    );
                    return 1;
                }

                IMotelySearchSettings nSettings = MotelyNativeFilterFactory.CreateSettings(
                    nativeFilter
                );

                nSettings = nSettings.WithDeck(nDeck).WithStake(nStake).WithThreadCount(nThreads);

                if (
                    !TryParseSeedOptions(
                        startSeedOption,
                        stopSeedOption,
                        out var nStartIdx,
                        out var nStopIdx,
                        out var seedOptError
                    )
                )
                {
                    Console.Error.WriteLine(seedOptError);
                    return 1;
                }

                if (
                    !CliSearchMode.TryApplySearchMode(
                        nSettings,
                        new CliSearchMode.Input(
                            SourcePath: sourceOption.HasValue() ? sourceOption.ParsedValue : null,
                            SeedsArgument: seedsOption.HasValue() ? seedsOption.ParsedValue : null,
                            Drown: false,
                            ResultsRootPath: resultsPathOption.HasValue()
                                ? resultsPathOption.ParsedValue
                                : null,
                            FilterId: null,
                            JamlSeeds: null,
                            KeywordInputs: BuildKeywordInputs(keywordOption, keywordsOption),
                            PaddingCharsOption: paddingOption.HasValue()
                                ? paddingOption.ParsedValue
                                : null,
                            RandomCount: nativeRandomCountOption.HasValue()
                                ? nativeRandomCountOption.ParsedValue
                                : null,
                            AestheticName: aestheticOption.HasValue()
                                ? aestheticOption.ParsedValue
                                : null,
                            StartBatch: startBatchOption.HasValue()
                                ? startBatchOption.ParsedValue
                                : null,
                            EndBatch: endBatchOption.HasValue() ? endBatchOption.ParsedValue : null,
                            StartPercent: startPercentOption.HasValue()
                                ? startPercentOption.ParsedValue
                                : null,
                            StartSeedSearchIndex: nStartIdx,
                            StopSeedSearchIndex: nStopIdx,
                            BatchCharacterCount: nBatch
                        ),
                        msg => Console.Error.WriteLine(msg),
                        out var nSearchModeError,
                        out nSettings,
                        out var nSourceLifetime
                    )
                )
                {
                    Console.Error.WriteLine(nSearchModeError);
                    return 1;
                }

                using var _nSourceLifetime = nSourceLifetime;

                // Always attach a progress callback so 'p' hotkey has fresh data;
                // quiet mode just swaps in the silent capture variant.
                nSettings = nSettings
                    .WithSeedMatchCallback(StickyProgress.WriteResultLine)
                    .WithProgressCallback(
                        quietOption.HasValue() ? CaptureProgress : WriteProgressLineToStderr
                    );

                if (!quietOption.HasValue())
                    Console.Error.WriteLine(
                        $"Motely native: {nativeOption.ParsedValue} | {nDeck} {nStake} | threads={nThreads} | batchCharCount={nBatch} (sequential only)"
                    );
                using var nSearch = nSettings.Start(_cts.Token);
                await nSearch.WaitForCompletionAsync(_cts.Token);
                PrintSummary(nSearch, nBatch, _cts.Token.IsCancellationRequested);
                return _cts.Token.IsCancellationRequested ? 1 : 0;
            }

            // --jaml mode — the main path: load the filter, build the search, run the passes.
            async Task<int> RunJamlMode()
            {
                if (!jamlOption.HasValue())
                {
                    Console.Error.WriteLine("Error: --jaml <path> or --native <name> required.");
                    return 1;
                }

                if (
                    !JamlFileLoader.TryLoadFromPath(
                        jamlOption.ParsedValue,
                        out var config,
                        out var loadError
                    )
                )
                {
                    Console.Error.WriteLine($"Error: {loadError}");
                    return 1;
                }

                var deck = config.Deck;
                var stake = config.Stake;
                bool drown = drownOption.HasValue();
                int threads = threadsOption.HasValue()
                    ? threadsOption.ParsedValue
                    : Environment.ProcessorCount;
                int batchCharCount = batchCharCountOption.HasValue()
                    ? batchCharCountOption.ParsedValue
                    : DefaultBatchCharCount;
                // Only non-null when the user actually typed --batchCharCount — an explicit request
                // for the real sequential sweep, which should override a JAML's saved seeds: list.
                int? explicitBatchCharCount = batchCharCountOption.HasValue() ? batchCharCount : null;

                // Default is auto: without --cutoff we self-tune the score gate instead of
                // emitting every seed. An explicit integer turns auto off and pins the gate.
                bool cutoffAuto = true;
                int cutoffFixed = int.MinValue;
                if (cutoffOption.HasValue())
                {
                    var cutoffValue = cutoffOption.ParsedValue.Trim();
                    if (string.Equals(cutoffValue, "auto", StringComparison.OrdinalIgnoreCase))
                    {
                        cutoffAuto = true;
                    }
                    else if (int.TryParse(cutoffValue, out cutoffFixed))
                    {
                        cutoffAuto = false;
                    }
                    else
                    {
                        Console.Error.WriteLine("Error: --cutoff must be an integer or 'auto'.");
                        return 1;
                    }
                }

                int engineCutoff = (!cutoffAuto && cutoffFixed > int.MinValue) ? cutoffFixed : 0;
                JamlSearchPlan plan;
                try
                {
                    // Push fixed --cutoff into the engine so low-scoring seeds are dropped at
                    // the scorer (no callback spam, no per-seed string concat). Auto still needs
                    // the caller-side running-max below since the engine threshold is static.
                    plan = JamlSearchBuilder.CreatePlan(config, engineCutoff);
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    return 1;
                }

                Console.WriteLine(
                    $"  Cost:  ~{config.SimdCostPerSeed():0.#} crunches/seed "
                        + $"({config.EstimateFilterCrunches():N0} per {MotelyGlobals.MaxVectorWidth}-seed batch, worst case)"
                );

                IMotelySearchSettings settings = plan
                    .Settings.WithDeck(deck)
                    .WithStake(stake)
                    .WithThreadCount(threads);

                if (
                    !TryParseSeedOptions(
                        startSeedOption,
                        stopSeedOption,
                        out var jStartIdx,
                        out var jStopIdx,
                        out var jSeedOptError
                    )
                )
                {
                    Console.Error.WriteLine(jSeedOptError);
                    return 1;
                }

                if (
                    !CliSearchMode.TryApplySearchMode(
                        settings,
                        new CliSearchMode.Input(
                            SourcePath: sourceOption.HasValue() ? sourceOption.ParsedValue : null,
                            SeedsArgument: seedsOption.HasValue() ? seedsOption.ParsedValue : null,
                            Drown: drown,
                            ResultsRootPath: resultsPathOption.HasValue()
                                ? resultsPathOption.ParsedValue
                                : null,
                            FilterId: config.Id,
                            JamlSeeds: config.Seeds,
                            KeywordInputs: BuildKeywordInputs(keywordOption, keywordsOption),
                            PaddingCharsOption: paddingOption.HasValue()
                                ? paddingOption.ParsedValue
                                : null,
                            RandomCount: randomOption.HasValue() ? randomOption.ParsedValue : null,
                            AestheticName: aestheticOption.HasValue()
                                ? aestheticOption.ParsedValue
                                : null,
                            StartBatch: startBatchOption.HasValue()
                                ? startBatchOption.ParsedValue
                                : null,
                            EndBatch: endBatchOption.HasValue() ? endBatchOption.ParsedValue : null,
                            StartPercent: startPercentOption.HasValue()
                                ? startPercentOption.ParsedValue
                                : null,
                            StartSeedSearchIndex: jStartIdx,
                            StopSeedSearchIndex: jStopIdx,
                            BatchCharacterCount: explicitBatchCharCount
                        ),
                        msg => Console.Error.WriteLine(msg),
                        out var jamlSearchModeError,
                        out settings,
                        out var jamlSourceLifetime
                    )
                )
                {
                    Console.Error.WriteLine(jamlSearchModeError);
                    return 1;
                }

                using var _jamlSourceLifetime = jamlSourceLifetime;

                int scoreTallyColumns = plan.ScoreTallyColumnCount;
                bool hasStructuredScores = scoreTallyColumns > 0;
                var resultSinks = new List<IMotelyResultSink>
            {
                new ConsoleResultSink(hasStructuredScores ? plan.TallyLabels : null),
            };
                if (hasStructuredScores)
                {
                    // Persist found seeds to the seed lake so --drown can replay them later.
                    resultSinks.Add(
                        new SeedLakeSink(
                            resultsPathOption.HasValue() ? resultsPathOption.ParsedValue : null,
                            config.Id
                        )
                    );
                }
                using var resultSink = new CompositeMotelyResultSink(resultSinks);
                int cliLearnedCutoff = cutoffAuto ? int.MinValue : engineCutoff;
                var saveSeedsCollector = new MotelyTopSeedSink.Collector(int.MaxValue);
                var saveSeedMatches = new List<string>();
                var saveSeedMatchSet = new HashSet<string>(StringComparer.Ordinal);

                // Always attach a progress callback so 'p' hotkey stays current;
                // quiet mode swaps in the silent capture variant.
                settings = settings
                    .WithProgressCallback(
                        quietOption.HasValue() ? CaptureProgress : WriteProgressLineToStderr
                    )
                    .WithAutoScoreCutoff(cutoffAuto);

                if (hasStructuredScores)
                {
                    settings = settings.WithScoredResultCallback(tally =>
                    {
                        if (
                            !ShouldEmitScore(tally.Score, cutoffAuto, cutoffFixed, ref cliLearnedCutoff)
                        )
                            return;

                        resultSink.OnScored(in tally);
                        saveSeedsCollector.Consider(tally.Seed, tally.Score);
                    });
                }
                else
                {
                    settings = settings.WithSeedMatchCallback(seed =>
                    {
                        // Every worker thread calls this, and the engine serializes nothing. A bare
                        // HashSet/List here loses finds under concurrent Add — silently, and more often
                        // the more threads are working. The lock costs nothing at match frequency.
                        lock (saveSeedMatches)
                        {
                            resultSink.OnSeed(seed);
                            if (saveSeedMatchSet.Add(seed))
                            {
                                saveSeedMatches.Add(seed);
                            }
                        }
                    });
                }

                if (!quietOption.HasValue())
                {
                    Console.Error.WriteLine(
                        $"Motely: {config.Name ?? jamlOption.ParsedValue} | {deck} {stake} | threads={threads} | batchCharCount={batchCharCount} {(drown ? "| makeitrain=Seeds CSV via DuckDB" : "(sequential only)")}"
                    );
                }

                bool cancelled = false;
                IMotelySearch search;

                async Task<bool> RunPass(IMotelySearch pass)
                {
                    try
                    {
                        await pass.WaitForCompletionAsync(_cts.Token);
                        return false;
                    }
                    catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
                    {
                        return true;
                    }
                }

                // Naming an explicit sequential range (--startBatch/--endBatch/--startPercent/
                // --startSeed/--stopSeed) says you want the sweep itself, so --collect skips the
                // aesthetic pass entirely rather than answering a different question than you asked.
                bool collectSequentialOnly =
                    startBatchOption.HasValue()
                    || endBatchOption.HasValue()
                    || startPercentOption.HasValue()
                    || startSeedOption.HasValue()
                    || stopSeedOption.HasValue();

                long collectLimit = 0;
                if (collectOption.HasValue())
                {
                    collectLimit = collectOption.ParsedValue;
                    if (collectLimit < 1)
                    {
                        Console.Error.WriteLine("--collect N requires N >= 1.");
                        return 1;
                    }
                }

                if (collectLimit > 0 && collectSequentialOnly)
                {
                    settings = settings
                        .WithSequentialSearch()
                        .WithBatchCharacterCount(batchCharCount)
                        .StopAfter(collectLimit);
                    search = settings.Start(_cts.Token);
                    cancelled = await RunPass(search);
                }
                else if (collectLimit > 0)
                {
                    // CliSearchMode already installed keyword / aesthetic / source / random /
                    // drown / inline-seeds providers onto settings. --collect must StopAfter that
                    // intent — stomping it with the multi-aesthetic prepass is the pigeonhole
                    // (CUM hunt silently became "pretty seeds" and wiped operator seed lists).
                    // JAML seeds: alone still takes the default aesthetic collect path.
                    bool namedExplicitSeedInput =
                        keywordOption.HasValue()
                        || keywordsOption.HasValue()
                        || aestheticOption.HasValue()
                        || sourceOption.HasValue()
                        || seedsOption.HasValue()
                        || randomOption.HasValue()
                        || drown;

                    if (namedExplicitSeedInput)
                    {
                        settings = settings.StopAfter(collectLimit);
                        search = settings.Start(_cts.Token);
                        cancelled = await RunPass(search);
                    }
                    else
                    {
                        // Default collect: every aesthetic first (digit-pad free slots), then sequential.
                        // Full-alphabet free slots are not a "tiny corner". Override pad with --padding.
                        var aesthetics =
                            aestheticOption.HasValue()
                            && JamlAestheticParser.TryParse(
                                aestheticOption.ParsedValue.Trim(),
                                out var onlyOne
                            )
                                ? new[] { onlyOne }
                                : Enum.GetValues<JamlAesthetic>();
                        char[] collectPad = paddingOption.HasValue()
                            ? MotelyGlobals.ParsePaddingChars(paddingOption.ParsedValue)
                                ?? JamlAesthetics.QuickPaddingChars
                            : JamlAesthetics.QuickPaddingChars;
                        settings = settings
                            .WithProviderSearch(
                                new MotelySeedListProvider(
                                    aesthetics.SelectMany(a =>
                                        JamlAesthetics.EnumerateSeeds(a, collectPad)
                                    ),
                                    aesthetics.Sum(a =>
                                        JamlAesthetics.GetSeedCount(a, collectPad)
                                    )
                                )
                            )
                            .StopAfter(collectLimit);

                        var aestheticPass = settings.Start(_cts.Token);
                        cancelled = await RunPass(aestheticPass);

                        long remaining = collectLimit - aestheticPass.MatchingSeeds;
                        if (!cancelled && remaining > 0)
                        {
                            aestheticPass.Dispose();
                            if (!quietOption.HasValue())
                            {
                                if (aestheticPass.MatchingSeeds == 0)
                                    Console.Error.WriteLine(
                                        "No aesthetic seed matched — falling back to the sequential sweep."
                                    );
                                else
                                    Console.Error.WriteLine(
                                        $"Collected {aestheticPass.MatchingSeeds}/{collectLimit} from aesthetics — sequential for the rest."
                                    );
                            }

                            settings = settings
                                .WithSequentialSearch()
                                .WithBatchCharacterCount(batchCharCount)
                                .StopAfter(remaining);
                            aestheticPass = settings.Start(_cts.Token);
                            cancelled = await RunPass(aestheticPass);
                        }
                        else if (!cancelled && !quietOption.HasValue())
                        {
                            Console.Error.WriteLine(
                                $"Collected {aestheticPass.MatchingSeeds} from aesthetics — no sequential sweep needed."
                            );
                        }

                        search = aestheticPass;
                    }
                }
                else
                {
                    search = settings.Start(_cts.Token);
                    cancelled = await RunPass(search);
                }

                using var _search = search;

                cancelled |= _cts.Token.IsCancellationRequested;
                {
                    var seedsToSave = hasStructuredScores
                        ? saveSeedsCollector.GetSeeds()
                        : (IReadOnlyList<string>)saveSeedMatches;

                    if (JamlFileLoader.TrySaveSeeds(jamlOption.ParsedValue, seedsToSave, out var saveError))
                        Console.Error.WriteLine(
                            $"Saved {seedsToSave.Count:N0} seed(s) into top-level seeds: in {jamlOption.ParsedValue}"
                        );
                    else
                        Console.Error.WriteLine(
                            $"Warning: could not save seeds back into JAML: {saveError}"
                        );
                }

                PrintSummary(search, batchCharCount, cancelled);
                return cancelled ? 1 : 0;
            }
        });

        try
        {
            return app.Execute(args);
        }
        catch (CommandParsingException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            escCts.Cancel();
        }
    }

    // ── Summary ──

    static void PrintSummary(IMotelySearch search, int batchCharCount, bool cancelled)
    {
        StickyProgress.Clear();
        Console.Out.Flush();
        Console.WriteLine();
        Console.WriteLine(cancelled ? "STOPPED" : "COMPLETED");
        var elapsed = TimeSpan.FromMilliseconds(search.ElapsedMs);

        // A run that stopped on the match limit quit on purpose, part-way through a batch. Seeds
        // searched and seeds/sec describe a sweep that never happened — the honest number for a
        // find-one run is how long it took to find one.
        if (search.StoppedOnMatchLimit)
        {
            Console.WriteLine($"  Found: {search.MatchingSeeds:N0} seed(s) (StopAfter; SIMD/thread overshoot ok)");
            Console.WriteLine($"  Time:  {elapsed:hh\\:mm\\:ss\\.fff}");
        }
        else
        {
            Console.WriteLine(
                $"  Seeds: {search.TotalSeedsSearched:N0} searched, {search.MatchingSeeds:N0} matched"
            );
            Console.WriteLine($"  Time:  {elapsed:hh\\:mm\\:ss\\.fff}");
            if (elapsed.TotalSeconds >= 1.0)
            {
                double speed = search.TotalSeedsSearched / elapsed.TotalSeconds;
                Console.WriteLine($"  Speed: {speed:N0} seeds/sec");
            }
            else
            {
                Console.WriteLine("  Speed: (too short to measure)");
            }
        }
        if (search.IsSequentialBatchSearch)
        {
            long max = (long)Math.Pow(35, 8 - batchCharCount);
            double pct = max > 0 ? (double)search.CompletedBatchCount * 100.0 / max : 0;
            Console.WriteLine($"  Batch: {search.CompletedBatchCount:N0} / {max:N0} ({pct:F4}%)");
            if (cancelled)
            {
                // Position, not count: see IMotelySearch.ResumeBatchIndex.
                long nextBatch = search.ResumeBatchIndex;
                Console.WriteLine($"  Resume: --startBatch {nextBatch}");
                if (nextBatch >= 0 && nextBatch < max)
                {
                    string minSeedInBatch = SeedMath.BatchIndexToMinSeed(nextBatch, batchCharCount);
                    Console.WriteLine($"  Resume: --startSeed {minSeedInBatch}");
                }
            }
        }
    }

    // ── Analyze ──

    static int ExecuteAnalyze(string seed, string deckName, string stakeName) =>
        ExecuteAnalyzeBatch([seed], deckName, stakeName);

    static int ExecuteAnalyzeBatch(string[] seeds, string deckName, string stakeName)
    {
        if (!Enum.TryParse<MotelyDeck>(deckName, true, out var d))
        {
            Console.Error.WriteLine($"Error: invalid deck '{deckName}'.");
            return 1;
        }
        if (!Enum.TryParse<MotelyStake>(stakeName, true, out var s))
        {
            Console.Error.WriteLine($"Error: invalid stake '{stakeName}'.");
            return 1;
        }

        foreach (var rawSeed in seeds)
        {
            var seed = MotelyGlobals.NormalizeSeed(rawSeed);

            var analysis = MotelyUnitTestAnalyzer.Analyze(new(seed, d, s));
            if (!string.IsNullOrEmpty(analysis.Error))
            {
                Console.Error.WriteLine($"[ERROR] {seed}: {analysis.Error}");
                return 1;
            }

            Console.WriteLine($"=== {seed} | {d} {s} ===");
            Console.Write(analysis);
            Console.WriteLine();
        }

        return 0;
    }

    // Cached latest progress so 'p' key can print on demand even under --quiet.
    static MotelyProgress? _latestProgress;

    /// <summary>
    /// Draws the progress line on every report — the engine reports once per completed batch
    /// (MotelySearch.PrintReport), so this is one line per batch and nothing is skipped. It used
    /// to redraw only when the whole-number percent ticked over, which on a 2.25-trillion-seed
    /// sweep is once an hour: the search looked hung.
    /// </summary>
    static void WriteProgressLineToStderr(MotelyProgress p)
    {
        _latestProgress = p;
        FormatProgressToStderr(p);
    }

    static void CaptureProgress(MotelyProgress p) => _latestProgress = p;

    static void PrintLatestProgressOnDemand()
    {
        if (_latestProgress is { } p)
            FormatProgressToStderr(p);
    }

    static void FormatProgressToStderr(MotelyProgress p)
    {
        double perSec = p.SeedsPerMillisecond * 1000.0;
        string speed =
            perSec >= 1_000_000 ? $"{perSec / 1_000_000:F2} M/s"
            : perSec >= 1_000 ? $"{perSec / 1_000:F1} K/s"
            : $"{perSec:F0}/s";
        string eta =
            p.EstimatedTimeRemainingMilliseconds is long etaMs && etaMs > 0
                ? $" | ETA {FormatEtaMs(etaMs)}"
                : "";
        string elapsed = TimeSpan
            .FromMilliseconds(p.ElapsedMilliseconds)
            .ToString(@"hh\:mm\:ss\.f");
        // Full-space sweeps sit below 1% for hours; F1 pins them at "0.0%" and hides the fact
        // that anything is moving at all. Widen the decimals only where they carry information.
        string pct =
            p.PercentComplete >= 1.0 ? $"{p.PercentComplete:F2}" : $"{p.PercentComplete:F5}";
        StickyProgress.Update(
            $"Progress: {pct}% | {p.SeedsSearched:N0} searched | {p.MatchingSeeds:N0} matches | {speed}{eta} | {elapsed}"
        );
    }

    static string FormatEtaMs(long milliseconds)
    {
        var rem = TimeSpan.FromMilliseconds(milliseconds);
        return rem.TotalHours >= 24 ? rem.ToString(@"d\.hh\:mm\:ss") : rem.ToString(@"hh\:mm\:ss");
    }

    static bool ShouldEmitScore(
        int score,
        bool cutoffAuto,
        int cutoffFixed,
        ref int cliLearnedCutoff
    )
    {
        if (!cutoffAuto)
            return cutoffFixed == int.MinValue || score >= cutoffFixed;

        int observed = Volatile.Read(ref cliLearnedCutoff);
        while (true)
        {
            if (score < observed)
                return false;

            if (score == observed)
                return true;

            int original = Interlocked.CompareExchange(ref cliLearnedCutoff, score, observed);
            if (original == observed)
                return true;

            observed = original;
        }
    }
}
