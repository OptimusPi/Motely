using Motely.Filters.Jaml;

namespace Motely.Analysis;

public sealed record MotelyJamlyzerSeedResult(
    string Seed,
    int Score,
    IReadOnlyList<MotelyJamlyzerAnteResult> Antes,
    MotelyJamlyzerEvents Events,
    MotelyJamlyzerStreamStates StreamStates,
    MotelyItem[]? ErraticDeck = null
);

public sealed record MotelyJamlyzerAnteResult(
    int Ante,
    MotelyBossBlind Boss,
    MotelyVoucher Voucher,
    MotelyTag SmallBlindTag,
    MotelyTag BigBlindTag,
    IReadOnlyList<MotelyItem> ShopItems,
    IReadOnlyList<MotelyJamlyzerPack> Packs,
    MotelyJamlyzerPulls Pulls,
    MotelyJamlyzerShopStreams ShopStreams
);

public sealed record MotelyJamlyzerPack(MotelyBoosterPack Pack, IReadOnlyList<MotelyItem> Items);

/// <summary>
/// Per-ante raw shop-source PRNG queues, read independently of the resolved <c>ShopItems</c>.
/// Each is the sequence that source would yield on its own — e.g. <see cref="ShopTarots"/> is
/// the tarots a shop would surface if every slot rolled a tarot. All arrays have length == eventRolls.
/// </summary>
public sealed record MotelyJamlyzerShopStreams(
    IReadOnlyList<MotelyItem> ShopJokers,
    IReadOnlyList<MotelyItem> CommonShopJokers,
    IReadOnlyList<MotelyItem> UncommonShopJokers,
    IReadOnlyList<MotelyItem> RareShopJokers,
    IReadOnlyList<MotelyItem> ShopTarots,
    IReadOnlyList<MotelyItem> ShopPlanets,
    IReadOnlyList<MotelyItem> ShopSpectrals
);

/// <summary>
/// Per-ante rolls from streams that only fire when activated by a specific card or joker.
/// All arrays have length == eventRolls. EmperorTarots has length == eventRolls * 2 (2 per use).
/// </summary>
public sealed record MotelyJamlyzerPulls(
    IReadOnlyList<MotelyItem> JudgementJokers,
    IReadOnlyList<MotelyItem> WraithJokers,
    IReadOnlyList<MotelyItem> EmperorTarots,
    IReadOnlyList<MotelyItem> PurpleSealTarots,
    IReadOnlyList<MotelyItem> SixthSenseSpectrals,
    IReadOnlyList<MotelyItem> SeanceSpectrals,
    IReadOnlyList<MotelyItem> RiffRaffJokers,
    IReadOnlyList<MotelyItem> RareTagJokers,
    IReadOnlyList<MotelyItem> UncommonTagJokers,
    IReadOnlyList<MotelyItem> LegendaryJokers,
    IReadOnlyList<MotelyVoucher> VoucherSequence
);

/// <summary>
/// Resumable state bag for every stream, in AND out. Returned on every result and accepted back as
/// <c>resumeFrom</c> on the next <see cref="MotelyJamlyzer.Analyze(JamlConfig, MotelyJamlyzerStreamStates, int)"/>
/// (or, per seed, <see cref="MotelyJamlyzer.Analyze(JamlConfig, IReadOnlyDictionary{string, MotelyJamlyzerStreamStates}, int)"/>)
/// so the next window continues exactly where this one stopped — no duplicated, no skipped rolls.
/// <para>
/// Two stream classes, two exact resume mechanisms, one bag:
/// <list type="bullet">
/// <item><b>Event streams</b> are single scalar PRNG streams — their whole position is one
/// <c>double</c>, so they resume by injecting the saved State (the doubles below).</item>
/// <item><b>Composite streams</b> (pulls + shop: jokers, tarots, spectrals, planets, vouchers) are
/// bundles of leaves — and a resample leaf is an inline array, not a single double. They resume by
/// replaying <see cref="RollOffset"/> rolls and discarding them, which is exact by construction.</item>
/// </list>
/// <see cref="RollOffset"/> is the cumulative number of windows-worth of rolls already consumed —
/// it is the composites' entire state (they are pure functions of seed + offset).
/// </para>
/// </summary>
public sealed record MotelyJamlyzerStreamStates(
    int RollOffset,
    double LuckyMoney,
    double LuckyMult,
    double WheelOfFortune,
    double Cavendish,
    double GrosMichel,
    double Space,
    double Business,
    double Bloodstone,
    double Parking,
    double EightBall,
    double Glass,
    double OmenGlobe,
    double TheWheel,
    double Misprint
);

public sealed record MotelyJamlyzerEvents(
    bool[] LuckyMoney,
    bool[] LuckyMult,
    MotelyItemEdition[] WheelOfFortune,
    bool[] Cavendish,
    bool[] GrosMichel,
    bool[] Space,
    bool[] Business,
    bool[] Bloodstone,
    bool[] Parking,
    bool[] EightBall,
    bool[] Glass,
    bool[] OmenGlobe,
    bool[] TheWheel,
    int[] Misprint
);

public static class MotelyJamlyzer
{
    /// <summary>Analyze each seed with every event stream starting at the seed's natural start (0).</summary>
    public static IReadOnlyList<MotelyJamlyzerSeedResult> Analyze(
        JamlConfig config,
        int eventRolls = 20
    ) => AnalyzeCore(config, resumeStates: null, eventRolls);

    /// <summary>
    /// Analyze each seed, resuming every event stream from <paramref name="resumeFrom"/> — the state
    /// bag handed back by a previous call — so the rolls continue where the last window stopped.
    /// </summary>
    public static IReadOnlyList<MotelyJamlyzerSeedResult> Analyze(
        JamlConfig config,
        MotelyJamlyzerStreamStates resumeFrom,
        int eventRolls = 20
    )
    {
        // The bag's 14 event-stream State doubles are positions in a *specific* seed's PRNG. Replaying
        // one bag across a multi-seed config would inject seed[0]'s state into seed[1..], silently
        // corrupting their event rolls. A single bag is inherently a single-seed scroll — to scroll
        // many seeds at once, hand each its own bag via the dictionary overload below.
        if (config.Seeds.Count > 1)
            throw new InvalidOperationException(
                $"Resume (resumeFrom) is single-seed only; config has {config.Seeds.Count} seeds. "
                    + "Scroll one seed at a time, or use the per-seed dictionary overload — the "
                    + "state bag is seed-specific."
            );
        return AnalyzeCore(
            config,
            new Dictionary<string, MotelyJamlyzerStreamStates> { [config.Seeds[0]] = resumeFrom },
            eventRolls
        );
    }

    /// <summary>
    /// Analyze each seed, resuming it from its own state bag in <paramref name="resumeFrom"/> (keyed
    /// by seed). Each <see cref="MotelyJamlyzerSeedResult"/> already carries its <c>Seed</c> and
    /// <c>StreamStates</c>, so the next page is just
    /// <c>prev.ToDictionary(r => r.Seed, r => r.StreamStates)</c>. Seeds absent from the map start
    /// fresh (offset 0), which is how a new seed joins a scroll already in progress. Duplicate seeds
    /// in <paramref name="config"/> collapse to one bag under the shared key.
    /// </summary>
    public static IReadOnlyList<MotelyJamlyzerSeedResult> Analyze(
        JamlConfig config,
        IReadOnlyDictionary<string, MotelyJamlyzerStreamStates> resumeFrom,
        int eventRolls = 20
    ) => AnalyzeCore(config, resumeFrom, eventRolls);

    private static IReadOnlyList<MotelyJamlyzerSeedResult> AnalyzeCore(
        JamlConfig config,
        IReadOnlyDictionary<string, MotelyJamlyzerStreamStates>? resumeStates,
        int eventRolls
    )
    {
        var antesToAnalyze = ComputeAntes(config);
        bool hasScore = config.Must.Count + config.Should.Count > 0;
        var results = new List<MotelyJamlyzerSeedResult>(config.Seeds.Count);

        foreach (var seed in config.Seeds)
        {
            // Each seed resumes from its own bag; one absent from the map starts fresh (offset 0).
            MotelyJamlyzerStreamStates? seedResume =
                resumeStates is not null && resumeStates.TryGetValue(seed, out var s) ? s : null;
            var filterDesc = new MotelyJamlyzerFilterDesc(antesToAnalyze, eventRolls, seedResume);
            var settings = new MotelySearchSettings<MotelyJamlyzerFilterDesc.JamlyzerFilter>(
                filterDesc
            )
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithSeedList([seed])
                .WithThreadCount(1);

            int score = 0;
            if (hasScore)
            {
                settings = settings
                    .WithSeedScoreProvider(
                        new JamlShouldScoreDesc(
                            [.. config.Must],
                            [.. config.Should],
                            minimumTotalScore: 0
                        )
                    )
                    .WithScoredResultCallback(tally => score = tally.Score);
            }

            using var search = settings.CreateSearch();
            search.Start();
            search.AwaitCompletion();

            results.Add(
                new(
                    seed,
                    score,
                    filterDesc.Antes,
                    filterDesc.Events!,
                    filterDesc.StreamStates!,
                    filterDesc.ErraticDeck
                )
            );
        }

        return results;
    }

    internal static int[] ComputeAntes(JamlConfig config)
    {
        var set = new SortedSet<int>();
        foreach (
            var clause in config
                .Must.Concat(config.Should)
                .Concat(config.MustNot)
                .OfType<IAnteScopedClause>()
        )
        foreach (var ante in clause.Antes)
            set.Add(ante);
        return set.Count > 0 ? [.. set] : [0, 1, 2, 3, 4, 5, 6, 7, 8];
    }
}
