using System.Collections.Concurrent;
using System.Collections.Generic;
using Motely.Filters;

namespace Motely;

public enum MotelyWasmSearchState
{
    Running,
    Completed,
    Cancelled,
    Faulted,
}

public record MotelyWasmSearchResult(string Seed, int Score, int[] TallyColumns);

public record MotelyWasmSearchSnapshot(
    long ElapsedMs,
    long TotalSeedsSearched,
    long MatchingSeeds,
    long FilteredSeeds,
    bool IsCompleted,
    bool IsSequentialBatchSearch,
    long BatchIndex,
    long CompletedBatchCount
);

public record MotelyWasmSearchCompletion(
    MotelyWasmSearchState State,
    long TotalSeedsSearched,
    long MatchingSeeds,
    string? Error
);

public record MotelyWasmSearchBatchResult(
    MotelyWasmSearchCompletion Completion,
    MotelyWasmSearchResult[] Results
);

public interface IMotelyWasmSearch : IDisposable
{
    MotelyWasmSearchSnapshot GetSnapshot();
    MotelyWasmSearchResult[] DrainResults(int maxCount);
    void Cancel();
    Task<MotelyWasmSearchCompletion> WaitForCompletion();
}

public sealed class MotelyWasmSearch(
    IMotelySearch search,
    ConcurrentQueue<MotelyWasmSearchResult> resultsQueue
) : IMotelyWasmSearch
{
    public MotelyWasmSearchSnapshot GetSnapshot()
    {
        return new(
            search.ElapsedMs,
            search.TotalSeedsSearched,
            search.MatchingSeeds,
            search.FilteredSeeds,
            search.IsCompleted,
            search.IsSequentialBatchSearch,
            search.BatchIndex,
            search.CompletedBatchCount
        );
    }

    public MotelyWasmSearchResult[] DrainResults(int maxCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);

        var results = new List<MotelyWasmSearchResult>(maxCount);
        for (int i = 0; i < maxCount && resultsQueue.TryDequeue(out var result); i++)
            results.Add(result);
        return [.. results];
    }

    public void Cancel()
    {
        search.Cancel();
    }

    public async Task<MotelyWasmSearchCompletion> WaitForCompletion()
    {
        try
        {
            await search.WaitForCompletionAsync();
            return new(
                MotelyWasmSearchState.Completed,
                search.TotalSeedsSearched,
                search.MatchingSeeds,
                null
            );
        }
        catch (OperationCanceledException)
        {
            return new(
                MotelyWasmSearchState.Cancelled,
                search.TotalSeedsSearched,
                search.MatchingSeeds,
                null
            );
        }
        catch (Exception ex)
        {
            return new(
                MotelyWasmSearchState.Faulted,
                search.TotalSeedsSearched,
                search.MatchingSeeds,
                ex.Message
            );
        }
    }

    public void Dispose()
    {
        search.Dispose();
    }
}
