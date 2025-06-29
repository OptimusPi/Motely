using System.CommandLine;
using System.CommandLine.Parsing;
using Motely;
using Motely.Filters;
using System.Diagnostics;

class Program
{
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Motely Ouija Search");

        var configOption = new Option<string>("--config", () => "schema.ouija.json")
        {
            Description = "Path to Ouija config JSON"
        };
        var startBatchOption = new Option<int>("--startBatch", () => 0)
        {
            Description = "Starting batch index"
        };
        var endBatchOption = new Option<int>("--endBatch", () => 1000)
        {
            Description = "Ending batch index"
        };
        var startingSeedOption = new Option<string>("--startSeed", () => "WEE11111")
        {
            Description = "8-char starting seed"
        };
        var numSeedsOption = new Option<int>("--numSeeds", () => 1000)
        {
            Description = "Number of seeds to search"
        };

        rootCommand.Add(configOption);
        rootCommand.Add(startBatchOption);
        rootCommand.Add(endBatchOption);
        rootCommand.Add(startingSeedOption);
        rootCommand.Add(numSeedsOption);

        var parseResult = rootCommand.Parse(args);

        var configName = parseResult.GetValue(configOption);
        var startBatch = parseResult.GetValue(startBatchOption);
        var endBatch = parseResult.GetValue(endBatchOption);
        var startSeed = parseResult.GetValue(startingSeedOption);
        var numSeeds = parseResult.GetValue(numSeedsOption);

        RunMotely(configName!, startBatch, endBatch, startSeed!, numSeeds);

        return 0;
    }

    static void RunMotely(string configName, int batchStart, int batchCount, string startingSeedChar8, int numSeeds)
    {
        Console.WriteLine($"Ouija-Motely running with params: config={configName}, startSeed={startingSeedChar8}, numSeeds={numSeeds}, batchStart={batchStart}, batchCount={batchCount}");

        // Load Ouija config
        var config = OuijaConfig.Load($"Ouija/ouija_configs/{configName}", OuijaConfig.GetOptions());

        // Set up results array
        var results = new OuijaResult[numSeeds];
        for (int i = 0; i < numSeeds; i++)
            results[i] = new OuijaResult();

        // Print CSV header
        var header = "+Seed,Score";
        if (config.ScoreNaturalNegatives) header += ",NaturalNegatives";
        if (config.ScoreDesiredNegatives) header += ",DesiredNegatives";
        for (int w = 0; w < config.Wants.Length; w++)
        {
            var want = config.Wants[w];
            string col = "";
            if (want.JokerStickers != null && want.JokerStickers.Count > 0)
                col += string.Join(",", want.JokerStickers);
            if (!string.IsNullOrEmpty(want.Edition) && want.Edition != "None")
                col += $"{want.Edition}";
            if (!string.IsNullOrEmpty(want.Value))
                col += $"{want.Value}";
            if (!string.IsNullOrEmpty(want.Rank))
                col += $"{want.Rank}";
            if (!string.IsNullOrEmpty(want.Suit))
                col += $"{want.Suit}";
            header += "," + col;
        }
        Console.WriteLine(header);

        var sw = Stopwatch.StartNew();

        // Run the Ouija search
        new MotelySearchSettings<OuijaJsonFilterDesc.OuijaJsonFilter>(new OuijaJsonFilterDesc(config))
            .WithThreadCount(4)
            .Start();

        sw.Stop();

        // Print successful results as CSV
        for (int i = 0; i < numSeeds; i++)
        {
            var result = results[i];
            if (result != null && result.Success)
            {
                string seedStr = IncrementSeedString(startingSeedChar8, i);
                // Output only the first score for each want (if multiple antes, just print the first for now)
                var wantScores = string.Join(",", result.ScoreWants.Take(config.Wants.Length));
                var row = $"{seedStr},{result.TotalScore}";
                if (config.ScoreNaturalNegatives) row += $",{result.NaturalNegativeJokers}";
                if (config.ScoreDesiredNegatives) row += $",{result.DesiredNegativeJokers}";
                row += "," + wantScores;
                Console.WriteLine(row);
            }
        }
    }

    // Helper: Increment 8-char seed string (base36, like ouija-cli)
    static string IncrementSeedString(string baseSeed, int offset)
    {
        // Convert to base36 int, add offset, convert back
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        ulong value = 0;
        for (int i = 0; i < 8; i++)
        {
            value = value * 36 + (ulong)chars.IndexOf(char.ToUpperInvariant(baseSeed[i]));
        }
        value += (ulong)offset;
        char[] result = new char[8];
        for (int i = 7; i >= 0; i--)
        {
            result[i] = chars[(int)(value % 36)];
            value /= 36;
        }
        return new string(result);
    }
}
