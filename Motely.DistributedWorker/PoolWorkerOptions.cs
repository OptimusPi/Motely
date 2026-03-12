namespace Motely.DistributedWorker;

/// <summary>Configuration for the in-process pool worker (when API runs the worker).</summary>
public sealed class PoolWorkerOptions
{
    public const string SectionName = "Pool";

    public string Url { get; set; } = "";
    public int Threads { get; set; } = Environment.ProcessorCount;
    public string WorkerId { get; set; } = "";
}
