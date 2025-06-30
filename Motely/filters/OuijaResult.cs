namespace Motely.Filters;

public class OuijaResult
{
    // Seed string (8 chars + null terminator in C, just use string in C#)
    public string Seed { get; set; } = string.Empty;
    // Total score for this result
    public ushort TotalScore { get; set; }
    // Number of natural negative jokers
    public byte NaturalNegativeJokers { get; set; }
    // Number of desired negative jokers
    public byte DesiredNegativeJokers { get; set; }
    // Score for each want (up to MAX_DESIRES_HOST)
    public byte[] ScoreWants { get; set; } = new byte[32]; // 32 = MAX_DESIRES_HOST
    // Success flag for Motely output (not in C struct, but useful for filtering)
    public bool Success { get; set; }
}
