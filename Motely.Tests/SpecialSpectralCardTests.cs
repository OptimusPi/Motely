using Motely.Filters;
using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// The two special spectral cards — TheSoul (Arcana + Spectral packs) and BlackHole
/// (Celestial + Spectral packs) — routed through the existing <c>spectralCard:</c> discriminator
/// to <see cref="SpecialSpectralCardFilterDesc"/>. Ground truth asserted directly against the
/// feature with known seeds the user supplied.
/// </summary>
public sealed class SpecialSpectralCardTests
{
    private static int CountMatches(string jaml, string seed)
    {
        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}\n{jaml}"
        );

        using var search = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .Start();
        search.AwaitCompletion();

        return (int)search.MatchingSeeds;
    }

    // FACT (user): famous seed ALEEB has 5 TheSoul across antes 1-8.
    // min:5 must still match, min:6 must not -> the count is exactly 5.
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 0)]
    public void ALEEB_TheSoul_AntesOneToEight_IsExactlyFive(int min, int expectedMatches)
    {
        var jaml = $"""
            name: aleeb-the-soul
            deck: Red
            stake: White
            must:
              - spectralCard: TheSoul
                antes: [1, 2, 3, 4, 5, 6, 7, 8]
                min: {min}
                sources:
                  boosterPacks: [0, 1, 2, 3, 4, 5]
            """;

        Assert.Equal(expectedMatches, CountMatches(jaml, "ALEEB"));
    }

    // FACT (user): seed LHOLEY56 contains a Black Hole.
    [Fact]
    public void LHOLEY56_BlackHole_IsFound()
    {
        const string Jaml = """
            name: lholey-black-hole
            deck: Red
            stake: White
            must:
              - spectralCard: BlackHole
                antes: [1, 2, 3, 4, 5, 6, 7, 8]
                sources:
                  boosterPacks: [0, 1, 2, 3, 4, 5]
            """;

        Assert.Equal(1, CountMatches(Jaml, "LHOLEY56"));
    }
}
