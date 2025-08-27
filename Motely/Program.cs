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
                "Minimum TotalScore threshold (0+ for fixed, 'auto' for auto-cutoff)",
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

            var scoreOnlyOption = app.Option(
                "--scoreOnly",
                "Run only the scoring filter without the base filter",
                CommandOptionType.NoValue);

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

                    MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(analyzeSeed, deck, stake));
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
                bool autoCutoff = cutoffStr.ToLowerInvariant() == "auto";
                int cutoffValue = autoCutoff ? 1 : (int.TryParse(cutoffStr, out var c) ? c : 0);
                int cutoff = cutoffValue;  // Use the parsed value or 1 for auto

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

                var scoreOnly = scoreOnlyOption.HasValue();
                return PiFreakLovesYou(configName, startBatch, endBatch, threads, batchSize, cutoff, autoCutoff,
                    enableDebug, wordlist, keyword, nofancy, seedInput, scoreOnly);
            });

            return app.Execute(args);
        }
        // Reusable function to create slice-chained search from JSON config
        public static MotelySearchSettings<MotelyJsonFilterDesc.MotelyFilter> CreateSliceChainedSearch(MotelyJsonConfig config, int threads, bool scoreOnly = false)
        {
            MotelySearchSettings<MotelyJsonFilterDesc.MotelyFilter>? searchSettings = null;
            
            if (!scoreOnly)
            {
                // Get only Must clauses for base filtering
                var mustClauses = config.Must.ToList();
                
                // Sort by FilterOrder (if specified), with default of 999 for unspecified
                // Then group by category to create filter slices
                var sortedClauses = mustClauses
                    .OrderBy(c => c.FilterOrder ?? 999)
                    .ThenBy(c => c.ItemTypeEnum)  // Secondary sort for stability
                    .ToList();
                
                // Group clauses by category while preserving order
                var categoryGroups = new List<(FilterCategory category, List<MotelyJsonConfig.MotleyJsonFilterClause> clauses)>();
                var processedCategories = new HashSet<FilterCategory>();
                
                foreach (var clause in sortedClauses)
                {
                    var category = GetFilterCategory(clause.ItemTypeEnum);
                    if (!processedCategories.Contains(category))
                    {
                        var categoryClauses = mustClauses.Where(c => GetFilterCategory(c.ItemTypeEnum) == category).ToList();
                        if (categoryClauses.Count > 0)
                        {
                            // DebugLogger.Log($"[CreateSliceChainedSearch] Adding filter group: {category} with {categoryClauses.Count} clauses"); // DISABLED FOR PERFORMANCE
                            categoryGroups.Add((category, categoryClauses));
                            processedCategories.Add(category);
                        }
                    }
                }

                // If we have base filter groups, create the chain
                if (categoryGroups.Count > 0)
                {
                    // Create the search settings starting with the first group
                    var firstGroup = categoryGroups[0];
                    searchSettings = new MotelySearchSettings<MotelyJsonFilterDesc.MotelyFilter>(
                        new MotelyJsonFilterDesc(firstGroup.category, firstGroup.clauses))
                        .WithThreadCount(threads);
                    
                    // Add remaining groups as additional filters
                    for (int i = 1; i < categoryGroups.Count; i++)
                    {
                        var group = categoryGroups[i];
                        searchSettings = searchSettings.WithAdditionalFilter(
                            new MotelyJsonFilterDesc(group.category, group.clauses));
                    }
                }
            }
            else
            {
                // ScoreOnly mode - create a dummy base filter that passes everything
                searchSettings = new MotelySearchSettings<MotelyJsonFilterDesc.MotelyFilter>(
                    new MotelyJsonFilterDesc(FilterCategory.Voucher, new List<MotelyJsonConfig.MotleyJsonFilterClause>()))
                    .WithThreadCount(threads);
            }
            
            
            // Return settings - scoring will be added separately via WithSeedScoreProvider
            return searchSettings!;
        }
        
        private static FilterCategory GetFilterCategory(MotelyFilterItemType itemType)
        {
            return itemType switch
            {
                MotelyFilterItemType.Voucher => FilterCategory.Voucher,
                MotelyFilterItemType.Joker or MotelyFilterItemType.SoulJoker => FilterCategory.Joker,
                MotelyFilterItemType.TarotCard => FilterCategory.TarotCard,
                MotelyFilterItemType.PlanetCard => FilterCategory.PlanetCard,
                MotelyFilterItemType.SpectralCard => FilterCategory.SpectralCard,
                MotelyFilterItemType.PlayingCard => FilterCategory.PlayingCard,
                MotelyFilterItemType.SmallBlindTag or MotelyFilterItemType.BigBlindTag => FilterCategory.Tag,
                MotelyFilterItemType.Boss => FilterCategory.Boss,
                _ => FilterCategory.Joker
            };
        }


        private static int RunMotelyFilter(string filterName, int threads, int batchSize, bool enableDebug, bool nofancy, string? specificSeed, ulong startBatch, ulong endBatch, string? wordlist)
        {
            DebugLogger.IsEnabled = enableDebug;
            FancyConsole.IsEnabled = !nofancy;

            string normalizedFilterName = filterName.ToLower().Trim();
            
            // Setup progress callback for all filters  
            DateTime lastProgressUpdate = DateTime.UtcNow;
            DateTime progressStartTime = DateTime.UtcNow;
            Action<ulong, ulong, ulong, double> progressCallback = (completed, total, seedsSearched, seedsPerMs) =>
            {
                // Limit update frequency to prevent console spam
                var now = DateTime.UtcNow;
                var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;
                if (timeSinceLastUpdate < 100) return; // Update at most every 100ms
                lastProgressUpdate = now;
                
                var elapsedMS = (now - progressStartTime).TotalMilliseconds;
                string timeLeftFormatted = "calculating...";
                if (total > 0 && completed > 0)
                {
                    double portionFinished = (double)completed / total;
                    double timeLeft = elapsedMS / portionFinished - elapsedMS;
                    TimeSpan timeLeftSpan = TimeSpan.FromMilliseconds(Math.Min(timeLeft, TimeSpan.MaxValue.TotalMilliseconds));
                    if (timeLeftSpan.Days == 0) timeLeftFormatted = $"{timeLeftSpan:hh\\:mm\\:ss}";
                    else timeLeftFormatted = $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
                }
                double pct = total > 0 ? Math.Clamp(((double)completed / total) * 100, 0, 100) : 0;
                string[] spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧"];
                var spinner = spinnerFrames[(int)(elapsedMS / 250) % spinnerFrames.Length];
                string progressLine = $"{spinner} {pct:F2}% | {timeLeftFormatted} remaining | {Math.Round(seedsPerMs)} seeds/ms";
                Console.Write($"\r{progressLine}                    \r{progressLine}");
            };

            IMotelySearch search;
            if (normalizedFilterName == "perkeoobservatory")
            {
                var settings = new MotelySearchSettings<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>(new PerkeoObservatoryFilterDesc())
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchSize)
                    .WithProgressCallback(progressCallback)
                    .WithResultCallback((seed, score, details) =>
                    {
                        Console.WriteLine($"\r{seed}                                                  ");
                        MotelySearch<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>.RestoreProgressLine();
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
                    .WithProgressCallback(progressCallback)
                    .WithResultCallback((seed, score, details) =>
                    {
                        Console.WriteLine($"\r{seed}                                                  ");
                        MotelySearch<TrickeoglyphFilterDesc.TrickeoglyphFilter>.RestoreProgressLine();
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
                        MotelySearch<NegativeCopyFilterDesc.NegativeCopyFilter>.RestoreProgressLine();
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
                        MotelySearch<SoulTestFilterDesc.SoulTestFilter>.RestoreProgressLine();
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

            var searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            search.Start();
            search.AwaitCompletion();
            searchStopwatch.Stop();
            
            // Print completion summary
            Console.WriteLine("\n✅ Search completed");
            
            // Calculate and display performance metrics
            ulong totalSeedsSearched = search.CompletedBatchCount * (ulong)Math.Pow(35, batchSize);
            var duration = searchStopwatch.Elapsed;
            
            Console.WriteLine($"   Total seeds searched: {totalSeedsSearched:N0}");
            
            // Show performance
            double durationMs = duration.TotalMilliseconds;
            if (durationMs >= 1)
            {
                double seedsPerMs = totalSeedsSearched / durationMs;
                Console.WriteLine($"   Time elapsed: {duration.TotalSeconds:F2} seconds");
                Console.WriteLine($"   Performance: {seedsPerMs:N0} seeds/ms");
            }
            else
            {
                Console.WriteLine($"   Time elapsed: < 1 ms (instant)");
                if (totalSeedsSearched > 0)
                {
                    double seedsPerMs = totalSeedsSearched / 0.001; // Assume 1 microsecond minimum
                    Console.WriteLine($"   Performance: > {seedsPerMs:N0} seeds/ms");
                }
            }
            return 0;
        }

        public static int PiFreakLovesYou(string configPath, ulong startBatch, ulong endBatch, int threads,
            int batchSize, int cutoff, bool autoCutoff, bool enableDebug, string? wordlist = null,
            string? keyword = null, bool nofancy = false, string? specificSeed = null, bool scoreOnly = false)
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

                string lastProgressLine = "";
                Action<MotelySeedScoreTally> onResultFound = (score) =>
                {
                    // Clear the current line (progress line)
                    Console.Write("\r" + new string(' ', Console.WindowWidth - 1) + "\r");
                    
                    // Print the result
                    Console.WriteLine($"{score.Seed},{score.Score},{string.Join(",", score.TallyColumns)}");
                    
                    // Restore the last known progress line
                    if (!string.IsNullOrEmpty(lastProgressLine))
                    {
                        Console.Write(lastProgressLine);
                    }
                };

                var scoreDesc = new MotelyJsonSeedScoreDesc(config, cutoff, autoCutoff, onResultFound, scoreOnly);

                if (autoCutoff)
                    Console.WriteLine($"✅ Loaded config with auto-cutoff (starting at {cutoff})");
                else
                    Console.WriteLine($"✅ Loaded config with cutoff: {cutoff}");


                // Create the search using MotelyJsonFilterSlice pattern
                // Start with main filter slice, then chain additional slices based on config

                // Create slice-chained search using reusable function (includes scoring if should clauses exist)
                var searchSettings = CreateSliceChainedSearch(config, threads, scoreOnly);

                // Add the score provider ONLY if we have valid should clauses or scoreOnly mode
                bool hasValidShouldClauses = config.Should != null && 
                    config.Should.Count > 0 && 
                    config.Should.Any(c => !string.IsNullOrEmpty(c.Type) || !string.IsNullOrEmpty(c.Value));
                    
                if (hasValidShouldClauses || scoreOnly)
                {
                    // DebugLogger.Log($"[Program] Adding score provider (hasValidShouldClauses={hasValidShouldClauses}, scoreOnly={scoreOnly}"); // DISABLED FOR PERFORMANCE
                    searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);
                }
                else
                {
                    // DebugLogger.Log("[Program] Skipping score provider - no valid should clauses"); // DISABLED FOR PERFORMANCE
                }

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

                // Progress callback for emoji progress display
                DateTime progressStartTime = DateTime.UtcNow;
                DateTime lastProgressUpdate = DateTime.UtcNow;
                searchSettings = searchSettings.WithProgressCallback((completed, total, seedsSearched, seedsPerMs) =>
                {
                    // Limit update frequency to prevent console spam
                    var now = DateTime.UtcNow;
                    var timeSinceLastUpdate = (now - lastProgressUpdate).TotalMilliseconds;
                    if (timeSinceLastUpdate < 100) return; // Update at most every 100ms
                    lastProgressUpdate = now;
                    
                    var elapsedMS = (now - progressStartTime).TotalMilliseconds;
                    string timeLeftFormatted = "calculating...";
                    if (total > 0 && completed > 0)
                    {
                        double portionFinished = (double)completed / total;
                        double timeLeft = elapsedMS / portionFinished - elapsedMS;
                        TimeSpan timeLeftSpan = TimeSpan.FromMilliseconds(Math.Min(timeLeft, TimeSpan.MaxValue.TotalMilliseconds));
                        if (timeLeftSpan.Days == 0) timeLeftFormatted = $"{timeLeftSpan:hh\\:mm\\:ss}";
                        else timeLeftFormatted = $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
                    }
                    double pct = total > 0 ? Math.Clamp(((double)completed / total) * 100, 0, 100) : 0;
                    string[] spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧"];
                    var spinner = spinnerFrames[(int)(elapsedMS / 250) % spinnerFrames.Length];
                    string progressLine = $"{spinner} {pct:F2}% | {timeLeftFormatted} remaining | {Math.Round(seedsPerMs)} seeds/ms";
                    lastProgressLine = $"\r{progressLine}"; // Store for restoration after result print
                    Console.Write($"\r{progressLine}                    \r{progressLine}");
                });

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
                    // Don't print completion here - let the main code handle it
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
                if (MotelyJsonSeedScoreDesc.MotelyJsonSeedScoreProvider.IsCancelled)
                    Console.WriteLine("\n✅ Search stopped gracefully");
                else
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
                
                // For very fast searches, show microseconds or just say "instant"
                double durationMs = duration.TotalMilliseconds;
                double seedsPerMs = 0;
                
                if (durationMs >= 1)
                {
                    seedsPerMs = totalSeedsSearched / durationMs;
                    Console.WriteLine($"   Duration: {duration:hh\\:mm\\:ss\\.fff} ({durationMs:N0}ms)");
                    
                    // For slow speeds, show more precision
                    if (seedsPerMs < 1)
                        Console.WriteLine($"   Speed: {seedsPerMs:F3} seeds/ms ({totalSeedsSearched / duration.TotalSeconds:N0} seeds/sec)");
                    else
                        Console.WriteLine($"   Speed: {seedsPerMs:N0} seeds/ms");
                }
                else if (duration.TotalMicroseconds >= 1)
                {
                    double seedsPerUs = totalSeedsSearched / duration.TotalMicroseconds;
                    Console.WriteLine($"   Duration: {duration.TotalMicroseconds:N0} microseconds");
                    Console.WriteLine($"   Speed: {seedsPerUs:N2} seeds/μs");
                }
                else
                {
                    Console.WriteLine($"   Duration: <1 microsecond");
                    Console.WriteLine($"   Speed: Instant");
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
            return 0;
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