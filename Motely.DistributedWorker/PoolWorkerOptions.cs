namespace Motely.DistributedWorker;

/// <summary>Configuration for the in-process pool worker (when API runs the worker).</summary>
public sealed class PoolWorkerOptions
{
    public const string SectionName = "Pool";

    public string Url { get; set; } = "";
    public int Threads { get; set; } = Environment.ProcessorCount;
    public string WorkerId { get; set; } = "";

    /// <summary>
    /// Shared DuckLake root for local results (partitioned by filter_id in Motely.DataLake).
    /// Default: Seeds/ducklake in the working directory.
    /// Set to empty string to disable local DB saving.
    /// </summary>
    public string LocalDbPath { get; set; } = "Seeds/ducklake";

    /// <summary>
    /// Optional: only claim blocks for this specific filter ID.
    /// If empty, claim from whatever active session needs help most.
    /// </summary>
    public string FilterId { get; set; } = "";
}
