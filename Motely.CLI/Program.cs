using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.DB.SeedSource;
using Motely.Executors;
using Motely.Filters;

namespace Motely;

partial class Program
{
    private static readonly CancellationTokenSource _cts = new();
    private static volatile IMotelySearch? _activeSearch;

    static void RequestTermination()
    {
        try { _activeSearch?.Cancel(); } catch { }
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

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestTermination();
        };

        // ESC key to quit (same as Ctrl+C) while a search is running
        var escCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!escCts.Token.IsCancellationRequested)
            {
                await Task.Delay(100, escCts.Token).ConfigureAwait(false);
                if (_activeSearch == null) continue;
                if (!Console.KeyAvailable) continue;
                if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                    RequestTermination();
            }
        }, escCts.Token);

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
            "Batch character count (1-7, default 2)",
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
        var randomOption = app.Option<int>(
            "--random <N>",
            "Random seed count",
            CommandOptionType.SingleValue
        );
        var palindromeOption = app.Option(
            "--palindrome",
            "Palindrome seeds",
            CommandOptionType.NoValue
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
            "Minimum score to print, or 'auto' to only show new highs",
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
        var writeJamlSchemaOption = app.Option(
            "--write-jaml-schema",
            "Generate and sync the JAML JSON schema files from the current code model",
            CommandOptionType.NoValue
        );

        threadsOption.DefaultValue = Environment.ProcessorCount;
        batchCharCountOption.DefaultValue = 4;

        app.OnExecuteAsync(async _ =>
        {
            if (writeJamlSchemaOption.HasValue())
            {
                string repoRoot = JamlSchemaGenerator.FindRepoRoot(AppContext.BaseDirectory);
                JamlSchemaGenerator.GenerateAndWriteAll(repoRoot);
                Console.WriteLine($"JAML schema written using MotelyVersion={JamlSchemaGenerator.ReadMotelyVersion(repoRoot)}");
                return 0;
            }

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
                var seedTokens = analyzeOption.ParsedValue
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (seedTokens.Length == 1)
                    return ExecuteAnalyze(seedTokens[0], analyzeDeck, analyzeStake, outputJsonOption.HasValue());

                // Batch mode — emit NDJSON for each seed (one JSON object per line)
                return ExecuteAnalyzeBatch(seedTokens, analyzeDeck, analyzeStake, outputJsonOption.HasValue());
            }

            // --jaml mode
            if (!jamlOption.HasValue())
            {
                Console.Error.WriteLine("Error: --jaml <path> required.");
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
            int batchCharCount = batchCharCountOption.HasValue() ? batchCharCountOption.ParsedValue : 4;

            bool hasSeedListMode = sourceOption.HasValue() || seedsOption.HasValue();
            bool hasKeywordMode = keywordOption.HasValue() || keywordsOption.HasValue();

            if (sourceOption.HasValue() && seedsOption.HasValue())
            {
                Console.Error.WriteLine(
                    "Error: choose only one explicit seed input: --source or --seeds."
                );
                return 1;
            }

            int explicitSearchModeCount = 0;
            if (hasSeedListMode) explicitSearchModeCount++;
            if (hasKeywordMode) explicitSearchModeCount++;
            if (randomOption.HasValue()) explicitSearchModeCount++;
            if (palindromeOption.HasValue()) explicitSearchModeCount++;

            if (explicitSearchModeCount > 1)
            {
                Console.Error.WriteLine(
                    "Error: choose only one search input mode: --source, --seeds, --keyword, --keywords, --random, or --palindrome."
                );
                return 1;
            }

            string[]? explicitSeeds = null;
            if (sourceOption.HasValue())
            {
                try
                {
                    var sourceSeeds = SeedReader.ReadSeeds(sourceOption.ParsedValue);
                    if (sourceSeeds.Count == 0)
                    {
                        Console.Error.WriteLine("Error: resolved source contained no seeds.");
                        return 1;
                    }

                    explicitSeeds = sourceSeeds.ToArray();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    return 1;
                }
            }
            else if (seedsOption.HasValue())
            {
                var seedsValue = seedsOption.ParsedValue;
                var looksLikeSourcePath = seedsValue.Contains(Path.DirectorySeparatorChar)
                    || seedsValue.Contains(Path.AltDirectorySeparatorChar)
                    || Path.HasExtension(seedsValue);

                if (looksLikeSourcePath)
                {
                    try
                    {
                        var sourceSeeds = SeedReader.ReadSeeds(seedsValue);
                        if (sourceSeeds.Count == 0)
                        {
                            Console.Error.WriteLine("Error: resolved seed source contained no seeds.");
                            return 1;
                        }

                        Console.Error.WriteLine("Warning: --seeds <path> is deprecated; use --source <path>.");
                        explicitSeeds = sourceSeeds.ToArray();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error: {ex.Message}");
                        return 1;
                    }
                }
                else
                {
                    var inlineSeeds = SeedReader.ParseInlineSeeds(seedsValue);
                    if (inlineSeeds.Count == 0)
                    {
                        Console.Error.WriteLine("Error: --seeds requires at least one inline seed.");
                        return 1;
                    }

                    explicitSeeds = inlineSeeds.ToArray();
                }
            }

            var keywordInputs = new List<string>();
            if (keywordOption.HasValue())
                keywordInputs.Add(keywordOption.ParsedValue);

            if (keywordsOption.HasValue())
            {
                keywordInputs.AddRange(
                    keywordsOption.ParsedValue.Split(',', StringSplitOptions.TrimEntries)
                );
            }

            var rawSearchOptions = new SearchOptionsDto
            {
                StartBatch = startBatchOption.HasValue() ? startBatchOption.ParsedValue : null,
                EndBatch = endBatchOption.HasValue() ? endBatchOption.ParsedValue : null,
                Seeds = explicitSeeds,
                Keywords = keywordInputs.Count > 0 ? keywordInputs.ToArray() : null,
                Padding = paddingOption.HasValue() ? paddingOption.ParsedValue : null,
                RandomSeeds = randomOption.HasValue() ? randomOption.ParsedValue : null,
                Palindrome = palindromeOption.HasValue() ? true : null,
            };
            var (searchRequest, requestError) = MotelySearchRequestFactory.FromOptions(
                rawSearchOptions,
                threads,
                batchCharCount
            );
            if (requestError != null || searchRequest == null)
            {
                Console.Error.WriteLine($"Error: {requestError ?? "Search request could not be created."}");
                return 1;
            }

            var (plan, _, prepareError) = MotelySearchOrchestrator.PrepareSearch(
                config,
                searchRequest
            );
            if (prepareError != null || plan == null)
            {
                Console.Error.WriteLine($"Error: {prepareError ?? "Search could not be prepared."}");
                return 1;
            }

            var settings = plan.Settings;
            bool hasStructuredScores = plan.ShouldClauseCount > 0;
            using ISeedResultSink? sink = sinkOption.HasValue()
                ? SeedResultSinkFactory.Create(sinkOption.ParsedValue, plan.ShouldClauseCount)
                : null;

            // CLI output — seeds to stdout, progress to stderr
            // Parse cutoff: number = fixed threshold, "best" = only print new highs
            bool cutoffBest = false;
            int cutoffNum = 0;
            if (cutoffOption.HasValue())
            {
                string cutoffVal = cutoffOption.Value()!;
                if (
                    cutoffVal.Equals("best", StringComparison.OrdinalIgnoreCase)
                    || cutoffVal.Equals("auto", StringComparison.OrdinalIgnoreCase)
                )
                    cutoffBest = true;
                else if (!int.TryParse(cutoffVal, out cutoffNum))
                {
                    Console.Error.WriteLine(
                        $"Error: --cutoff must be a number or 'auto', got '{cutoffVal}'."
                    );
                    return 1;
                }
            }

            int bestScoreSoFar = int.MinValue;
            object bestLock = new();

            if (hasStructuredScores)
            {
                settings.WithScoredResultCallback(tally =>
                {
                    sink?.AppendScoredResult(tally.Seed, tally.Score, tally.TallyValuesSpan);
                });
            }

            settings.WithSeedMatchCallback(line =>
            {
                if (!hasStructuredScores)
                    sink?.AppendSeed(line);

                // Parse score from format: SEED,SCORE,...
                int firstComma = line.IndexOf(',');
                if (firstComma < 0)
                {
                    Console.WriteLine(line);
                    return;
                }

                int secondComma = line.IndexOf(',', firstComma + 1);
                var scoreSpan =
                    secondComma >= 0
                        ? line.AsSpan(firstComma + 1, secondComma - firstComma - 1)
                        : line.AsSpan(firstComma + 1);

                if (!int.TryParse(scoreSpan, out int score))
                {
                    Console.WriteLine(line);
                    return;
                }

                if (cutoffBest)
                {
                    lock (bestLock)
                    {
                        if (score > bestScoreSoFar)
                        {
                            bestScoreSoFar = score;
                            Console.WriteLine(line);
                        }
                    }
                }
                else if (score >= cutoffNum)
                {
                    Console.WriteLine(line);
                }
            });
            settings.WithProgressMessageCallback(msg => Console.Error.WriteLine(msg));

            var header = "SEED,SCORE";
            foreach (var l in plan.MustLabels)
                header += $",\"{l}\"";
            foreach (var l in plan.ShouldLabels)
                header += $",\"{l}\"";
            Console.WriteLine(header);

            Console.Error.WriteLine(
                $"Motely: {config.Name ?? jamlOption.ParsedValue} | {deck} {stake} | threads={threads} batchCharCount={batchCharCount}"
            );
            if (sink != null)
                Console.Error.WriteLine($"Sink: {sink.OutputPath}");

            using var search = settings.CreateSearch();
            _activeSearch = search;
            try
            {
                await Task.Run(() => search.Start(_cts.Token));
            }
            catch (OperationCanceledException) { }
            finally
            {
                _activeSearch = null;
            }

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
            $"  Seeds: {search.TotalSeedsSearched:N0} searched, {search.MatchingSeeds} matched"
        );
        Console.WriteLine($"  Time:  {search.ElapsedTime:hh\\:mm\\:ss\\.fff}");
        double speed =
            search.ElapsedTime.TotalSeconds > 0
                ? search.TotalSeedsSearched / search.ElapsedTime.TotalSeconds
                : 0;
        Console.WriteLine($"  Speed: {speed:N0} seeds/sec");
        if (search.IsSequentialBatchSearch)
        {
            long max = (long)Math.Pow(35, 8 - batchCharCount);
            double pct = max > 0 ? (double)search.CompletedBatchCount * 100.0 / max : 0;
            Console.WriteLine($"  Batch: {search.CompletedBatchCount:N0} / {max:N0} ({pct:F4}%)");
            if (cancelled)
                Console.WriteLine($"  Resume: --startBatch {search.CompletedBatchCount}");
        }
    }

    // ── Analyze (batch) ──

    static int ExecuteAnalyzeBatch(string[] seeds, string deckName, string stakeName, bool json)
    {
        if (!Enum.TryParse<MotelyDeck>(deckName, true, out var d))
        {
            Console.Error.WriteLine($"Invalid deck: {deckName}");
            return 1;
        }
        if (!Enum.TryParse<MotelyStake>(stakeName, true, out var s))
        {
            Console.Error.WriteLine($"Invalid stake: {stakeName}");
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
                        Twos = erratic.Count(c => c.StartsWith("2_")),
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
            Console.Error.WriteLine($"Invalid deck: {deckName}");
            return 1;
        }
        if (!Enum.TryParse<MotelyStake>(stakeName, true, out var s))
        {
            Console.Error.WriteLine($"Invalid stake: {stakeName}");
            return 1;
        }

        var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, d, s));

        if (json)
        {
            var erratic =
                analysis.ErraticDeckComposition?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? [];
            var dto = new SeedAnalysisDto
            {
                Seed = seed,
                Deck = d.ToString(),
                Stake = s.ToString(),
                ErraticDeckComposition = erratic,
                Twos = erratic.Count(c => c.StartsWith("2_")),
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
            Console.WriteLine($"Analyzing: {seed} | {d} {s}");
            Console.Write(analysis);
        }
        return 0;
    }
}
