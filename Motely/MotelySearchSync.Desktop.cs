namespace Motely;

/// <summary>
/// Desktop: real OS thread per search worker.
/// </summary>
internal sealed class ThreadWorkerHandle(Action body)
{
    private readonly Thread _thread = new(new ThreadStart(body)) { IsBackground = true };

    public void Start() => _thread.Start();
    public bool Join(TimeSpan timeout) => _thread.Join(timeout);
    public bool IsAlive => _thread.IsAlive;
}
