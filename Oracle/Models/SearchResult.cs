using System;

namespace Oracle.Models;

public class SearchResult
{
    public string Seed { get; set; } = "";
    public int Score { get; set; }
    public int NaturalNegativeJokers { get; set; }
    public int DesiredNegativeJokers { get; set; }
    public DateTime FoundAt { get; set; }
    public string? FoundItemsJson { get; set; }
    
    // Computed properties for display
    public string ScoreClass => Score switch
    {
        >= 5 => "high-score",
        >= 3 => "medium-score",
        _ => "low-score"
    };
    
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - FoundAt;
            return diff switch
            {
                { TotalMinutes: < 1 } => "just now",
                { TotalMinutes: < 60 } => $"{(int)diff.TotalMinutes}m ago",
                { TotalHours: < 24 } => $"{(int)diff.TotalHours}h ago",
                { TotalDays: < 7 } => $"{(int)diff.TotalDays}d ago",
                _ => FoundAt.ToString("yyyy-MM-dd")
            };
        }
    }
}
