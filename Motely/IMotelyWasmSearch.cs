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
    void Cancel();
    Task<MotelyWasmSearchCompletion> WaitForCompletion();
    /// <summary>
    /// Pulls up to <paramref name="max"/> buffered scored results out of the search.
    /// Pull-based alternative to subscribing to <see cref="IMotelyWasmEvents.NotifyResult"/>;
    /// consumers that prefer polling (e.g. for backpressure / chunked rendering) use this
    /// instead of holding live event subscriptions. Returns an empty array when buffer is empty.
    /// </summary>
    MotelyWasmSearchResult[] DrainResults(int max);
}

public sealed class MotelyWasmSearch(IMotelySearch search) : IMotelyWasmSearch
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<MotelyWasmSearchResult> _buffer = new();

    /// <summary>Host-side enqueue: called from MotelyWasmHost's scored-result callback.</summary>
    internal void EnqueueResult(MotelyWasmSearchResult result) => _buffer.Enqueue(result);

    public MotelyWasmSearchResult[] DrainResults(int max)
    {
        if (max <= 0) return Array.Empty<MotelyWasmSearchResult>();
        var results = new List<MotelyWasmSearchResult>(Math.Min(max, 256));
        while (results.Count < max && _buffer.TryDequeue(out var r))
            results.Add(r);
        return results.ToArray();
    }

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
