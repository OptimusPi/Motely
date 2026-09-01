using Motely.Filters;
using Motely.Filters.Jaml;

namespace Motely.Tests;

public class JamlJsonLoaderTests
{
    [Fact]
    public void FromJson_HappyPath_ParsesDeckStakeAndClauses()
    {
        var config = JamlConfigLoader.FromJson(
            """
            {
              "name": "json happy",
              "deck": "Erratic",
              "stake": "Gold",
              "must": [{ "joker": "Blueprint" }],
              "should": [{ "voucher": "Telescope", "score": 5 }],
              "mustNot": [{ "joker": "Vagabond" }]
            }
            """
        );

        Assert.Equal(MotelyDeck.Erratic, config.Deck);
        Assert.Equal(MotelyStake.Gold, config.Stake);
        Assert.Single(config.Must);
        Assert.Single(config.Should);
        Assert.Single(config.MustNot);
    }

    [Fact]
    public void TryLoadFromJson_UnknownRootKey_IsRejected()
    {
        var ok = JamlConfigLoader.TryLoadFromJson(
            """{ "must": [{ "joker": "Blueprint" }], "boses": [] }""",
            out _,
            out var error
        );

        Assert.False(ok);
        Assert.Contains("boses", error);
    }

    [Fact]
    public void FromYaml_HappyPath_MatchesJson()
    {
        var fromYaml = JamlConfigLoader.FromYaml(
            """
            name: yaml happy
            deck: Red
            must:
              - joker: Blueprint
            """
        );
        var fromJson = JamlConfigLoader.FromJson(
            """{ "name": "yaml happy", "deck": "Red", "must": [{ "joker": "Blueprint" }] }"""
        );

        Assert.Equal(fromJson.Deck, fromYaml.Deck);
        Assert.IsType<JokerClause>(Assert.Single(fromYaml.Must));
        Assert.IsType<JokerClause>(Assert.Single(fromJson.Must));
    }

    [Fact]
    public void FromJson_InvalidJson_ThrowsWithMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JamlConfigLoader.FromJson("{ not json")
        );
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void FromYaml_InvalidYaml_ThrowsWithMessage()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JamlConfigLoader.FromYaml("must: [")
        );
        Assert.NotEmpty(ex.Message);
    }
}
