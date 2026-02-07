namespace Motely;

/// <summary>
/// Browser WASM (WasmEnableThreads=true): real Barrier, same as desktop. Pause/unpause works.
/// </summary>
public sealed class PauseSyncBrowser : IPauseSync
{
    private readonly Barrier _barrier;

    public PauseSyncBrowser(int participantCount)
    {
        _barrier = new Barrier(participantCount);
    }

    public void SignalAndWait() => _barrier.SignalAndWait();

    public void SignalAndWait(TimeSpan timeout) => _barrier.SignalAndWait(timeout);
}

/// <summary>
/// Browser WASM (WasmEnableThreads=true): real Thread (Web Worker). Multi-threading works, no Task.Run.
/// </summary>
public sealed class WorkerHandleBrowser : IWorkerHandle
{
    private readonly Thread _thread;

    public WorkerHandleBrowser(Action entryPoint)
    {
        _thread = new Thread(() => entryPoint());
    }

    public void Start() => _thread.Start();

    public bool Join(TimeSpan timeout) => _thread.Join(timeout);

    public bool IsAlive => _thread.IsAlive;
}

public static partial class MotelySearchPlatform
{
    public static partial IPauseSync CreatePauseSync(int participantCount) =>
        new PauseSyncBrowser(participantCount);

    public static partial IPauseSync CreateUnpauseSync(int participantCount) =>
        new PauseSyncBrowser(participantCount);

    public static partial IWorkerHandle CreateWorker(Action entryPoint) =>
        new WorkerHandleBrowser(entryPoint);
}
