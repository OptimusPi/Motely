namespace Motely.Tests;

/// <summary>
/// Single-line JAML clauses. Anywhere a clause may appear in a filter, one human line
/// ("Eternal Blueprint in antes 1 or 2") is accepted and becomes a real clause through the
/// engine's own line converter off MotelyItem identity — no second grammar, no second loader pass.
/// </summary>
public class JamlLineClauseTests
{
    [Fact]
    public void Jaml_LineClauseInMust_BecomesTheExpectedClause()
    {
        var config = JamlConfigLoader.FromJaml(
            """
            name: line in must
            must:
              - Eternal Blueprint in antes 1 or 2
            """
        );

        var joker = Assert.IsType<JokerClause>(Assert.Single(config.Must));
        Assert.Equal([MotelyJoker.Blueprint], joker.Jokers);
        Assert.Equal([MotelyJokerSticker.Eternal], joker.Stickers);
        Assert.Equal([1, 2], joker.Antes);
    }

    [Fact]
    public void Jaml_MixedLineAndStructuredClauses_BothLoad()
    {
        var config = JamlConfigLoader.FromJaml(
            """
            name: mixed
            must:
              - The Fool in ante 1
              - joker: Vagabond
            """
        );

        Assert.Equal(2, config.Must.Count);
        Assert.IsType<TarotCardClause>(config.Must[0]);
        Assert.IsType<JokerClause>(config.Must[1]);
    }

    [Fact]
    public void Jaml_LineClauseString_BecomesTheExpectedClause()
    {
        var config = JamlConfigLoader.FromJaml(
            """
            name: line clause
            must:
              - Boss The Wall in ante 3
            """
        );

        var boss = Assert.IsType<BossClause>(Assert.Single(config.Must));
        Assert.Equal([MotelyBossBlind.TheWall], boss.Bosses);
        Assert.Equal([3], boss.Antes);
    }

    [Fact]
    public void Jaml_LineClauseInsideAndBlock_Loads()
    {
        var config = JamlConfigLoader.FromJaml(
            """
            name: nested
            must:
              - and:
                  clauses:
                    - Eternal Blueprint in ante 1
                    - The Fool in ante 1
            """
        );

        var and = Assert.IsType<AndClause>(Assert.Single(config.Must));
        Assert.Equal(2, and.Clauses.Length);
    }

    [Fact]
    public void Jaml_GarbageLineClause_FailsLoudly()
    {
        var ok = JamlConfigLoader.TryLoad(
            """
            name: bad
            must:
              - Not A Real Item in ante 1
            """,
            out _,
            out var error
        );

        Assert.False(ok);
        Assert.Contains("Not A Real Item", error);
    }
}
