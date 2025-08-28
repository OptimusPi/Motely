using System;
using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely
{
    partial class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "Motely",
                Description = "Motely - Balatro Seed Searcher",
                OptionsComparison = StringComparison.OrdinalIgnoreCase
            };

            app.HelpOption("-?|-h|--help");

            // Core options
            var jsonOption = app.Option<string>("-j|--json <JSON>", "JSON config file (JsonItemFilters/)", CommandOptionType.SingleValue);
            var analyzeOption = app.Option<string>("--analyze <SEED>", "Analyze a specific seed", CommandOptionType.SingleValue);
            var nativeOption = app.Option<string>("-n|--native <FILTER>", "Run built-in native filter", CommandOptionType.SingleValue);
            var chainOption = app.Option<string>("--chain <FILTERS>", "Chain additional filters (comma-separated)", CommandOptionType.SingleValue);
            var scoreOption = app.Option<string>("--score <JSON>", "Add JSON scoring to native filter", CommandOptionType.SingleValue);
            var csvScoreOption = app.Option<string>("--csvScore <TYPE>", "Enable CSV scoring output (native for built-in)", CommandOptionType.SingleValue);
            
            // Search parameters
            var threadsOption = app.Option<int>("--threads <COUNT>", "Number of threads", CommandOptionType.SingleValue);
            var batchSizeOption = app.Option<int>("--batchSize <CHARS>", "Batch size", CommandOptionType.SingleValue);
            var startBatchOption = app.Option<long>("--startBatch <INDEX>", "Starting batch", CommandOptionType.SingleValue);
            var endBatchOption = app.Option<long>("--endBatch <INDEX>", "Ending batch", CommandOptionType.SingleValue);
            
            // Input options
            var seedOption = app.Option<string>("--seed <SEED>", "Specific seed", CommandOptionType.SingleValue);
            var wordlistOption = app.Option<string>("--wordlist <WL>", "Wordlist file", CommandOptionType.SingleValue);
            var keywordOption = app.Option<string>("--keyword <KEYWORD>", "Generate from keyword", CommandOptionType.SingleValue);
            
            // Game options
            var deckOption = app.Option<string>("--deck <DECK>", "Deck to use", CommandOptionType.SingleValue);
            var stakeOption = app.Option<string>("--stake <STAKE>", "Stake to use", CommandOptionType.SingleValue);
            
            // JSON specific
            var cutoffOption = app.Option<string>("--cutoff <SCORE>", "Min score threshold", CommandOptionType.SingleValue);
            var scoreOnlyOption = app.Option("--scoreOnly", "Score only mode", CommandOptionType.NoValue);
            
            // Output options
            var debugOption = app.Option("--debug", "Enable debug output", CommandOptionType.NoValue);
            var noFancyOption = app.Option("--nofancy", "Suppress fancy output", CommandOptionType.NoValue);
            var silentOption = app.Option("--silent", "Skip console output for matching seeds", CommandOptionType.NoValue);

            // Set defaults
            jsonOption.DefaultValue = "standard";
            threadsOption.DefaultValue = Environment.ProcessorCount;
            batchSizeOption.DefaultValue = 1;
            startBatchOption.DefaultValue = 0;
            endBatchOption.DefaultValue = 0;
            cutoffOption.DefaultValue = "0";
            deckOption.DefaultValue = "Red";
            stakeOption.DefaultValue = "White";

            app.OnExecute(() =>
            {
                // Analyze mode takes priority
                var analyzeSeed = analyzeOption.Value();
                if (!string.IsNullOrEmpty(analyzeSeed))
                {
                    return ExecuteAnalyze(analyzeSeed, deckOption.Value()!, stakeOption.Value()!);
                }

                // Build common parameters
                var parameters = new SearchParameters
                {
                    Threads = threadsOption.ParsedValue,
                    BatchSize = batchSizeOption.ParsedValue,
                    StartBatch = (long)startBatchOption.ParsedValue,
                    EndBatch = endBatchOption.ParsedValue,
                    EnableDebug = debugOption.HasValue(),
                    NoFancy = noFancyOption.HasValue(),
                    Silent = silentOption.HasValue(),
                    SpecificSeed = seedOption.Value(),
                    Wordlist = wordlistOption.Value(),
                    Keyword = keywordOption.Value(),
                    CsvScore = csvScoreOption.Value()
                };

                // Validate batch size
                if (parameters.BatchSize < 1 || parameters.BatchSize > 8)
                {
                    Console.WriteLine($"❌ Error: batchSize must be between 1 and 8 (got {parameters.BatchSize})");
                    Console.WriteLine($"   batchSize represents the number of seed digits to process in parallel.");
                    Console.WriteLine($"   Valid range: 1-8 (Balatro seeds are 1-8 characters)");
                    Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                    return 1;
                }

                // Validate batch ranges
                long maxBatches = (long)Math.Pow(35, 8 - parameters.BatchSize);
                if (parameters.EndBatch > maxBatches)
                {
                    Console.WriteLine($"❌ endBatch too large: {parameters.EndBatch} (max for batchSize {parameters.BatchSize}: {maxBatches:N0})");
                    return 1;
                }
                if (parameters.StartBatch >= parameters.EndBatch && parameters.EndBatch != 0)
                {
                    Console.WriteLine($"❌ startBatch ({parameters.StartBatch}) must be less than endBatch ({parameters.EndBatch})");
                    return 1;
                }

                // Check which mode to run
                var nativeFilter = nativeOption.Value();
                if (!string.IsNullOrEmpty(nativeFilter))
                {
                    // Native filter mode
                    var chainFilters = chainOption.Value();
                    var scoreConfig = scoreOption.Value();
                    
                    // Parse cutoff for native filters with scoring or CSV scoring
                    if (!string.IsNullOrEmpty(scoreConfig) || !string.IsNullOrEmpty(parameters.CsvScore))
                    {
                        var cutoffStr = cutoffOption.Value() ?? "0";
                        parameters.AutoCutoff = cutoffStr.ToLowerInvariant() == "auto";
                        parameters.Cutoff = parameters.AutoCutoff ? 1 : (int.TryParse(cutoffStr, out var c) ? c : 0);
                    }
                    
                    var executor = new NativeFilterExecutor(nativeFilter, parameters, chainFilters, scoreConfig);
                    return executor.Execute();
                }
                else
                {
                    // JSON config mode
                    var cutoffStr = cutoffOption.Value() ?? "0";
                    bool autoCutoff = cutoffStr.ToLowerInvariant() == "auto";
                    parameters.Cutoff = autoCutoff ? 0 : (int.TryParse(cutoffStr, out var c) ? c : 0);
                    parameters.AutoCutoff = autoCutoff;
                    parameters.ScoreOnly = scoreOnlyOption.HasValue();
                    
                    var executor = new JsonSearchExecutor(jsonOption.Value()!, parameters);
                    return executor.Execute();
                }
            });

            return app.Execute(args);
        }

        private static int ExecuteAnalyze(string seed, string deckName, string stakeName)
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

            MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, deck, stake));
            return 0;
        }

        // Keep this helper function since it's used by JsonSearchExecutor
        public static MotelySearchSettings<MotelyJsonFilterDesc.MotelyFilter> CreateSliceChainedSearch(MotelyJsonConfig config, int threads, int batchSize, bool scoreOnly = false)
        {
            MotelySearchSettings<MotelyJsonFilterDesc.MotelyFilter>? searchSettings = null;
            
            // Simple approach - just pass all must clauses to FilterMixed
            var mustClauses = scoreOnly ? new List<MotelyJsonConfig.MotleyJsonFilterClause>() : config.Must.ToList();
            
            searchSettings = new MotelySearchSettings<MotelyJsonFilterDesc.MotelyFilter>(
                new MotelyJsonFilterDesc(FilterCategory.Mixed, mustClauses))
                .WithThreadCount(threads)
                .WithBatchCharacterCount(batchSize);
            
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
    }
}