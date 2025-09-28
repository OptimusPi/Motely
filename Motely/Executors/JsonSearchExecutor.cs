using Motely.Filters;
using Motely.Utils;

namespace Motely.Executors
{
    /// <summary>
    /// Executes JSON-based filter searches with specialized vectorized filters
    /// </summary>
    public sealed class JsonSearchExecutor(string configPath, JsonSearchParams parameters)
    {
        private readonly string _configPath = configPath;
        private readonly JsonSearchParams _params = parameters;

        public int Execute()
        {
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;

            List<string>? seeds = LoadSeeds();

            Console.WriteLine($"🔍 Motely Ouija Search Starting");
            Console.WriteLine($"   Config: {_configPath}");
            Console.WriteLine($"   Threads: {_params.Threads}");
            
            if (_params.RandomSeeds.HasValue)
            {
                Console.WriteLine($"   Mode: Random ({_params.RandomSeeds} seeds)");
            }
            else
            {
                Console.WriteLine($"   Batch Size: {_params.BatchSize} chars");
                string endDisplay = _params.EndBatch == 0 ? "∞" : _params.EndBatch.ToString();
                Console.WriteLine($"   Range: {_params.StartBatch} to {endDisplay}");
            }
            if (_params.EnableDebug)
            {
                Console.WriteLine($"   Debug: Enabled");
            }

            Console.WriteLine();

            IMotelySearch? search = null;
            MotelyJsonConfig? config = null;
            
            // Set up Ctrl+C handler to ensure summary prints
            bool ctrlCPressed = false;
            Console.CancelKeyPress += (sender, e) =>
            {
                if (!ctrlCPressed)
                {
                    ctrlCPressed = true;
                    e.Cancel = true; // Prevent immediate termination on first press
                    
                    if (search != null)
                    {
                        Console.WriteLine("\n🛑 Stopping search...");
                        
                        // Immediately pause the search
                        try
                        {
                            search.Pause();
                            
                            // Print the final summary
                            PrintResultsSummary(search);
                            Console.Out.Flush();
                            
                            // Clean up
                            search.Dispose();
                        }
                        catch (Exception ex)
                        {
                            // If something fails, at least try to print what went wrong
                            Console.WriteLine($"\n❌ Error during shutdown: {ex.Message}");
                            try { search.Dispose(); } catch { }
                        }
                        finally
                        {
                            // ALWAYS exit the application, no matter what happens
                            Environment.Exit(0);
                        }
                    }
                }
                else
                {
                    // Second Ctrl+C - force immediate termination
                    e.Cancel = false;
                    Console.WriteLine("\n⛔ Force quitting...");
                }
            };

            try
            {
                config = LoadConfig();
                search = CreateSearch(config, seeds);
                if (search == null)
                {
                    return 1;
                }

                // Print CSV header if we have SHOULD clauses for scoring
                if (config.Should?.Count > 0)
                {
                    PrintResultsHeader(config);
                }

                // Poll for completion instead of blocking
                while (search.Status == MotelySearchStatus.Running && !ctrlCPressed)
                {
                    Thread.Sleep(100);
                }
                
                // If we were interrupted, wait a bit for threads to finish current batch
                if (ctrlCPressed && search.Status == MotelySearchStatus.Paused)
                {
                    Thread.Sleep(200); // Give threads time to finish current batch
                }

                Console.Out.Flush();
                Thread.Sleep(100);
                Console.Out.Flush();

                // Print summary once before returning
                if (search != null)
                {
                    PrintResultsSummary(search);
                    Console.Out.Flush();
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (_params.EnableDebug)
                {
                    Console.WriteLine($"[DEBUG] {ex}");
                }
                return 1;
            }
            finally
            {
                // Just dispose the search, don't print summary again
                if (search != null)
                {
                    search.Dispose();
                }
            }
        }

        private List<string>? LoadSeeds()
        {
            if (!string.IsNullOrEmpty(_params.SpecificSeed))
            {
                Console.WriteLine($"🔍 Searching for specific seed: {_params.SpecificSeed}");
                // Return just the specific seed - let the system handle partial batches
                var seeds = new List<string>()
                {
                    _params.SpecificSeed
                };
                return seeds;
            }

            if (!string.IsNullOrEmpty(_params.Wordlist))
            {
                string wordlistPath = Path.Combine("wordlists", _params.Wordlist + ".txt");
                if (!File.Exists(wordlistPath))
                {
                    throw new FileNotFoundException($"Wordlist not found: {wordlistPath}");
                }

                List<string> seeds = [.. File.ReadAllLines(wordlistPath).Where(static s => !string.IsNullOrWhiteSpace(s))];
                Console.WriteLine($"✅ Loaded {seeds.Count} seeds from wordlist: {wordlistPath}");
                return seeds;
            }

            return null; // Sequential search
        }

        private MotelyJsonConfig LoadConfig()
        {
            string configPath = Path.Combine("JsonItemFilters", _configPath + ".json");

            return !File.Exists(configPath)
                ? throw new FileNotFoundException($"Could not find JSON config file: {configPath}")
                : !MotelyJsonConfig.TryLoadFromJsonFile(configPath, out MotelyJsonConfig? config, out string? error)
                ? throw new Exception($"Failed to load config from {configPath}: {error}")
                : config;
        }

        private IMotelySearch CreateSearch(MotelyJsonConfig config, List<string>? seeds)
        {
            Console.WriteLine("CreateSearch...");
            
            // CRITICAL: Validate and normalize config ONCE at load time!
            // This removes ALL ambiguity from the hot path!
            MotelyJsonConfigValidator.ValidateConfig(config);

            // Create scoring config (SHOULD clauses + ALL voucher clauses for activation)
            var voucherMustClauses = config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher).ToList() ?? [];
            var voucherShouldClauses = config.Should?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher).ToList() ?? [];
            var allVoucherClauses = voucherMustClauses.Concat(voucherShouldClauses).ToList();
            
            MotelyJsonConfig scoringConfig = new()
            {
                Name = config.Name,
                Must = allVoucherClauses, // Include ALL voucher clauses for MaxVoucherAnte calculation
                Should = config.Should ?? new List<MotelyJsonConfig.MotleyJsonFilterClause>(), // ONLY the SHOULD clauses for scoring
                MustNot = [] // Empty - filters handle this
            };
            
            // Initialize parsed enums for scoring config clauses  
            // NOTE: Don't re-initialize SHOULD clauses if they're the same objects as MUST clauses!
            foreach (var clause in voucherMustClauses)
            {
                clause.InitializeParsedEnums();
            }
            
            // Only initialize SHOULD clauses if they haven't been initialized already
            if (config.Should != null)
            {
                foreach (var shouldClause in config.Should)
                {
                    // Check if this clause was already initialized as part of MUST clauses
                    bool alreadyInitialized = config.Must?.Contains(shouldClause) == true;
                    if (!alreadyInitialized)
                    {
                        shouldClause.InitializeParsedEnums();
                    }
                }
            }
            
            // Process the scoring config to calculate MaxVoucherAnte
            scoringConfig.PostProcess();

            // Create callback for CSV output - always output when scoring
            Action<MotelySeedScoreTally> scoreCallback = (MotelySeedScoreTally result) => 
            {
                // Output CSV format: Seed,TotalScore,col1,col2,...
                var scores = string.Join(",", result.TallyColumns.Select(v => v.ToString()));
                Console.WriteLine($"{result.Seed},{result.Score},{scores}");
            };
            
            // When using as a score provider with filters, must be in ScoreOnlyMode
            MotelyJsonSeedScoreDesc scoreDesc = new(scoringConfig, _params.Cutoff, _params.AutoCutoff, scoreCallback, ScoreOnlyMode: true);

            if (_params.AutoCutoff)
            {
                Console.WriteLine($"✅ Loaded config with auto-cutoff (starting at {_params.Cutoff})");
            }
            else
            {
                Console.WriteLine($"✅ Loaded config with cutoff: {_params.Cutoff}");
            }

            // Use specialized filter system
            List<MotelyJsonConfig.MotleyJsonFilterClause> mustClauses = config.Must?.ToList() ?? [];

            // Initialize parsed enums for all clauses
            foreach (MotelyJsonConfig.MotleyJsonFilterClause? clause in mustClauses)
            {
                clause.InitializeParsedEnums();
            }

            Dictionary<FilterCategory, List<MotelyJsonConfig.MotleyJsonFilterClause>> clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(mustClauses);

            if (clausesByCategory.Count == 0)
            {
                throw new Exception("No valid MUST clauses found for filtering");
            }

            // Boss filter now works with proper ante-loop structure

            // BYPASS BROKEN CHAINING: Use composite filter for multiple categories
            List<FilterCategory> categories = [.. clausesByCategory.Keys];
            
            if (categories.Count > 1)
            {
                // Multiple categories - use composite filter to avoid broken chaining
                Console.WriteLine($"[COMPOSITE] Creating composite filter with {categories.Count} filter types");
                var compositeFilter = new MotelyCompositeFilterDesc(mustClauses);
                var compositeSettings = new MotelySearchSettings<MotelyCompositeFilterDesc.MotelyCompositeFilter>(compositeFilter);
                
                // Apply all the same settings
                if (!string.IsNullOrEmpty(config.Deck) && Enum.TryParse(config.Deck, true, out MotelyDeck compositeDeck))
                    compositeSettings = compositeSettings.WithDeck(compositeDeck);
                if (!string.IsNullOrEmpty(config.Stake) && Enum.TryParse(config.Stake, true, out MotelyStake compositeStake))
                    compositeSettings = compositeSettings.WithStake(compositeStake);
                    
                compositeSettings = compositeSettings.WithThreadCount(_params.Threads);
                compositeSettings = compositeSettings.WithBatchCharacterCount(_params.BatchSize);
                compositeSettings = compositeSettings.WithStartBatchIndex((long)_params.StartBatch);
                if (_params.EndBatch > 0)
                    compositeSettings = compositeSettings.WithEndBatchIndex((long)_params.EndBatch);
                    
                bool compositeNeedsScoring = (config.Should?.Count > 0);
                if (compositeNeedsScoring)
                {
                    compositeSettings = compositeSettings.WithSeedScoreProvider(scoreDesc);
                    compositeSettings = compositeSettings.WithCsvOutput(true);
                }
                
                // Start search with composite filter (no chaining needed!)
                if (_params.RandomSeeds.HasValue)
                    return (IMotelySearch)compositeSettings.WithRandomSearch(_params.RandomSeeds.Value).Start();
                else if (seeds != null && seeds.Count > 0)
                    return (IMotelySearch)compositeSettings.WithListSearch(seeds).Start();
                else
                    return (IMotelySearch)compositeSettings.WithSequentialSearch().Start();
            }
            
            // Single category - use normal single filter (no chaining issues)
            FilterCategory primaryCategory = categories[0];
            List<MotelyJsonConfig.MotleyJsonFilterClause> primaryClauses = clausesByCategory[primaryCategory];
            
            // Debug logging for filter setup
            Console.WriteLine($"[FILTER SETUP] Base filter: {primaryCategory} with {primaryClauses.Count} clauses");
            for (int i = 1; i < categories.Count; i++)
            {
                Console.WriteLine($"[FILTER SETUP] Additional filter {i-1}: {categories[i]} with {clausesByCategory[categories[i]].Count} clauses");
            }

            IMotelySeedFilterDesc filterDesc = primaryCategory switch
            {
                FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(MotelyJsonSoulJokerFilterClause.ConvertClauses(primaryClauses)),
                FilterCategory.Joker => new MotelyJsonJokerFilterDesc(MotelyJsonJokerFilterClause.ConvertClauses(primaryClauses)),
                FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(MotelyJsonVoucherFilterClause.ConvertClauses(primaryClauses)),
                FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(MotelyJsonPlanetFilterClause.ConvertClauses(primaryClauses)),
                FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(MotelyJsonTarotFilterClause.ConvertClauses(primaryClauses)),
                FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(MotelyJsonSpectralFilterClause.ConvertClauses(primaryClauses)),
                FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(primaryClauses),
                FilterCategory.Boss => new MotelyJsonBossFilterDesc(primaryClauses), // RE-ENABLED with proper structure
                FilterCategory.Tag => new MotelyJsonTagFilterDesc(primaryClauses),
                _ => throw new ArgumentException($"Specialized filter not implemented: {primaryCategory}")
            };

            // Create search settings with explicit typing
            dynamic searchSettings = primaryCategory switch
            {
                FilterCategory.SoulJoker => new MotelySearchSettings<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>((MotelyJsonSoulJokerFilterDesc)filterDesc),
                FilterCategory.Joker => new MotelySearchSettings<MotelyJsonJokerFilterDesc.MotelyJsonJokerFilter>((MotelyJsonJokerFilterDesc)filterDesc),
                FilterCategory.Voucher => new MotelySearchSettings<MotelyJsonVoucherFilterDesc.MotelyJsonVoucherFilter>((MotelyJsonVoucherFilterDesc)filterDesc),
                FilterCategory.PlanetCard => new MotelySearchSettings<MotelyJsonPlanetFilterDesc.MotelyJsonPlanetFilter>((MotelyJsonPlanetFilterDesc)filterDesc),
                FilterCategory.TarotCard => new MotelySearchSettings<MotelyJsonTarotCardFilterDesc.MotelyJsonTarotCardFilter>((MotelyJsonTarotCardFilterDesc)filterDesc),
                FilterCategory.SpectralCard => new MotelySearchSettings<MotelyJsonSpectralCardFilterDesc.MotelyJsonSpectralCardFilter>((MotelyJsonSpectralCardFilterDesc)filterDesc),
                FilterCategory.PlayingCard => new MotelySearchSettings<MotelyJsonPlayingCardFilterDesc.MotelyJsonPlayingCardFilter>((MotelyJsonPlayingCardFilterDesc)filterDesc),
                FilterCategory.Boss => new MotelySearchSettings<MotelyJsonBossFilterDesc.MotelyJsonBossFilter>((MotelyJsonBossFilterDesc)filterDesc),
                FilterCategory.Tag => new MotelySearchSettings<MotelyJsonTagFilterDesc.MotelyJsonTagFilter>((MotelyJsonTagFilterDesc)filterDesc),
                _ => throw new ArgumentException($"Search settings not implemented: {primaryCategory}")
            };

            Console.WriteLine($"   + Base {primaryCategory} filter: {primaryClauses.Count} clauses");

            // Chain additional filters  
            for (int i = 1; i < categories.Count; i++)
            {
                FilterCategory category = categories[i];
                List<MotelyJsonConfig.MotleyJsonFilterClause> clauses = clausesByCategory[category];
                IMotelySeedFilterDesc additionalFilter = category switch
                {
                    FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses)),
                    FilterCategory.Joker => new MotelyJsonJokerFilterDesc(MotelyJsonJokerFilterClause.ConvertClauses(clauses)),
                    FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(MotelyJsonVoucherFilterClause.ConvertClauses(clauses)),
                    FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(MotelyJsonPlanetFilterClause.ConvertClauses(clauses)),
                    FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(MotelyJsonTarotFilterClause.ConvertClauses(clauses)),
                    FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(MotelyJsonSpectralFilterClause.ConvertClauses(clauses)),
                    FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(clauses),
                    FilterCategory.Boss => new MotelyJsonBossFilterDesc(clauses),
                    FilterCategory.Tag => new MotelyJsonTagFilterDesc(clauses),
                    _ => throw new ArgumentException($"Additional filter not implemented: {category}")
                };
                searchSettings = searchSettings.WithAdditionalFilter(additionalFilter);
                Console.WriteLine($"   + Chained {category} filter: {clauses.Count} clauses");
            }

            // Add scoring when SHOULD clauses exist
            bool needsScoring = (config.Should?.Count > 0);
            if (needsScoring)
            {
                searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);
                // Always enable CSV output when scoring
                searchSettings = searchSettings.WithCsvOutput(true);
            }

            // Apply deck and stake
            if (!string.IsNullOrEmpty(config.Deck) && Enum.TryParse(config.Deck, true, out MotelyDeck deck))
            {
                searchSettings = searchSettings.WithDeck(deck);
            }

            if (!string.IsNullOrEmpty(config.Stake) && Enum.TryParse(config.Stake, true, out MotelyStake stake))
            {
                searchSettings = searchSettings.WithStake(stake);
            }

            // Set batch configuration
            searchSettings = searchSettings.WithThreadCount(_params.Threads);
            searchSettings = searchSettings.WithBatchCharacterCount(_params.BatchSize);
            searchSettings = searchSettings.WithStartBatchIndex((long)_params.StartBatch);
            if (_params.EndBatch > 0)
            {
                searchSettings = searchSettings.WithEndBatchIndex((long)_params.EndBatch);
            }

            // Start search
            if (_params.RandomSeeds.HasValue)
            {
                // Use random seed provider for testing
                return (IMotelySearch)searchSettings.WithRandomSearch(_params.RandomSeeds.Value).Start();
            }
            else if (seeds != null && seeds.Count > 0)
            {
                // Use provided seed list
                return (IMotelySearch)searchSettings.WithListSearch(seeds).Start();
            }
            else
            {
                // Use sequential search
                return (IMotelySearch)searchSettings.WithSequentialSearch().Start();
            }
        }

        private static void PrintResultsHeader(MotelyJsonConfig config)
        {
            Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");
            string header = "Seed,TotalScore";
            if (config.Should != null)
            {
                foreach (MotelyJsonConfig.MotleyJsonFilterClause should in config.Should)
                {
                    string name = !string.IsNullOrEmpty(should.Label) ? should.Label
                                : !string.IsNullOrEmpty(should.Value) ? should.Value 
                                : should.Type;
                    header += $",{name}";
                }
            }
            Console.WriteLine(header);
        }

        private void PrintResultsSummary(IMotelySearch search)
        {
            Console.Out.Flush();
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("✅ SEARCH COMPLETED");
            Console.WriteLine(new string('═', 60));

            long lastBatchIndex = search.CompletedBatchCount > 0 ? (long)_params.StartBatch + search.CompletedBatchCount : 0;

            Console.WriteLine($"   Last batch Index: {lastBatchIndex}");
            Console.WriteLine($"   Seeds matched: {search.MatchingSeeds}");

            TimeSpan elapsed = search.ElapsedTime;
            if (elapsed.TotalMilliseconds > 100)
            {
                Console.WriteLine($"   Duration: {elapsed:hh\\:mm\\:ss\\.fff}");
                double speed = (double)search.TotalSeedsSearched / elapsed.TotalMilliseconds;
                Console.WriteLine($"   Speed: {speed:N0} seeds/ms");
            }
            Console.WriteLine(new string('═', 60));
        }
    }

    public record JsonSearchParams
    {
        public string Config { get; set; } = "standard";
        public int Threads { get; set; } = Environment.ProcessorCount;
        public int BatchSize { get; set; } = 1;
        public ulong StartBatch { get; set; }
        public ulong EndBatch { get; set; }
        public int Cutoff { get; set; }
        public bool AutoCutoff { get; set; }
        public bool EnableDebug { get; set; }
        public bool NoFancy { get; set; }
        public bool Silent { get; set; }
        public string? SpecificSeed { get; set; }
        public string? Wordlist { get; set; }
        public int? RandomSeeds { get; set; }
    }
}