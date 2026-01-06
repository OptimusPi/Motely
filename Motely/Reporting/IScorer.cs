using Motely.Filters;

namespace Motely.Reporting;

/// <summary>
/// Interface for scorers that find and evaluate data from a seed context
/// Scorers are "Data Finders" - they extract information from the game state
/// </summary>
public interface IScorer
{
    /// <summary>
    /// Evaluate the scorer against a seed context and return a result
    /// </summary>
    /// <param name="ctx">Single search context for the seed</param>
    /// <param name="runState">Current run state (for tracking mutations)</param>
    /// <returns>Result value (type depends on scorer implementation)</returns>
    object? Evaluate(ref MotelySingleSearchContext ctx, ref MotelyRunState runState);
}

/// <summary>
/// Adapter to convert existing filter clauses into scorers
/// </summary>
public class FilterClauseScorer : IScorer
{
    private readonly MotelyJsonConfig.MotelyJsonFilterClause _clause;

    public FilterClauseScorer(MotelyJsonConfig.MotelyJsonFilterClause clause)
    {
        _clause = clause ?? throw new ArgumentNullException(nameof(clause));
    }

    public object? Evaluate(ref MotelySingleSearchContext ctx, ref MotelyRunState runState)
    {
        // Use existing counting logic
        var count = MotelyJsonScoring.CountOccurrences(ref ctx, _clause, ref runState);
        return count;
    }
}
