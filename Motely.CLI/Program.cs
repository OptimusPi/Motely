using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using Motely.GPU;
using Motely.Reporting;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

namespace Motely
{
    partial class Program
    {
        private static readonly CancellationTokenSource _cts = new();

        static int Main(string[] args)
        {
            // Wire up Ctrl+C to CancellationTokenSource for immediate cancellation
            // NOTE: Console.CancelKeyPress handlers are synchronous, so we must use Cancel()
            // not CancelAsync(). The cancellation token propagates to all awaiting tasks immediately.
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Suppress default termination (allow graceful shutdown)
                _cts.Cancel(); // Signal cancellation token synchronously - propagates to all subscribers immediately
            };

            var app = new CommandLineApplication
            {
                Name = "Motely",
                Description = "Motely - Balatro Seed Searcher",
                OptionsComparison = StringComparison.OrdinalIgnoreCase,
            };

            app.HelpOption("-?|-h|--help");

            var noArgsProvided = args.Length == 0;

            // Core options
            var jsonOption = app.Option<string>(
                "-j|--json <JSON>",
                "JSON config file (JsonFilters/)",
                CommandOptionType.SingleValue
            );
            var jamlOption = app.Option<string>(
                "--jaml <JAML>",
                "JAML config file (JamlFilters/) - Joker Ante Markup Language",
                CommandOptionType.SingleValue
            );
            var analyzeOption = app.Option<string>(
                "--analyze <SEED>",
                "Analyze a specific seed",
                CommandOptionType.SingleValue
            );
            var outputJsonOption = app.Option(
                "--output-json",
                "Output analysis as JSON (for --analyze mode)",
                CommandOptionType.NoValue
            );
            var nativeOption = app.Option<string>(
                "-n|--native <FILTER>",
                "Run built-in native filter",
                CommandOptionType.SingleValue
            );
            var convertOption = app.Option(
                "--convert",
                "Convert all JSON filters to JAML format",
                CommandOptionType.NoValue
            );
            var scoreOption = app.Option<string>(
                "--score <JSON>",
                "Add JSON scoring to native filter",
                CommandOptionType.SingleValue
            );
            var csvScoreOption = app.Option<string>(
                "--csvScore <TYPE>",
                "Enable CSV scoring output (native for built-in)",
                CommandOptionType.SingleValue
            );
            var timeOption = app.Option<int>(
                "--time <SECONDS>",
                "Progress report interval in seconds (default: 1200)",
                CommandOptionType.SingleValue
            );

            // Search parameters
            var threadsOption = app.Option<int>(
                "--threads <COUNT>",
                "Number of threads",
                CommandOptionType.SingleValue
            );
            var batchSizeOption = app.Option<int>(
                "--batchSize <CHARS>",
                "Batch size",
                CommandOptionType.SingleValue
            );
            var startBatchOption = app.Option<long>(
                "--startBatch <INDEX>",
                "Starting batch",
                CommandOptionType.SingleValue
            );
            var endBatchOption = app.Option<long>(
                "--endBatch <INDEX>",
                "Ending batch",
                CommandOptionType.SingleValue
            );
            var startPercentOption = app.Option<double>(
                "--startPercent <PCT>",
                "Starting percent (0-100)",
                CommandOptionType.SingleValue
            );
            var startSeedOption = app.Option<string>(
                "--startSeed <SEED>",
                "Starting seed (e.g., TACO1111) - converts to batch number",
                CommandOptionType.SingleValue
            );
            var endPercentOption = app.Option<double>(
                "--endPercent <PCT>",
                "Ending percent (0-100)",
                CommandOptionType.SingleValue
            );

            // Input options
            var seedOption = app.Option<string>(
                "--seed <SEED>",
                "Specific seed",
                CommandOptionType.SingleValue
            );
            var seedsOption = app.Option<string>(
                "--seeds <SEEDS>",
                "Seed source file (txt, csv, or db) OR comma-separated seed list (e.g., TACO1111,PIES2222,CAKE3333)",
                CommandOptionType.SingleValue
            );
            var keywordOption = app.Option<string>(
                "--keyword <KEYWORD>",
                "Generate seeds containing keyword (all seeds by default, use --sfw to filter NSFW)",
                CommandOptionType.SingleValue
            );
            var keywordsOption = app.Option<string>(
                "--keywords <KEYWORDS>",
                "Comma-separated keywords to search sequentially (e.g., GAY,ASS,OOOO,AAAA)",
                CommandOptionType.SingleValue
            );
            var paddingOption = app.Option<string>(
                "--padding <CHARS>",
                "Restrict padding characters (e.g., --padding OU or --padding 1). Only works with --keyword.",
                CommandOptionType.SingleValue
            );
            var regenerateKeywordDbOption = app.Option(
                "--regenerate-keyword-db",
                "Force regeneration of keyword DB even if it exists",
                CommandOptionType.NoValue
            );
            var randomOption = app.Option<int>(
                "--random <COUNT>",
                "Test with random seeds",
                CommandOptionType.SingleValue
            );
            var palindromeOption = app.Option(
                "--palindrome",
                "Generate palindrome seeds (reads same forwards and backwards)",
                CommandOptionType.NoValue
            );

            // Game options
            var deckOption = app.Option<string>(
                "--deck <DECK>",
                "Deck to use",
                CommandOptionType.SingleValue
            );
            var stakeOption = app.Option<string>(
                "--stake <STAKE>",
                "Stake to use",
                CommandOptionType.SingleValue
            );

            // JSON specific
            var cutoffOption = app.Option<string>(
                "--cutoff <MODE>",
                "Min score threshold: number (0=no cutoff, 1+=manual), or specify 'auto' (AutoSmart) / 'best' (AutoBest)",
                CommandOptionType.SingleValue
            );

            // Output options
            var saveOption = app.Option(
                "--save",
                "Save results to SearchResults/ (DuckDB)",
                CommandOptionType.NoValue
            );
            var forceOption = app.Option(
                "--force",
                "Overwrite existing SearchResults database if schema changed",
                CommandOptionType.NoValue
            );

            var outputDbOption = app.Option<string>(
                "--output-db <PATH>",
                "Write results to DuckDB database file (instead of CSV to console)",
                CommandOptionType.SingleValue
            );
            var outputCsvOption = app.Option<string>(
                "--output-csv <PATH>",
                "Write results to CSV file (instead of CSV to console)",
                CommandOptionType.SingleValue
            );
            var debugOption = app.Option(
                "--debug",
                "Enable debug output",
                CommandOptionType.NoValue
            );

            // GPU acceleration options
            var dungmotOption = app.Option(
                "--dungmot",
                "Use GPU-accelerated dungmot as seed pre-filter",
                CommandOptionType.NoValue
            );
            var dungmotPathOption = app.Option<string>(
                "--dungmot-path <PATH>",
                "Path to dungmot executable (default: auto-detect based on filter type)",
                CommandOptionType.SingleValue
            );
            var noFancyOption = app.Option(
                "--nofancy",
                "Suppress fancy output",
                CommandOptionType.NoValue
            );
            var quietOption = app.Option(
                "--quiet",
                "Suppress all progress output (CSV only)",
                CommandOptionType.NoValue
            );

            // Set defaults for performance options
            threadsOption.DefaultValue = Environment.ProcessorCount;
            batchSizeOption.DefaultValue = 2;
            startBatchOption.DefaultValue = 0;
            endBatchOption.DefaultValue = 0;
            cutoffOption.DefaultValue = "0";
            deckOption.DefaultValue = "Red";
            stakeOption.DefaultValue = "White";
            timeOption.DefaultValue = 2; // 2 seconds

            app.OnExecute(() =>
            {
                if (noArgsProvided)
                {
                    app.ShowHelp();
                    return 0;
                }

                // Analyze mode takes priority
                var analyzeSeed = analyzeOption.Value();
                if (!string.IsNullOrEmpty(analyzeSeed))
                {
                    return ExecuteAnalyze(
                        analyzeSeed,
                        deckOption.Value()!,
                        stakeOption.Value()!,
                        outputJsonOption.HasValue()
                    );
                }

                // Build common parameters first
                var quietMode = quietOption.HasValue();

                // Parse cutoff option
                int cutoffValue = 0;
                ScoreCutoffMode cutoffMode = ScoreCutoffMode.None;
                var cutoffStr = cutoffOption.Value()?.ToLowerInvariant() ?? "0";
                if (cutoffStr == "auto" || cutoffStr == "smart")
                {
                    cutoffMode = ScoreCutoffMode.AutoSmart;
                }
                else if (cutoffStr == "best")
                {
                    cutoffMode = ScoreCutoffMode.AutoBest;
                }
                else if (int.TryParse(cutoffStr, out int parsedCutoff))
                {
                    cutoffValue = parsedCutoff;
                    cutoffMode = parsedCutoff > 0 ? ScoreCutoffMode.Manual : ScoreCutoffMode.None;
                }

                var parameters = new JsonSearchParams
                {
                    Threads = threadsOption.ParsedValue,
                    BatchSize = batchSizeOption.ParsedValue,
                    StartBatch = (ulong)startBatchOption.ParsedValue,
                    EndBatch = (ulong)endBatchOption.ParsedValue,
                    EnableDebug = debugOption.HasValue(),
                    NoFancy = noFancyOption.HasValue(),
                    Quiet = quietMode,
                    SpecificSeed = seedOption.Value(),
                    SeedSources = seedsOption.Value(),
                    Deck = deckOption.Value(),
                    Stake = stakeOption.Value(),
                    SeedList = null, // Will be set by keyword handling below
                    RandomSeeds = randomOption.HasValue() ? randomOption.ParsedValue : null,
                    PalindromeSeeds = palindromeOption.HasValue(),
                    CancellationToken = _cts.Token,
                    Cutoff = cutoffValue,
                    CutoffMode = cutoffMode,
                };

                if (forceOption.HasValue())
                {
                    parameters.ForceOverwrite = true;
                }
                else
                {
                    parameters.SchemaMismatchPrompt = (dbPath, message) => PromptForceOverwrite(dbPath, message, quietMode);
                }

                // Progress reporting is handled by MotelySearch.PrintReport() internally
                // No need for a separate CLI callback - it causes duplicate output

                // Handle --seeds: Check if it's comma-separated seeds or a file path
                if (seedsOption.HasValue())
                {
                    string seedsValue = seedsOption.Value()!;
                    
                    // Check if it's comma-separated seeds (contains comma and doesn't exist as file)
                    if (seedsValue.Contains(',') && !File.Exists(seedsValue))
                    {
                        // Treat as comma-separated seed list
                        var seedList = seedsValue
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(s => s.ToUpperInvariant().Replace('0', 'O'))
                            .Where(s => !string.IsNullOrEmpty(s));
                        
                        if (!parameters.Quiet)
                            Console.WriteLine($"📋 Using {seedList.Count()} comma-separated seeds from --seeds");
                        
                        parameters.SeedList = seedList;
                        parameters.SeedSources = null; // Don't use DuckDB for direct seed lists
                    }
                    // Otherwise, treat as file path (existing behavior)
                }

                // Handle --keyword: Use IEnumerable directly for fast keyword generation
                if (keywordOption.HasValue())
                {
                    string keyword = keywordOption.Value()!.ToUpperInvariant();

                    if (!parameters.Quiet)
                        Console.WriteLine($"🔧 Generating seeds for keyword '{keyword}'...");

                    string? paddingChars = paddingOption.HasValue() ? paddingOption.Value() : null;

                    // Calculate seed count first (needed for progress reporting)
                    int maxPad = 8 - keyword.Length;
                    char[] validChars = paddingChars != null 
                        ? paddingChars.ToUpperInvariant().ToCharArray()
                        : "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
                    long seedCount = GetCountOfSeeds(keyword, maxPad, validChars.Length);
                    
                    // Generate seeds as IEnumerable (lazy, no allocation)
                    var keywordSeedList = GenerateKeywordSeeds(
                        keyword,
                        paddingChars,
                        parameters.Quiet
                    );

                    // FAST PATH: Use IEnumerable directly (no DuckDB overhead)
                    // This skips all file I/O and locks - perfect for in-memory searching
                    parameters.SeedList = keywordSeedList;
                    parameters.SeedSources = null; // Don't use DuckDB for keywords
                    parameters.KeywordSeedCount = (int)Math.Min(seedCount, int.MaxValue);

                    if (!parameters.Quiet)
                        Console.WriteLine($"✅ Seeds ready for search (streaming mode)");
                }

                // Validate batch size
                if (parameters.BatchSize < 1 || parameters.BatchSize >= 8)
                {
                    Console.WriteLine(
                        $"❌ Error: batchSize must be between 1 and 7 (got {parameters.BatchSize})"
                    );
                    Console.WriteLine(
                        $"   batchSize represents the number of seed digits to process in parallel."
                    );
                    Console.WriteLine(
                        $"   Valid range: 1-7 (batchSize=8 creates a single 2.25 trillion seed batch)"
                    );
                    Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                    return 1;
                }

                // Calculate max batches for this batch size
                long maxBatches = (long)Math.Pow(35, 8 - parameters.BatchSize);

                // Convert startSeed to batch if specified
                if (startSeedOption.HasValue())
                {
                    var seedStr = startSeedOption.ParsedValue.ToUpperInvariant();
                    if (seedStr.Length != 8)
                    {
                        Console.WriteLine($"❌ Error: startSeed must be 8 characters (got {seedStr.Length})");
                        return 1;
                    }
                    try
                    {
                        var batchIndex = SeedMath.SeedToBatchIndex(seedStr, parameters.BatchSize);
                        parameters.StartBatch = (ulong)batchIndex;
                        if (!parameters.Quiet)
                        {
                            Console.WriteLine($"📍 Starting at seed {seedStr} = batch {parameters.StartBatch:N0}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error: Invalid seed '{seedStr}': {ex.Message}");
                        return 1;
                    }
                }

                // Convert percent to batch only when startSeed was not specified (startSeed takes priority)
                if (startPercentOption.HasValue() && !startSeedOption.HasValue())
                {
                    double startPct = startPercentOption.ParsedValue;
                    if (startPct < 0 || startPct > 100)
                    {
                        Console.WriteLine($"❌ Error: startPercent must be 0-100 (got {startPct})");
                        return 1;
                    }
                    parameters.StartBatch = (ulong)(maxBatches * startPct / 100.0);
                    if (!parameters.Quiet)
                    {
                        Console.WriteLine(
                            $"📍 Starting at {startPct}% = batch {parameters.StartBatch:N0}"
                        );
                    }
                }

                if (endPercentOption.HasValue())
                {
                    double endPct = endPercentOption.ParsedValue;
                    if (endPct < 0 || endPct > 100)
                    {
                        Console.WriteLine($"❌ Error: endPercent must be 0-100 (got {endPct})");
                        return 1;
                    }
                    parameters.EndBatch = (ulong)(maxBatches * endPct / 100.0);
                    if (!parameters.Quiet)
                    {
                        if (endPct == 0)
                            Console.WriteLine($"📍 Ending at ∞ (no limit)");
                        else
                            Console.WriteLine(
                                $"📍 Ending at {endPct}% = batch {parameters.EndBatch:N0}"
                            );
                    }
                }
                else if (parameters.EndBatch == 0 && startPercentOption.HasValue())
                {
                    // User specified startPercent but no end - show infinity
                    if (!parameters.Quiet)
                    {
                        Console.WriteLine($"📍 Ending at ∞ (no limit)");
                    }
                }

                // Validate batch ranges
                if ((long)parameters.EndBatch > maxBatches)
                {
                    Console.WriteLine(
                        $"❌ endBatch too large: {parameters.EndBatch} (max for batchSize {parameters.BatchSize}: {maxBatches:N0})"
                    );
                    return 1;
                }
                if (parameters.StartBatch >= parameters.EndBatch && parameters.EndBatch != 0)
                {
                    Console.WriteLine(
                        $"❌ startBatch ({parameters.StartBatch}) must be less than endBatch ({parameters.EndBatch})"
                    );
                    return 1;
                }

                // Determine output paths
                // dbPath is ONLY for user override - orchestrator handles default path generation
                string? dbPath = outputDbOption.Value();
                string? csvPath = outputCsvOption.Value();

                // Check which mode to run
                var nativeFilter = nativeOption.Value();

                // Set OutputDbPath ONLY if user explicitly provided it
                // For --save flag, set AutoSave=true and orchestrator will generate path from config
                if (!string.IsNullOrEmpty(dbPath))
                {
                    parameters.OutputDbPath = dbPath;
                }
                else if (saveOption.HasValue())
                {
                    parameters.AutoSave = true; // Orchestrator will generate path from config
                }

                // Setup CSV output if requested
                StreamWriter? csvWriter = null;
                if (!string.IsNullOrEmpty(csvPath))
                {
                    if (!parameters.Quiet)
                        Console.WriteLine($"💾 Writing results to CSV: {csvPath}");
                    
                    csvWriter = new StreamWriter(csvPath, false, Encoding.UTF8);
                    // Header will be printed by the executor inside the orchestrator
                    // But we need to handle the writing of rows if we want it in CSV.
                    // The Orchestrator's Launch method takes a resultCallback.
                }

                int exitCode = 0;
                try
                {
                    // Handle --keywords: Run searches sequentially for each keyword
                    if (keywordsOption.HasValue())
                    {
                        string keywordsValue = keywordsOption.Value()!;
                        var keywords = keywordsValue
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(k => k.ToUpperInvariant())
                            .Where(k => !string.IsNullOrEmpty(k))
                            .ToList();
                        
                        if (keywords.Count == 0)
                        {
                            Console.Error.WriteLine("❌ Error: --keywords must contain at least one keyword");
                            return 1;
                        }
                        
                        if (!parameters.Quiet)
                            Console.WriteLine($"🔑 Running searches for {keywords.Count} keywords: {string.Join(", ", keywords)}");
                        
                        // Save original parameters to restore after each search
                        var originalSeedList = parameters.SeedList;
                        var originalSeedSources = parameters.SeedSources;
                        
                        int completedKeywords = 0;
                        bool wasCancelled = false;
                        
                        for (int i = 0; i < keywords.Count; i++)
                        {
                            string keyword = keywords[i];
                            
                            if (!parameters.Quiet)
                            {
                                Console.WriteLine();
                                Console.WriteLine(new string('═', 60));
                                Console.WriteLine($"🔍 Keyword {i + 1}/{keywords.Count}: '{keyword}'");
                                Console.WriteLine(new string('═', 60));
                            }
                            
                            // Generate seeds for this keyword (same as --keyword)
                            string? paddingChars = paddingOption.HasValue() ? paddingOption.Value() : null;
                            var keywordSeedList = GenerateKeywordSeeds(
                                keyword,
                                paddingChars,
                                parameters.Quiet
                            );
                            
                            // Set up parameters for this keyword search
                            parameters.SeedList = keywordSeedList;
                            parameters.SeedSources = null;
                            
                            // Run the search (reuse the same search execution logic)
                            Action<MotelySeedScoreTally> keywordResultCallback = (result) =>
                            {
                                // Build CSV line efficiently without intermediate List allocations
                                var sb = new StringBuilder();
                                sb.Append('"').Append(result.Seed).Append('"').Append(',');
                                sb.Append(result.Score);
                                
                                // Append tally columns
                                if (result.TallyColumns != null && result.TallyColumns.Count > 0)
                                {
                                    foreach (var tally in result.TallyColumns)
                                    {
                                        sb.Append(',').Append(tally);
                                    }
                                }
                                
                                string csvLine = sb.ToString();
                                
                                // Always print to console (quiet mode still shows CSV results)
                                Console.WriteLine(csvLine);
                                
                                // Also write to CSV file if specified
                                if (csvWriter != null)
                                {
                                    csvWriter.WriteLine(csvLine);
                                }
                            };
                            
                            exitCode = RunSingleSearch(parameters, nativeFilter, jamlOption, jsonOption, 
                                deckOption, stakeOption, scoreOption, csvWriter, keywordResultCallback);
                            
                            // Restore original parameters
                            parameters.SeedList = originalSeedList;
                            parameters.SeedSources = originalSeedSources;
                            
                            // Check for cancellation
                            if (parameters.CancellationToken?.IsCancellationRequested == true)
                            {
                                wasCancelled = true;
                                if (!parameters.Quiet)
                                    Console.WriteLine($"\n⚠️  Search cancelled after keyword '{keyword}'");
                                break;
                            }
                            
                            // Track completed keywords (only if not cancelled and search succeeded)
                            if (exitCode == 0)
                            {
                                completedKeywords++;
                            }
                            else if (!parameters.Quiet)
                            {
                                Console.WriteLine($"⚠️  Search for keyword '{keyword}' failed, continuing to next keyword...");
                            }
                        }
                        
                        if (!parameters.Quiet)
                        {
                            Console.WriteLine();
                            Console.WriteLine(new string('═', 60));
                            if (wasCancelled)
                            {
                                Console.WriteLine($"⚠️  Searches cancelled: {completedKeywords}/{keywords.Count} keywords completed");
                            }
                            else
                            {
                                Console.WriteLine($"✅ Completed searches for all {completedKeywords}/{keywords.Count} keywords");
                            }
                            Console.WriteLine(new string('═', 60));
                        }
                    }
                    else
                    {
                        // Single search (original behavior)
                        Action<MotelySeedScoreTally> resultCallback = (result) =>
                        {
                            // Build CSV line efficiently without intermediate List allocations
                            var sb = new StringBuilder();
                            sb.Append('"').Append(result.Seed).Append('"').Append(',');
                            sb.Append(result.Score);
                            
                            // Append tally columns
                            if (result.TallyColumns != null && result.TallyColumns.Count > 0)
                            {
                                foreach (var tally in result.TallyColumns)
                                {
                                    sb.Append(',').Append(tally);
                                }
                            }
                            
                            string csvLine = sb.ToString();
                            
                            // Always print to console (quiet mode still shows CSV results)
                            Console.WriteLine(csvLine);
                            
                            // Also write to CSV file if specified
                            if (csvWriter != null)
                            {
                                csvWriter.WriteLine(csvLine);
                            }
                        };
                        
                        exitCode = RunSingleSearch(parameters, nativeFilter, jamlOption, jsonOption,
                            deckOption, stakeOption, scoreOption, csvWriter, resultCallback);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"❌ Search failed: {ex.Message}");
                    if (parameters.EnableDebug)
                        Console.Error.WriteLine(ex.StackTrace);
                    exitCode = 1;
                }
                finally
                {
                    if (csvWriter != null)
                    {
                        csvWriter.Flush();
                        csvWriter.Close();
                    }
                }

                return exitCode;
            });

            try
            {
                return app.Execute(args);
            }
            catch (McMaster.Extensions.CommandLineUtils.UnrecognizedCommandParsingException ex)
            {
                // Short error message - don't spam with full help
                Console.Error.WriteLine($"❌ Error: {ex.Message}");
                Console.Error.WriteLine("💡 Use -h or --help for available options");
                return 1;
            }
            catch (Exception ex)
                when (ex.Message.Contains("Unrecognized") || ex.Message.Contains("option"))
            {
                // Short error message - don't spam with full help
                Console.Error.WriteLine($"❌ Error: {ex.Message}");
                Console.Error.WriteLine("💡 Use -h or --help for available options");
                return 1;
            }
        }

        private static int ExecuteAnalyze(
            string seed,
            string deckName,
            string stakeName,
            bool outputJson
        )
        {
            if (!Enum.TryParse<MotelyDeck>(deckName, true, out var deck))
            {
                Console.WriteLine($"❌ Invalid deck: {deckName}");
                return 1;
            }

            if (!Enum.TryParse<MotelyStake>(stakeName, true, out var stake))
            {
                Console.WriteLine($"❌ Invalid stake: {stakeName}");
                return 1;
            }

            var analysis = MotelySeedAnalyzer.Analyze(
                new MotelySeedAnalysisConfig(seed, deck, stake)
            );

            if (outputJson)
            {
                // Output as JSON for script consumption using AOT-compatible source-generated serialization
                var erraticComposition = analysis.ErraticDeckComposition?.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries
                ) ?? Array.Empty<string>();
                
                var jsonOutput = new SeedAnalysisDto
                {
                    Seed = seed,
                    Deck = deck.ToString(),
                    Stake = stake.ToString(),
                    ErraticDeckComposition = erraticComposition,
                    Twos = erraticComposition.Count(c => c.StartsWith("2_")),
                    Error = analysis.Error,
                    Antes = analysis
                        .Antes.Select(ante => new AnteAnalysisDto
                        {
                            Ante = ante.Ante,
                            Boss = FormatUtils.FormatBoss(ante.Boss),
                            Voucher = FormatUtils.FormatVoucher(ante.Voucher),
                            SmallBlindTag = FormatUtils.FormatTag(ante.SmallBlindTag),
                            BigBlindTag = FormatUtils.FormatTag(ante.BigBlindTag),
                            DrawOrder = ante.DrawOrder ?? string.Empty,
                            ShopQueue = ante
                                .ShopQueue.Select(item => new ShopItemDto
                                {
                                    Id = item.ToString(),
                                    Name = FormatUtils.FormatItem(item),
                                })
                                .ToArray(),
                            Packs = ante
                                .Packs.Select(pack => new PackDto
                                {
                                    Type = FormatUtils.FormatPackName(pack.Type),
                                    Items = pack
                                        .Items.Select(item => FormatUtils.FormatItem(item))
                                        .ToArray(),
                                })
                                .ToArray(),
                        })
                        .ToArray(),
                };
                
                // Use AOT-compatible source-generated serialization context
                Console.WriteLine(
                    System.Text.Json.JsonSerializer.Serialize(
                        jsonOutput,
                        MotelyAotJsonContext.Default.SeedAnalysisDto
                    )
                );
            }
            else
            {
                Console.WriteLine($"🔍 Analyzing seed: '{seed}' with deck: {deck}, stake: {stake}");
                Console.Write(analysis);
            }
            return 0;
        }

        private static bool PromptForceOverwrite(string dbPath, string message, bool quiet)
        {
            while (true)
            {
                Console.WriteLine(message);
                Console.Write($"Overwrite {dbPath}? [y/N]: ");
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                    return false;

                input = input.Trim();
                if (input.Equals("y", StringComparison.OrdinalIgnoreCase) || input.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (input.Equals("n", StringComparison.OrdinalIgnoreCase) || input.Equals("no", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        /// <summary>
        /// Generate seeds containing a keyword and return as IEnumerable (lazy evaluation).
        /// </summary>
        private static IEnumerable<string> GenerateKeywordSeeds(
            string keyword,
            string? paddingChars,
            bool quiet
        )
        {
            // Validate keyword - only valid Balatro chars
            keyword = keyword.ToUpperInvariant().Replace('0', 'O');
            foreach (var c in keyword)
            {
                if (!"ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".Contains(c))
                {
                    throw new ArgumentException(
                        $"Invalid character '{c}' in keyword. Only A-Z and 1-9 allowed."
                    );
                }
            }

            if (keyword.Length > 8)
            {
                throw new ArgumentException(
                    $"Keyword too long ({keyword.Length} chars). Max 8 chars allowed."
                );
            }

            // Parse padding characters
            char[] validChars;
            if (!string.IsNullOrEmpty(paddingChars))
            {
                paddingChars = paddingChars.ToUpperInvariant().Replace('0', 'O');
                var paddingSet = new HashSet<char>();
                foreach (var c in paddingChars)
                {
                    if (!"ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".Contains(c))
                    {
                        throw new ArgumentException(
                            $"Invalid padding character '{c}'. Only A-Z and 1-9 allowed."
                        );
                    }
                    paddingSet.Add(c);
                }
                
                if (paddingSet.Count == 0)
                {
                    throw new ArgumentException(
                        "Padding characters must contain at least one valid character (A-Z, 1-9)."
                    );
                }
                
                validChars = paddingSet.ToArray();
            }
            else
            {
                // Default: use all valid chars
                validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789".ToCharArray();
            }
            
            // Final validation - ensure validChars is never null or empty
            if (validChars == null || validChars.Length == 0)
            {
                throw new InvalidOperationException(
                    "validChars must not be null or empty. This should never happen."
                );
            }

            int maxPad = 8 - keyword.Length;
            long count = GetCountOfSeeds(keyword, maxPad, validChars.Length);

            if (!quiet)
            {
                string paddingInfo = paddingChars != null ? $" (padding: {paddingChars})" : "";
                Console.WriteLine(
                    $"🔧 Generating {count:N0} seeds containing '{keyword}'{paddingInfo}..."
                );
            }

            return GenerateKeywordSeedsEnumerable(keyword, maxPad, validChars);
        }

        private static IEnumerable<string> GenerateKeywordSeedsEnumerable(
            string keyword,
            int maxPad,
            char[] validChars)
        {
            // Defensive null check
            if (validChars == null)
                throw new ArgumentNullException(nameof(validChars));
            
            if (validChars.Length == 0)
                throw new ArgumentException("validChars cannot be empty", nameof(validChars));
            
            if (string.IsNullOrEmpty(keyword))
                throw new ArgumentException("keyword cannot be null or empty", nameof(keyword));
            
            yield return keyword;

            // Generate with padding - yield directly (NO SFW filtering during generation)
            if (maxPad > 0)
            {
                for (int padLen = 1; padLen <= maxPad; padLen++)
                {
                    foreach (var seed in GeneratePaddedSeeds(keyword, padLen, validChars))
                    {
                        yield return seed;
                    }
                }
            }
        }

        private static IEnumerable<string> GeneratePaddedSeeds(
            string keyword,
            int padLen,
            char[] validChars
        )
        {
            // Defensive null and empty checks
            if (validChars == null)
                throw new ArgumentNullException(nameof(validChars));
            
            if (validChars.Length == 0)
                throw new ArgumentException("validChars cannot be empty", nameof(validChars));
            
            if (string.IsNullOrEmpty(keyword))
                throw new ArgumentException("keyword cannot be null or empty", nameof(keyword));

            if (padLen <= 0)
            {
                yield return keyword;
                yield break;
            }

            if (padLen == 1)
            {
                foreach (var c in validChars)
                {
                    yield return c + keyword;
                    yield return keyword + c;
                }
            }
            else if (padLen == 2)
            {
                foreach (var c1 in validChars)
                {
                    foreach (var c2 in validChars)
                    {
                        yield return $"{c1}{c2}{keyword}";
                        yield return $"{keyword}{c1}{c2}";
                        yield return $"{c1}{keyword}{c2}";
                    }
                }
            }
            else if (padLen == 3)
            {
                foreach (var c1 in validChars)
                {
                    foreach (var c2 in validChars)
                    {
                        foreach (var c3 in validChars)
                        {
                            yield return $"{c1}{c2}{c3}{keyword}";
                            yield return $"{keyword}{c1}{c2}{c3}";
                            yield return $"{c1}{keyword}{c2}{c3}";
                            yield return $"{c1}{c2}{keyword}{c3}";
                        }
                    }
                }
            }
            else
            {
                // For padLen > 3, generate all positions
                foreach (var seed in GenerateLargePaddedSeeds(keyword, padLen, validChars))
                {
                    yield return seed;
                }
            }
        }

        private static IEnumerable<string> GenerateLargePaddedSeeds(string keyword, int padLen, char[] validChars)
        {
            // Defensive null check
            if (validChars == null)
                throw new ArgumentNullException(nameof(validChars));
            
            if (validChars.Length == 0)
                throw new ArgumentException("validChars cannot be empty", nameof(validChars));
            
            if (padLen <= 0)
                throw new ArgumentException("padLen must be greater than 0", nameof(padLen));
            
            var padding = new char[padLen];
            return GenerateLargePaddedSeedsRec(keyword, validChars, padding, 0);
        }

        private static IEnumerable<string> GenerateLargePaddedSeedsRec(string keyword, char[] validChars, char[] padding, int depth)
        {
            // Defensive null check (should never be null at this point, but be safe)
            if (validChars == null || validChars.Length == 0)
                yield break;
            
            if (depth == padding.Length)
            {
                // Generate all positions for keyword within padding
                for (int pos = 0; pos <= padding.Length; pos++)
                {
                    var builder = new System.Text.StringBuilder(8);
                    builder.Append(padding, 0, pos);
                    builder.Append(keyword);
                    builder.Append(padding, pos, padding.Length - pos);
                    yield return builder.ToString();
                }
                yield break;
            }

            foreach (var c in validChars)
            {
                padding[depth] = c;
                foreach (var seed in GenerateLargePaddedSeedsRec(keyword, validChars, padding, depth + 1))
                {
                    yield return seed;
                }
            }
        }

        private static IEnumerable<string> GenerateAllCombinations(char[] validChars, int length)
        {
            if (length == 0)
            {
                yield return "";
                yield break;
            }

            var buffer = new char[length];
            foreach (var combo in GenerateCombinationsRecursive(validChars, buffer, 0))
            {
                yield return combo;
            }
        }

        private static IEnumerable<string> GenerateCombinationsRecursive(
            char[] validChars,
            char[] buffer,
            int index
        )
        {
            if (index == buffer.Length)
            {
                yield return new string(buffer);
                yield break;
            }

            foreach (var c in validChars)
            {
                buffer[index] = c;
                // Continue building - only yield when we reach the end (base case above)
                foreach (var result in GenerateCombinationsRecursive(validChars, buffer, index + 1))
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Convert a seed string to a batch number for sequential search.
        /// Batches are organized by the first (8 - BatchSize) characters.
        /// </summary>
        private static long ConvertSeedToBatch(string seed, int batchSize)
        {
            // Pad seed to max length if needed
            seed = seed.PadRight(8, '1');

            // Batches are organized by the characters that are NOT varying.
            // Motely iterates using the LEFT characters (indices 0 to batchSize-1)
            // as the varying parts within a batch.
            // The FIXED characters for a batch are the RIGHT characters (indices batchSize to 7).

            int fixedLength = 8 - batchSize;
            if (fixedLength <= 0)
                return 0;

            // Get the fixed part (the suffix)
            string suffix = seed.Substring(batchSize);

            // Convert to batch index:
            // digit at index batchSize is 35^0
            // digit at index batchSize+1 is 35^1
            // ...
            // digit at index 7 is 35^(fixedLength-1)

            long batchNum = 0;
            long multiplier = 1;

            for (int i = 0; i < suffix.Length; i++)
            {
                char c = suffix[i];
                int digitIndex = Array.IndexOf(Motely.SeedDigits, c);
                if (digitIndex < 0)
                {
                    throw new ArgumentException(
                        $"Invalid seed character '{c}' in '{seed}'. Valid chars: 1-9, A-Z"
                    );
                }

                batchNum += digitIndex * multiplier;
                multiplier *= 35;
            }

            return batchNum;
        }

        /// <summary>
        /// Save generated seeds to DuckDB file
        /// </summary>
        private static long GetCountOfSeeds(string keyword, int maxPad, int validCharCount)
        {
            long total = 1; // The keyword itself

            // Formula: sum( (padLen + 1) * N^padLen ) for padLen 1 to maxPad
            // where N is validCharCount
            
            for (int padLen = 1; padLen <= maxPad; padLen++)
            {
                long permutations = (long)Math.Pow(validCharCount, padLen);
                long positions = padLen + 1;
                total += positions * permutations;
            }

            return total;
        }

        /// <summary>
        /// Save generated seeds to DuckDB file using Motely.DB optimized storage (via Orchestrator)
        /// </summary>
        private static void SaveSeedsToDuckDB(IEnumerable<string> seeds, string dbPath, bool quiet, bool isRegenerating = false)
        {
            if (!quiet)
                Console.WriteLine($"💾 Saving seeds to {dbPath}...");
            
            long count = MotelySearchOrchestrator.BulkInsertSeeds(dbPath, seeds, deleteExisting: isRegenerating);
            
            if (!quiet)
                Console.WriteLine($"✅ Saved {count:N0} seeds to {dbPath}");
        }

        /// <summary>
        /// Insert a batch of seeds into DuckDB
        /// </summary>

        /// <summary>
        /// Run a single search with the given parameters
        /// </summary>
        private static int RunSingleSearch(
            JsonSearchParams parameters,
            string? nativeFilter,
            CommandOption<string>? jamlOption,
            CommandOption<string>? jsonOption,
            CommandOption<string> deckOption,
            CommandOption<string> stakeOption,
            CommandOption<string>? scoreOption,
            StreamWriter? csvWriter,
            Action<MotelySeedScoreTally>? resultCallback)
        {
            // ORCHESTRATOR HANDLES EVERYTHING - just give it the config!
            // Column names come from JAML labels (handled by GetColumnNames in config)
            // DB path generation comes from filterId (handled by orchestrator)
            IMotelySearch search;
            
            // Use provided callback or create default one
            Action<MotelySeedScoreTally> callback = resultCallback ?? ((result) =>
            {
                // Build CSV line efficiently without intermediate List allocations
                var sb = new StringBuilder();
                sb.Append('"').Append(result.Seed).Append('"').Append(',');
                sb.Append(result.Score);
                
                // Append tally columns
                if (result.TallyColumns != null && result.TallyColumns.Count > 0)
                {
                    foreach (var tally in result.TallyColumns)
                    {
                        sb.Append(',').Append(tally);
                    }
                }
                
                string csvLine = sb.ToString();
                
                // Always print to console (quiet mode still shows CSV results)
                Console.WriteLine(csvLine);
                
                // Also write to CSV file if specified
                if (csvWriter != null)
                {
                    csvWriter.WriteLine(csvLine);
                }
            });

            string? configName;
            if (!string.IsNullOrEmpty(nativeFilter))
            {
                configName = nativeFilter;
                search = MotelySearchOrchestrator.LaunchNative(
                    nativeFilter,
                    parameters,
                    scoreOption?.Value(),
                    new ConsoleTerminalOutput(),
                    new ConsoleCancelKeyHandler());
            }
            else
            {
                if (jamlOption?.HasValue() == true)
                {
                    configName = jamlOption.Value();
                    search = MotelySearchOrchestrator.LaunchJaml(jamlOption.Value()!, parameters, callback);
                }
                else
                {
                    configName = jsonOption?.Value() ?? "standard";
                    search = MotelySearchOrchestrator.LaunchJson(configName, parameters, callback);
                }
            }

            // Print startup info with column names BEFORE search starts (even in quiet mode)
            // Column names come from JAML labels (handled by GetColumnNames in config, printed by executor)
            PrintStartupInfo(search, parameters, configName ?? "standard", deckOption.Value()!, stakeOption.Value()!, null);

            search.Start(parameters.CancellationToken ?? default);
            
            // Use AwaitCompletion() for clean blocking - respects cancellation token internally
            search.AwaitCompletion();
            
            // Check if cancelled
            bool wasCancelled = parameters.CancellationToken?.IsCancellationRequested == true;
            
            // Print final summary ALWAYS (even in quiet mode on interrupt/completion)
            PrintSearchSummary(search, parameters, wasCancelled);
            search.Dispose();
            
            return wasCancelled ? 1 : 0;
        }

        /// <summary>
        /// Print startup info before search (filter name, deck, stake)
        /// Always prints, even in quiet mode - users need to know what's running
        /// CSV header is printed by the executor (ONE SOURCE OF TRUTH)
        /// </summary>
        private static void PrintStartupInfo(IMotelySearch search, JsonSearchParams parameters, string configName, string deck, string stake, MotelyJsonConfig? config)
        {
            Console.Out.Flush();
            Console.WriteLine($"🔍 Running filter: {configName}");
            Console.WriteLine($"   Deck: {deck}, Stake: {stake}");
            Console.WriteLine($"   Threads: {parameters.Threads}, BatchSize: {parameters.BatchSize}");
            if (parameters.StartBatch > 0)
                Console.WriteLine($"   Starting from batch: {parameters.StartBatch:N0}");
            Console.WriteLine();
        }

        /// <summary>
        /// Print search summary after completion or cancellation
        /// Always prints, even in quiet mode - user needs to know how to continue
        /// </summary>
        private static void PrintSearchSummary(IMotelySearch search, JsonSearchParams parameters, bool wasCancelled)
        {
            Console.Out.Flush();
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine(wasCancelled ? "🛑 SEARCH STOPPED" : "✅ SEARCH COMPLETED");
            Console.WriteLine(new string('═', 60));

            long lastBatchIndex = search.CompletedBatchCount;

            // Batches only apply to sequential (batch) search; for provider/list search, don't show batch progress
            if (search.IsSequentialBatchSearch)
            {
                double precisePercent = 0.0;
                long maxBatches = (long)Math.Pow(35, 8 - parameters.BatchSize);
                if (maxBatches > 0)
                    precisePercent = (double)lastBatchIndex * 100.0 / (double)maxBatches;
                Console.WriteLine($"   Last batch: {lastBatchIndex:N0} ({precisePercent:F4}%)");
            }
            Console.WriteLine($"   Seeds passed filter and cutoff: {search.MatchingSeeds}");
            Console.WriteLine($"   Duration: {search.ElapsedTime:hh\\:mm\\:ss\\.fff}");
            Console.WriteLine(
                search.IsSequentialBatchSearch
                    ? $"   Total seeds: {search.TotalSeedsSearched:N0} ({search.CompletedBatchCount} batches)"
                    : $"   Total seeds: {search.TotalSeedsSearched:N0}"
            );
            
            double speed = search.ElapsedTime.TotalSeconds > 0 
                ? (double)search.TotalSeedsSearched / search.ElapsedTime.TotalSeconds 
                : 0;
            Console.WriteLine($"   Speed: {speed:N0} seeds/second");

            if (wasCancelled && search.IsSequentialBatchSearch)
            {
                double precisePercent = 0.0;
                long maxBatches = (long)Math.Pow(35, 8 - parameters.BatchSize);
                if (maxBatches > 0)
                    precisePercent = (double)lastBatchIndex * 100.0 / (double)maxBatches;
                Console.WriteLine($"💡 To continue: --startBatch {lastBatchIndex} or --startPercent {precisePercent:F4}");
            }
            Console.WriteLine(new string('═', 60));
        }

    }
}
