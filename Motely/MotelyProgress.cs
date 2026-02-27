namespace Motely
{
    /// <summary>
    /// Progress information reported during Motely search operations
    /// </summary>
    public class MotelySearchPlanProgress
    {
        public long CompletedBatchCount { get; set; }
        public long TotalBatchCount { get; set; }
        public long SeedsSearched { get; set; }
        public double SeedsPerMillisecond { get; set; }
        public double PercentComplete { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
    }
}
