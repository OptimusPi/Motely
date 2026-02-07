namespace Motely;

/// <summary>
/// Browser WASM: no-op pause sync. Barrier is unsupported on browser even with WasmEnableThreads.
/// Search workers just skip pause/unpause in the browser.
/// </summary>
public sealed class PauseSyncBrowser : IPauseSync
{
    public void SignalAndWait() { /* no-op on browser */ }

    public void SignalAndWait(TimeSpan timeout) { /* no-op on browser */ }
}

/// <summary>
/// Browser WASM: wraps Task instead of Thread. Task.Run maps to Web Workers when WasmEnableThreads=true.
/// Avoids direct Thread usage which the browser platform analyzer flags as unsupported.
/// </summary>
public sealed class WorkerHandleBrowser : IWorkerHandle
{
    private Task? _task;
    private readonly Action _entryPoint;
    private volatile bool _isAlive;

    public WorkerHandleBrowser(Action entryPoint)
    {
        _entryPoint = entryPoint;
    }

    public void Start()
    {
        _isAlive = true;
        _task = Task.Run(() =>
        {
            try
            {
                _entryPoint();
            }
            finally
            {
                _isAlive = false;
            }
        });
    }

    public bool Join(TimeSpan timeout)
    {
        if (_task == null) return true;
        // On browser, Task.Wait blocks the main thread and deadlocks.
        // Just check if the task completed; the search loop already exits via cancellation token.
        return _task.IsCompleted;
    }

    public bool IsAlive => _isAlive;
}

public static partial class MotelySearchPlatform
{
    public static partial IPauseSync CreatePauseSync(int participantCount) =>
        new PauseSyncBrowser();

    public static partial IPauseSync CreateUnpauseSync(int participantCount) =>
        new PauseSyncBrowser();

    public static partial IWorkerHandle CreateWorker(Action entryPoint) =>
        new WorkerHandleBrowser(entryPoint);
}
