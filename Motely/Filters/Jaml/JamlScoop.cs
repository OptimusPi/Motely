namespace Motely.Filters.Jaml;

/// <summary>
/// One concrete match the scorer found: which <c>should</c> clause matched, what item, and where.
/// <see cref="ClauseIndex"/> indexes the should-clause list the analyzer scored (so it maps to the
/// CSV tally column and the clause label). <see cref="CardIndex"/> is the card slot within a pack,
/// or -1 for non-pack sources.
/// </summary>
public readonly record struct ScoopedMatch(
    int ClauseIndex,
    MotelyMatchSource Source,
    int Ante,
    int Slot,
    int CardIndex,
    MotelyItem Item,
    int Score
);

/// <summary>
/// Collects <see cref="ScoopedMatch"/>es during a single-seed scoring pass and hands them to the
/// analyzer. The driver sets <see cref="CurrentClauseIndex"/> before scoring each should-clause; the
/// scorer's match choke points call <see cref="Record"/>, which tags the match with that index.
/// AOT-friendly: a plain list, no LINQ, no per-match string work.
/// </summary>
public sealed class JamlScoop : IMotelyScoopSink
{
    private readonly List<ScoopedMatch> _matches = [];

    /// <summary>Index of the should-clause currently being scored; stamped onto each recorded match.</summary>
    public int CurrentClauseIndex { get; set; } = -1;

    public IReadOnlyList<ScoopedMatch> Matches => _matches;

    public void Record(
        MotelyMatchSource source,
        int ante,
        int slot,
        int cardIndex,
        MotelyItem item,
        int score
    ) => _matches.Add(new ScoopedMatch(CurrentClauseIndex, source, ante, slot, cardIndex, item, score));
}
