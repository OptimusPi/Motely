using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using Motely.Filters;

namespace Motely
{
    partial class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "MotelySearch",
                Description = "Motely Ouija Search - Dynamic Balatro Seed Searcher",
                OptionsComparison = StringComparison.OrdinalIgnoreCase
            };

            app.HelpOption("-?|-h|--help");

            var configOption = app.Option<string>(
                "-c|--config <CONFIG>",
                "Config file (looks in JsonItemFilters/ directory, add .ouija.json if not present)",
                CommandOptionType.SingleValue);
            configOption.DefaultValue = "standard";

            var startBatchOption = app.Option<int>(
                "--startBatch <INDEX>",
                "Starting batch index",
                CommandOptionType.SingleValue);
            startBatchOption.DefaultValue = 0;

            var endBatchOption = app.Option<int>(
                "--endBatch <INDEX>",
                "Ending batch index (-1 for unlimited)",
                CommandOptionType.SingleValue);
            endBatchOption.DefaultValue = 1000;

            var threadsOption = app.Option<int>(
                "--threads <COUNT>",
                "Number of search threads",
                CommandOptionType.SingleValue);
            threadsOption.DefaultValue = Environment.ProcessorCount;

            var batchSizeOption = app.Option<int>(
                "--batchSize <CHARS>",
                "Batch character count (2-4 recommended)",
                CommandOptionType.SingleValue);
            batchSizeOption.DefaultValue = 4;

            var cutoffOption = app.Option<int>(
                "--cutoff <SCORE>",
                "Minimum TotalScore threshold for results (0 for no cutoff)",
                CommandOptionType.SingleValue);
            cutoffOption.DefaultValue = 0;

            var debugOption = app.Option(
                "--debug",
                "Enable debug output messages",
                CommandOptionType.NoValue);

            var quietOption = app.Option(
                "--quiet",
                "Suppress progress and status output",
                CommandOptionType.NoValue);

            var noFancyOption = app.Option(
                "--nofancy",
                "Suppress fancy console output",
                CommandOptionType.NoValue);

            var wordlistOption = app.Option<string>(
                "--wordlist <WL>",
                "Wordlist file (loads WordLists/<WL>.txt, one 8-char seed per line)",
                CommandOptionType.SingleValue);

            var seedOption = app.Option<string>(
                "--seed <SEED>",
                "Specific seed to search for (overrides batch options)",
                CommandOptionType.SingleValue);
            seedOption.DefaultValue = string.Empty;

            var keywordOption = app.Option<string>(
                "--keyword <KEYWORD>",
                "Generate seeds from keyword with padding variations",
                CommandOptionType.SingleValue);
            keywordOption.DefaultValue = string.Empty;

            var analyzeOption = app.Option<string>(
                "--analyze <SEED>",
                "Analyze a specific seed and show detailed information",
                CommandOptionType.SingleValue);
            analyzeOption.DefaultValue = string.Empty;

            var deckOption = app.Option<string>(
                "--deck <DECK>",
                "Deck to use for analysis (default: Red)",
                CommandOptionType.SingleValue);
            deckOption.DefaultValue = "Red";

            var stakeOption = app.Option<string>(
                "--stake <STAKE>",
                "Stake to use for analysis (default: White)",
                CommandOptionType.SingleValue);
            stakeOption.DefaultValue = "White";

            app.OnExecute(() =>
            {
                var analyzeSeed = analyzeOption.Value();
                var deckName = deckOption.Value();
                var stakeName = stakeOption.Value();
                
                // Check if analyze mode
                if (!string.IsNullOrEmpty(analyzeSeed))
                {
                    Console.WriteLine("🔍 Analyzing seed...");
                    
                    // Parse deck and stake
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
                    
                    SeedAnalyzer.Analyze(analyzeSeed, deck, stake);
                    return 0;
                }
                
                Console.WriteLine("🔍 Starting Motely Ouija Search");
                var configName = configOption.Value()!;
                var startBatch = startBatchOption.ParsedValue;
                var endBatch = endBatchOption.ParsedValue;
                var threads = threadsOption.ParsedValue;
                var batchSize = batchSizeOption.ParsedValue;
                var cutoff = cutoffOption.ParsedValue;
                var enableDebug = debugOption.HasValue();
                var quiet = quietOption.HasValue();
                var wordlist = wordlistOption.Value();
                var keyword = keywordOption.Value();
                var nofancy = noFancyOption.HasValue();
                var seedInput = seedOption.Value()!;

                // Validate batchSize
                if (batchSize < 1 || batchSize > 8)
                {
                    Console.WriteLine($"❌ Error: batchSize must be between 1 and 8 (got {batchSize})");
                    Console.WriteLine($"   batchSize represents the number of seed digits to process in parallel.");
                    Console.WriteLine($"   Valid range: 1-8 (Balatro seeds are 1-8 characters)");
                    Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                    return 1;
                }

                RunOuijaSearch(configName, startBatch, endBatch, threads, batchSize, cutoff,
                    enableDebug, quiet, wordlist, keyword, nofancy, seedInput);
                Console.WriteLine("🔍 Search completed");
                return 0;
            });

            return app.Execute(args);
        }

        public static void RunOuijaSearch(string configPath, int startBatch, int endBatch, int threads,
            int batchSize, int cutoff, bool enableDebug, bool quiet, string? wordlist = null,
            string? keyword = null, bool nofancy = false, string? specificSeed = null)
        {
            // Set debug output flag
            DebugLogger.IsEnabled = enableDebug;
            FancyConsole.IsEnabled = !nofancy;

            List<string>? seeds = null;

            // Handle specific seed
            if (!string.IsNullOrEmpty(specificSeed))
            {
                seeds = new List<string> { specificSeed };
                if (!quiet)
                    Console.WriteLine($"🔍 Searching for specific seed: {specificSeed}");
            }
            // Handle keyword generation
            else if (!string.IsNullOrEmpty(keyword))
            {
                // Generate seeds from keyword pattern
                seeds = new List<string>();
                for (int i = 0; i <= 99; i++)
                {
                    string padded = keyword + i.ToString("00");
                    if (padded.Length == 8)
                        seeds.Add(padded);
                    else if (padded.Length < 8)
                        seeds.Add(padded.PadRight(8, '0'));
                    else
                        seeds.Add(padded.Substring(0, 8));
                }
                if (!quiet)
                {
                    Console.WriteLine($"🎯 Generated {seeds.Count} seeds from keyword: {keyword}");
                    if (seeds.Count <= 10)
                    {
                        Console.WriteLine($"   Seeds: {string.Join(", ", seeds)}");
                    }
                    else
                    {
                        Console.WriteLine($"   First 10: {string.Join(", ", seeds.Take(10))}...");
                    }
                }
            }
            // Handle wordlist loading
            else if (!string.IsNullOrEmpty(wordlist))
            {
                var wordlistPath = Path.Combine("WordLists", wordlist + ".txt");
                if (!File.Exists(wordlistPath))
                    throw new FileNotFoundException($"Wordlist file not found: {wordlistPath}");
                seeds = File.ReadAllLines(wordlistPath)
                    .Select(line => line.Trim())
                    .Where(line => line.Length == 8)
                    .ToList();
                if (!quiet)
                    Console.WriteLine($"✅ Loaded {seeds.Count} seeds from wordlist: {wordlistPath}");
            }

            if (!quiet)
            {
                Console.WriteLine($"🔍 Motely Ouija Search Starting");
                Console.WriteLine($"   Config: {configPath}");
                Console.WriteLine($"   Threads: {threads}");
                Console.WriteLine($"   Batch Size: {batchSize} chars");
                Console.WriteLine($"   Range: {startBatch} to {endBatch}");
                if (enableDebug)
                    Console.WriteLine($"   Debug: Enabled");
                Console.WriteLine();
            }

            try
            {
                // Load Ouija config
                var config = LoadConfig(configPath);
                try
                {
                    OuijaConfigValidator.ValidateConfig(config);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"❌ CONFIG VALIDATION FAILED:\n{ex.Message}");
                    return;
                }
                
                if (!quiet)
                    Console.WriteLine($"✅ Loaded config: {config.Must?.Count ?? 0} must, {config.Should?.Count ?? 0} should, {config.MustNot?.Count ?? 0} mustNot");

                // Print the parsed config for debugging
                if (!quiet && enableDebug)
                {
                    DebugLogger.Log("\n--- Parsed Ouija Config ---");
                    DebugLogger.Log(config.ToJson());
                    DebugLogger.Log("--- End Config ---\n");
                }

                // Search configuration is now part of MotelySearchSettings

                // The config loader now handles both formats automatically
                // It will convert new format to legacy format internally
                var filterDesc = new OuijaJsonFilterDesc(config);
                filterDesc.Cutoff = cutoff;
                
                if (!quiet)
                    Console.WriteLine($"✅ Loaded config with cutoff: {cutoff}");
                
                // Create the search using OuijaJsonFilterDesc
                var searchSettings = new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(filterDesc)
                    .WithThreadCount(threads)
                    .WithSequentialSearch()
                    .WithBatchCharacterCount(batchSize);
                    
                // Apply deck and stake from config
                if (!string.IsNullOrEmpty(config.Deck) && Enum.TryParse<MotelyDeck>(config.Deck, true, out var deck))
                {
                    searchSettings = searchSettings.WithDeck(deck);
                    DebugLogger.Log($"[Program] Using deck from config: {deck}");
                }
                else
                {
                    DebugLogger.Log($"[Program] Using default deck: {searchSettings.Deck}");
                }
                if (!string.IsNullOrEmpty(config.Stake) && Enum.TryParse<MotelyStake>(config.Stake, true, out var stake))
                {
                    searchSettings = searchSettings.WithStake(stake);
                    DebugLogger.Log($"[Program] Using stake from config: {stake}");
                }
                else
                {
                    DebugLogger.Log($"[Program] Using default stake: {searchSettings.Stake}");
                }
                
                // Set batch range if specified
                if (startBatch > 0)
                    searchSettings = searchSettings.WithStartBatchIndex(startBatch);
                if (endBatch > 0)
                {
                    // Note: No WithEndBatchIndex method - handle batch limit differently
                }
                    
                // Apply minimum score cutoff
                // Minimum score cutoff is handled by the filter itself
                
                // Start timing before search begins
                var searchStopwatch = Stopwatch.StartNew();
                
                IMotelySearch search;
                if (seeds != null && seeds.Count > 0)
                {
                    // Search specific seeds from list
                    if (!quiet && enableDebug)
                        Console.WriteLine($"[DEBUG] Starting list search with {seeds.Count} seeds");
                    search = searchSettings.WithListSearch(seeds).Start();
                }
                else
                {
                    // Sequential batch search
                    if (!quiet && enableDebug)
                        Console.WriteLine($"[DEBUG] Starting sequential batch search");
                    search = searchSettings.Start();
                }

                // Print CSV header for results
                PrintResultsHeader(config);
                
                // Clear results queue before starting (static queue persists between runs)
                while (OuijaJsonFilterDesc.OuijaJsonFilter.ResultsQueue.TryDequeue(out _)) { }
                
                // Reset cancellation flag
                OuijaJsonFilterDesc.OuijaJsonFilter.IsCancelled = false;

                // Setup cancellation token
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\n🛑 Stopping search...");
                    OuijaJsonFilterDesc.OuijaJsonFilter.IsCancelled = true;
                    search.Dispose();
                    Console.WriteLine("✅ Search stopped gracefully");
                };

                // Process results while search is running
                var resultsTask = System.Threading.Tasks.Task.Run(() =>
                {
                    while (search.Status == MotelySearchStatus.Running || !OuijaJsonFilterDesc.OuijaJsonFilter.ResultsQueue.IsEmpty)
                    {
                        if (OuijaJsonFilterDesc.OuijaJsonFilter.ResultsQueue.TryDequeue(out var result))
                        {
                            // Print result as CSV row
                            var row = $"{result.Seed},{result.TotalScore}";
                            if (result.ScoreWants != null)
                            {
                                foreach (var score in result.ScoreWants)
                                {
                                    row += $",{score}";
                                }
                            }
                            Console.WriteLine(row);
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(10);
                        }
                    }
                });
                
                // Wait for search to complete
                while (search.Status == MotelySearchStatus.Running)
                {
                    System.Threading.Thread.Sleep(100);
                }
                
                // Wait for results processing to complete
                resultsTask.Wait();
                
                // Stop timing
                searchStopwatch.Stop();
                
                // Results are printed in real-time by OuijaJsonFilterDesc
                var totalSearched = search.CompletedBatchCount;
                var duration = searchStopwatch.Elapsed;

                if (!quiet)
                {
                    Console.WriteLine($"\n✅ Search completed");
                    if (seeds != null && seeds.Count > 0)
                    {
                        // For list search, it's the actual number of seeds
                        Console.WriteLine($"   Total seeds searched: {totalSearched:N0}");
                    }
                    else
                    {
                        // For sequential search, it's batches - this is misleading
                        Console.WriteLine($"   Total batches processed: {totalSearched:N0}");
                        Console.WriteLine($"   (Note: Sequential search processes batch patterns, not individual seeds)");
                    }
                    Console.WriteLine($"   Duration: {duration:hh\\:mm\\:ss}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\n🛑 Search cancelled by user");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (enableDebug)
                    DebugLogger.Log(ex.StackTrace ?? "No stack trace available");
            }
        }

        static OuijaConfig LoadConfig(string configPath)
        {
            // If configPath is a rooted (absolute) path, use it directly
            if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            {
                DebugLogger.Log($"📁 Loading config from: {configPath}");
                return OuijaConfig.LoadFromJson(configPath);
            }

            // Always look in JsonItemFilters for configs
            string fileName = configPath.EndsWith(".ouija.json") ? configPath : configPath + ".ouija.json";
            string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
            if (File.Exists(jsonItemFiltersPath))
            {
                DebugLogger.Log($"📁 Loading config from: {jsonItemFiltersPath}");
                return OuijaConfig.LoadFromJson(jsonItemFiltersPath);
            }

            throw new FileNotFoundException($"Could not find config file: {configPath}");
        }

        static void PrintResultsHeader(OuijaConfig config)
        {
            // Print deck/stake info as comments
            Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");

            // Print CSV header only once, with + prefix
            var header = "+Seed,TotalScore";

            // Add column for each should clause
            if (config.Should != null)
            {
                foreach (var should in config.Should)
                {
                    var col = FormatShouldColumn(should);
                    header += $",{col}";
                }
            }
            Console.WriteLine(header);
        }

        static string FormatShouldColumn(OuijaConfig.FilterItem should)
        {
            if (should == null) return "Should";
            // Format as Type:Value or just Value if type is obvious
            var name = !string.IsNullOrEmpty(should.Value) ? should.Value : should.Type;
            if (!string.IsNullOrEmpty(should.Edition))
                name = should.Edition + name;
            return name;
        }
    }
}
    

//     IMotelySearch search = new MotelySearchSettings<FilledSoulFilterDesc.SoulFilter>(new FilledSoulFilterDesc())
// // IMotelySearch search = new MotelySearchSettings<TestFilterDesc.TestFilter>(new TestFilterDesc())
//     // IMotelySearch search = new MotelySearchSettings<LuckCardFilterDesc.LuckyCardFilter>(new LuckCardFilterDesc())
//     // IMotelySearch search = new MotelySearchSettings<ShuffleFinderFilterDesc.ShuffleFinderFilter>(new ShuffleFinderFilterDesc())
//     // IMotelySearch search = new MotelySearchSettings<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>(new PerkeoObservatoryFilterDesc())
//     // IMotelySearch search = new MotelySearchSettings<NegativeTagFilterDesc.NegativeTagFilter>(new NegativeTagFilterDesc())
//     .WithThreadCount(Environment.ProcessorCount - 2)
//     // .WithThreadCount(1)
//     // .WithStartBatchIndex(41428)

//     // .WithProviderSearch(new MotelyRandomSeedProvider(2000000000))
//     // .WithAdditionalFilter(new LuckyCardFilterDesc())
//     // .WithAdditionalFilter(new PerkeoObservatoryFilterDesc())
//     // .WithListSearch(["TACO"])
//     // .WithListSearch(["TIQR1111"])
//     // .WithStake(MotelyStake.Black)
//     .Start();