using System.Runtime.InteropServices;
using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;
using Motely;
using Motely.Analysis;
using Motely.CLI;
using Motely.Filters;
using Motely.Filters.Native;
using YamlDotNet.RepresentationModel;

partial class Program
{
    private static readonly CancellationTokenSource _cts = new();

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
        var seed = input.Trim().ToUpperInvariant().Replace('0', 'O');
        // Motely's sequential search ranges over full 8-char seeds (11111111 → ZZZZZZZZ),
        // so --startSeed/--stopSeed must be exactly 8 chars. No padding: a short seed
        // would silently map to a different point than the user typed.
        if (seed.Length != MotelyGlobals.MaxSeedLength)
        {
            error = $"'{input}' must be exactly {MotelyGlobals.MaxSeedLength} characters (1-9, A-Z).";
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
            "JAML config file",
            CommandOptionType.SingleValue
        );
        var analyzeOption = app.Option<string>(
            "--analyze <SEED[,SEED...]>",
            "Analyze one or more seeds (comma-separated) as human-readable text.",
            CommandOptionType.SingleValue
        );
        var saveSeedsOption = app.Option(
            "--save-seeds",
            "Write the top 1000 matched seeds back into the JAML file's top-level seeds: block.",
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
        var sourceOption = app.Option<string>(
            "--source <NAME_OR_PATH>",
            "Seed source file name or absolute path",
            CommandOptionType.SingleValue
        );
        var drownOption = app.Option(
            "--drown",
            "Replay all saved seeds for this JAML filter from results/<filterId>/results.csv using DuckDB.",
            CommandOptionType.NoValue
        );
        var resultsPathOption = app.Option<string>(
            "--results-path <PATH>",
            "Root folder containing per-filter results folders with results.csv files (default: ./results).",
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
            "Restrict padding chars for --keyword/--keywords (e.g. \"67Z\" uses only 6, 7, Z as padding)",
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
        batchCharCountOption.DefaultValue = 4;

        app.OnExecuteAsync(async _ =>
        {
            if (args.Length == 0)
            {
                app.ShowHelp();
                return 0;
            }

            // --analyze mode — supports single seed or comma-separated batch
            if (analyzeOption.HasValue())
            {
                var analyzeDeck = deckOption.HasValue() ? deckOption.ParsedValue : "Erratic";
                var analyzeStake = stakeOption.HasValue() ? stakeOption.ParsedValue : "White";
                var seedTokens = analyzeOption.ParsedValue.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                );

                if (seedTokens.Length == 1)
                    return ExecuteAnalyze(seedTokens[0], analyzeDeck, analyzeStake);

                return ExecuteAnalyzeBatch(seedTokens, analyzeDeck, analyzeStake);
            }

            // --native mode — run a hardcoded C# filter by name
            if (nativeOption.HasValue())
            {
                // WRONG i -- this is so fucked upo claude.
                if (drownOption.HasValue())
                {
                    Console.Error.WriteLine(
                        "Error: --drown currently requires --jaml so the CLI can resolve the normalized filterId."
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
                int nBatch = batchCharCountOption.ParsedValue;

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
                            StartSeedSearchIndex: nStartIdx,
                            StopSeedSearchIndex: nStopIdx,
                            BatchCharacterCount: nBatch,
                            JamlAestheticFallback: null
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
                    .WithSeedMatchCallback(seed => Console.WriteLine(seed))
                    .WithProgressCallback(
                        quietOption.HasValue()
                            ? CaptureNativeProgress
                            : WriteNativeProgressLineToStderr
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

            // --jaml mode
            if (!jamlOption.HasValue())
            {
                Console.Error.WriteLine("Error: --jaml <path> or --native <name> required.");
                return 1;
            }

            string jamlPath = jamlOption.ParsedValue;
            if (!Path.IsPathRooted(jamlPath) && !Path.HasExtension(jamlPath))
                jamlPath = Path.Combine("JamlFilters", jamlPath + ".jaml");
            string jamlContent;
            try
            {
                jamlContent = File.ReadAllText(jamlPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading JAML file: {ex.Message}");
                return 1;
            }

            if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var loadError))
            {
                Console.Error.WriteLine($"Error: {loadError}");
                return 1;
            }

            if (
                config.Must.Count == 0
                && config.Should.Count == 0
                && config.MustNot.Count == 0
            )
            {
                Console.Error.WriteLine("Error: no clauses in JAML.");
                return 1;
            }

            var deck = config.Deck;
            var stake = config.Stake;
            bool drown = drownOption.HasValue();
            int threads =
                drown ? 1
                : threadsOption.HasValue() ? threadsOption.ParsedValue
                : Environment.ProcessorCount;
            int batchCharCount = batchCharCountOption.ParsedValue;

            if (drown && threadsOption.HasValue() && threadsOption.ParsedValue != 1)
                Console.Error.WriteLine(
                    "Warning: --drown forces --threads 1 for safe provider reads."
                );

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
                        BatchCharacterCount: batchCharCount,
                        JamlAestheticFallback: null
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
            using var resultSink = CreateResultSink(
                hasStructuredScores,
                config.Id,
                plan.TallyLabels
            );
            int cliLearnedCutoff = cutoffAuto ? int.MinValue : engineCutoff;
            var saveSeedsCollector = saveSeedsOption.HasValue()
                ? new TopSeedCollector(MotelyGlobals.SavedSeedLimit)
                : null;
            var saveSeedMatches = saveSeedsOption.HasValue()
                ? new List<string>(MotelyGlobals.SavedSeedLimit)
                : null;
            var saveSeedMatchSet = saveSeedsOption.HasValue()
                ? new HashSet<string>(StringComparer.Ordinal)
                : null;

            // Always attach a progress callback so 'p' hotkey stays current;
            // quiet mode swaps in the silent capture variant.
            settings = settings
                .WithProgressCallback(
                    quietOption.HasValue() ? CaptureJamlProgress : WriteJamlProgressLineToStderr
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
                    saveSeedsCollector?.Consider(tally.Seed, tally.Score);
                });
            }
            else
            {
                settings = settings.WithSeedMatchCallback(seed =>
                {
                    resultSink.OnSeed(seed);
                    if (
                        saveSeedMatches != null
                        && saveSeedMatchSet != null
                        && saveSeedMatches.Count < MotelyGlobals.SavedSeedLimit
                        && saveSeedMatchSet.Add(seed)
                    )
                    {
                        saveSeedMatches.Add(seed);
                    }
                });
            }

            if (!quietOption.HasValue())
            {
                Console.Error.WriteLine(
                    $"Motely: {config.Name ?? jamlOption.ParsedValue} | {deck} {stake} | threads={threads} | batchCharCount={batchCharCount} {(drown ? "| drown=results.csv via DuckDB" : "(sequential only)")}"
                );
            }

            using var search = settings.Start(_cts.Token);
            bool cancelled = false;
            try
            {
                await search.WaitForCompletionAsync(_cts.Token);
            }
            catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
            {
                cancelled = true;
            }

            cancelled |= _cts.Token.IsCancellationRequested;
            if (saveSeedsOption.HasValue())
            {
                var seedsToSave = hasStructuredScores
                    ? saveSeedsCollector?.GetSeeds() ?? []
                    : (IReadOnlyList<string>)(saveSeedMatches ?? []);

                if (TryWriteSeedsToJamlFile(jamlOption.ParsedValue, seedsToSave, out var saveError))
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
        });

        try
        {
            return app.Execute(args);
        }
        catch (UnrecognizedCommandParsingException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
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

    private sealed class TopSeedCollector(int limit)
    {
        private readonly PriorityQueue<SavedSeedEntry, (int Score, long Sequence)> _queue = new();
        private long _sequence;

        public void Consider(string seed, int score)
        {
            _queue.Enqueue(new(seed, score, _sequence), (score, _sequence));
            _sequence++;

            if (_queue.Count > limit)
                _queue.Dequeue();
        }

        public IReadOnlyList<string> GetSeeds() =>
            _queue
                .UnorderedItems.Select(static item => item.Element)
                .OrderByDescending(static item => item.Score)
                .ThenBy(static item => item.Sequence)
                .Select(static item => item.Seed)
                .Distinct(StringComparer.Ordinal)
                .Take(limit)
                .ToArray();
    }

    private readonly record struct SavedSeedEntry(string Seed, int Score, long Sequence);

    private static bool TryWriteSeedsToJamlFile(
        string jamlPath,
        IReadOnlyList<string> seeds,
        out string? error
    )
    {
        error = null;

        string original;
        try
        {
            original = File.ReadAllText(jamlPath);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        string normalizedNewline = original.Contains("\r\n", StringComparison.Ordinal)
            ? "\r\n"
            : "\n";
        var normalizedSeeds = seeds
            .Select(static seed => seed.Trim().ToUpperInvariant().Replace('0', 'O'))
            .Where(static seed => !string.IsNullOrWhiteSpace(seed))
            .Distinct(StringComparer.Ordinal)
            .Take(MotelyGlobals.SavedSeedLimit)
            .ToArray();

        var originalHasTrailingNewline = original.EndsWith("\n", StringComparison.Ordinal);
        var lines = original.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        var replacementLines = BuildSeedsBlockLines(normalizedSeeds);

        int seedsStart = FindTopLevelSeedsLine(lines);
        if (seedsStart >= 0)
        {
            int seedsEndExclusive = FindNextTopLevelKeyLine(lines, seedsStart + 1);
            lines.RemoveRange(seedsStart, seedsEndExclusive - seedsStart);
            lines.InsertRange(seedsStart, replacementLines);
        }
        else
        {
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                lines.RemoveAt(lines.Count - 1);

            if (lines.Count > 0)
                lines.Add(string.Empty);

            lines.AddRange(replacementLines);
        }

        var updated = string.Join(normalizedNewline, lines);
        if (originalHasTrailingNewline || lines.Count > 0)
            updated += normalizedNewline;

        try
        {
            var yaml = new YamlStream();
            using var reader = new StringReader(updated);
            yaml.Load(reader);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        if (!JamlConfigLoader.TryLoad(updated, out _, out var loadError))
        {
            error = loadError ?? "Updated JAML did not validate.";
            return false;
        }

        try
        {
            File.WriteAllText(jamlPath, updated);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static List<string> BuildSeedsBlockLines(IReadOnlyList<string> seeds)
    {
        if (seeds.Count == 0)
            return ["seeds: []"];

        var lines = new List<string>(seeds.Count + 1) { "seeds:" };
        lines.AddRange(seeds.Select(static seed => $"  - {seed}"));
        return lines;
    }

    private static int FindTopLevelSeedsLine(IReadOnlyList<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (!TryGetTopLevelKey(lines[i], out var key))
                continue;

            if (string.Equals(key, "seeds", StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static int FindNextTopLevelKeyLine(IReadOnlyList<string> lines, int startIndex)
    {
        for (int i = startIndex; i < lines.Count; i++)
        {
            if (TryGetTopLevelKey(lines[i], out _))
                return i;
        }

        return lines.Count;
    }

    private static bool TryGetTopLevelKey(string line, out string? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(line))
            return false;
        if (char.IsWhiteSpace(line[0]))
            return false;

        var trimmed = line.Trim();
        if (
            trimmed.StartsWith("#", StringComparison.Ordinal)
            || trimmed.StartsWith("-", StringComparison.Ordinal)
        )
            return false;

        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex <= 0)
            return false;

        key = trimmed[..colonIndex].Trim();
        return key.Length > 0;
    }

    static void PrintSummary(IMotelySearch search, int batchCharCount, bool cancelled)
    {
        StickyProgress.Clear();
        Console.Out.Flush();
        Console.WriteLine();
        Console.WriteLine(cancelled ? "STOPPED" : "COMPLETED");
        Console.WriteLine(
            $"  Seeds: {search.TotalSeedsSearched:N0} searched, {search.MatchingSeeds:N0} matched"
        );
        var elapsed = TimeSpan.FromMilliseconds(search.ElapsedMs);
        Console.WriteLine($"  Time:  {elapsed:hh\\:mm\\:ss\\.fff}");
        double speed =
            elapsed.TotalSeconds > 0 ? search.TotalSeedsSearched / elapsed.TotalSeconds : 0;
        Console.WriteLine($"  Speed: {speed:N0} seeds/sec");
        if (search.IsSequentialBatchSearch)
        {
            long max = (long)Math.Pow(35, 8 - batchCharCount);
            double pct = max > 0 ? (double)search.CompletedBatchCount * 100.0 / max : 0;
            Console.WriteLine($"  Batch: {search.CompletedBatchCount:N0} / {max:N0} ({pct:F4}%)");
            if (cancelled)
            {
                long nextBatch = search.CompletedBatchCount;
                Console.WriteLine($"  Resume: --startBatch {nextBatch}");
                if (nextBatch >= 0 && nextBatch < max)
                {
                    string prefix = SeedMath.BatchIndexToSeedPrefix(nextBatch, batchCharCount);
                    string minSeedInBatch =
                        prefix + new string(MotelyGlobals.SeedDigits[0], batchCharCount);
                    Console.WriteLine($"  Resume: --startSeed {minSeedInBatch}");
                }
            }
        }
    }

    // ── Analyze (batch) ──

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
            var seed = rawSeed.Trim().ToUpperInvariant().Replace('0', 'O');

            var analysis = MotelyLegacyTextAnalyzer.Analyze(new(seed, d, s));
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

    // ── Analyze (single) ──

    static int ExecuteAnalyze(string seed, string deckName, string stakeName)
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

        var normalizedSeed = seed.Trim().ToUpperInvariant().Replace('0', 'O');

        var analysis = MotelyLegacyTextAnalyzer.Analyze(new(normalizedSeed, d, s));
        if (!string.IsNullOrEmpty(analysis.Error))
        {
            Console.Error.WriteLine($"Error: {analysis.Error}");
            return 1;
        }

        Console.WriteLine($"=== {normalizedSeed} | {d} {s} ===");
        Console.Write(analysis);
        Console.WriteLine();
        return 0;
    }

    // Cached latest progress so 'p' key can print on demand even under --quiet.
    static MotelyProgress? _latestProgress;

    static int _lastNativePercent = -1;

    static void WriteNativeProgressLineToStderr(MotelyProgress p)
    {
        _latestProgress = p;
        int pct = (int)p.PercentComplete;
        if (pct <= _lastNativePercent)
            return;
        _lastNativePercent = pct;
        FormatProgressToStderr(p);
    }

    static int _lastJamlPercent = -1;

    static void WriteJamlProgressLineToStderr(MotelyProgress p)
    {
        _latestProgress = p;
        int pct = (int)p.PercentComplete;
        if (pct <= _lastJamlPercent)
            return;
        _lastJamlPercent = pct;
        FormatProgressToStderr(p);
    }

    // Quiet-mode callbacks: capture latest progress silently so the 'p' hotkey
    // still has something to print.
    static void CaptureNativeProgress(MotelyProgress p) => _latestProgress = p;

    static void CaptureJamlProgress(MotelyProgress p) => _latestProgress = p;

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
        StickyProgress.Update(
            $"Progress: {p.PercentComplete:F1}% | {p.SeedsSearched:N0} searched | {p.MatchingSeeds:N0} matches | {speed}{eta} | {elapsed}"
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

    static IMotelyResultSink CreateResultSink(
        bool hasStructuredScores,
        string filterId,
        IReadOnlyList<string> tallyLabels
    )
    {
        return new CompositeMotelyResultSink(new IMotelyResultSink[] { new ConsoleResultSink() });
    }
}
