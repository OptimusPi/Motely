namespace Motely.API;

/// <summary>
/// Represents a search result with seed, score, and optional tallies.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// The seed string that was found.
    /// </summary>
    public string Seed { get; set; } = "";

    /// <summary>
    /// The score for this seed.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Optional tallies for additional scoring data.
    /// </summary>
    public List<int>? Tallies { get; set; }
}
