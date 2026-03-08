using System;
using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Minimal search context wrapper - delegates to IMotelySearch.
/// Start() wraps the synchronous search in Task.Run so UI layers can poll progress.
/// </summary>
public sealed class MotelySearchContext : IMotelySearchContext
{
    private readonly IMotelySearch _search;
    private Task? _runTask;

    public MotelySearchContext(IMotelySearch search, string filterId)
    {
        _search = search;
        FilterId = filterId;
    }

    public string FilterId { get; }
    public bool IsCompleted => _search.IsCompleted;
    public bool IsSequentialBatchSearch => _search.IsSequentialBatchSearch;
    public long BatchIndex => _search.BatchIndex;
    public long CompletedBatchCount => _search.CompletedBatchCount;
    public TimeSpan ElapsedTime => _search.ElapsedTime;
    public long TotalSeedsSearched => _search.TotalSeedsSearched;
    public long MatchingSeeds => _search.MatchingSeeds;
    public long FilteredSeeds => _search.FilteredSeeds;
    public int ResultCount => 0; // Results flow via callback, not stored
    public IReadOnlyList<string> ColumnNames => Array.Empty<string>();

    public Task Start(CancellationToken cancellationToken = default)
    {
        if (_runTask is { IsCompleted: false })
            return _runTask;
        _runTask = Task.Run(() => _search.Start().WaitForCompletionAsync(cancellationToken));
        return _runTask;
    }

    public List<MotelySearchResultRow> GetResults(int offset, int limit)
    {
        // Results flow via callback, not stored - return empty list
        return new List<MotelySearchResultRow>();
    }

    public List<MotelySearchResultRow> GetTopResults(int limit = 1000)
    {
        // Results flow via callback, not stored - return empty list
        return new List<MotelySearchResultRow>();
    }

    public void Dispose() => _search.Dispose();
}
