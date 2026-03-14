using System.Collections.Generic;
using Motely.Filters;

namespace Motely;

/// <summary>
/// One seed match (seed string + score). Used in BlockSearchResult.
/// </summary>
public sealed record BlockSeedResult(string Seed, int Score);

/// <summary>
/// Result of running one block of sequential search. Returned by ProcessBlock.
/// NOTE: Later we can add long[] SeedIndices (bijective 0..~2.3T) to reduce bandwidth.
/// </summary>
public sealed record BlockSearchResult(
    int BlockId,
    long SeedsSearched,
    int SeedsFound,
    IReadOnlyList<BlockSeedResult> Seeds
);

/// <summary>
/// Runs sequential search for exactly one block. Single call = one block done.
/// Used by DistributedWorker and Node addon; no start/poll/stop.
/// </summary>
public static class ProcessBlockRunner
{
    public const int DefaultBatchCharCount = 5;

    /// <summary>
    /// Run one block of sequential search. Parses JAML, runs block [blockId, blockId+1), returns result.
    /// </summary>
    /// <param name="jamlContent">Full JAML filter string (deck/stake in JAML).</param>
    /// <param name="blockId">Block index 0 .. 42,874.</param>
    /// <param name="cancellationToken">Cancel the search.</param>
    /// <returns>Block result with seeds found; or null if JAML invalid.</returns>
    public static async Task<BlockSearchResult?> ProcessBlockAsync(
        string jamlContent,
        int blockId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jamlContent))
            return null;
        if (blockId < 0 || blockId >= 35 * 35 * 35)
            return null;

        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out _) || config is null)
            return null;

        var seeds = new List<BlockSeedResult>();
        var settings = JamlSearchBuilder.CreatePlan(config).Settings
            .WithThreadCount(Environment.ProcessorCount)
            .WithBatchCharacterCount(DefaultBatchCharCount)
            .WithStartBatchIndex(blockId)
            .WithEndBatchIndex(blockId + 1)
            .WithSequentialSearch()
            .WithSeedMatchCallback(line =>
            {
                int comma = line.IndexOf(',');
                string seed;
                int score = 0;
                if (comma < 0)
                {
                    seed = line;
                }
                else
                {
                    seed = line[..comma];
                    if (comma + 1 < line.Length)
                    {
                        var rest = line[(comma + 1)..];
                        int c2 = rest.IndexOf(',');
                        var scoreSpan = c2 >= 0 ? rest.AsSpan(0, c2) : rest.AsSpan();
                        int.TryParse(scoreSpan, out score);
                    }
                }
                seeds.Add(new BlockSeedResult(seed, score));
            });

        using var search = settings.CreateSearch();
        search.Start(cancellationToken);
        await search.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);

        return new BlockSearchResult(
            BlockId: blockId,
            SeedsSearched: search.TotalSeedsSearched,
            SeedsFound: seeds.Count,
            Seeds: seeds
        );
    }
}
