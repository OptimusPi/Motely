using System.Runtime.InteropServices;
using System.Text.Json;
using McMaster.Extensions.CommandLineUtils;
using Motely;
using Motely.Analysis;
using Motely.DB.SeedSource;
using Motely.Filters;


partial class Program
{
    private static readonly CancellationTokenSource _cts = new();

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

        // ESC key to quit (same as Ctrl+C) while a search is running
        var escCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!escCts.Token.IsCancellationRequested)
            {
                await Task.Delay(100, escCts.Token).ConfigureAwait(false);
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
        var repeatsOption = app.Option<string>(
            "--repeats <COUNT>",
            "Search for repeating chars (2-8): AAA, BBB, CCC, ... (generates 26 keywords)",
            CommandOptionType.SingleValue
        );
        var ascendingOption = app.Option<string>(
            "--ascending <LENGTH>",
            "Search for ascending sequences (3-8): 123, 234, 345, ... (3-8 char length)",
            CommandOptionType.SingleValue
        );
        var descendingOption = app.Option<string>(
            "--descending <LENGTH>",
            "Search for descending sequences (3-8): 321, 432, 543, ... (3-8 char length)",
            CommandOptionType.SingleValue
        );
        var grossOption = app.Option(
            "--gross",
            "Search for funny/crude keywords: FART, BUTT, POOP, PUKE, BURP, TOOT, GUTS, SLIME, YUCK, DUMB, LAME, UGLY, WEAK",
            CommandOptionType.NoValue
        );
        var nsfwOption = app.Option(
            "--nsfw",
            "Search for NSFW/swear words and slurs (detection mode for safety warnings)",
            CommandOptionType.NoValue
        );
        var aestheticOption = app.Option(
            "--aesthetic",
            "Search for aesthetic/beautiful keywords: BEAUTY, GRACE, LOVELY, SERENE, BLISS, PEACE, CALM, PURE, BRIGHT",
            CommandOptionType.NoValue
        );
        var mirrorOption = app.Option<string>(
            "--mirror <LENGTH>",
            "Search for visually symmetric patterns (3-8): AHA, OHO, 1A1, 8V8, etc.",
            CommandOptionType.SingleValue
        );
        var funnyOption = app.Option(
            "--funny",
            "Search for funny/humor keywords: LOL, HAHA, LMAO, ROFL, YEET, BRUH, MEME, GOOFY, SILLY, WACKY, ZANY",
            CommandOptionType.NoValue
        );
        var balatrOption = app.Option(
            "--balatro",
            "Search for Balatro game terms: JOKER, CHIPS, MULT, HAND, SPADE, HEART, JIMBO, SHOP, BLIND, ANTE, CARD, WILD",
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
                Console.WriteLine($"JAML schema written using Motely;Version={JamlSchemaGenerator.ReadMotelyVersion(repoRoot)}");
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
            if (repeatsOption.HasValue()) explicitSearchModeCount++;
            if (ascendingOption.HasValue()) explicitSearchModeCount++;
            if (descendingOption.HasValue()) explicitSearchModeCount++;
            if (grossOption.HasValue()) explicitSearchModeCount++;
            if (nsfwOption.HasValue()) explicitSearchModeCount++;
            if (aestheticOption.HasValue()) explicitSearchModeCount++;
            if (mirrorOption.HasValue()) explicitSearchModeCount++;
            if (funnyOption.HasValue()) explicitSearchModeCount++;
            if (balatrOption.HasValue()) explicitSearchModeCount++;

            if (explicitSearchModeCount > 1)
            {
                Console.Error.WriteLine(
                    "Error: choose only one search input mode: --source, --seeds, --keyword, --keywords, --random, --palindrome, --repeats, --ascending, --descending, --gross, --nsfw, --aesthetic, --mirror, --funny, or --balatro."
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

            if (repeatsOption.HasValue())
            {
                if (!int.TryParse(repeatsOption.ParsedValue, out int repeatCount))
                {
                    Console.Error.WriteLine("Error: --repeats requires a numeric value (2-8).");
                    return 1;
                }

                if (repeatCount < 2 || repeatCount > 8)
                {
                    Console.Error.WriteLine("Error: --repeats must be between 2 and 8.");
                    return 1;
                }

                // Generate repeating character keywords: AAA, BBB, CCC, ... ZZZ
                for (char c = 'A'; c <= 'Z'; c++)
                {
                    keywordInputs.Add(new string(c, repeatCount));
                }
            }

            if (ascendingOption.HasValue())
            {
                if (!int.TryParse(ascendingOption.ParsedValue, out int ascendingLength))
                {
                    Console.Error.WriteLine("Error: --ascending requires a numeric value (3-8).");
                    return 1;
                }

                if (ascendingLength < 3 || ascendingLength > 8)
                {
                    Console.Error.WriteLine("Error: --ascending must be between 3 and 8.");
                    return 1;
                }

                // Generate ascending sequences: 123, 234, 345, ... 89A, 9AB, ABC, ... XYZ
                string chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                for (int i = 0; i <= chars.Length - ascendingLength; i++)
                {
                    keywordInputs.Add(chars.Substring(i, ascendingLength));
                }
            }

            if (descendingOption.HasValue())
            {
                if (!int.TryParse(descendingOption.ParsedValue, out int descendingLength))
                {
                    Console.Error.WriteLine("Error: --descending requires a numeric value (3-8).");
                    return 1;
                }

                if (descendingLength < 3 || descendingLength > 8)
                {
                    Console.Error.WriteLine("Error: --descending must be between 3 and 8.");
                    return 1;
                }

                // Generate descending sequences: 321, 432, 543, ... ZYX, ZYXW, etc.
                string chars = "ZYXWVUTSRQPONMLKJIHGFEDCBA987654321";
                for (int i = 0; i <= chars.Length - descendingLength; i++)
                {
                    keywordInputs.Add(chars.Substring(i, descendingLength));
                }
            }

            if (grossOption.HasValue())
            {
                var grossKeywordCompatSeeds = new[]
                {
                    "FART", "BUTT", "POOP", "PUKE", "BURP", "TOOT", "GUTS", "SLIME",
                    "YUCK", "DUMB", "LAME", "UGLY", "WEAK", "CRUD", "CRAP", "SICK",
                    "NASTY", "GROSS"
                };
                keywordInputs.AddRange(grossKeywordCompatSeeds);
            }

            if (nsfwOption.HasValue())
            {
                // Comprehensive NSFW detection list (for content safety warnings)
                var nsfwKeywords = new[]
                {
                    "FUCK", "SHIT", "DAMN", "HELL", "CRAP", "ASSES", "ASSHAT",
                    "DICK", "COCK", "PUSSY", "WHORE", "SLUT", "BITCH", "PISS",
                    "CUNT", "TWAT", "ARSE", "BUGGER", "SOD", "TITS", "BOOBS",
                    "WANKER", "BASTARD", "MOTHERFUCKER", "FAGS", "FAGGOT", "KIKE",
                    "DYKE", "SPIC", "GOOK", "CHINK", "TRANNY", "HOMO", "LESBO",
                    "RETARD", "NIGGA", "NIGGER"
                };
                keywordInputs.AddRange(nsfwKeywords);
            }

            if (aestheticOption.HasValue())
            {
                var aestheticKeywords = new[]
                {
                    "BEAUTY", "GRACE", "LOVELY", "SERENE", "BLISS", "PEACE", "CALM",
                    "PURE", "BRIGHT", "SHINE", "GLEAM", "GLOW", "DIVINE", "SUBLIME",
                    "ELEGANT", "PRETTY", "CHARM", "TENDER", "SWEET", "LOVE", "JOY",
                    "HOPE", "DREAM", "MAGIC", "WONDER", "ANGEL", "GRACE", "SERENE"
                };
                keywordInputs.AddRange(aestheticKeywords);
            }

            if (mirrorOption.HasValue())
            {
                if (!int.TryParse(mirrorOption.ParsedValue, out int mirrorLength))
                {
                    Console.Error.WriteLine("Error: --mirror requires a numeric value (3-8).");
                    return 1;
                }

                if (mirrorLength < 3 || mirrorLength > 8)
                {
                    Console.Error.WriteLine("Error: --mirror must be between 3 and 8.");
                    return 1;
                }

                // Visually symmetric characters
                string symmetricChars = "AHIMOTUVWXy18";

                // Generate all combinations of symmetric characters up to mirrorLength
                void GenerateMirrorPatterns(string current, int remainingLength)
                {
                    if (remainingLength == 0)
                    {
                        keywordInputs.Add(current);
                        return;
                    }

                    foreach (char c in symmetricChars)
                    {
                        GenerateMirrorPatterns(current + c, remainingLength - 1);
                    }
                }

                GenerateMirrorPatterns("", mirrorLength);
            }

            if (funnyOption.HasValue())
            {
                var funnyKeywords = new[]
                {
                    // Classic internet/laughter
                    "LOL", "HAHA", "HEHE", "LMAO", "ROFL", "YEET", "BRUH", "LULZ",
                    // Silly/goofy
                    "SILLY", "GOOFY", "WACKY", "ZANY", "NUTTY", "DOPEY", "DAFT",
                    "WITTY", "PUNNY", "JOKEY", "QUIRKY",
                    // Memes/internet
                    "MEME", "NOOB", "DERP", "YOLO", "DODO", "BOBO", "JOJO",
                    // Sounds/exclamations
                    "BOOM", "ZOOM", "WOOSH", "YOYO", "TEHE",
                    // Funny vibes
                    "GEEKY", "LOOPY", "BONKY", "WONKY", "FUNKY"
                };
                keywordInputs.AddRange(funnyKeywords);
            }

            if (balatrOption.HasValue())
            {
                var balaroKeywords = new[]
                {
                    // Core mechanics
                    "JOKER", "CHIPS", "MULT", "HAND", "CARD", "DECK", "SUIT", "RANK",
                    "WILD", "SCORE", "ROUND", "ANTE", "BLIND", "STAKE",
                    // Card suits
                    "SPADE", "HEART", "CLUB", "DIAMOND",
                    // Famous Jokers
                    "JIMBO", "JOLLY", "SEANCE", "GLASS", "STEEL", "STONE", "BRONZE",
                    "SILVER", "GOLD", "ETERNAL", "VOID", "TORN", "BLUE",
                    // Mechanics
                    "BUFF", "COPY", "SEAL", "SHOP", "SELL", "LEVEL", "PAYOUT",
                    "EDGE", "BONUS", "RETRO", "PETRIFIED", "FOREX", "LUCKY",
                    // General
                    "RUN", "WIN", "BEAT", "BOSS", "FLUSH", "FIVE", "HOUSE"
                };
                keywordInputs.AddRange(balaroKeywords);
            }

            var plan = JamlSearchBuilder.CreatePlan(config);
            var settings = plan.Settings
                .WithDeck(deck)
                .WithStake(stake)
                .WithThreadCount(threads)
                .WithBatchCharacterCount(batchCharCount);

            if (explicitSeeds != null)
            {
                settings.WithListSearch(explicitSeeds, explicitSeeds.Length);
            }
            else if (keywordInputs.Count > 0)
            {
                char[]? paddingChars = paddingOption.HasValue()
                    ? paddingOption.ParsedValue.ToCharArray()
                    : null;
                var prov = Motely.Motely.GeneratePaddedSeedsForKeywords(keywordInputs, paddingChars);
                settings.WithProviderSearch(
                    new MotelySeedListProvider(
                        prov,
                        prov.Count()
                    )
                );
            }
            else if (randomOption.HasValue())
            {
                settings.WithRandomSearch(randomOption.ParsedValue);
            }
            else if (palindromeOption.HasValue())
            {
                settings.WithPalindromeSearch();
            }
            else
            {
                settings.WithSequentialSearch();


                if (startBatchOption.HasValue())
                    settings.WithStartBatchIndex(startBatchOption.ParsedValue);
                if (endBatchOption.HasValue())
                    settings.WithEndBatchIndex(endBatchOption.ParsedValue);
            }
            bool hasStructuredScores = plan.ShouldClauseCount > 0;
            using ISeedResultSink? sink = sinkOption.HasValue()
                ? SeedResultSinkFactory.Create(sinkOption.ParsedValue, plan.ShouldClauseCount)
                : null;

            if (hasStructuredScores)
            {
                settings.WithScoredResultCallback(tally =>
                {
                    var tallies = string.Join(",", tally.TallyValuesSpan.ToArray());
                    Console.WriteLine($"{tally.Seed},{tally.Score},{tallies}");
                    sink?.AppendScoredResult(tally.Seed, tally.Score, tally.TallyValuesSpan);
                });
            }

            Console.Error.WriteLine(
                $"Motely: {config.Name ?? jamlOption.ParsedValue} | {deck} {stake} | threads={threads} batchCharCount={batchCharCount}"
            );
            if (sink != null)
                Console.Error.WriteLine($"Sink: {sink.OutputPath}");

            using var search = settings.CreateSearch();
            search.Start(_cts.Token);
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
            Console.WriteLine($"Analyzing: {seed} | {d} {s}");
            Console.Write(analysis);
        }
        return 0;
    }
}
