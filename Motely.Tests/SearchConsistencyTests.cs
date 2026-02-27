using System.Collections.Concurrent;
using Motely.Filters;
using Xunit;
using Xunit.Abstractions;

namespace Motely.Tests;

/// <summary>
/// Integration tests that run actual JAML searches and verify consistent results.
/// Uses single-threaded list search for deterministic behavior in tests.
/// </summary>
public class SearchConsistencyTests(ITestOutputHelper output)
{
    private const string ShowmanJaml = """
        name: Showman
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

    /// <summary>
    /// Seeds known to match the Showman filter (from manual CLI testing).
    /// </summary>
    private static readonly string[] KnownMatchingSeeds = ["6CDS1K57", "B5HN237J"];

    /// <summary>
    /// Basic smoke test: parse JAML, run single-threaded search on known seeds,
    /// verify matching seed count matches expectations.
    /// </summary>
    [Fact]
    public async Task KnownSeeds_MatchWithSingleThread()
    {
        Assert.True(
            JamlConfigLoader.TryLoad(ShowmanJaml, out var config, out var error),
            $"JAML parse failed: {error}"
        );

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch(KnownMatchingSeeds, KnownMatchingSeeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.Start(CancellationToken.None);

        output.WriteLine($"MatchingSeeds: {search.MatchingSeeds}");
        output.WriteLine($"SearchedSeeds: {search.TotalSeedsSearched}");

        Assert.Equal(KnownMatchingSeeds.Length, search.MatchingSeeds);
    }

    /// <summary>
    /// Non-matching seeds should produce 0 matches.
    /// </summary>
    [Fact]
    public async Task NonMatchingSeeds_ProduceZeroMatches()
    {
        Assert.True(JamlConfigLoader.TryLoad(ShowmanJaml, out var config, out _));

        string[] badSeeds = ["AAAAAAAA", "BBBBBBBB", "CCCCCCCC"];
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch(badSeeds, badSeeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.Start(CancellationToken.None);

        output.WriteLine($"MatchingSeeds: {search.MatchingSeeds}");
        Assert.Equal(0, search.MatchingSeeds);
    }

    /// <summary>
    /// Multi-threaded search must find the same number of matching seeds.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task MatchCount_ConsistentAcrossThreadCounts(int threadCount)
    {
        Assert.True(JamlConfigLoader.TryLoad(ShowmanJaml, out var config, out _));

        // Pad with non-matching seeds so batching works properly
        var allSeeds = KnownMatchingSeeds
            .Concat([
                "AAAAAAAA",
                "BBBBBBBB",
                "CCCCCCCC",
                "DDDDDDDD",
                "EEEEEEEE",
                "FFFFFFFF",
                "GGGGGGGG",
                "HHHHHHHH",
                "IIIIIIII",
                "JJJJJJJJ",
                "KKKKKKKK",
                "LLLLLLLL",
                "MMMMMMMM",
                "NNNNNNNN",
                "OOOOOOOO",
                "PPPPPPPP",
            ])
            .ToArray();

        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch(allSeeds, allSeeds.Length)
            .WithThreadCount(threadCount)
            .WithQuietMode(true);

        using var search = settings.Start();
        await search.Start(CancellationToken.None);

        output.WriteLine(
            $"threads={threadCount}: searched={search.TotalSeedsSearched}, matched={search.MatchingSeeds}"
        );
        Assert.Equal(KnownMatchingSeeds.Length, search.MatchingSeeds);
    }

    /// <summary>
    /// Callback-based test: verify the actual seed strings are reported.
    /// Sorted for deterministic comparison across thread counts.
    /// </summary>
    [Fact]
    public async Task CallbackReportsCorrectSeeds()
    {
        Assert.True(JamlConfigLoader.TryLoad(ShowmanJaml, out var config, out _));

        var capturedSeeds = new ConcurrentBag<string>();
        var settings = JamlSearchBuilder
            .CreateSettings(config!)
            .WithListSearch(KnownMatchingSeeds, KnownMatchingSeeds.Length)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithSeedMatchCallback(line =>
            {
                // Parse seed from CSV line: SEED,SCORE,...
                var comma = line.IndexOf(',');
                if (comma > 0)
                    capturedSeeds.Add(line[..comma]);
            });

        using var search = settings.Start();
        await search.Start(CancellationToken.None);

        var sorted = capturedSeeds.OrderBy(x => x).ToArray();
        var expected = KnownMatchingSeeds.OrderBy(x => x).ToArray();

        output.WriteLine($"Captured: [{string.Join(", ", sorted)}]");
        output.WriteLine($"Expected: [{string.Join(", ", expected)}]");

        Assert.Equal(expected, sorted);
    }
}
