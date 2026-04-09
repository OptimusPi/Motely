#nullable enable
using Motely;

namespace Motely.BrowserWasm;

/// <summary>
/// JS-facing search handle: methods only (Bootsharp does not emit valid glue for interface property getters on <see cref="IMotelySearch"/>).
/// </summary>
public interface IMotelySearchSession : IDisposable
{
    long GetTotalSeedsSearched();
    long GetMatchingSeeds();
    long GetFilteredSeeds();
    bool GetIsCompleted();
    bool GetIsSequentialBatchSearch();
    long GetBatchIndex();
    long GetCompletedBatchCount();
    void Cancel();
    Task WaitForCompletionAsync(CancellationToken cancellationToken = default);
}

public sealed class MotelySearchSession : IMotelySearchSession
{
    internal static readonly MotelySearchSession Placeholder = new(null!);

    private readonly IMotelySearch _search;

    internal MotelySearchSession(IMotelySearch search)
    {
        _search = search;
    }

    public long GetTotalSeedsSearched() => _search.TotalSeedsSearched;

    public long GetMatchingSeeds() => _search.MatchingSeeds;

    public long GetFilteredSeeds() => _search.FilteredSeeds;

    public bool GetIsCompleted() => _search.IsCompleted;

    public bool GetIsSequentialBatchSearch() => _search.IsSequentialBatchSearch;

    public long GetBatchIndex() => _search.BatchIndex;

    public long GetCompletedBatchCount() => _search.CompletedBatchCount;

    public void Cancel() => _search.Cancel();

    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default) =>
        _search.WaitForCompletionAsync(cancellationToken);

    public void Dispose() => _search.Dispose();
}
