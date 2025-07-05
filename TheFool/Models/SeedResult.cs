using System;
using System.Collections.Generic;

namespace Oracle.Models;

public class SeedResult
{
    public string Seed { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? SearchId { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime FoundAt { get; set; } = DateTime.UtcNow;
    public string? NaturalNegativeJokers { get; set; }
    public string? DesiredNegativeJokers { get; set; }
    public string TimeAgo => GetTimeAgo();

    private string GetTimeAgo()
    {
        var elapsed = DateTime.UtcNow - FoundAt;
        return elapsed.TotalDays >= 1 ? $"{(int)elapsed.TotalDays}d ago" :
               elapsed.TotalHours >= 1 ? $"{(int)elapsed.TotalHours}h ago" :
               elapsed.TotalMinutes >= 1 ? $"{(int)elapsed.TotalMinutes}m ago" :
               "Just now";
    }
}
