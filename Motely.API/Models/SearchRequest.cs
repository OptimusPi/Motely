using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Motely.Filters;

namespace Motely.API.Models;

/// <summary>
/// Request to start a new Motely search
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// MotelyJsonConfig filter configuration (MUST, SHOULD, MUSTNOT clauses)
    /// </summary>
    [Required]
    public MotelyJsonConfig? Config { get; set; }

    /// <summary>
    /// Search criteria (threads, batch size, deck, stake, etc.)
    /// </summary>
    public SearchCriteriaDto? Criteria { get; set; }
}

/// <summary>
/// Source type for search criteria (used for burst-mode detection)
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SearchSourceType
{
    Unknown = 0,
    Single,
    SeedSources,
    Wordlist,
    DbList,
    Random,
    Sequential,
}

/// <summary>
/// Search execution criteria
/// </summary>
public class SearchCriteriaDto
{
    /// <summary>
    /// Number of threads to use (default: CPU count)
    /// </summary>
    public int ThreadCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Batch character count (2 = 35^2 seeds per batch)
    /// </summary>
    public int BatchSize { get; set; } = 2;

    /// <summary>
    /// Deck to use (Red, Blue, Yellow, Ghost, Abandoned, Checkered, Zodiac, Painted, Anaglyph, Plasma, Erratic, Challenge)
    /// </summary>
    public string? Deck { get; set; } = "Red";

    /// <summary>
    /// Stake level (White, Red, Green, Black, Blue, Purple, Orange, Gold)
    /// </summary>
    public string? Stake { get; set; } = "White";

    /// <summary>
    /// Minimum score threshold for results (only applies if SHOULD clauses exist)
    /// </summary>
    public int MinScore { get; set; } = 0;

    /// <summary>
    /// Starting batch index (0 = beginning)
    /// </summary>
    public ulong StartBatch { get; set; } = 0;

    /// <summary>
    /// Ending batch index (ulong.MaxValue = infinite)
    /// </summary>
    public ulong EndBatch { get; set; } = ulong.MaxValue;

    /// <summary>
    /// Source type for burst-mode detection (single, wordlist, dblist, etc.)
    /// </summary>
    public SearchSourceType SourceType { get; set; } = SearchSourceType.Unknown;
}

public class SearchResponse
{
    public string SearchId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class SearchStatusResponse
{
    public string SearchId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // running, completed, cancelled, error
    public string FilterName { get; set; } = string.Empty;
    public int ResultsFound { get; set; }
    public long SeedsSearched { get; set; }
    public double ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SeedResult>? Results { get; set; }
}

public class SeedResult
{
    public string Seed { get; set; } = string.Empty;
    public int Score { get; set; }
    public Dictionary<string, object>? Details { get; set; }
}
