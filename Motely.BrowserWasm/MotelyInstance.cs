namespace Motely.BrowserWasm;

/// <summary>
/// A single Motely runtime instance. Holds its own search state and cancellation.
/// Multiple instances can run concurrently (e.g. one searching, one analyzing).
/// </summary>
internal sealed class MotelyInstance : IDisposable
{
    private static int _nextId;
    private static readonly Dictionary<int, MotelyInstance> _instances = new();

    public int Id { get; }
    private CancellationTokenSource? _activeCts;
    private bool _disposed;

    private MotelyInstance(int id) => Id = id;

    internal static int Create()
    {
        var id = Interlocked.Increment(ref _nextId);
        var instance = new MotelyInstance(id);
        lock (_instances) _instances[id] = instance;
        return id;
    }

    internal static MotelyInstance Get(int id)
    {
        lock (_instances)
        {
            if (_instances.TryGetValue(id, out var inst))
                return inst;
        }
        throw new InvalidOperationException($"No instance with id {id}.");
    }

    internal static void Destroy(int id)
    {
        MotelyInstance? inst;
        lock (_instances)
        {
            if (!_instances.Remove(id, out inst))
                return;
        }
        inst.Dispose();
    }

    internal bool IsSearchActive => _activeCts != null;

    internal CancellationToken BeginSearch()
    {
        if (_activeCts != null)
            throw new InvalidOperationException("Search already running on this instance.");

        var cts = new CancellationTokenSource();
        _activeCts = cts;
        return cts.Token;
    }

    internal void EndSearch()
    {
        _activeCts = null;
    }

    internal void CancelSearch()
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
