namespace Motely.Filters;

/// <summary>
/// A search result: seed + aggregate score + per-clause tally counts (0-255 each).
/// Tally is a flat byte[] whose indices correspond to clause positions in the config.
/// </summary>
public struct MotelySeedScoreTally(string seed, int score) : IMotelySeedScore
{
    public string Seed { get; set; } = seed;
    public int Score { get; set; } = score;
    public byte[] Tally { get; set; } = [];
}
