using Xunit;

namespace Motely.Tests;

/// <summary>
/// Pins the JSON load path (JamlConfigLoader.TryLoadFromJson / FromJson / TryParseRootJson)
/// to the same contract as the YAML path: strict unknown-key rejection (the v13/v14
/// false-positive class) and nested and/or logic-block normalization.
/// </summary>
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
    public void TryLoadFromJson_UnknownClauseKey_IsRejected()
    {
        var ok = JamlConfigLoader.TryLoadFromJson(
            """{ "must": [{ "joker": "Blueprint", "boosterPakcz": [0] }] }""",
            out _,
            out var error
        );

        Assert.False(ok);
        Assert.Contains("boosterPakcz", error);
    }

    [Fact]
    public void TryLoadFromJson_NestedLogicBlock_MatchesYamlEquivalent()
    {
        // Legacy nested logic syntax: and: { clauses: [...], antes: [...] } — shared keys hoist.
        var jsonOk = JamlConfigLoader.TryLoadFromJson(
            """
            {
              "must": [
                {
                  "and": {
                    "clauses": [{ "joker": "Blueprint" }, { "joker": "Showman" }],
                    "antes": [3, 4]
                  }
                }
              ]
            }
            """,
            out var fromJson,
            out var jsonError
        );

        var yamlOk = JamlConfigLoader.TryLoad(
            """
            must:
              - and:
                  clauses:
                    - joker: Blueprint
                    - joker: Showman
                  antes: [3, 4]
            """,
            out var fromYaml,
            out var yamlError
        );

        Assert.True(jsonOk, $"JSON parse failed: {jsonError}");
        Assert.True(yamlOk, $"YAML parse failed: {yamlError}");

        var jsonAnd = Assert.IsType<AndClause>(Assert.Single(fromJson!.Must));
        var yamlAnd = Assert.IsType<AndClause>(Assert.Single(fromYaml!.Must));
        Assert.Equal(yamlAnd.Clauses.Length, jsonAnd.Clauses.Length);

        var jsonChild = Assert.IsType<JokerClause>(jsonAnd.Clauses[0]);
        var yamlChild = Assert.IsType<JokerClause>(yamlAnd.Clauses[0]);
        Assert.Equal(yamlChild.Antes, jsonChild.Antes);
        Assert.Equal(new[] { 3, 4 }, jsonChild.Antes);
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
