namespace Motely.Executors;

/// <summary>
/// Per-handle search state for browser (multi-instance). Lives in orchestration.
/// </summary>
public sealed class MotelySearchSession : IDisposable
{
    private static int _nextId;
    private static readonly Dictionary<int, MotelySearchSession> Instances = new();

    public int Id { get; }
    private CancellationTokenSource? _activeCts;
    private bool _disposed;

    private MotelySearchSession(int id) => Id = id;

    public static int Create()
    {
        var id = Interlocked.Increment(ref _nextId);
        var instance = new MotelySearchSession(id);
        lock (Instances) Instances[id] = instance;
        return id;
    }

    public static MotelySearchSession Get(int id)
    {
        lock (Instances)
        {
            if (Instances.TryGetValue(id, out var inst))
                return inst;
        }
        throw new InvalidOperationException($"No instance with id {id}.");
    }

    public static void Destroy(int id)
    {
        MotelySearchSession? inst;
        lock (Instances)
        {
            if (!Instances.Remove(id, out inst))
                return;
        }
        inst.Dispose();
    }

    public bool IsSearchActive => _activeCts != null;

    public CancellationToken BeginSearch()
    {
        if (_activeCts != null)
            throw new InvalidOperationException("Search already running on this instance.");

        var cts = new CancellationTokenSource();
        _activeCts = cts;
        return cts.Token;
    }

    public void EndSearch() => _activeCts = null;

    public void CancelSearch()
    {
        try { _activeCts?.Cancel(); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelSearch();
        _activeCts?.Dispose();
        _activeCts = null;
    }
}
