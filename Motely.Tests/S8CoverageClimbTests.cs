using System.Runtime.Intrinsics;
using Motely.Filters.Jaml;
using Motely.SeedProviders;

namespace Motely.Tests;

/// <summary>
/// S8 climb — list-search only. Known seeds pinned by prior CLI --seeds runs.
/// No sequential MustFindOne (that was the hang).
/// </summary>
public sealed class S8CoverageClimbTests
{
    private const string VoucherOverstock = """
        name: s8-voucher
        deck: Red
        stake: White
        must:
          - voucher: Overstock
            antes: [1]
        """;

    private const string StartingDrawAceHearts = """
        name: s8-starting
        deck: Red
        stake: White
        must:
          - startingDraw:
            rank: Ace
            suit: Hearts
            antes: [1]
        """;

    private const string PlanetPluto = """
        name: s8-planet
        deck: Red
        stake: White
        must:
          - planetCard: Pluto
            antes: [1]
        """;

    private const string PlanetPlutoPacks = """
        name: s8-planet-packs
        deck: Red
        stake: White
        must:
          - planetCard: Pluto
            antes: [1]
            sources:
              shopItems: [0, 1, 2, 3]
              boosterPacks: [0, 1]
        """;

    private static readonly string[] VoucherSeeds = ["5X5", "616", "696", "6J6", "7H7"];
    private static readonly string[] DrawSeeds = ["99", "CC", "F", "Q", "R", "VV"];
    private static readonly string[] PlanetSeeds = ["R", "H", "I", "Z", "88"];
    private static readonly string[] FixtureSeeds =
        ["ALEEB", "MOTELY77", "UNITTEST", "5X5", "616", "696", "6J6", "7H7"];

    // ── R1 list proofs ──

    [Fact]
    public void Voucher_Overstock_KnownSeedsMatch() =>
        ProofSearch.MustMatchAll(VoucherOverstock, VoucherSeeds);

    [Fact]
    public void StartingDraw_AceHearts_KnownSeedsMatch() =>
        ProofSearch.MustMatchAll(StartingDrawAceHearts, DrawSeeds);

    [Fact]
    public void Planet_Pluto_KnownSeedsMatch() =>
        ProofSearch.MustMatchAll(PlanetPluto, PlanetSeeds);

    /// <summary>
    /// R2 differential: default sources are shop slots 0-7, so all five seeds hit. Narrowing to
    /// slots 0-3 (+ packs 0-1) is a real gate — only the two seeds whose Pluto sits in an early
    /// shop slot survive. A test that asserted "still matches all five" would prove the gate is dead.
    /// </summary>
    [Fact]
    public void Planet_NarrowedShopSlots_GateOutLateSlotSeeds()
    {
        ProofSearch.MustMatchAll(PlanetPluto, PlanetSeeds);

        var (matching, matched) = ProofSearch.ListMatch(PlanetPlutoPacks, PlanetSeeds);
        Assert.Equal(2L, matching);
        Assert.Equal(
            ["88", "I"],
            matched.OrderBy(static s => s, StringComparer.Ordinal).ToArray()
        );
        Assert.True(matched.Count < PlanetSeeds.Length, "narrowed sources must be a strict subset");
    }

    // ── Filter path execution via list (coverage without sequential) ──

    private static void RunClause(
        IJamlClause clause,
        string[] seeds,
        MotelyDeck deck = MotelyDeck.Red
    )
    {
        var config = new JamlConfig
        {
            Id = "s8-clause",
            Deck = deck,
            Stake = MotelyStake.White,
        };
        config.Must.Add(clause);
        using var search = JamlSearchBuilder
            .CreateSettings(config)
            .WithListSearch(seeds, seeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .Start();
        search.AwaitCompletion();
        Assert.True(search.TotalSeedsSearched >= 1);
    }

    [Fact]
    public void Voucher_HighRolls_ListRuns() =>
        RunClause(
            new VoucherClause
            {
                Vouchers = [MotelyVoucher.Overstock, MotelyVoucher.Grabber, MotelyVoucher.Telescope],
                Antes = [1, 2, 3, 4],
                Rolls = [0, 1, 2, 3],
                Min = 1,
                Max = 20,
            },
            FixtureSeeds
        );

    [Fact]
    public void MultiVoucherFilterDesc_ListRuns()
    {
        var clauses = new[]
        {
            new VoucherClause
            {
                Vouchers = [MotelyVoucher.Overstock],
                Antes = [1],
                Rolls = [0],
            },
            new VoucherClause
            {
                Vouchers = [MotelyVoucher.Grabber],
                Antes = [1, 2],
                Rolls = [0, 1],
            },
        };
        using var search = new MotelySearchSettings<MultiVoucherFilterDesc.MultiVoucherFilter>(
            new MultiVoucherFilterDesc(clauses)
        )
            .WithDeck(MotelyDeck.Red)
            .WithStake(MotelyStake.White)
            .WithListSearch(FixtureSeeds, FixtureSeeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .Start();
        search.AwaitCompletion();
        Assert.True(search.TotalSeedsSearched >= 1);
    }

    [Fact]
    public void StartingDraw_RankOnly_ListRuns() =>
        RunClause(
            new StartingDrawClause { Rank = MotelyStandardcardRank.Ace, Antes = [1] },
            DrawSeeds
        );

    [Fact]
    public void Planet_MultiTargetPacks_ListRuns() =>
        RunClause(
            new PlanetCardClause
            {
                Planets = [MotelyPlanetCard.Pluto, MotelyPlanetCard.Mercury],
                Antes = [1, 2],
                Min = 1,
                Max = 10,
                Sources = new PlanetSourceConfig
                {
                    ShopItems = [0, 1, 2, 3, 4, 5, 6, 7],
                    BoosterPacks = [0, 1, 2],
                },
            },
            FixtureSeeds.Concat(PlanetSeeds).Distinct().ToArray()
        );

    [Fact]
    public void Tarot_AllSources_ListRuns() =>
        RunClause(
            new TarotCardClause
            {
                Tarots = [MotelyTarotCard.Death, MotelyTarotCard.TheFool],
                Antes = [1, 2],
                Sources = new TarotCardSourceConfig
                {
                    ShopItems = [0, 1, 2, 3],
                    BoosterPacks = [0, 1],
                    Emperor = [0, 1],
                    PurpleSealOrEightBall = [0],
                    CharmTag = true,
                },
            },
            FixtureSeeds
        );

    [Fact]
    public void Spectral_GhostDeck_ListRuns() =>
        RunClause(
            new SpectralCardClause
            {
                Spectrals = [MotelySpectralCard.Familiar, MotelySpectralCard.Grim],
                Antes = [1, 2],
                Sources = new SpectralCardSourceConfig
                {
                    ShopItems = [0, 1, 2, 3],
                    BoosterPacks = [0, 1],
                },
            },
            FixtureSeeds,
            MotelyDeck.Ghost
        );

    [Fact]
    public void UncommonRare_EditionSources_ListRuns()
    {
        RunClause(
            new UncommonJokerClause
            {
                Jokers = [MotelyJokerUncommon.Mime, MotelyJokerUncommon.Fibonacci],
                Antes = [1, 2, 3],
                Edition = MotelyItemEdition.Foil,
                Sources = new JokerSourceConfig
                {
                    ShopItems = [0, 1, 2, 3, 4],
                    BoosterPacks = [0, 1],
                    UncommonShopJokers = [0, 1],
                },
            },
            FixtureSeeds
        );
        RunClause(
            new RareJokerClause
            {
                Jokers = [MotelyJokerRare.Blueprint, MotelyJokerRare.Brainstorm],
                Antes = [1, 2, 3, 4],
                Edition = MotelyItemEdition.Negative,
                Sources = new JokerSourceConfig
                {
                    ShopItems = [0, 1, 2, 3],
                    BoosterPacks = [0, 1],
                    RareShopJokers = [0],
                },
            },
            FixtureSeeds
        );
    }

    [Fact]
    public void Legendary_ListRuns()
    {
        RunClause(
            new LegendaryJokerClause
            {
                Jokers = [MotelyJoker.Canio, MotelyJoker.Perkeo],
                Antes = [1, 2, 3, 4],
            },
            FixtureSeeds
        );
        RunClause(
            new LegendaryJokerClause
            {
                IsWildcard = true,
                Antes = [1, 2],
                Sources = new LegendaryJokerSourceConfig { SpectralPacks = [0, 1, 2] },
            },
            FixtureSeeds
        );
    }

    [Fact]
    public void BossTagStandard_ListRuns()
    {
        RunClause(
            new BossClause { Bosses = [MotelyBossBlind.TheHook, MotelyBossBlind.TheWall], Antes = [1, 2] },
            FixtureSeeds
        );
        RunClause(
            new TagClause { Tags = [MotelyTag.CharmTag, MotelyTag.SpeedTag], Antes = [1], Rolls = [0, 1] },
            FixtureSeeds
        );
        RunClause(
            new StandardCardClause
            {
                Rank = MotelyStandardcardRank.Ace,
                Antes = [1],
                Sources = new StandardCardSourceConfig
                {
                    ShopItems = [0, 1, 2, 3],
                    BoosterPacks = [0, 1],
                },
            },
            FixtureSeeds
        );
    }

    [Fact]
    public void EventsAndErratic_ListRuns()
    {
        RunClause(new MisprintMultClause { Mult = 1, Rolls = [0, 1], Min = 1 }, FixtureSeeds);
        RunClause(new WheelOfFortuneClause { Rolls = [0, 1], Min = 1 }, FixtureSeeds);
        RunClause(
            new ErraticRankClause { Rank = MotelyStandardcardRank.Ace, Antes = [1] },
            FixtureSeeds,
            MotelyDeck.Erratic
        );
        RunClause(
            new ErraticSuitClause { Suit = MotelyStandardcardSuit.Hearts, Antes = [1] },
            FixtureSeeds,
            MotelyDeck.Erratic
        );
    }

    // ── Direct unit rails (no search hang) ──

    private sealed class RecordingSink : IMotelyResultSink
    {
        public List<string> Seeds { get; } = [];
        public List<int> Scores { get; } = [];
        public bool Disposed { get; private set; }

        public void OnSeed(string seed) => Seeds.Add(seed);

        public void OnScored(in MotelyScoredSeedResult tally)
        {
            Seeds.Add(tally.Seed);
            Scores.Add(tally.Score);
        }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void CompositeResultSink_ForwardsAndDisposes()
    {
        var a = new RecordingSink();
        var b = new RecordingSink();
        using (var composite = new CompositeMotelyResultSink([a, b]))
        {
            composite.OnSeed("SEED1");
            var scored = new MotelyScoredSeedResult();
            scored.Reset("SEED2", 42);
            composite.OnScored(in scored);
        }
        Assert.Equal(["SEED1", "SEED2"], a.Seeds);
        Assert.Equal(["SEED1", "SEED2"], b.Seeds);
        Assert.Equal([42], a.Scores);
        Assert.True(a.Disposed && b.Disposed);
    }

    [Fact]
    public void ChainedSeedProvider_FirstThenSecond()
    {
        var chained = new MotelyChainedSeedProvider(
            new MotelySeedListProvider(["A", "B"]),
            new MotelySeedListProvider(["C"])
        );
        Assert.Equal(3, chained.SeedCount);
        Assert.Equal("A", chained.NextSeed().ToString());
        Assert.Equal("B", chained.NextSeed().ToString());
        Assert.Equal("C", chained.NextSeed().ToString());
        Assert.True(chained.NextSeed().IsEmpty);

        var batch = new MotelyChainedSeedProvider(
            new MotelySeedListProvider(["X"]),
            new MotelySeedListProvider(["Y", "Z"])
        );
        var buf = new string[3];
        Assert.Equal(3, batch.NextSeeds(buf));
        Assert.Equal("X", buf[0]);
        Assert.Equal("Y", buf[1]);
        Assert.Equal("Z", buf[2]);
    }

    [Fact]
    public void VectorMask_And_VectorUtils_ShiftLeft()
    {
        var a = VectorMask.AllBitsSet;
        var b = VectorMask.NoBitsSet;
        Assert.True(a.IsAllTrue());
        Assert.True(b.IsAllFalse());
        Assert.Equal(0u, (a & b).Value);
        Assert.Equal(0xFFu, (a | b).Value);
        Assert.True(new VectorMask(0x0F).IsPartiallyTrue());

        var value = Vector256.Create(1, 2, 3, 4, 5, 6, 7, 8);
        var shift = Vector256.Create(1, 1, 1, 1, 1, 1, 1, 1);
        var shifted = MotelyVectorUtils.ShiftLeft(value, shift);
        for (int i = 0; i < 8; i++)
            Assert.Equal(value[i] << 1, shifted[i]);
    }
}
