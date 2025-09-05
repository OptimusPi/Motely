using System;
using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using Motely.Utils;

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
                var parameters = new JsonSearchParams
                {
                    Threads = threadsOption.ParsedValue,
                    BatchSize = batchSizeOption.ParsedValue,
                    StartBatch = (ulong)startBatchOption.ParsedValue,
                    EndBatch = (ulong)endBatchOption.ParsedValue,
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
                if ((long)parameters.EndBatch > maxBatches)
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

        // TODO: Fix this method after specialized filters are complete
        /*
        public static MotelySearchSettings<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter> CreateSliceChainedSearch(MotelyJsonConfig config, int threads, int batchSize, bool scoreOnly = false)
        {
            MotelySearchSettings<MotelyJsonSoulJokerFilterDesc.MotelyJsonSoulJokerFilter>? searchSettings = null;
            
            // MUST clauses go to FILTER, not scoring - they MUST pass to continue
            var mustClauses = config.Must?.ToList() ?? new List<MotelyJsonConfig.MotleyJsonFilterClause>();
            
            // CreateSliceChainedSearch: Processing must clauses
            
            // PROPER SLICING: Group clauses by category for optimal vectorization
            var clausesByCategory = FilterCategoryMapper.GroupClausesByCategory(mustClauses);
            
            if (clausesByCategory.Count == 0)
            {
                // No must clauses - create empty filter
                searchSettings = new MotelySearchSettings<MotelyJsonFilterDesc.MotelyJsonFilter>(
                    new MotelyJsonFilterDesc(FilterCategory.Joker, new List<MotelyJsonConfig.MotleyJsonFilterClause>()))
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchSize);
                Console.WriteLine("   + No must clauses - using passthrough filter");
            }
            else if (clausesByCategory.Count == 1)
            {
                // Single category - use as base filter
                var (category, clauses) = clausesByCategory.First();
                searchSettings = new MotelySearchSettings<MotelyJsonFilterDesc.MotelyJsonFilter>(
                    new MotelyJsonFilterDesc(category, clauses))
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchSize);
                Console.WriteLine($"   + Single {category} filter: {clauses.Count} clauses");
            }
            else
            {
                // Multiple categories - use proper slicing with chaining
                var categories = clausesByCategory.ToList();
                var (baseCategory, baseClauses) = categories[0];
                
                searchSettings = new MotelySearchSettings<MotelyJsonFilterDesc.MotelyJsonFilter>(
                    new MotelyJsonFilterDesc(baseCategory, baseClauses))
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchSize);
                Console.WriteLine($"   + Base {baseCategory} filter: {baseClauses.Count} clauses");
                
                for (int i = 1; i < categories.Count; i++)
                {
                    var (category, clauses) = categories[i];
                    var chainedFilter = new MotelyJsonFilterDesc(category, clauses);
                    searchSettings = searchSettings.WithAdditionalFilter(chainedFilter);
                    Console.WriteLine($"   + Chained {category} filter: {clauses.Count} clauses");
                }
            }
            
            Console.WriteLine($"   + Filter setup complete: {mustClauses.Count} total clauses");
            return searchSettings!;
        }
        */
    }
}