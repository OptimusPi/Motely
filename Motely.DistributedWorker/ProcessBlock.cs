using Motely;
using Motely.Filters;
using Motely.Filters.Jaml;

namespace Motely.DistributedWorker;

/// <summary>
/// Result of running one block of distributed search.
/// 1 block = 35^BatchCharCount seeds (default 35^5 = 52,521,875).
/// </summary>
public sealed record BlockSearchResult(
    int BlockId,
    int SeedsFound,
    int HighestScore,
    IReadOnlyCollection<string> Seeds
);

/// <summary>
/// Converts a block index into Motely search params and runs one block.
/// Block is a DistributedWorker concept — core Motely just takes batch ranges.
/// </summary>
public static class ProcessBlockRunner
{
    /// <summary>
    /// Default characters per batch. Each block searches 35^BatchCharCount seeds.
    /// Helpers can configure a lower batchCharCount for faster I/O responsiveness,
    /// but the block still represents the same search space — just split into more batches.
    /// </summary>
    public const int DefaultBatchCharCount = 5;

    /// <summary>Total number of blocks at default batch char count.</summary>
    public static readonly int TotalBlocks = ComputePower(
        MotelyGlobals.SeedDigits.Length,
        MotelyGlobals.MaxSeedLength - DefaultBatchCharCount);

    /// <summary>Seeds per block at default batch char count.</summary>
    public static readonly long SeedsPerBlock = ComputePowerLong(
        MotelyGlobals.SeedDigits.Length, DefaultBatchCharCount);

    private static int ComputePower(int baseVal, int exp)
    {
        int result = 1;
        for (int i = 0; i < exp; i++)
            result *= baseVal;
        return result;
    }

    private static long ComputePowerLong(int baseVal, int exp)
    {
        long result = 1;
        for (int i = 0; i < exp; i++)
            result *= baseVal;
        return result;
    }

    /// <summary>
    /// Convert a block index + optional helper batchCharCount into the correct
    /// startBatch/endBatch params for core Motely's search settings.
    /// </summary>
    public static (long StartBatch, long EndBatch, int BatchCharCount) BlockToSearchParams(
        int blockId, int helperBatchCharCount = DefaultBatchCharCount)
    {
        if (helperBatchCharCount == DefaultBatchCharCount)
            return (blockId, blockId + 1, DefaultBatchCharCount);

        // Helper wants finer batches. Convert block range into the helper's batch space.
        int charDiff = DefaultBatchCharCount - helperBatchCharCount;
        long batchesPerBlock = ComputePowerLong(MotelyGlobals.SeedDigits.Length, charDiff);
        long startBatch = blockId * batchesPerBlock;
        long endBatch = startBatch + batchesPerBlock;
        return (startBatch, endBatch, helperBatchCharCount);
    }

    /// <summary>
    /// Run one block of sequential search. Parses JAML, converts block to batch params, runs, returns result.
    /// </summary>
    public static async Task<BlockSearchResult?> ProcessBlockAsync(
        string jamlConfig,
        int blockId,
        int batchCharCount = DefaultBatchCharCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jamlConfig))
            return null;
        if (blockId < 0 || blockId >= TotalBlocks)
            return null;

        if (!JamlConfigLoader.TryLoad(jamlConfig, out var config, out _) || config is null)
            return null;

        var (startBatch, endBatch, effectiveBatchCharCount) = BlockToSearchParams(blockId, batchCharCount);

        var seeds = new List<string>();
        int highestScore = 0;

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithThreadCount(Environment.ProcessorCount)
            .WithBatchCharacterCount(effectiveBatchCharCount)
            .WithStartBatchIndex(startBatch)
            .WithEndBatchIndex(endBatch)
            .WithSequentialSearch();

        if (plan.ScoreTallyColumnCount > 0)
            settings = settings.WithScoredResultCallback(tally =>
            {
                seeds.Add(tally.Seed);
                if (tally.Score > highestScore) highestScore = tally.Score;
            });
        else
            settings = settings.WithSeedMatchCallback(seed => seeds.Add(seed));

        using var search = settings.Start(cancellationToken);
        await search.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);

        return new BlockSearchResult(
            BlockId: blockId,
            SeedsFound: seeds.Count,
            HighestScore: highestScore,
            Seeds: seeds
        );
    }
}
