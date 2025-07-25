using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
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
                "-c|--config <CONFIG>",
                "Config file (looks in JsonItemFilters/ directory, add .ouija.json if not present)",
                CommandOptionType.SingleValue);
            configOption.DefaultValue = "standard";

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
                var seedInput = seedOption.Value()!;

                // Validate batchSize
                if (batchSize < 1 || batchSize > 8)
                {
                    Console.WriteLine($"❌ Error: batchSize must be between 1 and 8 (got {batchSize})");
                    Console.WriteLine($"   batchSize represents the number of seed digits to process in parallel.");
                    Console.WriteLine($"   Valid range: 1-8 (Balatro seeds are 1-8 characters)");
                    Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                    return 1;
                }

                RunOuijaSearch(configName, startBatch, endBatch, threads, batchSize, cutoff,
                    enableDebug, quiet, wordlist, keyword, nofancy, seedInput);
                Console.WriteLine("🔍 Search completed");
                return 0;
            });

            return app.Execute(args);
        }

        public static void RunOuijaSearch(string configPath, int startBatch, int endBatch, int threads,
            int batchSize, int cutoff, bool enableDebug, bool quiet, string? wordlist = null,
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
                // Load Ouija config
                var config = LoadConfig(configPath);
                if (!quiet)
                    Console.WriteLine($"✅ Loaded config: {config.Needs?.Length ?? 0} needs, {config.Wants?.Length ?? 0} wants");

                // Print the parsed config for debugging
                if (!quiet && enableDebug)
                {
                    Console.WriteLine("\n--- Parsed Ouija Config ---");
                    Console.WriteLine(config.ToJson());
                    Console.WriteLine("--- End Config ---\n");
                }

                // Create search configuration
                var searchConfig = new SearchConfiguration
                {
                    ThreadCount = threads,
                    BatchSize = batchSize,
                    StartBatchIndex = startBatch,
                    MinimumScore = cutoff,
                    Seeds = seeds
                };

                // Create search engine
                var engine = new MotelySearchEngine(searchConfig);

                // Print CSV header for results
                PrintResultsHeader(config);

                // Keep track of last progress update for console output
                var lastProgressTime = Stopwatch.StartNew();
                SearchProgress? lastProgress = null;

                // Setup cancellation token
                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                // Run search
                var progress = new Progress<SearchProgress>(p =>
                {
                    lastProgress = p;
                    if (!quiet && lastProgressTime.ElapsedMilliseconds > 500)
                    {
                        lastProgressTime.Restart();
                        PrintProgress(p);
                    }
                });

                var results = engine.SearchSync(config, progress, cts.Token);

                // Print results
                foreach (var result in results.Results)
                {
                    PrintResult(config, result);
                }

                if (!quiet)
                {
                    Console.WriteLine($"\n✅ Search completed");
                    Console.WriteLine($"   Total seeds searched: {results.TotalSearched:N0}");
                    Console.WriteLine($"   Results found: {results.Results.Count}");
                    Console.WriteLine($"   Duration: {results.Duration:hh\\:mm\\:ss}");
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
                    Console.WriteLine(ex.StackTrace);
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

        static void PrintProgress(SearchProgress progress)
        {
            var timeLeft = progress.EstimatedTimeRemaining;
            string timeLeftFormatted = timeLeft.Days == 0 ?
                $"{timeLeft:hh\\:mm\\:ss}" :
                $"{timeLeft:d\\:hh\\:mm\\:ss}";

            string seedsPerSec = progress.SeedsPerSecond > 0 ?
                $"{progress.SeedsPerSecond:N0}" : "--";

            OuijaStyleConsole.SetBottomLine(
                $"{progress.PercentComplete:F2}% ~{timeLeftFormatted} remaining ({seedsPerSec} seeds/s) | {progress.SeedsSearched:N0} seeds searched");
        }

        static void PrintResult(OuijaConfig config, SearchResult result)
        {
            var line = $"{result.Seed},{result.TotalScore}";

            if (config.ScoreNaturalNegatives)
                line += $",{result.NaturalNegatives}";
            if (config.ScoreDesiredNegatives)
                line += $",{result.DesiredNegatives}";

            // Add want scores
            if (config.Wants != null)
            {
                for (int i = 0; i < config.Wants.Length && i < result.WantScores.Length; i++)
                {
                    line += $",{result.WantScores[i]}";
                }
            }

            Console.WriteLine(line);
        }

        static string FormatWantColumn(OuijaConfig.Desire want)
        {
            if (want == null) return "Want";
            return want.GetDisplayString();
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