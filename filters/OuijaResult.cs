namespace Motely.Filters;

public class OuijaResult
{
    // Seed string (8 chars + null terminator in C, just use string in C#)
    public string Seed { get; set; } = string.Empty;
    // Total score for this result
    public ushort TotalScore { get; set; }
    // Number of natural negative jokers
    public int NaturalNegativeJokers { get; set; }
    // Number of desired negative jokers
    public int DesiredNegativeJokers { get; set; }
    // Score for each want (up to MAX_DESIRES_HOST)
    public int[] ScoreWants { get; set; } = new int[32];
    // Success flag for Motely output (not in C struct, but useful for filtering)
    public bool Success { get; set; }
}
