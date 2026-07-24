namespace Motely.Tests;

/// <summary>
/// Drives the SIMD Must paths of the per-rarity joker descs, the voucher desc, and the
/// legendary Soul matcher through the real search pipeline with named targets, editions, and
/// explicit source combinations — the branches the wildcard smoke tests never enter.
/// Assertions are structural (the search ran the filter over the batch); behavior pinning
/// lives in the golden tests.
/// </summary>
public sealed class JamlMustPathCoverageTests
{
    private static readonly string[] Seeds = ["ALEEB", "MOTELY77", "UNITTEST"];

    private static long RunMust(IJamlClause clause, MotelyDeck deck = MotelyDeck.Red)
    {
        var config = new JamlConfig
        {
            Id = "must-cov",
            Deck = deck,
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
        Assert.True(search.TotalSeedsSearched >= 1);
        return search.MatchingSeeds;
    }

    [Fact]
    public void UncommonJoker_NamedTargets_EachSourceCombination()
    {
        Assert.True(
            RunMust(
                new UncommonJokerClause
                {
                    Jokers = [MotelyJokerUncommon.Fibonacci, MotelyJokerUncommon.Hack],
                    Antes = [1, 2],
                    Sources = new JokerSourceConfig { ShopItems = [0, 1, 2, 3] },
                }
            ) >= 0
        );
        Assert.True(
            RunMust(
                new UncommonJokerClause
                {
                    Jokers = [MotelyJokerUncommon.Mime],
                    Antes = [1, 2],
                    Sources = new JokerSourceConfig { BoosterPacks = [0, 1] },
                }
            ) >= 0
        );
        Assert.True(
            RunMust(
                new UncommonJokerClause
                {
                    Jokers = [MotelyJokerUncommon.Blackboard],
                    Antes = [1],
                    Sources = new JokerSourceConfig { UncommonShopJokers = [0, 1] },
                }
            ) >= 0
        );
    }

    [Fact]
    public void UncommonJoker_WithEdition_FiltersEditionBranch()
    {
        Assert.True(
            RunMust(
                new UncommonJokerClause
                {
                    IsWildcard = true,
                    Edition = MotelyItemEdition.Foil,
                    Antes = [1, 2],
                }
            ) >= 0
        );
    }

    [Fact]
    public void RareJoker_NamedAndEdition()
    {
        Assert.True(
            RunMust(new RareJokerClause { Jokers = [MotelyJokerRare.Blueprint], Antes = [1, 2] }) >= 0
        );
        Assert.True(
            RunMust(
                new RareJokerClause
                {
                    IsWildcard = true,
                    Edition = MotelyItemEdition.Negative,
                    Antes = [1],
                }
            ) >= 0
        );
    }

    [Fact]
    public void CommonJoker_NamedAndEdition()
    {
        Assert.True(
            RunMust(new CommonJokerClause { Jokers = [MotelyJokerCommon.Joker], Antes = [1, 2] }) >= 0
        );
        Assert.True(
            RunMust(
                new CommonJokerClause
                {
                    IsWildcard = true,
                    Edition = MotelyItemEdition.Holographic,
                    Antes = [1],
                }
            ) >= 0
        );
    }

    [Fact]
    public void AnyRarityJoker_NamedWithEdition()
    {
        Assert.True(
            RunMust(
                new JokerClause
                {
                    Jokers = [MotelyJoker.Blueprint, MotelyJoker.Brainstorm],
                    Antes = [1, 2, 3],
                }
            ) >= 0
        );
        Assert.True(
            RunMust(
                new JokerClause
                {
                    IsWildcard = true,
                    Edition = MotelyItemEdition.Polychrome,
                    Antes = [1, 2],
                }
            ) >= 0
        );
    }

    [Fact]
    public void Voucher_MultiRollMultiAnteAndMultiTarget()
    {
        Assert.True(
            RunMust(
                new VoucherClause
                {
                    Vouchers = [MotelyVoucher.Overstock, MotelyVoucher.Grabber, MotelyVoucher.Hone],
                    Rolls = [0, 1, 2],
                    Antes = [1, 2],
                }
            ) >= 0
        );
        Assert.True(
            RunMust(
                new VoucherClause
                {
                    Vouchers = [MotelyVoucher.MagicTrick],
                    Rolls = [0],
                    Antes = [1, 2, 3, 4],
                }
            ) >= 0
        );
    }

    [Fact]
    public void Tag_MultiRollBothBlinds()
    {
        Assert.True(
            RunMust(
                new TagClause
                {
                    Tags = [MotelyTag.SpeedTag, MotelyTag.CharmTag],
                    Rolls = [0, 1, 2],
                    Antes = [1, 2],
                }
            ) >= 0
        );
    }

    [Fact]
    public void Legendary_EachVariant()
    {
        // Named legendary, no edition: the plain Soul-match path across an ante range.
        Assert.True(
            RunMust(
                new LegendaryJokerClause { Jokers = [MotelyJoker.Canio], Antes = [1, 2, 3] }
            ) >= 0
        );
        // Wildcard legendary from spectral packs only.
        Assert.True(
            RunMust(
                new LegendaryJokerClause
                {
                    IsWildcard = true,
                    Antes = [1, 2],
                    Sources = new LegendaryJokerSourceConfig { SpectralPacks = [0, 1] },
                }
            ) >= 0
        );
        // Min > 1 forces the matcher to keep counting after the first hit.
        Assert.True(
            RunMust(
                new LegendaryJokerClause { IsWildcard = true, Antes = [1, 2, 3, 4], Min = 2 }
            ) >= 0
        );
    }

    [Fact]
    public void Boss_MultiAnte()
    {
        Assert.True(
            RunMust(
                new BossClause
                {
                    Bosses = [MotelyBossBlind.TheHook, MotelyBossBlind.TheWall],
                    Antes = [1, 2, 3],
                }
            ) >= 0
        );
    }

    [Fact]
    public void GhostDeck_SpectralInShop_MustPath()
    {
        Assert.True(
            RunMust(
                new SpectralCardClause
                {
                    Spectrals = [MotelySpectralCard.Familiar],
                    Antes = [1, 2],
                    Sources = new SpectralCardSourceConfig { ShopItems = [0, 1, 2, 3] },
                },
                MotelyDeck.Ghost
            ) >= 0
        );
    }

    [Fact]
    public void Tarot_EmperorAndPurpleSeal_MustPath()
    {
        Assert.True(
            RunMust(
                new TarotCardClause
                {
                    Tarots = [MotelyTarotCard.Death],
                    Antes = [1, 2],
                    Sources = new TarotCardSourceConfig
                    {
                        Emperor = [0, 1],
                        PurpleSealOrEightBall = [0, 1],
                        CharmTag = true,
                        BoosterPacks = [0, 1],
                    },
                }
            ) >= 0
        );
    }
}
