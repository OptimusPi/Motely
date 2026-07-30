using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// JAML spells a joker wildcard <c>Any</c>, matched case-insensitively by
/// <c>JamlConfigLoader.IsAny</c>. <c>*any*</c> is YAML alias syntax, not JAML, and stays rejected —
/// it should fail loudly rather than become a second spelling.
/// </summary>
public sealed class JamlWildcardTests
{
    private static string Doc(string token) =>
        $"name: wildcard-probe\ndeck: Red\nstake: White\nshould:\n  - joker: {token}\n    antes: [1]\n    score: 1\n";

    [Theory]
    [InlineData("Any")]
    [InlineData("any")]
    [InlineData("ANY")]
    public void AnyIsTheJokerWildcard(string token)
    {
        Assert.True(JamlConfigLoader.TryLoad(Doc(token), out var config, out var error), error);
        var clause = Assert.IsType<JokerClause>(config!.Should[0]);
        Assert.True(clause.IsWildcard);
        Assert.Empty(clause.Jokers);
    }

    [Fact]
    public void YamlAliasSyntaxIsRejected()
    {
        Assert.False(JamlConfigLoader.TryLoad(Doc("*any*"), out _, out var error));
        Assert.Contains("*any*", error);
    }

    private static string TarotDoc(string token) =>
        $"name: tarot-wildcard-probe\ndeck: Red\nstake: White\nshould:\n  - tarotCard: {token}\n    score: 1\n";

    [Theory]
    [InlineData("Any")]
    [InlineData("any")]
    [InlineData("ANY")]
    public void AnyIsTheTarotWildcard(string token)
    {
        Assert.True(JamlConfigLoader.TryLoad(TarotDoc(token), out var config, out var error), error);
        var clause = Assert.IsType<TarotCardClause>(config!.Should[0]);
        Assert.True(clause.IsWildcard);
        Assert.Empty(clause.Tarots);
    }
}
