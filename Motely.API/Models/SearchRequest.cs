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
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Unknown = 0,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Single,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    SeedSources,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Wordlist,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    DbList,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Random,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Sequential,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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
    public int BatchSize { get; set; } = 4;

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

/// <summary>
/// Response from starting a search.
/// </summary>
public class SearchResponse
{
    /// <summary>Unique identifier for the search.</summary>
    public string SearchId { get; set; } = string.Empty;
    /// <summary>Current status (e.g. running, completed).</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Human-readable message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Current status and progress of a search.
/// </summary>
public class SearchStatusResponse
{
    /// <summary>Unique identifier for the search.</summary>
    public string SearchId { get; set; } = string.Empty;
    /// <summary>Status: running, completed, cancelled, or error.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>Name of the JAML filter.</summary>
    public string FilterName { get; set; } = string.Empty;
    /// <summary>Number of seeds matching the filter so far.</summary>
    public int ResultsFound { get; set; }
    /// <summary>Total number of seeds searched so far.</summary>
    public long SeedsSearched { get; set; }
    /// <summary>Progress as a percentage (0–100).</summary>
    public double ProgressPercent { get; set; }
    /// <summary>Error message if status is error.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Top results (seeds and scores).</summary>
    public List<SeedResult>? Results { get; set; }
}

/// <summary>
/// A single seed result from a search.
/// </summary>
public class SeedResult
{
    /// <summary>The seed string.</summary>
    public string Seed { get; set; } = string.Empty;
    /// <summary>Score from the filter.</summary>
    public int Score { get; set; }
    /// <summary>Optional per-seed details.</summary>
    public Dictionary<string, object>? Details { get; set; }
}
