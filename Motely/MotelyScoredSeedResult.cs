using Motely.Filters;

namespace Motely;

/// <summary>
/// Interop-safe scored seed payload for JS/WASM consumers.
/// </summary>
public sealed record MotelyScoredSeedResult(string Seed, int Score, IReadOnlyList<int> Tallies)
{
    public static MotelyScoredSeedResult FromTally(in MotelySeedScoreTally tally) =>
        new(tally.Seed, tally.Score, tally.TallyValuesSpan.ToArray());
}
