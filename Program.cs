using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using Motely;
using Motely.Filters;
using static Motely.PerkeoObservatoryFilterDesc;
using static Motely.OuijaJsonFilterDesc;

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

        // .WithListSearch(["811M2111"])

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

    static void RunOuijaSearch(string configPath, int startBatch, int endBatch, int threads, int batchSize, int cutoff, bool enableDebug, bool quiet, string? wordlist = null, string? keyword = null, bool nofancy = false)
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
            //var search = new MotelySearchSettings<PerkeoObservatoryFilter>(new PerkeoObservatoryFilterDesc())
            // uncomment for OuijaFilter
            var ouijaDesc = new OuijaJsonFilterDesc(config) { Cutoff = cutoff };
            var searchSettings = new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(ouijaDesc)
                .WithThreadCount(threads)
                .WithQuiet(quiet);
            if (seeds != null)
                searchSettings = searchSettings.WithListSearch(seeds);
            else
                searchSettings = searchSettings.WithSequentialSearch();
            var search = searchSettings.Start();
            if (!quiet)
                Console.WriteLine($"✅ Search started successfully");

            // Print CSV header for results
            if (!quiet)
                PrintResultsHeader(config);

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
        var header = "+Seed,TotalScore";

        if (config.ScoreNaturalNegatives)
            header += ",NaturalNegatives";
        if (config.ScoreDesiredNegatives)
            header += ",DesiredNegatives";

        // Add column for each want
        if (config.Wants != null)
        {
            for (int i = 0; i < config.Wants.Length; i++)
            {
                var want = config.Wants[i];
                var col = FormatWantColumn(want);
                header += $",{col}";
            }
        }

        Console.WriteLine(header);
    }

    static string FormatWantColumn(OuijaConfig.Desire want)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(want.Edition) && want.Edition != "None")
            parts.Add(want.Edition);
        if (!string.IsNullOrEmpty(want.Value))
            parts.Add(want.Value);
        if (!string.IsNullOrEmpty(want.Rank))
            parts.Add(want.Rank);
        if (!string.IsNullOrEmpty(want.Suit))
            parts.Add(want.Suit);
        if (want.JokerStickers?.Count > 0)
            parts.AddRange(want.JokerStickers);

        return string.Join("_", parts);
    }

    static void PrintOuijaConfigDebug(OuijaConfig config)
    {
        if (config == null)
        {
            Console.WriteLine("<null config>");
            return;
        }
        Console.WriteLine($"NumNeeds: {config.Needs?.Length ?? 0}, NumWants: {config.Wants?.Length ?? 0}");
        if (config.Needs != null && config.Needs.Length > 0)
        {
            Console.WriteLine("Needs:");
            foreach (var need in config.Needs)
            {
                if (need == null) continue;
                Console.WriteLine($"  - Type: {need.Type}, Value: '{need.Value}', JokerEnum: {need.JokerEnum?.ToString() ?? "<MISSING_ENUM>"}, PlanetEnum: {need.PlanetEnum?.ToString() ?? "<MISSING_ENUM>"}, SpectralEnum: {need.SpectralEnum?.ToString() ?? "<MISSING_ENUM>"}, VoucherEnum: {need.VoucherEnum?.ToString() ?? "<MISSING_ENUM>"}, TagEnum: {need.TagEnum?.ToString() ?? "<MISSING_ENUM>"}, TarotEnum: {need.TarotEnum?.ToString() ?? "<MISSING_ENUM>"}, Edition: {need.Edition}, Score: {need.Score}, DesireByAnte: {need.DesireByAnte}, SearchAntes: [{string.Join(",", need.SearchAntes ?? new int[0])}] ");
            }
        }
        if (config.Wants != null && config.Wants.Length > 0)
        {
            Console.WriteLine("Wants:");
            foreach (var want in config.Wants)
            {
                if (want == null) continue;
                Console.WriteLine($"  - Type: {want.Type}, Value: '{want.Value}', JokerEnum: {want.JokerEnum?.ToString() ?? "<MISSING_ENUM>"}, PlanetEnum: {want.PlanetEnum?.ToString() ?? "<MISSING_ENUM>"}, SpectralEnum: {want.SpectralEnum?.ToString() ?? "<MISSING_ENUM>"}, VoucherEnum: {want.VoucherEnum?.ToString() ?? "<MISSING_ENUM>"}, TagEnum: {want.TagEnum?.ToString() ?? "<MISSING_ENUM>"}, TarotEnum: {want.TarotEnum?.ToString() ?? "<MISSING_ENUM>"}, Edition: {want.Edition}, Score: {want.Score}, DesireByAnte: {want.DesireByAnte}, SearchAntes: [{string.Join(",", want.SearchAntes ?? new int[0])}] ");
            }
        }
    }
}

//IMotelySearch search = new MotelySearchSettings<LuckyCardFilterDesc.LuckyCardFilter>(new LuckyCardFilterDesc())
//IMotelySearch search = new MotelySearchSettings<ShuffleFinderFilterDesc.ShuffleFinderFilter>(new ShuffleFinderFilterDesc())
//IMotelySearch search = new MotelySearchSettings<NegativeTagFilterDesc.NegativeTagFilter>(new NegativeTagFilterDesc())
// IMotelySearch search = new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(new OuijaJsonFilterDesc(config: OuijaConfig.Load("JsonItemFilters/test.ouija.json", OuijaConfig.GetOptions())))
//     .WithThreadCount(Environment.ProcessorCount - 1)
//     .WithListSearch([
// "D9FTADTT",
// "D9FTBIZH",
// "D9FTFEG1",
// "D9FTVQHE",
// "D9FU3U3O",
// "D9FU3XYT",
// "D9FU84RQ",
// "D9FUDGOU",
// "D9FUIT2N",
// "D9FUJ7VX",
// "D9FUMBOF",
// "D9FUOF64",
// "D9FUVAQ1",
// "D9FVA9AO",
// "D9FVF61C",
// "D9FTJ4VJ"])
    // .WithProviderSearch(new MotelyRandomSeedProvider(2000000000))
    //.WithSequentialSearch()
    //.Start();
//IMotelySearch search = new MotelySearchSettings<LuckyCardFilterDesc.LuckyCardFilter>(new LuckyCardFilterDesc())
// IMotelySearch search = new MotelySearchSettings<ShuffleFinderFilterDesc.ShuffleFinderFilter>(new ShuffleFinderFilterDesc())
// IMotelySearch search = new MotelySearchSettings<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>(new PerkeoObservatoryFilterDesc())
// IMotelySearch search = new MotelySearchSettings<NegativeTagFilterDesc.NegativeTagFilter>(new NegativeTagFilterDesc())
    //.WithThreadCount(Environment.ProcessorCount - 2)
    // .WithThreadCount(1)
    // .WithStartBatchIndex(41428)

    // .WithListSearch(["ES6B2111"])
    // .WithProviderSearch(new MotelyRandomSeedProvider(2000000000))
    // .WithAdditionalFilter(new LuckyCardFilterDesc())
    //.WithAdditionalFilter(new PerkeoObservatoryFilterDesc())
    //.Start();

// var search = new MotelySearchSettings<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>(new PerkeoObservatoryFilterDesc())
//                 .WithProviderSearch(new MotelyRandomSeedProvider(2100000000))
//                 .WithThreadCount(16)
//                 .WithSequentialSearch()
//                 .WithStartBatchIndex(0)
//                 .Start();
