namespace Motely;

/// <summary>
/// Abstraction for pause/unpause synchronization. Desktop: Barrier. Browser: no-op (WASM doesn't support Barrier).
/// </summary>
public interface IPauseSync
{
    void SignalAndWait();
    void SignalAndWait(TimeSpan timeout);
}

/// <summary>
/// Abstraction for worker run + join. Desktop: Thread. Browser: Task (no Thread.Start on WASM).
/// </summary>
public interface IWorkerHandle
{
    void Start();
    bool Join(TimeSpan timeout);
    bool IsAlive { get; }
}

/// <summary>
/// Platform-specific factory for search sync and workers. Implemented in Desktop/Browser partials.
/// </summary>
public static partial class MotelySearchPlatform
{
    public static partial IPauseSync CreatePauseSync(int participantCount);
    public static partial IPauseSync CreateUnpauseSync(int participantCount);
    public static partial IWorkerHandle CreateWorker(Action entryPoint);
}
