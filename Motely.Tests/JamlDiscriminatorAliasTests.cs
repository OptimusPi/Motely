using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Every wire discriminator generated into <see cref="JamlSchema"/> must be recognised by the
/// loader. Prevents a second hand list from drifting away from the attribute-driven schema.
/// </summary>
public sealed class JamlDiscriminatorAliasTests
{
    public static TheoryData<string> AllDiscriminators()
    {
        var data = new TheoryData<string>();
        foreach (var key in JamlSchema.Discriminators)
            data.Add(key);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllDiscriminators))]
    public void EverySchemaDiscriminator_IsRecognisedByTheLoader(string discriminator)
    {
        // A clause whose only key is the discriminator. Some kinds will still reject this
        // minimal shape for a missing value — that's fine; the one error that must never
        // appear is the loader failing to recognise the discriminator at all.
        var jaml = $"must:\n  - {discriminator}: 1\n";
        JamlConfigLoader.TryLoad(jaml, out _, out var error);

        Assert.False(
            error?.Contains("no recognised discriminator", StringComparison.OrdinalIgnoreCase) ?? false,
            $"'{discriminator}' is in JamlSchema but the loader does not recognise it: {error}");
    }

    [Theory]
    [InlineData("tags", "[NegativeTag, CharmTag]")]
    [InlineData("tarotCards", "[TheFool]")]
    [InlineData("spectralCards", "[Ankh]")]
    [InlineData("planetCards", "[Pluto]")]
    public void PluralItemAliases_ParseToTheSameClauseAsTheSingular(string discriminator, string value)
    {
        var loaded = JamlConfigLoader.TryLoad($"must:\n  - {discriminator}: {value}\n", out var config, out var error);

        Assert.True(loaded, $"'{discriminator}' failed to load: {error}");
        Assert.NotNull(config);
        Assert.Single(config.Must);
    }

    [Theory]
    [InlineData("tag", new[] { 0, 1 })]
    [InlineData("tags", new[] { 0, 1 })]
    [InlineData("smallBlindTag", new[] { 0 })]
    [InlineData("bigBlindTag", new[] { 1 })]
    public void TagWire_DefaultRolls_MatchBlindSemantics(string discriminator, int[] expectedRolls)
    {
        Assert.Equal(expectedRolls, JamlSchema.RollsDefaultFor(discriminator));

        var loaded = JamlConfigLoader.TryLoad(
            $"must:\n  - {discriminator}: NegativeTag\n",
            out var config,
            out var error);

        Assert.True(loaded, $"'{discriminator}' failed to load: {error}");
        var tag = Assert.IsType<TagClause>(Assert.Single(config!.Must));
        Assert.Equal(expectedRolls, tag.Rolls);
    }
}
