using System;
using System.Collections.Generic;

namespace Oracle.Models;

public class SearchCriteria
{
    public int SeedCount { get; set; } = 10000;
    public int ThreadCount { get; set; } = System.Environment.ProcessorCount;
    public double? MinScore { get; set; } = 1000000;
    public string? TargetPattern { get; set; }
    public string? Deck { get; set; }
    public string? Stake { get; set; }
    public List<string> Needs { get; set; } = new();
    public List<string> Wants { get; set; } = new();
    public bool ScoreNaturalNegatives { get; set; }
    public bool ScoreDesiredNegatives { get; set; }
}

public class SearchProgress
{
    public double PercentComplete { get; set; }
    public string Message { get; set; } = string.Empty;
    public IEnumerable<SeedResult>? NewResults { get; set; }
    public double SeedsPerSecond { get; set; }
    public TimeSpan Elapsed { get; set; }
}

public class PerformanceDataPoint
{
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double GpuUsage { get; set; }
}

public class PerformanceMetrics
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double GpuUsage { get; set; }
}
