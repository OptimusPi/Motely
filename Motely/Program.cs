using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.Filters; // For OuijaConfig, OuijaJsonFilterDesc, OuijaConfigValidator

namespace Motely
{
    partial class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "Motely",
                Description = "Motely Ouija Search - Dynamic Balatro Seed Searcher",
                OptionsComparison = StringComparison.OrdinalIgnoreCase
            };

            app.HelpOption("-?|-h|--help");

            var configOption = app.Option<string>(
                "-c|--config <CONFIG>",
                "Config file (looks in JsonItemFilters/ directory, add .json if not present)",
                CommandOptionType.SingleValue);
            configOption.DefaultValue = "standard";

            var startBatchOption = app.Option<ulong>(
                "--startBatch <INDEX>",
                "Starting batch index",
                CommandOptionType.SingleValue);
            startBatchOption.DefaultValue = 0;

            var endBatchOption = app.Option<ulong>(
                "--endBatch <INDEX>",
                "Ending batch index (0 for unlimited)",
                CommandOptionType.SingleValue);
            endBatchOption.DefaultValue = 0;

            var threadsOption = app.Option<int>(
                "--threads <COUNT>",
                "Number of search threads",
                CommandOptionType.SingleValue);
            threadsOption.DefaultValue = Environment.ProcessorCount;

            var batchSizeOption = app.Option<int>(
                "--batchSize <CHARS>",
                "Batch character count (2-4 recommended)",
                CommandOptionType.SingleValue);
            batchSizeOption.DefaultValue = 1;

            var cutoffOption = app.Option<string>(
                "--cutoff <SCORE>",
                "Minimum TotalScore threshold (1-10 for fixed, 0 for auto-cutoff)",
                CommandOptionType.SingleValue);
            cutoffOption.DefaultValue = "0";

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

            var prefilterOption = app.Option(
                "--prefilter",
                "Enable cheap lookahead prefilter (soul legendary / joker identity first-pass)",
                CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                var analyzeSeed = analyzeOption.Value();
                var deckName = deckOption.Value();
                var stakeName = stakeOption.Value();
                
                // Check if analyze mode
                if (!string.IsNullOrEmpty(analyzeSeed))
                {
                    // Don't print anything - match Immolate's output exactly
                    
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
                    
                    MotelySeedAnalyzer.AnalyzeToConsole(analyzeSeed, deck, stake);
                    return 0;
                }
                
                Console.WriteLine("🔍 Starting Motely Ouija Search");
                var configName = configOption.Value()!;
                var startBatch = startBatchOption.ParsedValue;
                var endBatch = endBatchOption.ParsedValue;
                var threads = threadsOption.ParsedValue;
                var batchSize = batchSizeOption.ParsedValue;
                var enableDebug = debugOption.HasValue();
                var cutoffStr = cutoffOption.Value() ?? "0";
                int cutoffValue = int.TryParse(cutoffStr, out var c) ? c : 0;
                bool autoCutoff = cutoffValue == 0;  // 0 means auto-cutoff
                int cutoff = autoCutoff ? 1 : cutoffValue;  // Start at 1 for auto-cutoff
                
                if (enableDebug)
                {
                    Console.WriteLine($"[DEBUG] Cutoff string: '{cutoffStr}'");
                    Console.WriteLine($"[DEBUG] Cutoff value: {cutoffValue}");
                    Console.WriteLine($"[DEBUG] Auto-cutoff: {autoCutoff}");
                    Console.WriteLine($"[DEBUG] Effective starting cutoff: {cutoff}");
                }
                var quiet = quietOption.HasValue();
                var wordlist = wordlistOption.Value();
                var keyword = keywordOption.Value();
                var nofancy = noFancyOption.HasValue();
                var seedInput = seedOption.Value()!;
                var enablePrefilter = prefilterOption.HasValue();

                // Apply global prefilter flag before search starts
                OuijaJsonFilterDesc.PrefilterEnabled = enablePrefilter;

                // Validate batchSize
                if (batchSize < 1 || batchSize > 8)
                {
                    Console.WriteLine($"❌ Error: batchSize must be between 1 and 8 (got {batchSize})");
                    Console.WriteLine($"   batchSize represents the number of seed digits to process in parallel.");
                    Console.WriteLine($"   Valid range: 1-8 (Balatro seeds are 1-8 characters)");
                    Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                    return 1;
                }

                // Execute the search (this was previously commented out which made the app a no-op)
                RunOuijaSearch(configName, startBatch, endBatch, threads, batchSize, cutoff, autoCutoff,
                    enableDebug, quiet, wordlist, keyword, nofancy, seedInput);
                return 0; // RunOuijaSearch handles its own console output & completion text
            });

            return app.Execute(args);
        }

        public static void RunOuijaSearch(string configPath, ulong startBatch, ulong endBatch, int threads,
            int batchSize, int cutoff, bool autoCutoff, bool enableDebug, bool quiet, string? wordlist = null,
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
                    string endDisplay = endBatch <= 0 ? "∞" : endBatch.ToString();
                    Console.WriteLine($"   Range: {startBatch} to {endDisplay}");
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
                filterDesc.AutoCutoff = autoCutoff;
                
                if (!quiet)
                {
                    if (autoCutoff)
                        Console.WriteLine($"✅ Loaded config with auto-cutoff (starting at {cutoff})");
                    else
                        Console.WriteLine($"✅ Loaded config with cutoff: {cutoff}");
                    if (OuijaJsonFilterDesc.PrefilterEnabled)
                        Console.WriteLine("⚡ Prefilter enabled");
                }

                // Create the search using OuijaJsonFilterDesc
                var searchSettings = new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(filterDesc)
                    .WithThreadCount(threads);
                    
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
                    searchSettings = searchSettings.WithStartBatchIndex((ulong)startBatch);
                if (endBatch > 0)
                {
                    searchSettings = searchSettings.WithEndBatchIndex((ulong)endBatch);
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
                    {
                        Console.WriteLine($"[DEBUG] Starting list search with {seeds.Count} seeds");
                        Console.WriteLine($"[DEBUG] Seeds to search: {string.Join(", ", seeds.Take(5))}...");
                    }
                    search = searchSettings.WithListSearch(seeds).Start();
                    DebugLogger.Log($"[Program] List search created. Status immediately after Start(): {search.Status}");
                }
                else
                {
                    // Sequential batch search
                    if (!quiet && enableDebug)
                        Console.WriteLine($"[DEBUG] Starting sequential batch search");
                    search = searchSettings
                    .WithSequentialSearch()
                    .WithBatchCharacterCount(batchSize)
                    .Start();
                }

                // Apply quiet mode throttling to search progress output
                if (quiet && search is MotelySearch<OuijaJsonFilterDesc.OuijaJsonFilter> concreteSearch)
                {
                    concreteSearch.SetQuietMode(true);
                }

                // Print CSV header for results
                PrintResultsHeader(config);
                
                // Reset cancellation flag
                OuijaJsonFilterDesc.OuijaJsonFilter.IsCancelled = false;
                
                // Register callback to print results with scores (CSV only – suppress standalone/plain seed lines)
                OuijaJsonFilterDesc.OnResultFound = (seed, totalScore, scores) =>
                {
                    // Only emit a single CSV line per result. This avoids duplicate plain-seed output
                    // specifically for OuijaJsonFilterDesc.
                    // Format: Seed,TotalScore,<per-should scores>
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(seed.Length + 16 + scores.Length * 4);
                    sb.Append(seed).Append(',').Append(totalScore);
                    for (int i = 0; i < scores.Length; i++)
                    {
                        sb.Append(',').Append(scores[i]);
                    }
                    Console.WriteLine(sb.ToString());
                };

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
                
                // WAIT FOR SEARCH TO COMPLETE!
                DebugLogger.Log($"[Program] Search started. Initial status: {search.Status}");
                int loopCount = 0;
                while (search.Status != MotelySearchStatus.Completed && !OuijaJsonFilterDesc.OuijaJsonFilter.IsCancelled)
                {
                    System.Threading.Thread.Sleep(100);
                    loopCount++;
                    if (loopCount % 10 == 0) // Log every second
                    {
                        DebugLogger.Log($"[Program] Waiting for search... Status: {search.Status}, CompletedBatchCount: {search.CompletedBatchCount}");
                    }
                }
                DebugLogger.Log($"[Program] Search loop exited. Final status: {search.Status}, Cancelled: {OuijaJsonFilterDesc.OuijaJsonFilter.IsCancelled}");
                
                // Stop timing
                searchStopwatch.Stop();
                
                // Results are printed in real-time by OuijaJsonFilterDesc
                Console.WriteLine("\n✅ Search completed");

                // Summary metrics
                ulong totalBatchesCompleted = (ulong)search.CompletedBatchCount;
                var duration = searchStopwatch.Elapsed;
                ulong totalSeedsSearched;

                if (seeds != null && seeds.Count > 0)
                {
                    totalSeedsSearched = (ulong)seeds.Count;
                }
                else
                {
                    ulong seedsPerBatch = (ulong)Math.Pow(35, batchSize);
                    ulong batchesProcessed = startBatch > 0 ? (totalBatchesCompleted - (ulong)startBatch + 1UL) : totalBatchesCompleted;
                    totalSeedsSearched = batchesProcessed * seedsPerBatch;

                    Console.WriteLine($"   Total batches processed: {batchesProcessed:N0}");
                    Console.WriteLine($"   Seeds per batch: {seedsPerBatch:N0} (35^{batchSize})");
                }

                Console.WriteLine($"   Total seeds searched: {totalSeedsSearched:N0}");
                double seedsPerMs = duration.TotalMilliseconds > 0 ? totalSeedsSearched / duration.TotalMilliseconds : 0;
                Console.WriteLine($"   Duration: {duration:hh\\:mm\\:ss}");
                Console.WriteLine($"   TOTAL AVG SPEED PER MS OF SEEDS: {seedsPerMs:N0}");

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
                if (!OuijaConfig.TryLoadFromJsonFile(configPath, out var config))
                    throw new InvalidOperationException($"Config loading failed for: {configPath}");
                return config;
            }

            // Always look in JsonItemFilters for configs
            string fileName = configPath.EndsWith(".json") ? configPath : configPath + ".json";
            string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
            if (File.Exists(jsonItemFiltersPath))
            {
                DebugLogger.Log($"📁 Loading config from: {jsonItemFiltersPath}");
                if (!OuijaConfig.TryLoadFromJsonFile(jsonItemFiltersPath, out var config))
                    throw new InvalidOperationException($"Config loading failed for: {jsonItemFiltersPath}");
                return config;
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