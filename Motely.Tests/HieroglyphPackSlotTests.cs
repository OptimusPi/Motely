using Motely.Filters;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Regression tests for the Hieroglyph / Petroglyph scenario: these vouchers reset progression
/// one ante backward and re-open the shop, effectively unlocking pack slots 4 and 5 in ante 1
/// (which normally caps at 4 packs / slots 0..3). The booster-pack PRNG stream actually has six
/// packs of output at every ante — the per-ante gameplay limit is just the default reachability.
///
/// Seed <c>KHTW99TC</c> is the canonical example: a Negative-edition Perkeo appears in the
/// ante-1 Arcana at pack slot 5, only accessible after buying Hieroglyph in ante 2 to rewind
/// to ante 1.
///
/// These tests pin the filter's ability to match that seed so future per-ante clamping work
/// does not silently remove Hieroglyph-accessible matches. If a clamp is added, it must either
/// leave explicit <c>boosterPacks</c> lists alone, or gate itself behind an opt-in field.
/// </summary>
public class HieroglyphPackSlotTests
{
    private const string HieroglyphPerkeoSeed = "KHTW99TC";

    private static (long SeedsSearched, long MatchingSeeds) RunSingleSeedJaml(string jaml, string seed)
    {
        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}\n{jaml}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch([seed], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        search.AwaitCompletion();
        return (search.TotalSeedsSearched, search.MatchingSeeds);
    }

    /// <summary>
    /// KHTW99TC: ante-1 pack slot 5 contains a Negative Perkeo (Hieroglyph-reachable only).
    /// Opt into Hieroglyph scanning with <c>earlyAntesMaxPack: 5</c>. Without the opt-in, ante 1
    /// scoring clamps to slot 3 (normal gameplay) and the Perkeo is invisible.
    /// </summary>
    [Fact]
    public void KHTW99TC_HasNegativePerkeo_InAnte1_Slot5_WhenEarlyAntesMaxPackIs5()
    {
        var jaml = """
            name: HieroglyphPerkeo
            deck: Red
            stake: White
            must:
              - legendaryJoker: Perkeo
                edition: Negative
                antes: [1]
                sources:
                  boosterPacks: [5]
                  earlyAntesMaxPack: 5
            """;

        var result = RunSingleSeedJaml(jaml, HieroglyphPerkeoSeed);
        Assert.Equal(1, result.SeedsSearched);
        Assert.Equal(1, result.MatchingSeeds);
    }

    /// <summary>
    /// Full slot range [0..5] on ante 1 with explicit Hieroglyph opt-in — must match.
    /// </summary>
    [Fact]
    public void KHTW99TC_HasNegativePerkeo_InAnte1_FullSlotRange_WithHieroglyphOptIn()
    {
        var jaml = """
            name: HieroglyphPerkeoFullRange
            deck: Red
            stake: White
            must:
              - legendaryJoker: Perkeo
                edition: Negative
                antes: [1]
                sources:
                  boosterPacks: [0, 1, 2, 3, 4, 5]
                  earlyAntesMaxPack: 5
            """;

        var result = RunSingleSeedJaml(jaml, HieroglyphPerkeoSeed);
        Assert.Equal(1, result.SeedsSearched);
        Assert.Equal(1, result.MatchingSeeds);
    }

    /// <summary>
    /// Clamping sanity: restricting to slots [0..3] on ante 1 must NOT match this seed, because
    /// the Perkeo is specifically at slot 5 (only reachable with Hieroglyph). This pins the
    /// behaviour that an explicit restricted list is honoured exactly.
    /// </summary>
    [Fact]
    public void KHTW99TC_DoesNotMatch_WhenRestrictedTo_NormalAnte1_Slots()
    {
        var jaml = """
            name: HieroglyphPerkeoRestricted
            deck: Red
            stake: White
            must:
              - legendaryJoker: Perkeo
                edition: Negative
                antes: [1]
                sources:
                  boosterPacks: [0, 1, 2, 3]
            """;

        var result = RunSingleSeedJaml(jaml, HieroglyphPerkeoSeed);
        Assert.Equal(1, result.SeedsSearched);
        Assert.Equal(0, result.MatchingSeeds);
    }

    /// <summary>
    /// Default behaviour (no <c>earlyAntesMaxPack</c>): ante 1 scoring caps at slot 3 even if the
    /// user asks for slot 5. The Hieroglyph-only Perkeo must NOT match — this protects filters
    /// written for normal gameplay from phantom false positives.
    /// </summary>
    [Fact]
    public void KHTW99TC_DoesNotMatch_OnAnte1Slot5_WithDefaultEarlyAntesMaxPack()
    {
        var jaml = """
            name: HieroglyphPerkeoDefault
            deck: Red
            stake: White
            must:
              - legendaryJoker: Perkeo
                edition: Negative
                antes: [1]
                sources:
                  boosterPacks: [5]
            """;

        var result = RunSingleSeedJaml(jaml, HieroglyphPerkeoSeed);
        Assert.Equal(1, result.SeedsSearched);
        Assert.Equal(0, result.MatchingSeeds);
    }

    /// <summary>
    /// Bare clause with no antes/sources gets the loader defaults stamped: antes [1..8] and
    /// boosterPacks [0..5] with earlyAntesMaxPack = 3. Ante 1 slot 5 is blocked, but the seed
    /// might still match via another ante — this test only confirms the clause compiles cleanly
    /// and does not produce a parse error with the new field.
    /// </summary>
    [Fact]
    public void BareLegendaryJokerClause_CompilesAndRuns_WithDefaultEarlyAntesMaxPack()
    {
        var jaml = """
            name: HieroglyphPerkeoBare
            deck: Red
            stake: White
            must:
              - legendaryJoker: Perkeo
            """;

        // We don't assert match/no-match here because the seed may or may not have Perkeo in
        // other antes; this test pins that the bare form still parses and runs.
        var result = RunSingleSeedJaml(jaml, HieroglyphPerkeoSeed);
        Assert.Equal(1, result.SeedsSearched);
    }
}
