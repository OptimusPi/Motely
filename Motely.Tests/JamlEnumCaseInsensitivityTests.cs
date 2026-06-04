using Motely.Filters;

namespace Motely.Tests;

/// <summary>
/// Locks the JAML "case-insensitive parse, PascalCase canonical" contract:
/// any casing variant of an enum scalar — including the <c>any</c> wildcard — must
/// load to the same typed value. Author UX: type whatever; editor autocomplete
/// shows PascalCase (the C# enum member name) as the canonical form.
/// </summary>
public class JamlEnumCaseInsensitivityTests
{
    [Theory]
    [InlineData("Blueprint")]
    [InlineData("blueprint")]
    [InlineData("BLUEPRINT")]
    [InlineData("BluePrint")]
    public void Joker_ParsesAnyCasing(string casing)
    {
        var jaml = $$"""
            name: case-test
            must:
              - joker: {{casing}}
            """;

        var ok = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(ok, $"Failed to parse '{casing}': {error}");
        Assert.NotNull(config);
        var clause = Assert.Single(config!.Must.OfType<JokerClause>());
        Assert.False(clause.IsWildcard);
        Assert.Equal([MotelyJoker.Blueprint], clause.Jokers);
    }

    [Theory]
    [InlineData("any")]
    [InlineData("Any")]
    [InlineData("ANY")]
    [InlineData("aNy")]
    public void Joker_AnyWildcard_ParsesAnyCasing(string casing)
    {
        var jaml = $$"""
            name: case-test
            must:
              - joker: {{casing}}
            """;

        var ok = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(ok, $"Failed to parse '{casing}': {error}");
        Assert.NotNull(config);
        var clause = Assert.Single(config!.Must.OfType<JokerClause>());
        Assert.True(clause.IsWildcard);
        Assert.Empty(clause.Jokers);
    }

    [Theory]
    [InlineData("uncommonJoker", "showman")]
    [InlineData("commonJoker", "BLUEJOKER")]
    [InlineData("rareJoker", "Blueprint")]
    public void RarityNarrowedJoker_ParsesAnyCasing(string key, string casing)
    {
        var jaml = $$"""
            name: case-test
            must:
              - {{key}}: {{casing}}
            """;

        var ok = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(ok, $"Failed to parse '{key}: {casing}': {error}");
        Assert.NotNull(config);
    }

    [Fact]
    public void Boss_ParsesLowercase()
    {
        var jaml = """
            name: case-test
            must:
              - boss: thearm
            """;

        var ok = JamlConfigLoader.TryLoad(jaml, out var config, out var error);

        Assert.True(ok, $"Failed: {error}");
        Assert.NotNull(config);
        var clause = Assert.Single(config!.Must.OfType<BossClause>());
        Assert.Equal([MotelyBossBlind.TheArm], clause.Bosses);
    }
}
