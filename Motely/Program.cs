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

        app.OnExecute(() =>
        {
            var configName = configOption.Value()!;
            var startBatch = startBatchOption.ParsedValue;
            var endBatch = endBatchOption.ParsedValue;
            var threads = threadsOption.ParsedValue;
            var batchSize = batchSizeOption.ParsedValue;

            RunOuijaSearch(configName, startBatch, endBatch, threads, batchSize);
            return 0;
        });

        return app.Execute(args);
    }

    static void RunOuijaSearch(string configPath, int startBatch, int endBatch, int threads, int batchSize)
    {
        Console.WriteLine($"🔍 Motely Ouija Search Starting");
        Console.WriteLine($"   Config: {configPath}");
        Console.WriteLine($"   Threads: {threads}");
        Console.WriteLine($"   Batch Size: {batchSize} chars");
        Console.WriteLine($"   Range: {startBatch} to {endBatch}");
        Console.WriteLine();

        try
        {
            // Load Ouija config - try multiple paths
            var config = LoadConfig(configPath);
            Console.WriteLine($"✅ Loaded config: {config.Needs?.Length ?? 0} needs, {config.Wants?.Length ?? 0} wants");

            // Print CSV header for results
            PrintResultsHeader(config);

            var sw = Stopwatch.StartNew();

            // Create and run the search
            using var search = new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(new OuijaJsonFilterDesc(config))
                .WithThreadCount(threads)
                .WithBatchCharacterCount(batchSize)
                .WithStartBatchIndex(startBatch)
                .WithEndBatchIndex(endBatch)
                .Start();

            // Process results as they come in
            int resultCount = 0;
            while (!search.IsCompleted)
            {
                while (search.Results.TryDequeue(out var result))
                {
                    if (result.Success)
                    {
                        PrintResult(result, config);
                        resultCount++;
                    }
                }
                Thread.Sleep(100); // Don't hammer the CPU
            }

            // Get any remaining results
            while (search.Results.TryDequeue(out var result))
            {
                if (result.Success)
                {
                    PrintResult(result, config);
                    resultCount++;
                }
            }

            sw.Stop();
            Console.WriteLine();
            Console.WriteLine($"🎯 Search Complete! Found {resultCount} matching seeds in {sw.Elapsed.TotalSeconds:F2}s");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            return;
        }
    }

    static OuijaConfig LoadConfig(string configPath)
    {
        // Try multiple locations for the config file
        string[] attempts = [
            configPath,
            Path.Combine(".", configPath),
            Path.Combine("..", configPath),
            Path.Combine("../Ouija/ouija_configs", configPath),
            Path.Combine("Ouija", "ouija_configs", configPath),
            Path.Combine("ouija_configs", configPath),
            Path.Combine(".", "ouija_configs", configPath),
            configPath.EndsWith(".json") ? configPath : configPath + ".ouija.json"
        ];

        foreach (var path in attempts)
        {
            if (File.Exists(path))
            {
                Console.WriteLine($"📁 Loading config from: {path}");
                return OuijaConfig.Load(path, OuijaConfig.GetOptions());
            }
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

        Console.WriteLine(row);
    }
}
