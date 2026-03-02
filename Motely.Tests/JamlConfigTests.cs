using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Tests for JAML config parsing — verifies shorthand keys, source config mapping,
/// and graceful handling of unknown YAML keys (which are silently ignored).
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
    public void UnknownSourceKey_ShopSlots_IsIgnored()
    {
        // shopSlots is not a recognized key — the parser silently ignores it
        // (IgnoreUnmatchedProperties). The clause still parses, but shopSlots
        // won't map to any source. Users should use shopItems instead.
        var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1]
                sources:
                  shopSlots: [0,1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Parse should succeed (unknown keys are ignored): {error}");
        Assert.NotNull(config);
        Assert.Single(config!.Must.Jokers);
    }

    [Fact]
    public void UnknownSourceKey_PackSlots_IsIgnored()
    {
        // packSlots is not a recognized key — silently ignored.
        // Users should use boosterPacks instead.
        var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1]
                sources:
                  packSlots: [0,1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Parse should succeed (unknown keys are ignored): {error}");
        Assert.NotNull(config);
        Assert.Single(config!.Must.Jokers);
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
    public void UnknownClauseKey_IsIgnored()
    {
        // Unknown top-level clause keys are silently ignored by YamlDotNet's
        // IgnoreUnmatchedProperties — this is by design for forward compatibility.
        var jaml = """
            name: Test
            must:
              - joker: Showman
                totallyFakeKey: 42
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(success, $"Parse should succeed (unknown keys are ignored): {error}");
        Assert.NotNull(config);
        Assert.Single(config!.Must.Jokers);
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

