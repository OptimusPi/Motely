using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Tests for JAML config parsing — catches the bugs where unknown YAML keys
/// were silently ignored and shopSlots/packSlots weren't recognized.
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
    public void UnknownProperty_ShopSlots_Throws()
    {
        var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1]
                sources:
                  shopSlots: [0,1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out _, out var error);

        Assert.False(success, "shopSlots should NOT be a valid key — use shopItems");
        Assert.NotNull(error);
    }

    [Fact]
    public void UnknownProperty_PackSlots_Throws()
    {
        var jaml = """
            name: Test
            must:
              - joker: Showman
                antes: [1]
                sources:
                  packSlots: [0,1]
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out _, out var error);

        Assert.False(success, "packSlots should NOT be a valid key — use boosterPacks");
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
    public void CompletelyBogusKey_Throws()
    {
        var jaml = """
            name: Test
            must:
              - joker: Showman
                totallyFakeKey: 42
            """;

        var success = JamlConfigLoader.TryLoad(jaml, out _, out _);

        Assert.False(success, "Unknown top-level clause keys should cause parse failure");
    }
}
