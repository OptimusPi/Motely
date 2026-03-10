using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Tests for JAML config parsing — verifies shorthand keys, source config mapping,
/// and strict handling of unknown YAML keys.
/// </summary>
public class JamlConfigTests
{
    [Fact]
    public void ValidJaml_ParsesSuccessfully()
    {
        var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Failed to parse: {error}");
        Assert.NotNull(config);
        Assert.True(config!.Must.HasAnyClauses);
        Assert.Single(config.Must.Jokers);
    }

    [Fact]
    public void Sources_shopItems_AreMapped()
    {
        var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1]
                sources:
                  shopItems: [0,1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Parse should succeed (unknown keys are ignored): {error}");
        Assert.NotNull(config);
        Assert.Single(config!.Must.Jokers);
        Assert.Equal([0, 1], config.Must.Jokers[0].Sources.ShopItems);
    }

    [Fact]
    public void Sources_boosterPacks_AreMapped()
    {
        var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1]
                sources:
                  boosterPacks: [0,1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Parse should succeed (unknown keys are ignored): {error}");
        Assert.NotNull(config);
        Assert.Single(config!.Must.Jokers);
        Assert.Equal([0, 1], config.Must.Jokers[0].Sources.BoosterPacks);
    }

    [Fact]
    public void JokerRarityClauses_ParseIntoTypedLists()
    {
        var jaml = """
            name: TypedJokers
            must:
              - commonJoker: HalfJoker
              - uncommonJoker: Showman
              - rareJoker: Blueprint
              - mixedJokers: [Blueprint, Showman]
              - soulJoker: Perkeo
                sources:
                  boosterPacks: [0,1,2,3]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Failed to parse: {error}");
        Assert.NotNull(config);
        Assert.Single(config!.Must.CommonJokers);
        Assert.Single(config.Must.UncommonJokers);
        Assert.Single(config.Must.RareJokers);
        Assert.Single(config.Must.MixedJokers);
        Assert.Single(config.Must.LegendaryJokers);
    }

    [Fact]
    public void JokerSources_RawShopStreams_AreMapped()
    {
        var jaml = """
            name: RawStreams
            must:
              - uncommonJoker: Showman
                antes: [1]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
                  judgement: [0]
                  wraith: [0]
                  riffRaff: [0,1]
                  rareTag: [0]
                  uncommonTag: [0]
                  commonShopJokers: [0,2]
                  uncommonShopJokers: [1,3]
                  rareShopJokers: [4]
                  allShopJokers: [0,1,2,3,4]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Failed to parse: {error}");
        Assert.NotNull(config);
        var clause = config!.Must.UncommonJokers[0];
        Assert.Equal([0, 1], clause.Sources.ShopItems);
        Assert.Equal([0, 1], clause.Sources.BoosterPacks);
        Assert.Equal([0], clause.Sources.Judgement);
        Assert.Equal([0], clause.Sources.Wraith);
        Assert.Equal([0, 1], clause.Sources.RiffRaff);
        Assert.Equal([0], clause.Sources.RareTag);
        Assert.Equal([0], clause.Sources.UncommonTag);
        Assert.Equal([0, 2], clause.Sources.CommonShopJokers);
        Assert.Equal([1, 3], clause.Sources.UncommonShopJokers);
        Assert.Equal([4], clause.Sources.RareShopJokers);
        Assert.Equal([0, 1, 2, 3, 4], clause.Sources.AllShopJokers);
    }

    [Fact]
    public void LegendaryJoker_ParsesPerkeo()
    {
        var jaml = """
            name: Test
            must:
              - legendaryJoker: Perkeo
                antes: [1,2,3]
                sources:
                  boosterPacks: [0,1,2,3]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Failed to parse: {error}");
        Assert.NotNull(config);
        Assert.Single(config!.Must.LegendaryJokers);
    }

    [Fact]
    public void MustAndShould_BothParse()
    {
        var jaml = """
            name: Showman
            deck: Anaglyph
            stake: White
            must:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
            should:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
                score: 100
              - joker: OopsAll6s
                antes: [1,2,3]
                score: 1
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Failed to parse: {error}");
        Assert.NotNull(config);
        Assert.True(config!.Must.HasAnyClauses);
        Assert.Equal(2, config.Should.Jokers.Count);
    }

    [Fact]
    public void UnknownClauseKey_FailsParse()
    {
        var jaml = """
            name: Test
            must:
              - joker: Showman
                totallyFakeKey: 42
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.False(success);
        Assert.Null(config);
        Assert.NotNull(error);
        Assert.Contains("totallyFakeKey", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownNestedSourcesKey_FailsParse()
    {
        var jaml = """
            name: Test
            must:
              - joker: ScaryFace
                antes: [1]
                sources:
                  boosterPakcz: [0, 1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.False(success);
        Assert.Null(config);
        Assert.NotNull(error);
        Assert.Contains("boosterPakcz", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownTopLevelKey_FailsParse()
    {
        var jaml = """
            name: Test
            madeUpTopLevel: 123
            must:
              - joker: Showman
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.False(success);
        Assert.Null(config);
        Assert.NotNull(error);
        Assert.Contains("madeUpTopLevel", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeckAndStake_Parse()
    {
        var jaml = """
            name: DeckTest
            deck: Anaglyph
            stake: Gold
            must:
              - joker: Showman
                antes: [1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Failed to parse: {error}");
        Assert.NotNull(config);
        Assert.Equal(MotelyDeck.Anaglyph, config!.Deck);
        Assert.Equal(MotelyStake.Gold, config.Stake);
    }
}

