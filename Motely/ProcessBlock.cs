using System.Collections.Generic;
using Motely.Filters;

namespace Motely;

/// <summary>
/// Result of running one block of sequential search. Returned by ProcessBlock.
/// NOTWE from YOUR COMMANDER!!! YOU MUST OBEY! MAKE IT WORK THIS WAY!
/// </summary>
public sealed record BlockSearchResult(
    int BlockId,
    int SeedsFound,
    int HighestScore,
    IReadOnlyCollection<string> Seeds
);

/// <summary>
/// Runs sequential search for exactly one block. Single call = one block done.
/// Used by DistributedWorker and Node addon; no start/poll/stop.
/// </summary>
public static class ProcessBlockRunner
{
    /// <summary>
    /// Characters per batch (how many chars vary within one block).
    /// Each block searches 35^5 = 52,521,875 seeds.
    /// Total blocks = 35^(MaxSeedLength - BatchCharCount) = 42,875.
    /// </summary>
    public const int BatchCharCount = 5;

    /// <summary>Total number of blocks. Derived: 35^(MaxSeedLength - BatchCharCount).</summary>
    public static readonly int TotalBlocks = ComputePower(
        MotelyCore.SeedDigits.Length,
        MotelyCore.MaxSeedLength - BatchCharCount);

    private static int ComputePower(int baseVal, int exp)
    {
        int result = 1;
        for (int i = 0; i < exp; i++)
            result *= baseVal;
        return result;
    }

    /// <summary>
    /// Run one block of sequential search. Parses JAML, runs block [blockId, blockId+1), returns result.
    /// </summary>
    /// <param name="jamlContent">Full JAML filter string (deck/stake in JAML).</param>
    /// <param name="blockId">Block index 0 .. <see cref="TotalBlocks"/> - 1.</param>
    /// <param name="cancellationToken">Cancel the search.</param>
    /// <returns>Block result with seeds found; or null if JAML invalid.</returns>
    public static async Task<BlockSearchResult?> ProcessBlockAsync(
        string jamlContent,
        int blockId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jamlContent))
            return null;
        if (blockId < 0 || blockId >= TotalBlocks)
            return null;

        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out _) || config is null)
            return null;

        var seeds = new List<string>();
        int highestScore = 0;

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithThreadCount(Environment.ProcessorCount)
            .WithBatchCharacterCount(BatchCharCount)
            .WithStartBatchIndex(blockId)
            .WithEndBatchIndex(blockId + 1)
            .WithSequentialSearch();

        if (plan.ShouldClauseCount > 0)
            settings = settings.WithScoredResultCallback(tally =>
            {
                seeds.Add(tally.Seed);
                if (tally.Score > highestScore) highestScore = tally.Score;
            });
        else
            settings = settings.WithSeedMatchCallback(seed => seeds.Add(seed));

        using var search = settings.CreateSearch();
        search.Start(cancellationToken);
        await search.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);

        return new BlockSearchResult(
            BlockId: blockId,
            SeedsFound: seeds.Count,
            HighestScore: highestScore,
            Seeds: seeds
        );
    }
}
