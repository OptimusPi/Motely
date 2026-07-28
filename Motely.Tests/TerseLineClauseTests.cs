using Motely.Filters.Jaml;

namespace Motely.Tests;

/// <summary>
/// The terse spelling reaches every key its family owns: a one-line clause carries
/// continuation keys underneath it, so "- Negative Perkeo" + "    ante: 0" is the same
/// clause as the structured legendaryJoker mapping. One grammar — JamlLine builds the
/// clause, the family's own desc applies the keys.
/// </summary>
public sealed class TerseLineClauseTests
{
    private const string Structured = """
        name: consumer
        deck: Red
        stake: White
        must:
          - joker: Perkeo
            antes: [1]
            edition: Negative
        """;

    private const string Terse = """
        name: consumer
        deck: Red
        stake: White
        must:
          - Negative Perkeo
            ante: 1
        """;

    [Fact]
    public void TerseLineWithKeys_EqualsTheStructuredClause()
    {
        var structured = Assert.IsType<JokerClause>(
            JamlConfigLoader.FromJaml(Structured).Must[0]
        );
        var terse = Assert.IsType<JokerClause>(JamlConfigLoader.FromJaml(Terse).Must[0]);

        Assert.Equal(structured.Jokers, terse.Jokers);
        Assert.Equal(structured.Edition, terse.Edition);
        Assert.Equal(structured.Antes, terse.Antes);
    }

    [Fact]
    public void BareTerseLine_StillLoadsWithoutKeys()
    {
        var config = JamlConfigLoader.FromJaml("""
            must:
              - Perkeo
            """);
        var clause = Assert.IsType<JokerClause>(config.Must[0]);
        Assert.Equal([MotelyJoker.Perkeo], clause.Jokers);
    }

    [Fact]
    public void TerseLine_CarriesCommonKeys()
    {
        var config = JamlConfigLoader.FromJaml("""
            should:
              - Blueprint
                antes: [1, 2]
                score: 50
                label: bp
            """);
        var clause = Assert.IsType<JokerClause>(config.Should[0]);
        Assert.Equal([1, 2], clause.Antes);
        Assert.Equal(50, clause.Score);
        Assert.Equal("bp", clause.Label);
    }

    [Fact]
    public void TerseLine_CarriesAFamilyKey()
    {
        var config = JamlConfigLoader.FromJaml("""
            must:
              - Perkeo
                edition: Negative
            """);
        var clause = Assert.IsType<JokerClause>(config.Must[0]);
        Assert.Equal(MotelyItemEdition.Negative, clause.Edition);
    }

    [Fact]
    public void TerseLine_RejectsAKeyTheFamilyDoesNotOwn()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JamlConfigLoader.FromJaml("""
                must:
                  - Perkeo
                    notAKey: 3
                """)
        );
        Assert.Contains("notAKey", ex.Message);
    }

    /// <summary>Both spellings find the same seeds — the proof that this is one grammar.</summary>
    [Fact]
    public void BothSpellings_MatchTheSameSeeds()
    {
        string[] seeds = ["ALEEB", "MOTELY77", "UNITTEST", "5X5", "616", "696", "6J6", "7H7"];

        const string structured = """
            deck: Red
            stake: White
            must:
              - joker: Any
                antes: [1]
            """;
        const string terse = """
            deck: Red
            stake: White
            must:
              - Any
                ante: 1
            """;

        var (structuredCount, structuredSeeds) = ProofSearch.ListMatch(structured, seeds);
        var (terseCount, terseSeeds) = ProofSearch.ListMatch(terse, seeds);

        Assert.True(structuredCount > 0, "control matched nothing");
        Assert.Equal(structuredCount, terseCount);
        Assert.Equal(
            structuredSeeds.OrderBy(s => s, StringComparer.Ordinal),
            terseSeeds.OrderBy(s => s, StringComparer.Ordinal)
        );
    }
}
