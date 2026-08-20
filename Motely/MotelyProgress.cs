namespace Motely;

/// <summary>
/// Progress information reported during Motely search operations.
/// Durations are milliseconds (same basis as the internal progress ticker).
/// </summary>
public sealed record MotelyProgress
{
    public long CompletedBatchCount { get; set; }
    public long TotalBatchCount { get; set; }
    public long SeedsSearched { get; set; }
    public long MatchingSeeds { get; set; }
    public double SeedsPerMillisecond { get; set; }
    public double PercentComplete { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public long? EstimatedTimeRemainingMilliseconds { get; set; }
}
