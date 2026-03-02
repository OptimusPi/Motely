using System.Collections.Concurrent;
using Motely.Filters;
using Xunit;
using Xunit.Abstractions;

namespace Motely.Tests;

/// <summary>
/// Integration tests that run actual JAML searches and verify consistent results.
/// These tests do NOT hardcode "known matching seeds" — seed results can shift
/// whenever the PRNG implementation is corrected (e.g. the FMA rounding fix).
/// Instead, we verify behavioral invariants:
///   • Non-matching seeds → 0 results
///   • Same filter → same results across thread counts
///   • Callback captures exactly the seeds the engine reports
/// </summary>
public class SearchConsistencyTests(ITestOutputHelper output)
{
    /// <summary>
    /// A simple JAML filter that should match *some* seeds in a sequential search.
    /// Uses a broad filter (any joker in antes 1-2) so we're likely to find hits.
    /// </summary>
    private const string SimpleJaml = """
        name: SimpleTest
        deck: Red
        stake: White
        must:
          - joker: Showman
            antes: [1,2]
            sources:
              shopItems: [0,1]
              boosterPacks: [0,1]
        """;

    /// <summary>
    /// Non-matching seeds should produce 0 matches.
    /// </summary>
    [Fact]
    public async Task NonMatchingSeeds_ProduceZeroMatches()
    {
        Assert.True(JamlConfigLoader.TryLoad(SimpleJaml, out var config, out _));

        string[] badSeeds = ["AAAAAAAA", "BBBBBBBB", "CCCCCCCC"];
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch(badSeeds, badSeeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        output.WriteLine($"MatchingSeeds: {search.MatchingSeeds}");
        Assert.Equal(0, search.MatchingSeeds);
    }

    /// <summary>
    /// Multi-threaded searches must produce identical match counts.
    /// We first discover how many matches exist with 1 thread,
    /// then verify 2 and 4 threads produce the same count.
    /// </summary>
    [Fact]
    public async Task MatchCount_ConsistentAcrossThreadCounts()
    {
        Assert.True(JamlConfigLoader.TryLoad(SimpleJaml, out var config, out _));

        // Use a small sequential search (3-char batch = 35^3 = ~42K seeds)
        // to find a baseline match count
        var baseline = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSequentialSearch()
            .WithBatchCharacterCount(2) // 35^2 = 1225 seeds — fast!
            .WithStartBatchIndex(0)
            .WithEndBatchIndex(1) // One batch
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search1 = baseline.Start();
        await search1.WaitForCompletionAsync();

        long baselineMatches = search1.MatchingSeeds;
        long baselineSearched = search1.TotalSeedsSearched;
        output.WriteLine(
            $"Baseline: searched={baselineSearched}, matched={baselineMatches}"
        );

        // Now verify 2-thread and 4-thread produce the same match count
        foreach (int threadCount in new[] { 2, 4 })
        {
            Assert.True(JamlConfigLoader.TryLoad(SimpleJaml, out var cfg, out _));
            var settings = JamlSearchBuilder
                .CreateSettings(cfg!)
                .WithSequentialSearch()
                .WithBatchCharacterCount(2)
                .WithStartBatchIndex(0)
                .WithEndBatchIndex(1)
                .WithThreadCount(threadCount)
                .WithQuietMode(true);

            using var search = settings.Start();
            await search.WaitForCompletionAsync();

            output.WriteLine(
                $"threads={threadCount}: searched={search.TotalSeedsSearched}, matched={search.MatchingSeeds}"
            );

            Assert.Equal(baselineMatches, search.MatchingSeeds);
        }
    }

    /// <summary>
    /// Callback should capture seed strings for every match.
    /// We run a list search with a set of fake seeds and verify
    /// that the callback fires exactly for the matched seeds.
    /// </summary>
    [Fact]
    public async Task Callback_FiresForMatchedSeeds()
    {
        Assert.True(JamlConfigLoader.TryLoad(SimpleJaml, out var config, out _));

        var capturedSeeds = new ConcurrentBag<string>();

        // Use sequential search on a tiny batch to find some seeds
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSequentialSearch()
            .WithBatchCharacterCount(2)
            .WithStartBatchIndex(0)
            .WithEndBatchIndex(1)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithSeedMatchCallback(line =>
            {
                // Parse seed from CSV line: SEED,SCORE,...
                var comma = line.IndexOf(',');
                if (comma > 0)
                    capturedSeeds.Add(line[..comma]);
                else
                    capturedSeeds.Add(line.Trim());
            });

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        output.WriteLine($"Matched: {search.MatchingSeeds}, Captured: {capturedSeeds.Count}");

        // The callback should fire for every match
        Assert.Equal(search.MatchingSeeds, capturedSeeds.Count);
    }

    /// <summary>
    /// Verifies the JAML parser + search builder pipeline doesn't throw
    /// on a complex multi-clause filter.
    /// </summary>
    [Fact]
    public async Task ComplexFilter_RunsWithoutErrors()
    {
        var jaml = """
            name: Complex
            deck: Anaglyph
            stake: White
            must:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
              - legendaryJoker: Perkeo
                antes: [1,2,3,4,5,6]
                sources:
                  boosterPacks: [0,1,2,3,4,5]
            should:
              - joker: Showman
                antes: [1,2]
                sources:
                  shopItems: [0,1]
                  boosterPacks: [0,1]
                score: 100
            """;

        Assert.True(
            JamlConfigLoader.TryLoad(jaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithSequentialSearch()
            .WithBatchCharacterCount(2)
            .WithStartBatchIndex(0)
            .WithEndBatchIndex(1)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.WaitForCompletionAsync();

        output.WriteLine(
            $"ComplexFilter: searched={search.TotalSeedsSearched}, matched={search.MatchingSeeds}"
        );

        // Just verify it completes without exceptions and searches some seeds
        Assert.True(search.TotalSeedsSearched > 0, "Should have searched some seeds");
        Assert.True(search.IsCompleted, "Search should be completed");
    }
}

