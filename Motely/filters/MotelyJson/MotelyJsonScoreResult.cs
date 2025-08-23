namespace Motely.Filters;

public class MotelyJsonResult
{
    // Seed string (8 chars + null terminator in C, just use string in C#)
    public string Seed { get; set; } = string.Empty;
    // Total score for this result
    public int TotalScore { get; set; }
    // Number of natural negative jokers
    public int NaturalNegativeJokers { get; set; }
    // Number of desired negative jokers
    public int DesiredNegativeJokers { get; set; }
    // Score for each want (up to MAX_DESIRES_HOST)
    public int[] ScoreWants { get; set; } = new int[32];
    // Success flag for Motely output (not in C struct, but useful for filtering)
    public bool Success { get; set; }

    public string ToCsvRow(MotelyJsonConfig config, int numWants = -1)
    {
        // Start with pipe marker, then seed and total score
        var row = $"|{Seed},{TotalScore}";

        // Add scores for each should clause, limited by numWants parameter
        if (ScoreWants != null && config?.Should != null)
        {
            // If numWants is -1 (default), show all; if 0 or positive, limit to that number
            int wantsToShow = numWants == -1 ? ScoreWants.Length : Math.Min(numWants, ScoreWants.Length);
            for (int i = 0; i < wantsToShow && i < config.Should.Count; i++)
            {
                var score = ScoreWants[i];
                row += $",{score}";
            }
        }
        return row;
    }
}
