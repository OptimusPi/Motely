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

/// <summary>
/// Default <see cref="IMotelySearchSession"/> for Bootsharp DI only (<c>AddBootsharp</c> needs a registered handler
/// before it can build the JS export for <see cref="MotelySearchSession"/>). Real searches use <see cref="MotelySearchSession"/>.
/// </summary>
/// <summary>Registered in DI for Bootsharp; kept public so NativeAOT does not trim it away.</summary>
public sealed class IdleMotelySearchSession : IMotelySearchSession
{
    public long GetTotalSeedsSearched()
    {
        return 0;
    }

    public long GetMatchingSeeds()
    {
        return 0;
    }

    public long GetFilteredSeeds()
    {
        return 0;
    }

    public bool GetIsCompleted()
    {
        return true;
    }

    public bool GetIsSequentialBatchSearch()
    {
        return false;
    }

    public long GetBatchIndex()
    {
        return 0;
    }

    public long GetCompletedBatchCount()
    {
        return 0;
    }

    public void Cancel()
    {
    }

    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}

public sealed class MotelySearchSession : IMotelySearchSession
{
    private readonly IMotelySearch _search;

    internal MotelySearchSession(IMotelySearch search)
    {
        _search = search;
    }

    public long GetTotalSeedsSearched()
    {
        return _search.TotalSeedsSearched;
    }

    public long GetMatchingSeeds()
    {
        return _search.MatchingSeeds;
    }

    public long GetFilteredSeeds()
    {
        return _search.FilteredSeeds;
    }

    public bool GetIsCompleted()
    {
        return _search.IsCompleted;
    }

    public bool GetIsSequentialBatchSearch()
    {
        return _search.IsSequentialBatchSearch;
    }

    public long GetBatchIndex()
    {
        return _search.BatchIndex;
    }

    public long GetCompletedBatchCount()
    {
        return _search.CompletedBatchCount;
    }

    public void Cancel()
    {
        _search.Cancel();
    }

    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        return _search.WaitForCompletionAsync(cancellationToken);
    }

    public void Dispose()
    {
        _search.Dispose();
    }
}
