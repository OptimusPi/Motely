using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Motely.Analysis;
using Motely.Enums;
using Motely.Filters.Jaml;

namespace Motely.MCP;

[McpServerToolType]
public static class MotelyTools
{
    // Numeric enums (default JsonSerializer behavior) are meaningless to an LLM reading tool
    // output — MotelyJamlyzerSeedResult is enum-heavy (Boss, Voucher, Tag, ...), so every
    // JsonSerializer call here needs string enum names instead. No WriteIndented: this text
    // goes through the MCP content wrapper, which re-escapes every space/newline as literal
    // "/\r\n bytes — indentation only adds cost here, an LLM reader gets nothing from it.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Same directory-resolution convention as Motely.CLI/Motely.HelperAPI: walk up
    /// from the running assembly until a JamlFilters/ folder appears, env var wins if set.</summary>
    private static string ResolveFiltersDir()
    {
        string? env = Environment.GetEnvironmentVariable("MOTELY_FILTERS_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "JamlFilters");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Path.GetFullPath("JamlFilters");
    }

    [McpServerTool(Name = "list_filters")]
    [Description("List available JAML filter files (Balatro seed-search filter configs) by name.")]
    public static string ListFilters()
    {
        string dir = ResolveFiltersDir();
        if (!Directory.Exists(dir))
            return JsonSerializer.Serialize(
                new { filtersDir = dir, filters = Array.Empty<string>() },
                JsonOptions
            );

        var names = Directory
            .EnumerateFiles(dir, "*.jaml")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(new { filtersDir = dir, filters = names }, JsonOptions);
    }

    [McpServerTool(Name = "analyze_seed")]
    [Description(
        "Analyze a single Balatro seed with the JAMLyzer engine: what boss, voucher, tags, "
            + "and shop items appear each ante, for the given deck and stake. Returns a "
            + "per-ante summary by default — pass includeRawStreams=true for the full raw "
            + "PRNG stream dump (shop-source queues, pull queues, resume state), which is "
            + "large and only useful for debugging the engine itself."
    )]
    public static string AnalyzeSeed(
        [Description("The 8-character Balatro seed (e.g. ALEEB123).")] string seed,
        [Description("Deck name, e.g. Red, Blue, Erratic. Default: Red.")] string deck = "Red",
        [Description("Stake name, e.g. White, Gold. Default: White.")] string stake = "White",
        [Description("How many event rolls (shop/pack draws) to analyze per stream. Default: 20.")]
            int eventRolls = 20,
        [Description(
            "Include the full raw per-stream PRNG queues (ShopStreams, Pulls, StreamStates) "
                + "instead of the per-ante summary. Off by default — this can be 1M+ characters "
                + "for the default 9-ante, 20-roll analysis. Default: false."
        )]
            bool includeRawStreams = false
    )
    {
        if (!Enum.TryParse<MotelyDeck>(deck, ignoreCase: true, out var deckValue))
            return JsonSerializer.Serialize(new { error = $"Unknown deck '{deck}'." }, JsonOptions);
        if (!Enum.TryParse<MotelyStake>(stake, ignoreCase: true, out var stakeValue))
            return JsonSerializer.Serialize(new { error = $"Unknown stake '{stake}'." }, JsonOptions);

        var config = new JamlConfig
        {
            Id = "mcp-analyze",
            Deck = deckValue,
            Stake = stakeValue,
            Seeds = [seed],
        };

        var results = MotelyJamlyzer.Analyze(config, eventRolls);
        if (results.Count == 0)
            return JsonSerializer.Serialize(
                new { error = "No result — check the seed is 8 valid characters." },
                JsonOptions
            );

        if (includeRawStreams)
            return JsonSerializer.Serialize(results[0], JsonOptions);

        var result = results[0];
        var summary = new
        {
            result.Seed,
            result.Score,
            // MotelyItem.ToString() folds all 12 struct fields (edition, seal, enhancement,
            // stickers, type) into one string like "Foil ScaryFace" — the struct's own dump
            // otherwise repeats "Seal: None, Enhancement: None, ..." on every single item.
            Antes = result.Antes.Select(a => new
            {
                a.Ante,
                a.Boss,
                a.Voucher,
                a.SmallBlindTag,
                a.BigBlindTag,
                ShopItems = a.ShopItems.Select(i => i.ToString()),
                Packs = a.Packs.Select(p => new
                {
                    p.Pack,
                    Items = p.Items.Select(i => i.ToString()),
                }),
            }),
            ErraticDeck = result.ErraticDeck?.Select(i => i.ToString()),
        };
        return JsonSerializer.Serialize(summary, JsonOptions);
    }
}
