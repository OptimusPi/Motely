using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Every discriminator the registry declares must be recognised by the loader. The loader once
/// kept its own hand-written discriminator list, and tags/tarotCards/spectralCards/planetCards
/// silently fell out of it — this sweep makes that whole failure mode impossible to reintroduce.
/// </summary>
public sealed class JamlDiscriminatorAliasTests
{
    public static TheoryData<string> AllDiscriminators()
    {
        var data = new TheoryData<string>();
        foreach (var key in JamlDiscriminatorRegistry.Entries.Keys)
            data.Add(key);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllDiscriminators))]
    public void EveryRegistryDiscriminator_IsRecognisedByTheLoader(string discriminator)
    {
        // A clause whose only key is the discriminator. Some kinds will still reject this
        // minimal shape for a missing value — that's fine; the one error that must never
        // appear is the loader failing to recognise the discriminator at all.
        var jaml = $"must:\n  - {discriminator}: 1\n";
        JamlConfigLoader.TryLoad(jaml, out _, out var error);

        Assert.False(
            error?.Contains("no recognised discriminator", StringComparison.OrdinalIgnoreCase) ?? false,
            $"'{discriminator}' is in JamlDiscriminatorRegistry but the loader does not recognise it: {error}");
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
}
