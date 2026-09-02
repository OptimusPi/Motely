namespace Motely;

/// <summary>One find: seed, score, per-should tallies.</summary>
public readonly record struct MotelySeedScore(string Seed, int Score, int[] Tally);
