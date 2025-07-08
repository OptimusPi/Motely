using System.Diagnostics;
using McMaster.Extensions.CommandLineUtils;
using Motely;
using Motely.Filters;

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
        batchSizeOption.DefaultValue = 3;

        var cutoffOption = app.Option<int>(
            "--cutoff <SCORE>",
            "Minimum TotalScore threshold for results (0 for no cutoff)",
            CommandOptionType.SingleValue);
        cutoffOption.DefaultValue = 0;

        var debugOption = app.Option(
            "--debug",
            "Enable debug output messages",
            CommandOptionType.NoValue);

        app.OnExecute(() =>
        {
            var configName = configOption.Value()!;
            var startBatch = startBatchOption.ParsedValue;
            var endBatch = endBatchOption.ParsedValue;
            var threads = threadsOption.ParsedValue;
            var batchSize = batchSizeOption.ParsedValue;
            var cutoff = cutoffOption.ParsedValue;
            var enableDebug = debugOption.HasValue();

            // Validate batchSize to prevent stack overflow
            if (batchSize < 1 || batchSize > 8)
            {
                Console.WriteLine($"❌ Error: batchSize must be between 1 and 8 (got {batchSize})");
                Console.WriteLine($"   batchSize represents the number of seed digits to process in parallel.");
                Console.WriteLine($"   Valid range: 1-8 (Balatro seeds are 1-8 characters)");
                Console.WriteLine($"   Recommended: 2-4 for optimal performance");
                return 1;
            }

            RunOuijaSearch(configName, startBatch, endBatch, threads, batchSize, cutoff, enableDebug);
            return 0;
        });

        return app.Execute(args);
    }

    static void RunOuijaSearch(string configPath, int startBatch, int endBatch, int threads, int batchSize, int cutoff, bool enableDebug)
    {
        // Set debug output flag
        DebugLogger.IsEnabled = enableDebug;

        Console.WriteLine($"🔍 Motely Ouija Search Starting");
        Console.WriteLine($"   Config: {configPath}");
        Console.WriteLine($"   Threads: {threads}");
        Console.WriteLine($"   Batch Size: {batchSize} chars");
        Console.WriteLine($"   Range: {startBatch} to {endBatch}");
        if (enableDebug)
            Console.WriteLine($"   Debug: Enabled");
        Console.WriteLine();

        try
        {
            // Load Ouija config - try multiple paths
            var config = LoadConfig(configPath);
            Console.WriteLine($"✅ Loaded config: {config.Needs?.Length ?? 0} needs, {config.Wants?.Length ?? 0} wants");

            // Print the parsed config for debugging
            Console.WriteLine("\n--- Parsed Ouija Config ---");
            try {
                var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
            } catch (Exception ex) {
                Console.WriteLine($"[DEBUG] Could not serialize config: {ex.Message}");
                Console.WriteLine(config.ToString());
            }
            Console.WriteLine("--- End Config ---\n");

            // Print CSV header for results
            PrintResultsHeader(config);

            var sw = Stopwatch.StartNew();

            // Create and run the search
            using var search = new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(new OuijaJsonFilterDesc(config))
                .WithThreadCount(threads)
                .WithBatchCharacterCount(batchSize)
                .WithStartBatchIndex(startBatch)
                .Start();

            // Process results as they come in
            int resultCount = 0;
            int totalResultsProcessed = 0;
            while (search.Status is MotelySearchStatus.Running or MotelySearchStatus.Paused)
            {
                // Process results in the queue
                while (search.Results.TryDequeue(out var result))
                {
                    totalResultsProcessed++;
                    if (result.Success && result.TotalScore >= cutoff)
                    {
                        PrintResult(result, config);
                        resultCount++;
                    }
                    Thread.Sleep(1);
                }

                // Sleep briefly to avoid busy-waiting
                Thread.Sleep(1);
            }
            {
                while (search.Results.TryDequeue(out var result))
                {
                    totalResultsProcessed++;
                    if (result.Success && result.TotalScore >= cutoff)
                    {
                        PrintResult(result, config);
                        resultCount++;
                    }
                    Thread.Sleep(1);
                }

                Thread.Sleep(1);
            }

            sw.Stop();
            Console.WriteLine();

            // Flush any remaining debug messages
            DebugLogger.ForceFlush();
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
            return OuijaConfig.Load(configPath, OuijaConfig.GetOptions());
        }

        // Always look in JsonItemFilters for configs
        string fileName = configPath.EndsWith(".ouija.json") ? configPath : configPath + ".ouija.json";
        string jsonItemFiltersPath = Path.Combine("JsonItemFilters", fileName);
        if (File.Exists(jsonItemFiltersPath))
        {
            Console.WriteLine($"📁 Loading config from: {jsonItemFiltersPath}");
            return OuijaConfig.Load(jsonItemFiltersPath, OuijaConfig.GetOptions());
        }

        throw new FileNotFoundException($"Could not find config file: {configPath}");
    }

    static void PrintResultsHeader(OuijaConfig config)
    {
        var header = "Seed,TotalScore";

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

    static void PrintResult(MotelySearchResult result, OuijaConfig config)
    {
        var row = $"{result.Seed},{result.TotalScore}";

        if (config.ScoreNaturalNegatives)
            row += $",{result.NaturalNegativeJokers}";
        if (config.ScoreDesiredNegatives)
            row += $",{result.DesiredNegativeJokers}";

        // Add scores for each want
        if (config.Wants != null && result.ScoreWants != null)
        {
            for (int i = 0; i < Math.Min(result.ScoreWants.Length, config.Wants.Length); i++)
            {
                row += $",{result.ScoreWants[i]}";
            }
        }

        // TODO - add custom columns that some filters may define

        Console.WriteLine(row);
    }
}

// IMotelySearch search = new MotelySearchSettings<LuckyCardFilterDesc.LuckyCardFilter>(new LuckyCardFilterDesc())
// IMotelySearch search = new MotelySearchSettings<ShuffleFinderFilterDesc.ShuffleFinderFilter>(new ShuffleFinderFilterDesc())
//IMotelySearch search = new MotelySearchSettings<PerkeoObservatoryFilterDesc.PerkeoObservatoryFilter>(new PerkeoObservatoryFilterDesc())
    // await new MotelySearchSettings<NegativeTagFilterDesc.NegativeTagFilter>(new NegativeTagFilterDesc())
    // .WithThreadCount(Environment.ProcessorCount - 2)
    // .WithThreadCount(1)
    // .WithStartBatchIndex(41428)

    // .WithListSearch(["811M2111"])
    // .WithProviderSearch(new MotelyRandomSeedProvider(2000000000))
    //.Start();

