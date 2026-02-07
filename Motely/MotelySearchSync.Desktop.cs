namespace Motely;

/// <summary>
/// Desktop: real Barrier for pause/unpause. Used only when building for net10.0.
/// </summary>
public sealed class PauseSyncDesktop : IPauseSync
{
    private readonly Barrier _barrier;

    public PauseSyncDesktop(int participantCount)
    {
        _barrier = new Barrier(participantCount);
    }

    public void SignalAndWait() => _barrier.SignalAndWait();

    public void SignalAndWait(TimeSpan timeout) => _barrier.SignalAndWait(timeout);
}

/// <summary>
/// Desktop: wraps System.Threading.Thread for Join/IsAlive. Used only when building for net10.0.
/// </summary>
public sealed class WorkerHandleDesktop : IWorkerHandle
{
    private readonly Thread _thread;

    public WorkerHandleDesktop(Action entryPoint)
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
        new PauseSyncDesktop(participantCount);

    public static partial IPauseSync CreateUnpauseSync(int participantCount) =>
        new PauseSyncDesktop(participantCount);

    public static partial IWorkerHandle CreateWorker(Action entryPoint) =>
        new WorkerHandleDesktop(entryPoint);
}
