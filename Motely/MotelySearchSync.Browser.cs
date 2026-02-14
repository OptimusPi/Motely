namespace Motely;

/// <summary>
/// Browser WASM: LongRunning Task per search worker (.NET manages the real
/// pthread via SharedArrayBuffer internally).
/// </summary>
internal sealed class ThreadWorkerHandle(Action body)
{
    private volatile bool _isAlive;
    private Task? _task;

    public void Start()
    {
        var t = new Task(() =>
        {
            _isAlive = true;
            try { body(); }
            finally { _isAlive = false; }
        }, TaskCreationOptions.LongRunning);
        _task = t;
        t.Start();
    }

    public bool Join(TimeSpan timeout) => _task?.Wait(timeout) ?? true;
    public bool IsAlive => _isAlive;
}
