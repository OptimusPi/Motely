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
            Console.WriteLine($"   Batch Size: {_params.BatchSize} chars");
            string endDisplay = _params.EndBatch == 0 ? "∞" : _params.EndBatch.ToString();
            Console.WriteLine($"   Range: {_params.StartBatch} to {endDisplay}");
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

                // Print CSV header
                PrintResultsHeader(config);

                search.AwaitCompletion();

                Thread.Sleep(100);
                Console.Out.Flush();

                PrintResultsSummary(search);
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
                return [_params.SpecificSeed];
            }

            if (!string.IsNullOrEmpty(_params.Wordlist))
            {
                string wordlistPath = Path.Combine("wordlists", _params.Wordlist + ".txt");
                if (!File.Exists(wordlistPath))
                {
                    throw new FileNotFoundException($"Wordlist not found: {wordlistPath}");
                }

                List<string> seeds = File.ReadAllLines(wordlistPath).Where(static s => !string.IsNullOrWhiteSpace(s)).ToList();
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
            foreach (var clause in voucherMustClauses.Concat(config.Should ?? []))
            {
                clause.InitializeParsedEnums();
            }
            
            // Process the scoring config to calculate MaxVoucherAnte
            scoringConfig.PostProcess();

            static void dummyCallback(MotelySeedScoreTally _) { } // Empty callback - using buffer approach
            MotelyJsonSeedScoreDesc scoreDesc = new(scoringConfig, _params.Cutoff, _params.AutoCutoff, dummyCallback);

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

            // Create base filter with first category
            List<FilterCategory> categories = [.. clausesByCategory.Keys];
            FilterCategory primaryCategory = categories[0];
            List<MotelyJsonConfig.MotleyJsonFilterClause> primaryClauses = clausesByCategory[primaryCategory];

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

            // Add scoring only if there are SHOULD clauses or complex MUST requirements
            bool needsScoring = (config.Should?.Count > 0) || (config.MustNot?.Count > 0);
            if (needsScoring)
            {
                searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);
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
            return seeds != null && seeds.Count > 0
                ? (IMotelySearch)searchSettings.WithListSearch(seeds).Start()
                : (IMotelySearch)searchSettings.WithSequentialSearch().Start();
        }

        private static void PrintResultsHeader(MotelyJsonConfig config)
        {
            Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");
            string header = "Seed,TotalScore";
            if (config.Should != null)
            {
                foreach (MotelyJsonConfig.MotleyJsonFilterClause should in config.Should)
                {
                    string name = !string.IsNullOrEmpty(should.Value) ? should.Value : should.Type;
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
            Console.WriteLine($"   Seeds searched: {search.TotalSeedsSearched:N0}");
            Console.WriteLine($"   Seeds matched: {search.MatchingSeeds:N0}");

            TimeSpan elapsed = search.ElapsedTime;
            if (elapsed.TotalMilliseconds > 100)
            {
                Console.WriteLine($"   Duration: {elapsed:hh\\:mm\\:ss\\.fff}");
                double speed = search.TotalSeedsSearched / elapsed.TotalMilliseconds;
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
    }
}