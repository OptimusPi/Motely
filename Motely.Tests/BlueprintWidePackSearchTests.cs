using Motely.Filters;
using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// "Go wide, let scoring tighten" pattern: declare every booster slot you'd accept
/// (0..15 covers base + Hieroglyph + Petroglyph rewind reachability), let the SIMD
/// pre-filter cast a deliberately wide net, and rely on the scoring pass to enforce
/// the realized per-seed cap (which depends on whether/when those vouchers actually
/// fired in the run).
///
/// JAML expresses *intent* ("any of these slots would be fine"). The scoring layer
/// expresses *game rules* ("could this run actually reach that slot"). The two
/// don't need to know about each other.
///
/// Seed <c>TEMPSEED</c> is a placeholder — this test will fail until replaced with
/// a real Blueprint-containing seed found by running this same JAML through the
/// search tool.
/// </summary>
public sealed class BlueprintWidePackSearchTests
{
    private const string PlaceholderSeed = "TEMPSEED";

    [Fact]
    public void Blueprint_WidePackSlots_FindsPlaceholderSeed()
    {
        const string Jaml = """
            name: BlueprintWidePacks
            deck: Red
            stake: White
            must:
              - joker: Blueprint
                antes: [1]
                sources:
                  rareShopJokers: [0, 1, 2, 3, 4, 5]
                  boosterPacks: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(Jaml, out var config, out var error),
            $"JAML parse failed: {error}\n{Jaml}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([PlaceholderSeed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        search.AwaitCompletion();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }
}
