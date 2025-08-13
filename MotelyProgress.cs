using System;

namespace Motely
{
    /// <summary>
    /// Progress information reported during Motely search operations
    /// </summary>
    public class MotelyProgress
    {
        public ulong CompletedBatchCount { get; set; }
        public ulong TotalBatchCount { get; set; }
        public ulong SeedsSearched { get; set; }
        public double SeedsPerMillisecond { get; set; }
        public double PercentComplete { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        public string FormattedMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Creates a formatted progress message
        /// </summary>
        public static string FormatProgress(double percent, TimeSpan? timeRemaining, double seedsPerMillisecond)
        {
            if (!timeRemaining.HasValue)
            {
                return $"{percent:0.00}% ({seedsPerMillisecond * 1000:F0} seeds/s)";
            }
            
            string timeLeftFormatted;
            var timeLeft = timeRemaining.Value;
            
            if (timeLeft.TotalDays >= 1)
            {
                timeLeftFormatted = $"{(int)timeLeft.TotalDays} days, {timeLeft.Hours} hours";
            }
            else if (timeLeft.TotalHours >= 1)
            {
                timeLeftFormatted = $"{(int)timeLeft.TotalHours} hours, {timeLeft.Minutes} minutes";
            }
            else if (timeLeft.TotalMinutes >= 1)
            {
                timeLeftFormatted = $"{(int)timeLeft.TotalMinutes} minutes";
            }
            else
            {
                timeLeftFormatted = $"{timeLeft.Seconds} seconds";
            }
            
            return $"{percent:0.00}% ~{timeLeftFormatted} remaining ({seedsPerMillisecond * 1000:F0} seeds/s)";
        }
    }
}