using Motely.Filters;
using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// Drives every JAML FilterDesc family through the real search pipeline using C# objects (no
/// loader, no YAML) so the vectorized <c>Filter()</c> (Must path) and the scalar JamlScoring
/// (Should path) both execute. The assertions are deliberately structural — that the search runs
/// the filter over the seed batch without throwing — because the point here is coverage of the
/// SIMD/scoring code, not pinning specific seed outcomes (those live in the behavior tests).
/// </summary>
public class JamlSimdCoverageTests
{
    private static readonly string[] Seeds = ["MOTELY77"];

    /// <summary>Runs a clause through the SIMD <c>Filter()</c> path (Must) and asserts the search ran.</summary>
    private static long RunMust(IJamlClause clause)
    {
        var config = new JamlConfig
        {
            Id = "cov-must",
            Deck = MotelyDeck.Red,
            Stake = MotelyStake.White,
        };
        config.Must.Add(clause);

        var settings = JamlSearchBuilder
            .CreateSettings(config)
            .WithListSearch(Seeds, Seeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        search.AwaitCompletion();
        Assert.True(search.TotalSeedsSearched >= 1, "the search must actually run the SIMD filter");
        return search.MatchingSeeds;
    }

    /// <summary>Runs a clause through the scalar JamlScoring path (Should) and returns the score.</summary>
    private static int RunShould(IJamlClause clause)
    {
        clause.Score = 1;
        var config = new JamlConfig
        {
            Id = "cov-should",
            Deck = MotelyDeck.Red,
            Stake = MotelyStake.White,
        };
        config.Should.Add(clause);

        int score = -1;
        var settings = JamlSearchBuilder
            .CreateSettings(config)
            .WithListSearch(Seeds, Seeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithScoredResultCallback(result => score = result.Score);

        using var search = settings.Start();
        search.AwaitCompletion();
        return score;
    }

    private static void ExerciseBoth(IJamlClause must, IJamlClause should)
    {
        Assert.True(RunMust(must) >= 0);
        Assert.True(RunShould(should) >= 0);
    }

    [Fact]
    public void Jokers_AllRarities_FilterAndScore()
    {
        ExerciseBoth(
            new JokerClause { IsWildcard = true, Antes = [1, 2] },
            new JokerClause { Jokers = [MotelyJoker.Blueprint], Antes = [1, 2] }
        );
        ExerciseBoth(
            new CommonJokerClause { IsWildcard = true, Antes = [1] },
            new CommonJokerClause { IsWildcard = true, Antes = [1] }
        );
        ExerciseBoth(
            new UncommonJokerClause
            {
                IsWildcard = true,
                Antes = [1],
                // exercise the fast-path rarity stream branch
                Sources = new JokerSourceConfig
                {
                    ShopItems = [0, 1],
                    BoosterPacks = [0],
                    UncommonShopJokers = [0],
                },
            },
            new UncommonJokerClause { IsWildcard = true, Antes = [1] }
        );
        ExerciseBoth(
            new RareJokerClause { IsWildcard = true, Antes = [1] },
            new RareJokerClause { IsWildcard = true, Antes = [1] }
        );
        ExerciseBoth(
            new LegendaryJokerClause
            {
                IsWildcard = true,
                Antes = [1],
                Sources = new LegendaryJokerSourceConfig { ArcanaPacks = [0], SpectralPacks = [0] },
            },
            new LegendaryJokerClause
            {
                IsWildcard = true,
                Antes = [1],
                Sources = new LegendaryJokerSourceConfig { ArcanaPacks = [0], SpectralPacks = [0] },
            }
        );
        // T4: edition prefilter path inside LegendaryJokerFilterDesc (LegendarySoulEditionPrefilter).
        ExerciseBoth(
            new LegendaryJokerClause
            {
                Jokers = [MotelyJoker.Perkeo],
                Edition = MotelyItemEdition.Negative,
                Antes = [1],
                Min = 1,
            },
            new LegendaryJokerClause
            {
                Jokers = [MotelyJoker.Perkeo],
                Edition = MotelyItemEdition.Negative,
                Antes = [1],
                Min = 1,
            }
        );
    }

    [Fact]
    public void Cards_AllTypes_FilterAndScore()
    {
        // Tarot: shop + arcana pack + Emperor + Purple Seal — hits every source branch.
        ExerciseBoth(
            new TarotCardClause
            {
                Tarots = [MotelyTarotCard.TheFool],
                Antes = [1],
                Sources = new TarotCardSourceConfig
                {
                    ShopItems = [0, 1],
                    BoosterPacks = [0],
                    Emperor = [0],
                    PurpleSealOrEightBall = [0],
                },
            },
            new TarotCardClause { Tarots = [MotelyTarotCard.TheFool], Antes = [1] }
        );

        ExerciseBoth(
            new SpectralCardClause
            {
                Spectrals = [MotelySpectralCard.Familiar],
                Antes = [1],
                Sources = new SpectralCardSourceConfig
                {
                    ShopItems = [0],
                    BoosterPacks = [0],
                    SixthSense = [0],
                    Seance = [0],
                },
            },
            new SpectralCardClause { Spectrals = [MotelySpectralCard.Familiar], Antes = [1] }
        );

        // T4: Soul/BlackHole take SpecialSpectralCardFilterDesc (pack-type narrow + scalar confirm).
        ExerciseBoth(
            new SpectralCardClause { Spectrals = [MotelySpectralCard.TheSoul], Antes = [1] },
            new SpectralCardClause { Spectrals = [MotelySpectralCard.TheSoul], Antes = [1] }
        );
        ExerciseBoth(
            new SpectralCardClause { Spectrals = [MotelySpectralCard.BlackHole], Antes = [1] },
            new SpectralCardClause { Spectrals = [MotelySpectralCard.BlackHole], Antes = [1] }
        );

        ExerciseBoth(
            new PlanetCardClause { Planets = [MotelyPlanetCard.Mercury], Antes = [1] },
            new PlanetCardClause { Planets = [MotelyPlanetCard.Mercury], Antes = [1] }
        );

        ExerciseBoth(
            new StandardCardClause
            {
                Rank = MotelyStandardcardRank.Two,
                Suit = MotelyStandardcardSuit.Spades,
                Antes = [1],
            },
            new StandardCardClause { Rank = MotelyStandardcardRank.Two, Antes = [1] }
        );

        ExerciseBoth(
            new ErraticRankClause { Rank = MotelyStandardcardRank.Two, Antes = [1] },
            new ErraticRankClause { Rank = MotelyStandardcardRank.Two, Antes = [1] }
        );

        ExerciseBoth(
            new ErraticSuitClause { Suit = MotelyStandardcardSuit.Spades, Antes = [1] },
            new ErraticSuitClause { Suit = MotelyStandardcardSuit.Spades, Antes = [1] }
        );
    }

    [Fact]
    public void Features_FilterAndScore()
    {
        ExerciseBoth(
            new VoucherClause
            {
                Vouchers = [MotelyVoucher.Overstock],
                Rolls = [0],
                Antes = [1],
            },
            new VoucherClause
            {
                Vouchers = [MotelyVoucher.Overstock],
                Rolls = [0],
                Antes = [1],
            }
        );
        ExerciseBoth(
            new TagClause
            {
                Tags = [MotelyTag.RareTag],
                Rolls = [0],
                Antes = [1],
            },
            new TagClause
            {
                Tags = [MotelyTag.RareTag],
                Rolls = [0],
                Antes = [1],
            }
        );
        ExerciseBoth(
            new BossClause { Bosses = [MotelyBossBlind.CeruleanBell], Antes = [1] },
            new BossClause { Bosses = [MotelyBossBlind.CeruleanBell], Antes = [1] }
        );
        ExerciseBoth(
            new StartingDrawClause { Rank = MotelyStandardcardRank.Two, Antes = [1] },
            new StartingDrawClause { Rank = MotelyStandardcardRank.Two, Antes = [1] }
        );
    }

    [Fact]
    public void Events_FilterAndScore()
    {
        IJamlClause[] Make() =>
            [
                new LuckyMoneyClause { Rolls = [0, 1] },
                new LuckyMultClause { Rolls = [0, 1] },
                new MisprintMultClause { Rolls = [0, 1], Mult = 1 },
                new WheelOfFortuneClause { Rolls = [0] },
                new CavendishExtinctClause { Rolls = [0] },
                new GrosMichelExtinctClause { Rolls = [0] },
                new SpaceLevelupClause { Rolls = [0] },
                new BusinessPayoutClause { Rolls = [0] },
                new BloodstoneTriggerClause { Rolls = [0] },
                new ParkingPayoutClause { Rolls = [0] },
                new GlassDestroyClause { Rolls = [0] },
                new WheelStaysFlippedClause { Rolls = [0] },
            ];

        var must = Make();
        var should = Make();
        for (int i = 0; i < must.Length; i++)
            ExerciseBoth(must[i], should[i]);
    }
}
