using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.Filters; // For MotelyJsonConfig, MotelyJsonSeedScoreDesc, MotelyJsonConfigValidator

namespace Motely
{
    partial class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "Motely",
                Description = "Motely JSON Search Filter - Dynamic Balatro Seed Searcher",
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

            var motelyOption = app.Option<string>(
                "--motely <FILTER>",
                "Run built-in Motely filter (PerkeoObservatory, Trickeoglyph, SoulTest, etc.)",
                CommandOptionType.SingleValue);
            motelyOption.DefaultValue = string.Empty;

            app.OnExecute(() =>
            {
                var analyzeSeed = analyzeOption.Value();
                var motelyFilter = motelyOption.Value();
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

                // Validate batch ranges based on search space
                ulong maxBatches = (ulong)Math.Pow(35, 8 - batchSize); // 35^(8-batchSize)
                if (endBatch > maxBatches)
                {
                    Console.WriteLine($"❌ endBatch too large: {endBatch} (max for batchSize {batchSize}: {maxBatches:N0})");
                    return 1;
                }
                if (startBatch >= endBatch && endBatch != 0)
                {
                    Console.WriteLine($"❌ startBatch ({startBatch}) must be less than endBatch ({endBatch})");
                    return 1;
                }
                if (enableDebug)
                {
                    Console.WriteLine($"[DEBUG] Cutoff string: '{cutoffStr}'");
                    Console.WriteLine($"[DEBUG] Cutoff value: {cutoffValue}");
                    Console.WriteLine($"[DEBUG] Auto-cutoff: {autoCutoff}");
                    Console.WriteLine($"[DEBUG] Effective starting cutoff: {cutoff}");
                }
                var wordlist = wordlistOption.Value();
                var keyword = keywordOption.Value();
                var nofancy = noFancyOption.HasValue();
                var seedInput = seedOption.Value();

                // Validate batchSize
                if (batchSize < 1 || batchSize > 8)
                {
                    Console.WriteLine($"❌ Error: batchSize must be between 1 and 8 (got {batchSize})");
                    Console.WriteLine($"   batchSize represents the number of seed digits to process in parallel.");
                    Console.WriteLine($"   Valid range: 1-8 (Balatro seeds are 1-8 characters)");
                    Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                    return 1;
                }

                // Check if built-in Motely filter mode
                if (!string.IsNullOrEmpty(motelyFilter))
                {
                    return RunMotelyFilter(motelyFilter, threads, batchSize, enableDebug, nofancy, seedInput, startBatch, endBatch, wordlist);
                }

                // Execute the search (this was previously commented out which made the app a no-op)
                PiFreakLovesYou(configName, startBatch, endBatch, threads, batchSize, cutoff, autoCutoff,
                    enableDebug, wordlist, keyword, nofancy, seedInput);
                return 0;
            });

            return app.Execute(args);
        }
        private static int RunMotelyFilter(string filterName, int threads, int batchSize, bool enableDebug, bool nofancy, string? specificSeed, ulong startBatch, ulong endBatch, string? wordlist)
        {
            DebugLogger.IsEnabled = enableDebug;
            FancyConsole.IsEnabled = !nofancy;

            string normalizedFilterName = filterName.ToLower().Trim();

            IMotelySearch search;
            if (normalizedFilterName == "perkeoobservatory")
            {
                var settings = new MotelySearchSettings<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>(new PerkeoObservatoryFilterDesc())
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchSize)
                    .WithResultCallback((seed, score, details) =>
                    {
                        Console.WriteLine($"{seed}");
                    });

                if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
                if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

                if (!string.IsNullOrEmpty(specificSeed))
                {
                    search = settings.WithListSearch([specificSeed]).Start();
                }
                else if (!string.IsNullOrEmpty(wordlist))
                {
                    var wordlistPath = $"WordLists/{wordlist}.txt";
                    if (!File.Exists(wordlistPath))
                    {
                        Console.WriteLine($"❌ Wordlist file not found: {wordlistPath}");
                        return 1;
                    }
                    var seeds = File.ReadAllLines(wordlistPath).Where(line => !string.IsNullOrWhiteSpace(line));
                    search = settings.WithListSearch(seeds).Start();
                }
                else
                {
                    search = settings.WithSequentialSearch().Start();
                }
            }
            else if (normalizedFilterName == "trickeoglyph")
            {
                var settings = new MotelySearchSettings<TrickeoglyphFilterDesc.TrickeoglyphFilter>(new TrickeoglyphFilterDesc())
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchSize)
                    .WithResultCallback((seed, score, details) =>
                    {
                        Console.WriteLine($"{seed}");
                    });

                if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
                if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

                if (!string.IsNullOrEmpty(specificSeed))
                {
                    search = settings.WithListSearch([specificSeed]).Start();
                }
                else if (!string.IsNullOrEmpty(wordlist))
                {
                    var wordlistPath = $"WordLists/{wordlist}.txt";
                    if (!File.Exists(wordlistPath))
                    {
                        Console.WriteLine($"❌ Wordlist file not found: {wordlistPath}");
                        return 1;
                    }
                    var seeds = File.ReadAllLines(wordlistPath).Where(line => !string.IsNullOrWhiteSpace(line));
                    search = settings.WithListSearch(seeds).Start();
                }
                else
                {
                    search = settings.WithSequentialSearch().Start();
                }
            }
            else if (normalizedFilterName == "negativecopy")
            {
                var settings = new MotelySearchSettings<NegativeCopyFilterDesc.NegativeCopyFilter>(new NegativeCopyFilterDesc())
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchSize)
                    .WithResultCallback((seed, score, details) =>
                    {
                        Console.WriteLine($"{seed}");
                    });

                if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
                if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

                if (!string.IsNullOrEmpty(specificSeed))
                {
                    search = settings.WithListSearch([specificSeed]).Start();
                }
                else if (!string.IsNullOrEmpty(wordlist))
                {
                    var wordlistPath = $"WordLists/{wordlist}.txt";
                    if (!File.Exists(wordlistPath))
                    {
                        Console.WriteLine($"❌ Wordlist file not found: {wordlistPath}");
                        return 1;
                    }
                    var seeds = File.ReadAllLines(wordlistPath).Where(line => !string.IsNullOrWhiteSpace(line));
                    search = settings.WithListSearch(seeds).Start();
                }
                else
                {
                    search = settings.WithSequentialSearch().Start();
                }
            }
            else if (normalizedFilterName == "soultest")
            {
                var settings = new MotelySearchSettings<SoulTestFilterDesc.SoulTestFilter>(new SoulTestFilterDesc())
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchSize)
                    .WithResultCallback((seed, score, details) =>
                    {
                        Console.WriteLine($"{seed}");
                    });

                if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
                if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);

                if (!string.IsNullOrEmpty(specificSeed))
                {
                    search = settings.WithListSearch([specificSeed]).Start();
                }
                else if (!string.IsNullOrEmpty(wordlist))
                {
                    var wordlistPath = $"WordLists/{wordlist}.txt";
                    if (!File.Exists(wordlistPath))
                    {
                        Console.WriteLine($"❌ Wordlist file not found: {wordlistPath}");
                        return 1;
                    }
                    var seeds = File.ReadAllLines(wordlistPath).Where(line => !string.IsNullOrWhiteSpace(line));
                    search = settings.WithListSearch(seeds).Start();
                }
                else
                {
                    search = settings.WithSequentialSearch().Start();
                }
            }
            else
            {
                throw new ArgumentException($"Unknown Motely filter: {filterName}");
            }

            Console.WriteLine($"🔍 Running Motely filter: {filterName}" +
                (!string.IsNullOrEmpty(specificSeed) ? $" on seed: {specificSeed}" : ""));

            search.Start();
            search.AwaitCompletion();
            return 0;
        }

        public static void PiFreakLovesYou(string configPath, ulong startBatch, ulong endBatch, int threads,
            int batchSize, int cutoff, bool autoCutoff, bool enableDebug, string? wordlist = null,
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
                Console.WriteLine($"🔍 Searching for specific seed: {specificSeed}");
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

                Console.WriteLine($"✅ Loaded {seeds.Count} seeds from wordlist: {wordlistPath}");
            }


            Console.WriteLine($"🔍 Motely Ouija Search Starting");
            Console.WriteLine($"   Config: {configPath}");
            Console.WriteLine($"   Threads: {threads}");
            Console.WriteLine($"   Batch Size: {batchSize} chars");
            string endDisplay = endBatch <= 0 ? "∞" : endBatch.ToString();
            Console.WriteLine($"   Range: {startBatch} to {endDisplay}");
            if (enableDebug)
                Console.WriteLine($"   Debug: Enabled");
            Console.WriteLine();

            try
            {
                // Load config
                var config = LoadConfig(configPath);

                Console.WriteLine($"✅ Loaded config: {config.Must?.Count ?? 0} must, {config.Should?.Count ?? 0} should, {config.MustNot?.Count ?? 0} mustNot");

                // Print the parsed config for debugging
                if (enableDebug)
                {
                    DebugLogger.Log("\n--- Parsed Motely JSON Config ---");
                    DebugLogger.Log(config.ToJson());
                    DebugLogger.Log("--- End Config ---\n");
                }

                Action<MotelySeedScoreTally> onResultFound = (score) =>
                {
                    Console.WriteLine($"{score.Seed},{score.Score},{string.Join(",", score.TallyColumns)}");
                };

                var scoreDesc = new MotelyJsonSeedScoreDesc(config, cutoff, autoCutoff, onResultFound);

                if (autoCutoff)
                    Console.WriteLine($"✅ Loaded config with auto-cutoff (starting at {cutoff})");
                else
                    Console.WriteLine($"✅ Loaded config with cutoff: {cutoff}");


                // Create the search using MotelyJsonFilterSlice pattern
                // Start with main filter slice, then chain additional slices based on config
                
                // Get ALL clauses (Must + Should + MustNot) for each category
                var allClauses = config.Must.Concat(config.Should ?? []).Concat(config.MustNot ?? []).ToList();
                var voucherClauses = allClauses.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher).ToList();
                
                var mainFilterSlice = new MotelyJsonFilterDesc(FilterCategory.Voucher, voucherClauses);

                var searchSettings = new MotelySearchSettings<MotelyJsonFilterDesc.MotelyFilter>(mainFilterSlice)
                    .WithThreadCount(threads);

                // Chain additional filter slices using ALL clauses
                var tarotClauses = allClauses.Where(c => c.ItemTypeEnum == MotelyFilterItemType.TarotCard).ToList();
                if (tarotClauses.Count > 0)
                    searchSettings = searchSettings.WithAdditionalFilter(new MotelyJsonFilterDesc(FilterCategory.TarotCard, tarotClauses));
                    
                var jokerClauses = allClauses.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Joker || c.ItemTypeEnum == MotelyFilterItemType.SoulJoker).ToList();
                if (jokerClauses.Count > 0)
                    searchSettings = searchSettings.WithAdditionalFilter(new MotelyJsonFilterDesc(FilterCategory.Joker, jokerClauses));

                // Add final scoring
                searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);

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
                    if (enableDebug)
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
                    if (enableDebug)
                        Console.WriteLine($"[DEBUG] Starting sequential batch search");
                    search = searchSettings
                    .WithSequentialSearch()
                    .WithBatchCharacterCount(batchSize)
                    .Start();
                }

                // Print CSV header for results
                PrintResultsHeader(config);

                // Reset cancellation flag
                MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider.IsCancelled = false;

                // Callback already set above when creating filterDesc

                // Setup cancellation token
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\n🛑 Stopping search...");
                    MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider.IsCancelled = true;
                    search.Dispose();
                    Console.WriteLine("✅ Search stopped gracefully");
                };

                // WAIT FOR SEARCH TO COMPLETE!
                DebugLogger.Log($"[Program] Search started. Initial status: {search.Status}");
                int loopCount = 0;
                while (search.Status != MotelySearchStatus.Completed && !MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider.IsCancelled)
                {
                    System.Threading.Thread.Sleep(100);
                    loopCount++;
                    if (loopCount % 10 == 0) // Log every second
                    {
                        DebugLogger.Log($"[Program] Waiting for search... Status: {search.Status}, CompletedBatchCount: {search.CompletedBatchCount}");
                    }
                }
                DebugLogger.Log($"[Program] Search loop exited. Final status: {search.Status}, Cancelled: {MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider.IsCancelled}");

                // Stop timing
                searchStopwatch.Stop();

                // Results are printed in real-time by MotelyJsonSeedScoreDesc
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

        static MotelyJsonConfig LoadConfig(string configPath)
        {
            // If configPath is a rooted (absolute) path, use it directly
            if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            {
                DebugLogger.Log($"📁 Loading config from: {configPath}");
                if (!MotelyJsonConfig.TryLoadFromJsonFile(configPath, out var config))
                    throw new InvalidOperationException($"Config loading failed for: {configPath}");
                return config;
            }

            // Always look in ./JsonItemFilters for configs
            string fileName = configPath.EndsWith(".json") ? configPath : configPath + ".json";
            string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
            if (File.Exists(jsonItemFiltersPath))
            {
                DebugLogger.Log($"📁 Loading config from: {jsonItemFiltersPath}");
                if (!MotelyJsonConfig.TryLoadFromJsonFile(jsonItemFiltersPath, out var config))
                    throw new InvalidOperationException($"Config loading failed for: {jsonItemFiltersPath}");
                return config;
            }

            throw new FileNotFoundException($"Could not find config file: {configPath}");
        }

        static void PrintResultsHeader(MotelyJsonConfig config)
        {
            // Print deck/stake info as comments
            Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");

            // Print CSV header only once
            var header = "Seed,TotalScore";

            // Add column for each should clause
            if (config.Should != null)
            {
                foreach (var should in config.Should)
                {
                    var col = GetColumnLabel(should);
                    header += $",{col}";
                }
            }
            Console.WriteLine(header);
        }

        static string GetColumnLabel(MotelyJsonConfig.MotleyJsonFilterClause should)
        {
            // Format as Type:Value or just Value if type is obvious
            var name = should.Label ?? should.Value ?? should.Type;
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