using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using Motely;
using Motely.Filters;
using static Motely.PerkeoObservatoryFilterDesc;
using static Motely.OuijaJsonFilterDesc;

namespace Motely
{
    partial class Program
    {
        static int Main(string[] args)
        {
            
            var app = new CommandLineApplication
            {
                Name = "MotelySearch",
                Description = "Motely Ouija Search - Dynamic Balatro Seed Searcher"
            };

            app.HelpOption("-?|-h|--help");

            var configOption = app.Option<string>(
                "--config <PATH>",
                "Path to Ouija config JSON file",
                CommandOptionType.SingleValue);
            configOption.DefaultValue = "test.ouija.json";

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

            var seedInput = seedOption.Value()!;

            if (!string.IsNullOrEmpty(seedInput))
            {
                // If a seed is provided, run a single search for that seed
                Console.WriteLine($"🔍 Searching for specific seed: {seedInput}");
                RunOuijaSearch(configOption.Value()!, 0, 0, threadsOption.ParsedValue, batchSizeOption.ParsedValue, cutoffOption.ParsedValue, debugOption.HasValue(), quietOption.HasValue());
                return 0;
            }

            app.OnExecute(() =>
            {
                
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

                // Validate batchSize to prevent stack overflow
                if (batchSize < 1 || batchSize > 8)
                {
                    Console.WriteLine($"❌ Error: batchSize must be between 1 and 8 (got {batchSize})");
                    Console.WriteLine($"   batchSize represents the number of seed digits to process in parallel.");
                    Console.WriteLine($"   Valid range: 1-8 (Balatro seeds are 1-8 characters)");
                    Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                    return 1;
                }

                RunOuijaSearch(configName, startBatch, endBatch, threads, batchSize, cutoff, enableDebug, quiet, wordlist, keyword, nofancy);
                Console.WriteLine("🔍 Search completed");
                return 0;
            });

            return app.Execute(args);
        }

        public static void RunOuijaSearch(string configPath, int startBatch, int endBatch, int threads, int batchSize, int cutoff, bool enableDebug, bool quiet, string? wordlist = null, string? keyword = null, bool nofancy = false)
        {
            // Set debug output flag
            DebugLogger.IsEnabled = enableDebug;
            FancyConsole.IsEnabled = !nofancy;

            List<string>? seeds = null;
            
            // Handle keyword generation
            if (!string.IsNullOrEmpty(keyword))
            {
                seeds = SeedGenerator.GenerateSeedsFromKeyword(keyword);
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
                // Load Ouija config - try multiple paths
                var config = LoadConfig(configPath);
                if (!quiet)
                    Console.WriteLine($"✅ Loaded config: {config.Needs?.Length ?? 0} needs, {config.Wants?.Length ?? 0} wants");

                // Print the parsed config for debugging
                if (!quiet)
                {
                    Console.WriteLine("\n--- Parsed Ouija Config ---");
                    try
                    {
                        PrintOuijaConfigDebug(config);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[DEBUG] Could not pretty-print config: {ex.Message}");
                        Console.WriteLine(config.ToJson());
                    }
                    Console.WriteLine("--- End Config ---\n");
                }

                // Create and run the search
                if (!quiet)
                    Console.WriteLine($"🔍 Starting search with {threads} threads, batch size {batchSize}, starting at batch index {startBatch}, cutoff score {cutoff}");
                // uncomment for Perkeo Observatory filter
                //var searchSettings = new MotelySearchSettings<NegativeTagFilterDesc.NegativeTagFilter>(new NegativeTagFilterDesc())
                // uncomment for OuijaFilter
                var ouijaDesc = new OuijaJsonFilterDesc(config) { Cutoff = cutoff };
                var searchSettings = new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(ouijaDesc)
                    .WithSequentialSearch()
                    .WithBatchCharacterCount(4)
                    .WithThreadCount(threads)
                    .WithStartBatchIndex(startBatch)
                    .WithQuiet(quiet);
                if (seeds != null)
                    searchSettings = searchSettings.WithListSearch(seeds);
                else
                    searchSettings = searchSettings.WithSequentialSearch();

                if (!quiet)
                    Console.WriteLine($"✅ Search starting");

                // Print CSV header for results
                PrintResultsHeader(config);

                // Create search but don't start yet
                var search = new MotelySearch<OuijaJsonFilterDesc.OuijaJsonFilter>(searchSettings);

                // Activate Ouija reporting to suppress duplicate output
                search.ActivateOuijaReporting(OuijaJsonFilterDesc.GetWantsColumnNames(config));

                // Now start the search
                search.Start();
                
                // Flush any remaining debug messages
                DebugLogger.ForceFlush();

                while (search.Status == MotelySearchStatus.Running)
                {
                    // Wait for search to complete
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                DebugLogger.ForceFlush();
            }
        }

        static OuijaConfig LoadConfig(string configPath)
        {
            // If configPath is a rooted (absolute) path, use it directly
            if (Path.IsPathRooted(configPath) && File.Exists(configPath))
            {
                Console.WriteLine($"📁 Loading config from: {configPath}");
                return OuijaConfigLoader.Load(configPath);
            }

            // Always look in JsonItemFilters for configs
            string fileName = configPath.EndsWith(".ouija.json") ? configPath : configPath + ".ouija.json";
            string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
            if (File.Exists(jsonItemFiltersPath))
            {
                Console.WriteLine($"📁 Loading config from: {jsonItemFiltersPath}");
                return OuijaConfigLoader.Load(jsonItemFiltersPath);
            }

            throw new FileNotFoundException($"Could not find config file: {configPath}");
        }

        static void PrintResultsHeader(OuijaConfig config)
        {
            // Print deck/stake info as comments
            Console.WriteLine($"# Deck: {config.Deck}, Stake: {config.Stake}");
            Console.WriteLine($"# Max Ante: {config.MaxSearchAnte}");
            // Print CSV header only once, with + prefix
            var header = "+Seed,TotalScore";
            if (config.ScoreNaturalNegatives)
                header += ",NaturalNegatives";
            if (config.ScoreDesiredNegatives)
                header += ",DesiredNegatives";
            // Add column for each want
            if (config.Wants != null)
            {
                foreach (var want in config.Wants)
                {
                    var col = FormatWantColumn(want);
                    header += $",{col}";
                }
            }
            Console.WriteLine(header);
        }

        // Helper to pretty-print OuijaConfig for debug
        static void PrintOuijaConfigDebug(OuijaConfig config)
        {
            if (config == null)
            {
                Console.WriteLine("[DEBUG] Config is null");
                return;
            }
            Console.WriteLine(config.ToJson());
        }

        // Helper to format a want column for the CSV header
        static string FormatWantColumn(OuijaConfig.Desire want)
        {
            if (want == null) return "Want";
            return want.GetDisplayString();
        }
    }
}
