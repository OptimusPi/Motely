using System.Runtime.InteropServices;
using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;
using Motely;
using Motely.CLI;
using Motely.Analysis;
using Motely.DB.SeedSource;
using Motely.Filters;
using Motely.Filters.Native;


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
        out string? error)
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
        if (seed.Length == 0 || seed.Length > MotelyGlobals.MaxSeedLength)
        {
            error = $"'{input}' must be 1–{MotelyGlobals.MaxSeedLength} characters (1-9, A-Z).";
            return false;
        }
        // Pad to 8 chars — short seeds like "A" mean "A1111111" (leftmost significant)
        if (seed.Length < MotelyGlobals.MaxSeedLength)
            seed = seed + new string(MotelyGlobals.SeedDigits[0], MotelyGlobals.MaxSeedLength - seed.Length);
        foreach (char c in seed)
        {
            if (!MotelyGlobals.SeedDigits.Contains(c))
            {
                error = $"'{input}' contains invalid character '{c}'. Valid: 1-9, A-Z (no 0).";
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

        // Key listener: Esc quits, 'p' prints the latest progress on demand.
        // Skip entirely when stdin is redirected (Console.KeyAvailable throws in that case),
        // and swallow exceptions so a terminal that can't check keys doesn't kill the run.
        var escCts = new CancellationTokenSource();
        var escThread = new Thread(() =>
        {
            if (Console.IsInputRedirected) return;
            try
            {
                while (!escCts.Token.IsCancellationRequested)
                {
                    // Avoid Console.ReadKey — it holds an internal lock that can deadlock
                    // with Console.WriteLine on worker threads.
                    Thread.Sleep(100);
                    if (!Console.KeyAvailable) continue;
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        RequestTermination();
                    }
                    else if (key.KeyChar == 'p' || key.KeyChar == 'P')
                    {
                        PrintLatestProgressOnDemand();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (InvalidOperationException) { /* stdin went away mid-run */ }
            catch (IOException) { /* terminal disconnected */ }
        })
        { IsBackground = true, Name = "Console Key Listener" };
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
            "Analyze one or more seeds (comma-separated). With --output-json emits NDJSON.",
            CommandOptionType.SingleValue
        );
        var outputJsonOption = app.Option(
            "--output-json",
            "Output analysis as JSON (or NDJSON for multiple seeds)",
            CommandOptionType.NoValue
        );
        var deckOption = app.Option<string>(
            "--deck <DECK>",
            "Deck name for analysis/search (default: Red for search, Erratic for analyze)",
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
        var sinkOption = app.Option<string>(
            "--sink <NAME_OR_PATH>",
            "Result sink file name or absolute path",
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
        var quietOption = app.Option(
            "-q|--quiet|--no-progress",
            "Suppress per-batch progress lines and the startup preamble on stderr (stdout results unaffected).",
            CommandOptionType.NoValue
        );
        var writeJamlSchemaOption = app.Option(
            "--write-jaml-schema",
            "Regenerate jaml.schema.json from JamlConfig.cs via the AOT-safe schema exporter. Writes to repo root and tools/jaml-language/{jaml-schema,vscode-extension}/schemas/.",
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

            // --write-jaml-schema is a standalone maintenance command; run it and exit.
            if (writeJamlSchemaOption.HasValue())
            {
                return JamlSchemaGenerator.WriteDefault(log: Console.Error);
            }

            // --analyze mode — supports single seed or comma-separated batch
            if (analyzeOption.HasValue())
            {
                var analyzeDeck = deckOption.HasValue() ? deckOption.ParsedValue : "Erratic";
                var analyzeStake = stakeOption.HasValue() ? stakeOption.ParsedValue : "White";
                var seedTokens = analyzeOption.ParsedValue
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (seedTokens.Length == 1)
                    return ExecuteAnalyze(seedTokens[0], analyzeDeck, analyzeStake, outputJsonOption.HasValue());

                // Batch mode — emit NDJSON for each seed (one JSON object per line)
                return ExecuteAnalyzeBatch(seedTokens, analyzeDeck, analyzeStake, outputJsonOption.HasValue());
            }

            // --native mode — run a hardcoded C# filter by name
            if (nativeOption.HasValue())
            {
                var nDeck = deckOption.HasValue() ? Enum.Parse<MotelyDeck>(deckOption.ParsedValue, true) : MotelyDeck.Red;
                var nStake = stakeOption.HasValue() ? Enum.Parse<MotelyStake>(stakeOption.ParsedValue, true) : MotelyStake.White;
                int nThreads = threadsOption.HasValue() ? threadsOption.ParsedValue : Environment.ProcessorCount;
                int nBatch = batchCharCountOption.ParsedValue;

                if (!MotelyNativeFilterNames.TryParse(nativeOption.ParsedValue, out var nativeFilter))
                {
                    Console.Error.WriteLine(
                        $"Error: unknown native filter '{nativeOption.ParsedValue}'. Known: {string.Join(", ", MotelyNativeFilterNames.DisplayNames)}");
                    return 1;
                }

                IMotelySearchSettings nSettings = MotelyNativeFilterFactory.CreateSettings(nativeFilter);

                nSettings = nSettings
                    .WithDeck(nDeck)
                    .WithStake(nStake)
                    .WithThreadCount(nThreads);

                if (!TryParseSeedOptions(startSeedOption, stopSeedOption, out var nStartIdx, out var nStopIdx, out var seedOptError))
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
                            KeywordInputs: BuildKeywordInputs(keywordOption, keywordsOption),
                            PaddingCharsOption: paddingOption.HasValue() ? paddingOption.ParsedValue : null,
                            RandomCount: randomOption.HasValue() ? randomOption.ParsedValue : null,
                            AestheticName: aestheticOption.HasValue() ? aestheticOption.ParsedValue : null,
                            StartBatch: startBatchOption.HasValue() ? startBatchOption.ParsedValue : null,
                            EndBatch: endBatchOption.HasValue() ? endBatchOption.ParsedValue : null,
                            StartPercent: startPercentOption.HasValue() ? startPercentOption.ParsedValue : null,
                            StartSeedSearchIndex: nStartIdx,
                            StopSeedSearchIndex: nStopIdx,
                            BatchCharacterCount: nBatch,
                            JamlAestheticFallback: null
                        ),
                        msg => Console.Error.WriteLine(msg),
                        out var nSearchModeError,
                        out nSettings
                    )
                )
                {
                    Console.Error.WriteLine(nSearchModeError);
                    return 1;
                }

                // Always attach a progress callback so 'p' hotkey has fresh data;
                // quiet mode just swaps in the silent capture variant.
                nSettings = nSettings
                    .WithSeedMatchCallback(seed => Console.WriteLine(seed))
                    .WithProgressCallback(quietOption.HasValue()
                        ? CaptureNativeProgress
                        : WriteNativeProgressLineToStderr);

                if (!quietOption.HasValue())
                    Console.Error.WriteLine(
                        $"Motely native: {nativeOption.ParsedValue} | {nDeck} {nStake} | threads={nThreads} | batchCharCount={nBatch} (sequential only)");
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

            if (
                !JamlConfigLoader.TryLoadFromFile(
                    jamlOption.ParsedValue,
                    out var config,
                    out var loadError
                )
            )
            {
                Console.Error.WriteLine($"Error: {loadError}");
                return 1;
            }

            if (!config.HasAnyClauses)
            {
                Console.Error.WriteLine("Error: no clauses in JAML.");
                return 1;
            }

            var deck = config.Deck;
            var stake = config.Stake;
            int threads = threadsOption.HasValue()
                ? threadsOption.ParsedValue
                : Environment.ProcessorCount;
            int batchCharCount = batchCharCountOption.ParsedValue;

            bool cutoffAuto = false;
            int cutoffFixed = int.MinValue;
            int currentHigh = int.MinValue;
            if (cutoffOption.HasValue())
            {
                var cutoffValue = cutoffOption.ParsedValue.Trim();
                if (string.Equals(cutoffValue, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    cutoffAuto = true;
                }
                else if (!int.TryParse(cutoffValue, out cutoffFixed))
                {
                    Console.Error.WriteLine("Error: --cutoff must be an integer or 'auto'.");
                    return 1;
                }
            }

            JamlSearchPlan plan;
            try
            {
                // Push fixed --cutoff into the engine so low-scoring seeds are dropped at
                // the scorer (no callback spam, no per-seed string concat). Auto still needs
                // the caller-side running-max below since the engine threshold is static.
                int engineCutoff = (!cutoffAuto && cutoffFixed > int.MinValue) ? cutoffFixed : 0;
                plan = JamlSearchBuilder.CreatePlan(config, engineCutoff);
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }

            IMotelySearchSettings settings = plan.Settings
                .WithDeck(deck)
                .WithStake(stake)
                .WithThreadCount(threads);

            if (!TryParseSeedOptions(startSeedOption, stopSeedOption, out var jStartIdx, out var jStopIdx, out var jSeedOptError))
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
                        KeywordInputs: BuildKeywordInputs(keywordOption, keywordsOption),
                        PaddingCharsOption: paddingOption.HasValue() ? paddingOption.ParsedValue : null,
                        RandomCount: randomOption.HasValue() ? randomOption.ParsedValue : null,
                        AestheticName: aestheticOption.HasValue() ? aestheticOption.ParsedValue : null,
                        StartBatch: startBatchOption.HasValue() ? startBatchOption.ParsedValue : null,
                        EndBatch: endBatchOption.HasValue() ? endBatchOption.ParsedValue : null,
                        StartPercent: startPercentOption.HasValue() ? startPercentOption.ParsedValue : null,
                        StartSeedSearchIndex: jStartIdx,
                        StopSeedSearchIndex: jStopIdx,
                        BatchCharacterCount: batchCharCount,
                        JamlAestheticFallback: config.Aesthetics
                    ),
                    msg => Console.Error.WriteLine(msg),
                    out var jamlSearchModeError,
                    out settings
                )
            )
            {
                Console.Error.WriteLine(jamlSearchModeError);
                return 1;
            }

            int scoreTallyColumns = plan.ScoreTallyColumnCount;
            bool hasStructuredScores = scoreTallyColumns > 0;

            using ISeedResultSink? sink = sinkOption.HasValue()
                ? SeedResultSinkFactory.Create(sinkOption.ParsedValue, scoreTallyColumns)
                : null;

            // Always attach a progress callback so 'p' hotkey stays current;
            // quiet mode swaps in the silent capture variant.
            settings = settings.WithProgressCallback(quietOption.HasValue()
                ? CaptureJamlProgress
                : WriteJamlProgressLineToStderr);

            if (hasStructuredScores)
            {
                settings = settings.WithScoredResultCallback(tally =>
                {
                    if (cutoffAuto)
                    {
                        // Running maximum: print every seed at or above the best score so far (ties at the max included).
                        if (tally.Score < currentHigh) return;
                        currentHigh = Math.Max(currentHigh, tally.Score);
                    }
                    else if (tally.Score < cutoffFixed)
                        return;

                    var tallies = string.Join(",", tally.TallyValuesSpan.ToArray());
                    Console.WriteLine($"{tally.Seed},{tally.Score},{tallies}");
                    sink?.AppendScoredResult(tally.Seed, tally.Score, tally.TallyValuesSpan);
                });
            }

            if (!quietOption.HasValue())
            {
                Console.Error.WriteLine(
                    $"Motely: {config.Name ?? jamlOption.ParsedValue} | {deck} {stake} | threads={threads} | batchCharCount={batchCharCount} (sequential only)"
                );
                if (sink != null)
                    Console.Error.WriteLine($"Sink: {sink.OutputPath}");
            }

            using var search = settings.Start(_cts.Token);
            await search.WaitForCompletionAsync(_cts.Token);

            bool cancelled = _cts.Token.IsCancellationRequested;
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

    static void PrintSummary(IMotelySearch search, int batchCharCount, bool cancelled)
    {
        Console.Out.Flush();
        Console.WriteLine();
        Console.WriteLine(cancelled ? "STOPPED" : "COMPLETED");
        Console.WriteLine(
            $"  Seeds: {search.TotalSeedsSearched:N0} searched, {search.MatchingSeeds:N0} matched"
        );
        var elapsed = TimeSpan.FromMilliseconds(search.ElapsedMs);
        Console.WriteLine($"  Time:  {elapsed:hh\\:mm\\:ss\\.fff}");
        double speed =
            elapsed.TotalSeconds > 0
                ? search.TotalSeedsSearched / elapsed.TotalSeconds
                : 0;
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

    static int ExecuteAnalyzeBatch(string[] seeds, string deckName, string stakeName, bool json)
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

        int errors = 0;
        foreach (var seed in seeds)
        {
            try
            {
                var normalizedSeed = seed.Trim().ToUpperInvariant().Replace('0', 'O');
                var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(normalizedSeed, d, s));

                if (json)
                {
                    var erratic =
                        analysis.ErraticDeckComposition?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        ?? [];
                    var dto = new SeedAnalysisDto
                    {
                        Seed = normalizedSeed,
                        Deck = d.ToString(),
                        Stake = s.ToString(),
                        ErraticDeckComposition = erratic,
                        Error = analysis.Error,
                        Antes = analysis
                            .Antes.Select(a => new AnteAnalysisDto
                            {
                                Ante = a.Ante,
                                Boss = FormatUtils.FormatBoss(a.Boss),
                                Voucher = FormatUtils.FormatVoucher(a.Voucher),
                                SmallBlindTag = FormatUtils.FormatTag(a.SmallBlindTag),
                                BigBlindTag = FormatUtils.FormatTag(a.BigBlindTag),
                                DrawOrder = a.DrawOrder ?? "",
                                ShopQueue = a
                                    .ShopQueue.Select(item => new ShopItemDto
                                    {
                                        Id = item.ToString(),
                                        Name = FormatUtils.FormatItem(item),
                                        Value = item.Value,
                                    })
                                    .ToArray(),
                                Packs = a
                                    .Packs.Select(p => new PackDto
                                    {
                                        Type = FormatUtils.FormatPackName(p.Type),
                                        Items = p.Items.Select(FormatUtils.FormatItem).ToArray(),
                                    })
                                    .ToArray(),
                            })
                            .ToArray(),
                    };
                    // NDJSON: one JSON object per line, no extra whitespace
                    Console.WriteLine(
                        JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto)
                    );
                }
                else
                {
                    Console.WriteLine($"=== {normalizedSeed} | {d} {s} ===");
                    Console.Write(analysis);
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ERROR] {seed}: {ex.Message}");
                errors++;
            }
        }

        return errors == 0 ? 0 : 1;
    }

    // ── Analyze (single) ──

    static int ExecuteAnalyze(string seed, string deckName, string stakeName, bool json)
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
        var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(normalizedSeed, d, s));

        if (json)
        {
            var erratic =
                analysis.ErraticDeckComposition?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? [];
            var dto = new SeedAnalysisDto
            {
                Seed = normalizedSeed,
                Deck = d.ToString(),
                Stake = s.ToString(),
                ErraticDeckComposition = erratic,
                Error = analysis.Error,
                Antes = analysis
                    .Antes.Select(a => new AnteAnalysisDto
                    {
                        Ante = a.Ante,
                        Boss = FormatUtils.FormatBoss(a.Boss),
                        Voucher = FormatUtils.FormatVoucher(a.Voucher),
                        SmallBlindTag = FormatUtils.FormatTag(a.SmallBlindTag),
                        BigBlindTag = FormatUtils.FormatTag(a.BigBlindTag),
                        DrawOrder = a.DrawOrder ?? "",
                        ShopQueue = a
                            .ShopQueue.Select(item => new ShopItemDto
                            {
                                Id = item.ToString(),
                                Name = FormatUtils.FormatItem(item),
                                Value = item.Value,
                            })
                            .ToArray(),
                        Packs = a
                            .Packs.Select(p => new PackDto
                            {
                                Type = FormatUtils.FormatPackName(p.Type),
                                Items = p.Items.Select(FormatUtils.FormatItem).ToArray(),
                            })
                            .ToArray(),
                    })
                    .ToArray(),
            };
            Console.WriteLine(
                JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto)
            );
        }
        else
        {
            Console.WriteLine($"=== {normalizedSeed} | {d} {s} ===");
            Console.Write(analysis);
            Console.WriteLine();
        }
        return 0;
    }

    // Cached latest progress so 'p' key can print on demand even under --quiet.
    static MotelyProgress? _latestProgress;

    static int _lastNativePercent = -1;
    static void WriteNativeProgressLineToStderr(MotelyProgress p)
    {
        _latestProgress = p;
        int pct = (int)p.PercentComplete;
        if (pct <= _lastNativePercent) return;
        _lastNativePercent = pct;
        FormatProgressToStderr(p);
    }

    static int _lastJamlPercent = -1;
    static void WriteJamlProgressLineToStderr(MotelyProgress p)
    {
        _latestProgress = p;
        int pct = (int)p.PercentComplete;
        if (pct <= _lastJamlPercent) return;
        _lastJamlPercent = pct;
        FormatProgressToStderr(p);
    }

    // Quiet-mode callbacks: capture latest progress silently so the 'p' hotkey
    // still has something to print.
    static void CaptureNativeProgress(MotelyProgress p) => _latestProgress = p;
    static void CaptureJamlProgress(MotelyProgress p) => _latestProgress = p;

    static void PrintLatestProgressOnDemand()
    {
        if (_latestProgress is { } p) FormatProgressToStderr(p);
    }

    static void FormatProgressToStderr(MotelyProgress p)
    {
        double perSec = p.SeedsPerMillisecond * 1000.0;
        string speed =
            perSec >= 1_000_000
                ? $"{perSec / 1_000_000:F2} M/s"
                : perSec >= 1_000
                    ? $"{perSec / 1_000:F1} K/s"
                    : $"{perSec:F0}/s";
        string eta = p.EstimatedTimeRemainingMilliseconds is long etaMs && etaMs > 0
            ? $" | ETA {FormatEtaMs(etaMs)}"
            : "";
        string elapsed = TimeSpan.FromMilliseconds(p.ElapsedMilliseconds).ToString(@"hh\:mm\:ss\.f");
        Console.Error.WriteLine(
            $"Progress: {p.PercentComplete:F1}% | {p.SeedsSearched:N0} searched | {p.MatchingSeeds:N0} matches | {speed}{eta} | {elapsed}");
    }

    static string FormatEtaMs(long milliseconds)
    {
        var rem = TimeSpan.FromMilliseconds(milliseconds);
        return rem.TotalHours >= 24 ? rem.ToString(@"d\.hh\:mm\:ss") : rem.ToString(@"hh\:mm\:ss");
    }
}