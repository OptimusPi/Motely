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
        private bool _cancelled = false;

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

            try
            {
                MotelyJsonConfig config = LoadConfig();
                IMotelySearch search = CreateSearch(config, seeds);
                if (search == null)
                {
                    return 1;
                }

                // Print CSV header if we have SHOULD clauses for scoring
                if (config.Should?.Count > 0)
                {
                    PrintResultsHeader(config);
                }

                // Setup cancellation handler
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    _cancelled = true;
                    Console.WriteLine("\n🛑 Stopping search...");
                    // Don't dispose here - let it finish gracefully
                };

                search.Start();

                // Wait for completion - progress shown by MotelySearch.PrintReport() in FancyConsole bottom line
                while (search.Status != MotelySearchStatus.Completed && !_cancelled)
                {
                    Thread.Sleep(100);
                }

                // Stop the search gracefully (if cancelled)
                if (_cancelled)
                {
                    search.Dispose();

                    // CRITICAL: Wait for final batch to flush before showing stats
                    // The search may have queued results that need to be written
                    Console.Out.Flush();
                    Thread.Sleep(500); // Give time for final batch flush
                    Console.Out.Flush();
                }

                Console.Out.Flush();
                Thread.Sleep(100);
                Console.Out.Flush();

                PrintResultsSummary(search);
                Console.Out.Flush();
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

            // Create scoring config (SHOULD clauses + voucher Must clauses for activation)
            var voucherMustClauses = config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher).ToList() ?? [];
            MotelyJsonConfig scoringConfig = new()
            {
                Name = config.Name,
                Must = voucherMustClauses, // Include voucher Must clauses for MaxVoucherAnte calculation
                Should = config.Should, // ONLY the SHOULD clauses for scoring
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
                // Add padding to clear any stderr progress text that might be on the line

                // Use colorized output for tallies
                string outputLine = TallyColorizer.FormatResultLine(result.Seed, result.Score, result.TallyColumns);
                Console.WriteLine($"{outputLine}                    ");
            };
            
            MotelyJsonSeedScoreDesc scoreDesc = new(scoringConfig, _params.Cutoff, _params.AutoCutoff, scoreCallback);

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

            // If no MUST clauses, use passthrough filter (accept all seeds, score via SHOULD)
            if (clausesByCategory.Count == 0)
            {
                Console.WriteLine($"[PASSTHROUGH] No MUST clauses - accepting all seeds for scoring");
                var passthroughFilter = new PassthroughFilterDesc();
                var passthroughSettings = new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(passthroughFilter);

                if (!string.IsNullOrEmpty(config.Deck) && Enum.TryParse(config.Deck, true, out MotelyDeck passthroughDeck))
                    passthroughSettings = passthroughSettings.WithDeck(passthroughDeck);
                if (!string.IsNullOrEmpty(config.Stake) && Enum.TryParse(config.Stake, true, out MotelyStake passthroughStake))
                    passthroughSettings = passthroughSettings.WithStake(passthroughStake);

                passthroughSettings = passthroughSettings.WithThreadCount(_params.Threads);
                passthroughSettings = passthroughSettings.WithBatchCharacterCount(_params.BatchSize);
                passthroughSettings = passthroughSettings.WithStartBatchIndex((long)_params.StartBatch);
                if (_params.EndBatch > 0)
                    passthroughSettings = passthroughSettings.WithEndBatchIndex((long)_params.EndBatch + 1); // +1 for inclusive

                passthroughSettings = passthroughSettings.WithSeedScoreProvider(scoreDesc);

                if (seeds != null && seeds.Count > 0)
                    return passthroughSettings.WithListSearch(seeds).Start();
                else
                    return passthroughSettings.WithSequentialSearch().Start();
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
                FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(
                    MotelyJsonSoulJokerFilterClause.CreateCriteria(MotelyJsonSoulJokerFilterClause.ConvertClauses(primaryClauses))),
                FilterCategory.Joker => new MotelyJsonJokerFilterDesc(
                    MotelyJsonJokerFilterClause.CreateCriteria(MotelyJsonJokerFilterClause.ConvertClauses(primaryClauses))),
                FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(
                    MotelyJsonVoucherFilterClause.CreateCriteria(MotelyJsonVoucherFilterClause.ConvertClauses(primaryClauses))),
                FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(
                    MotelyJsonPlanetFilterClause.CreateCriteria(MotelyJsonPlanetFilterClause.ConvertClauses(primaryClauses))),
                FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(
                    MotelyJsonTarotFilterClause.CreateCriteria(MotelyJsonTarotFilterClause.ConvertClauses(primaryClauses))),
                FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(
                    MotelyJsonSpectralFilterClause.CreateCriteria(MotelyJsonSpectralFilterClause.ConvertClauses(primaryClauses))),
                FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreatePlayingCardCriteria(primaryClauses)),
                FilterCategory.Boss => new MotelyJsonBossFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateBossCriteria(primaryClauses)),
                FilterCategory.Tag => new MotelyJsonTagFilterDesc(
                    MotelyJsonFilterClauseExtensions.CreateTagCriteria(primaryClauses)),
                FilterCategory.And or FilterCategory.Or => new MotelyCompositeFilterDesc(primaryClauses),
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
                FilterCategory.And or FilterCategory.Or => new MotelySearchSettings<MotelyCompositeFilterDesc.MotelyCompositeFilter>((MotelyCompositeFilterDesc)filterDesc),
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
                    FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(
                        MotelyJsonSoulJokerFilterClause.CreateCriteria(MotelyJsonSoulJokerFilterClause.ConvertClauses(clauses))),
                    FilterCategory.Joker => new MotelyJsonJokerFilterDesc(
                        MotelyJsonJokerFilterClause.CreateCriteria(MotelyJsonJokerFilterClause.ConvertClauses(clauses))),
                    FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(
                        MotelyJsonVoucherFilterClause.CreateCriteria(MotelyJsonVoucherFilterClause.ConvertClauses(clauses))),
                    FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(
                        MotelyJsonPlanetFilterClause.CreateCriteria(MotelyJsonPlanetFilterClause.ConvertClauses(clauses))),
                    FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(
                        MotelyJsonTarotFilterClause.CreateCriteria(MotelyJsonTarotFilterClause.ConvertClauses(clauses))),
                    FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(
                        MotelyJsonSpectralFilterClause.CreateCriteria(MotelyJsonSpectralFilterClause.ConvertClauses(clauses))),
                    FilterCategory.PlayingCard => new MotelyJsonPlayingCardFilterDesc(
                        MotelyJsonFilterClauseExtensions.CreatePlayingCardCriteria(clauses)),
                    FilterCategory.Boss => new MotelyJsonBossFilterDesc(
                        MotelyJsonFilterClauseExtensions.CreateBossCriteria(clauses)),
                    FilterCategory.Tag => new MotelyJsonTagFilterDesc(
                        MotelyJsonFilterClauseExtensions.CreateTagCriteria(clauses)),
                    FilterCategory.And or FilterCategory.Or => new MotelyCompositeFilterDesc(clauses),
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
                    string name = GetClauseHeaderName(should);
                    header += $",{name}";
                }
            }
            Console.WriteLine(header);
        }

        /// <summary>
        /// Generate a meaningful column name for a clause, handling And/Or groupings
        /// </summary>
        private static string GetClauseHeaderName(MotelyJsonConfig.MotleyJsonFilterClause clause)
        {
            // Handle And/Or clauses with nested clauses
            if (clause.ItemTypeEnum == MotelyFilterItemType.And || clause.ItemTypeEnum == MotelyFilterItemType.Or)
            {
                if (clause.Clauses == null || clause.Clauses.Count == 0)
                    return clause.ItemTypeEnum == MotelyFilterItemType.And ? "And(empty)" : "Or(empty)";

                // Create a compound name from nested clauses (recursively)
                var nestedNames = clause.Clauses.Select(c => GetClauseBaseNameInternal(c)).ToArray();
                string baseName = clause.ItemTypeEnum == MotelyFilterItemType.And
                    ? $"And({string.Join("+", nestedNames)})"
                    : $"Or({string.Join("+", nestedNames)})";

                // Extract ante from first nested clause that has it (they should all match for And/Or groups)
                var anteClause = clause.Clauses.FirstOrDefault(c => c.Antes != null && c.Antes.Length > 0);
                if (anteClause != null && anteClause.Antes != null)
                {
                    if (anteClause.Antes.Length == 1)
                        baseName += $"@A{anteClause.Antes[0]}";
                    else if (anteClause.Antes.Length < 8)
                        baseName += $"@A{string.Join("_", anteClause.Antes)}";
                }

                return baseName;
            }

            // Standard clause - get base name and add ante suffix
            string name = GetClauseBaseNameInternal(clause);

            // Add ante suffix if specific antes are specified (not default all antes)
            if (clause.Antes != null && clause.Antes.Length > 0)
            {
                // If single ante, show as "@A1", if multiple show as "@A1_2_3"
                if (clause.Antes.Length == 1)
                    name += $"@A{clause.Antes[0]}";
                else if (clause.Antes.Length < 8) // Don't show if it's all 8 antes (default)
                    name += $"@A{string.Join("_", clause.Antes)}";
            }

            return name;
        }

        private static string GetClauseBaseNameInternal(MotelyJsonConfig.MotleyJsonFilterClause clause)
        {
            // Standard clause - use Value if available, or Values array, otherwise Type
            if (!string.IsNullOrEmpty(clause.Value))
                return clause.Value;

            // Handle values array (OR matching within a single clause)
            if (clause.Values != null && clause.Values.Length > 0)
            {
                // If multiple values, show as "Val1+Val2+Val3"
                if (clause.Values.Length > 1)
                    return string.Join("+", clause.Values);
                else
                    return clause.Values[0];
            }

            // Fallback to type
            return clause.Type;
        }

        private void PrintResultsSummary(IMotelySearch search)
        {
            Console.Out.Flush();
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("✅ SEARCH COMPLETED");
            Console.WriteLine(new string('═', 60));

            long lastBatchIndex = search.CompletedBatchCount > 0 ? (long)_params.StartBatch + search.CompletedBatchCount : 0;

            // Calculate percentage of total search space
            long maxBatches = (long)Math.Pow(35, 8 - _params.BatchSize);
            int percentComplete = (int)(lastBatchIndex * 100 / maxBatches);

            Console.WriteLine($"   Last batch: {lastBatchIndex:N0} ({percentComplete}%)");
            Console.WriteLine($"   Seeds passed filter: {search.FilteredSeeds}");
            Console.WriteLine($"   Seeds passed cutoff: {search.MatchingSeeds}");

            TimeSpan elapsed = search.ElapsedTime;
            if (elapsed.TotalMilliseconds > 100)
            {
                Console.WriteLine($"   Duration: {elapsed:hh\\:mm\\:ss\\.fff}");
                Console.WriteLine($"   Total seeds: {search.TotalSeedsSearched:N0} ({search.CompletedBatchCount} batches)");
                double speed = (double)search.TotalSeedsSearched / elapsed.TotalMilliseconds;
                Console.WriteLine($"   Speed: {speed:N0} seeds/ms");
            }
            Console.WriteLine(new string('═', 60));
            Console.WriteLine($"💡 To continue: --startBatch {lastBatchIndex} or --startPercent {percentComplete}");
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
        public string? SpecificSeed { get; set; }
        public string? Wordlist { get; set; }
        public int? RandomSeeds { get; set; }
    }
}