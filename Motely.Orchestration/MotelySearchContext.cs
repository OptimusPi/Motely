using System;
using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Minimal search context wrapper - just delegates to IMotelySearch.
/// </summary>
public sealed class MotelySearchContext : IMotelySearchContext
{
    private readonly IMotelySearch _search;

    public MotelySearchContext(IMotelySearch search, string searchId, string filterId)
    {
        _search = search;
        SearchId = searchId;
        FilterId = filterId;
    }

    public string SearchId { get; }
    public string FilterId { get; }
    public int ResultCount => 0; // Results flow via callback, not stored
    public IReadOnlyList<string> ColumnNames => Array.Empty<string>();
    public List<MotelySearchResultRow> GetResults(int offset, int limit) => new();
    public List<MotelySearchResultRow> GetTopResults(int limit = 1000) => new();

    // IMotelySearch delegation
    public MotelySearchStatus Status => _search.Status;
    public bool IsSequentialBatchSearch => _search.IsSequentialBatchSearch;
    public long BatchIndex => _search.BatchIndex;
    public long CompletedBatchCount => _search.CompletedBatchCount;
    public TimeSpan ElapsedTime => _search.ElapsedTime;
    public long TotalSeedsSearched => _search.TotalSeedsSearched;
    public long MatchingSeeds => _search.MatchingSeeds;
    public long FilteredSeeds => _search.FilteredSeeds;

    public void Start(CancellationToken cancellationToken = default) => _search.Start(cancellationToken);
    public void AwaitCompletion() => _search.AwaitCompletion();
    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default) => _search.WaitForCompletionAsync(cancellationToken);
    public void Cancel() => _search.Cancel();
    public void ForceProgressReport() => _search.ForceProgressReport();
    public void Dispose() => _search.Dispose();
}
