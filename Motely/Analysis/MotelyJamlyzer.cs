using Motely.Filters.Jaml;

namespace Motely.Analysis;

// ── Internal record (full data, no score) ────────────────────────────────────
internal sealed record MotelyJamlyzerSeedData(
    string Seed,
    IReadOnlyList<MotelyJamlyzerAnteResult> Antes,
    MotelyJamlyzerEvents Events
);

// ── WASM-facing / JS-serialisable types ──────────────────────────────────────

/// <summary>Analysis payload for a single seed (antes + event rolls).</summary>
public sealed class MotelySeedAnalysis
{
    public required IReadOnlyList<MotelyJamlyzerAnteResult> Antes { get; set; }
    public required MotelyJamlyzerEvents Events { get; set; }
}

/// <summary>Per-seed result returned by <see cref="MotelyJamlyzer.Jamlyzer"/>.</summary>
public sealed class MotelyJamlyzerSeedResult
{
    public required string Seed { get; set; }
    public int Score { get; set; }
    public MotelySeedAnalysis? Analysis { get; set; }
}

/// <summary>Top-level result wrapper returned to JavaScript.</summary>
public sealed class MotelyJamlyzerResult
{
    public string? Error { get; set; }
    public string[] TallyLabels { get; set; } = [];
    public MotelyJamlyzerSeedResult[] Seeds { get; set; } = [];
}

public sealed record MotelyJamlyzerAnteResult(
    int Ante,
    MotelyBossBlind Boss,
    MotelyVoucher Voucher,
    MotelyTag SmallBlindTag,
    MotelyTag BigBlindTag,
    IReadOnlyList<MotelyItem> ShopItems,
    IReadOnlyList<MotelyJamlyzerPack> Packs
);

public sealed record MotelyJamlyzerPack(
    MotelyBoosterPack Pack,
    IReadOnlyList<MotelyItem> Items
);

public sealed record MotelyJamlyzerEvents(
    bool[]                LuckyMoney,
    bool[]                LuckyMult,
    MotelyItemEdition[]   WheelOfFortune,
    bool[]                Cavendish,
    bool[]                GrosMichel,
    bool[]                Space,
    bool[]                Business,
    bool[]                Bloodstone,
    bool[]                Parking,
    bool[]                EightBall,
    bool[]                Glass,
    bool[]                OmenGlobe,
    bool[]                TheWheel,
    int[]                 Misprint
);

public static class MotelyJamlyzer
{
    internal static IReadOnlyList<MotelyJamlyzerSeedData> Analyze(JamlConfig config, int eventRolls = 20)
    {
        var antesToAnalyze = ComputeAntes(config);
        var results = new List<MotelyJamlyzerSeedData>(config.Seeds.Count);

        foreach (var seed in config.Seeds)
        {
            var filterDesc = new MotelyJamlyzerFilterDesc(antesToAnalyze, eventRolls);
            var searchSettings = new MotelySearchSettings<MotelyJamlyzerFilterDesc.JamlyzerFilter>(filterDesc)
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithListSearch([seed])
                .WithThreadCount(1);

            using var search = searchSettings.CreateSearch();
            search.RunSearchUntilCompletion();

            results.Add(new(seed, filterDesc.Antes, filterDesc.Events!));
        }

        return results;
    }

    /// <summary>WASM-facing entry: runs Jamlyzer and returns a JS-serialisable result.</summary>
    public static MotelyJamlyzerResult Jamlyzer(JamlConfig config, string[] tallyLabels, int eventRolls = 20)
    {
        try
        {
            var raw = Analyze(config, eventRolls);
            var seeds = raw.Select(r => new MotelyJamlyzerSeedResult
            {
                Seed     = r.Seed,
                Score    = 0,
                Analysis = new MotelySeedAnalysis { Antes = r.Antes, Events = r.Events },
            }).ToArray();
            return new MotelyJamlyzerResult { TallyLabels = tallyLabels, Seeds = seeds };
        }
        catch (Exception ex)
        {
            return new MotelyJamlyzerResult { Error = ex.Message };
        }
    }

    internal static int[] ComputeAntes(JamlConfig config)
    {
        var set = new SortedSet<int>();
        foreach (var clause in config.Must.Concat(config.Should).Concat(config.MustNot).OfType<JamlClause>())
            foreach (var ante in clause.Antes)
                set.Add(ante);
        return set.Count > 0 ? [.. set] : [1, 2, 3, 4, 5, 6, 7, 8];
    }
}
