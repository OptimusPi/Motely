using Motely.Filters;
using Motely.Filters.Jaml;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Regression coverage for Tacodiva/Motely#5 — the cached-partial-hash handoff from
/// base filter to additional filter in <see cref="MotelySearch{T}.MotelySearchPlan.BatchSeeds"/>.
///
/// Multi-clause joker filters register two or more pseudo-hash key lengths. When a partial
/// batch (fewer than 8 seeds) is handed off via <c>WithAdditionalFilter</c>, the source read
/// must be <c>(double*)Cache[keyLength])[lane]</c>. Using <c>[i * 8 + lane]</c> instead
/// reads out-of-bounds starting at i = 1 and corrupts the cached hashes fed to the inner
/// filter, which silently rejects seeds that should match.
///
/// This test was originally deleted in the Feb 19 2026 "refactor" commit (de506102); restoring
/// it under the current JAML API so the regression window cannot reopen.
/// </summary>
public sealed class ChainedMustClauseSeedTests
{
    [Fact]
    public void ChainedMustClauses_SingleSeed_C7AOGOYY_ShouldMatch()
    {
        // Two distinct joker clauses in `must` → JAML compiles this as
        // base filter (rareJoker: Baron) + additional filter (uncommonJoker: Mime),
        // which is exactly the handoff path guarded by this regression.
        const string Jaml = """
            name: ChainedMustRegressionTest
            deck: Ghost
            stake: Black
            must:
              - rareJoker: Baron
                antes: [1, 2, 3, 4]
              - uncommonJoker: Mime
                antes: [1, 2, 3, 4]
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(Jaml, out var config, out var error),
            $"JAML parse failed: {error}\n{Jaml}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch(["C7AOGOYY"], 1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        search.AwaitCompletion();

        Assert.Equal(1, search.TotalSeedsSearched);
        Assert.Equal(1, search.MatchingSeeds);
    }
}
