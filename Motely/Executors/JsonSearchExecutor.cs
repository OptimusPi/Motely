using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Motely.Filters;
using Motely.Utils;

namespace Motely.Executors
{
    /// <summary>
    /// Executes JSON-based filter searches with specialized vectorized filters
    /// </summary>
    public sealed class JsonSearchExecutor
    {
        private readonly string _configPath;
        private readonly JsonSearchParams _params;
        private volatile bool _cancelled = false;

        public JsonSearchExecutor(string configPath, JsonSearchParams parameters)
        {
            _configPath = configPath;
            _params = parameters;
        }

        public int Execute()
        {
            DebugLogger.IsEnabled = _params.EnableDebug;
            FancyConsole.IsEnabled = !_params.NoFancy;
            
            List<string>? seeds = LoadSeeds();
            
            Console.WriteLine($"🔍 Motely Ouija Search Starting");
            Console.WriteLine($"   Config: {_configPath}");
            Console.WriteLine($"   Threads: {_params.Threads}");
            Console.WriteLine($"   Batch Size: {_params.BatchSize} chars");
            var endDisplay = _params.EndBatch == 0 ? "∞" : _params.EndBatch.ToString();
            Console.WriteLine($"   Range: {_params.StartBatch} to {endDisplay}");
            if (_params.EnableDebug)
                Console.WriteLine($"   Debug: Enabled");
            Console.WriteLine();

            try
            {
                var config = LoadConfig();
                var search = CreateSearch(config, seeds);
                if (search == null) return 1;

                // Print CSV header
                PrintResultsHeader(config);
                
                search.AwaitCompletion();
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
                return new List<string> { _params.SpecificSeed };
            }

            if (!string.IsNullOrEmpty(_params.WordList))
            {
                var wordlistPath = Path.Combine("wordlists", _params.WordList + ".txt");
                if (!File.Exists(wordlistPath))
                    throw new FileNotFoundException($"Wordlist not found: {wordlistPath}");

                var seeds = File.ReadAllLines(wordlistPath).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                Console.WriteLine($"✅ Loaded {seeds.Count} seeds from wordlist: {wordlistPath}");
                return seeds;
            }

            return null; // Sequential search
        }

        private MotelyJsonConfig LoadConfig()
        {
            var configPath = Path.Combine("JsonItemFilters", _configPath + ".json");
            
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Could not find JSON config file: {configPath}");

            if (!MotelyJsonConfig.TryLoadFromJsonFile(configPath, out var config, out var error))
                throw new Exception($"Failed to load config from {configPath}: {error}");

            return config;
        }

        private IMotelySearch CreateSearch(MotelyJsonConfig config, List<string>? seeds)
        {
            Console.WriteLine("CreateSearch...");
            
            // Create scoring config (SHOULD clauses only)
            var scoringConfig = new MotelyJsonConfig
            {
                Name = config.Name,
                Must = new List<MotelyJsonConfig.MotleyJsonFilterClause>(), // Empty - filters handle this
                Should = config.Should, // ONLY the SHOULD clauses for scoring
                MustNot = new List<MotelyJsonConfig.MotleyJsonFilterClause>() // Empty - filters handle this
            };
            
            Action<MotelySeedScoreTally> dummyCallback = _ => { }; // Empty callback - using buffer approach
            var scoreDesc = new MotelyJsonSeedScoreDesc(scoringConfig, _params.Cutoff, _params.AutoCutoff, dummyCallback);
            
            if (_params.AutoCutoff)
                Console.WriteLine($"✅ Loaded config with auto-cutoff (starting at {_params.Cutoff})");
            else
                Console.WriteLine($"✅ Loaded config with cutoff: {_params.Cutoff}");
                
            // Use specialized filter system
            var mustClauses = config.Must?.ToList() ?? new List<MotelyJsonConfig.MotleyJsonFilterClause>();
            var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(mustClauses);
            
            if (clausesByCategory.Count == 0)
                throw new Exception("No valid MUST clauses found for filtering");
            
            // Boss filter now works with proper ante-loop structure
            
            // Create base filter with first category
            var categories = clausesByCategory.Keys.ToList();
            var primaryCategory = categories[0]; 
            var primaryClauses = clausesByCategory[primaryCategory];
            
            // Create specialized filter based on category
            IMotelySeedFilterDesc filterDesc = primaryCategory switch
            {
                FilterCategory.SoulJoker => new MotelyJsonSoulJokerFilterDesc(primaryClauses),
                FilterCategory.Joker => new MotelyJsonJokerFilterDesc(primaryClauses),
                FilterCategory.Voucher => new MotelyJsonVoucherFilterDesc(primaryClauses),
                FilterCategory.PlanetCard => new MotelyJsonPlanetFilterDesc(primaryClauses),
                FilterCategory.TarotCard => new MotelyJsonTarotCardFilterDesc(primaryClauses),
                FilterCategory.SpectralCard => new MotelyJsonSpectralCardFilterDesc(primaryClauses),
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
                var category = categories[i];
                var clauses = clausesByCategory[category];
                var additionalFilter = category switch
                {
                    FilterCategory.SoulJoker => (IMotelySeedFilterDesc)new MotelyJsonSoulJokerFilterDesc(clauses),
                    FilterCategory.Joker => (IMotelySeedFilterDesc)new MotelyJsonJokerFilterDesc(clauses),
                    FilterCategory.Voucher => (IMotelySeedFilterDesc)new MotelyJsonVoucherFilterDesc(clauses),
                    FilterCategory.PlanetCard => (IMotelySeedFilterDesc)new MotelyJsonPlanetFilterDesc(clauses),
                    FilterCategory.TarotCard => (IMotelySeedFilterDesc)new MotelyJsonTarotCardFilterDesc(clauses),
                    FilterCategory.SpectralCard => (IMotelySeedFilterDesc)new MotelyJsonSpectralCardFilterDesc(clauses),
                    FilterCategory.PlayingCard => (IMotelySeedFilterDesc)new MotelyJsonPlayingCardFilterDesc(clauses),
                    FilterCategory.Boss => (IMotelySeedFilterDesc)new MotelyJsonBossFilterDesc(clauses), // RE-ENABLED
                    FilterCategory.Tag => (IMotelySeedFilterDesc)new MotelyJsonTagFilterDesc(clauses),
                    _ => throw new ArgumentException($"Additional filter not implemented: {category}")
                };
                searchSettings.WithAdditionalFilter(additionalFilter);
                Console.WriteLine($"   + Chained {category} filter: {clauses.Count} clauses");
            }
            
            // Add scoring
            searchSettings = searchSettings.WithSeedScoreProvider(scoreDesc);
            
            // Apply deck and stake
            if (!string.IsNullOrEmpty(config.Deck) && Enum.TryParse<MotelyDeck>(config.Deck, true, out var deck))
                searchSettings = searchSettings.WithDeck(deck);
            if (!string.IsNullOrEmpty(config.Stake) && Enum.TryParse<MotelyStake>(config.Stake, true, out var stake))
                searchSettings = searchSettings.WithStake(stake);
                
            // Set batch configuration
            searchSettings = searchSettings.WithThreadCount(_params.Threads);
            searchSettings = searchSettings.WithBatchCharacterCount(_params.BatchSize);
            searchSettings = searchSettings.WithStartBatchIndex((long)_params.StartBatch);
            if (_params.EndBatch > 0)
                searchSettings = searchSettings.WithEndBatchIndex((long)_params.EndBatch);
                
            // Start search
            if (seeds != null && seeds.Count > 0)
                return searchSettings.WithListSearch(seeds).Start();
            else
                return searchSettings.WithSequentialSearch().Start();
        }
        
        private void PrintResultsHeader(MotelyJsonConfig config)
        {
            Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");
            var header = "Seed,TotalScore";
            if (config.Should != null)
            {
                foreach (var should in config.Should)
                {
                    var name = !string.IsNullOrEmpty(should.Value) ? should.Value : should.Type;
                    header += $",{name}";
                }
            }
            Console.WriteLine(header);
        }

        private void PrintResultsSummary(IMotelySearch search)
        {
            Console.WriteLine(_cancelled ? "\n✅ Search stopped gracefully" : "\n✅ Search completed");
            
            long lastBatchIndex = search.CompletedBatchCount > 0 ? (long)_params.StartBatch + search.CompletedBatchCount : 0;
            
            Console.WriteLine($"   Last batch Index: {lastBatchIndex}");
            Console.WriteLine($"   Seeds searched: {search.TotalSeedsSearched:N0}");
            Console.WriteLine($"   Seeds matched: {search.MatchingSeeds:N0}");
            
            var elapsed = search.ElapsedTime;
            if (elapsed.TotalMilliseconds > 100)
            {
                Console.WriteLine($"   Duration: {elapsed:hh\\:mm\\:ss\\.fff}");
                var speed = elapsed.TotalMilliseconds > 0 ? search.TotalSeedsSearched / elapsed.TotalMilliseconds : 0;
                Console.WriteLine($"   Speed: {speed:N0} seeds/ms");
            }
        }
    }

    public record JsonSearchParams
    {
        public string Config { get; set; } = "standard";
        public int Threads { get; set; } = Environment.ProcessorCount;
        public int BatchSize { get; set; } = 1;
        public ulong StartBatch { get; set; } = 0;
        public ulong EndBatch { get; set; } = 0;
        public int Cutoff { get; set; } = 0;
        public bool AutoCutoff { get; set; } = false;
        public bool EnableDebug { get; set; } = false;
        public bool NoFancy { get; set; } = false;
        public bool Silent { get; set; } = false;
        public string? SpecificSeed { get; set; }
        public string? WordList { get; set; }
        public string? Wordlist { get; set; } // Compatibility
        public string? CsvScore { get; set; } // Compatibility
        public string? Keyword { get; set; } // Compatibility
        public bool ScoreOnly { get; set; } = false; // Compatibility
    }
}