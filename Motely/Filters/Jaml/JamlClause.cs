namespace Motely.Filters.Jaml;

/// <summary>
/// JAML clause contract. The 4 polymorphic members (<see cref="MaxAnte"/>,
/// <see cref="EstimatedCost"/>, <see cref="Describe"/>, <see cref="CreateDesc"/>)
/// replace the per-clause switch statements that used to live in
/// <see cref="JamlSearchBuilder"/>. Concrete clauses inherit from
/// <see cref="JamlClause"/>, which is the only valid implementation path.
/// </summary>
public interface IJamlClause
{
    string Label { get; init; }
    int Score { get; init; }
    int Min { get; init; }
    int? Max { get; init; }

    /// <summary>Highest ante index touched by this clause (0 if not ante-scoped).</summary>
    int MaxAnte { get; }

    /// <summary>Relative scheduling cost — drives cheapest-first clause ordering in the planner.</summary>
    int EstimatedCost { get; }

    /// <summary>Human-readable plan-line for <see cref="JamlSearchBuilder.ExplainPlan"/>.</summary>
    string Describe();

    /// <summary>Build the SIMD filter descriptor that implements this clause.</summary>
    IMotelySeedFilterDesc CreateDesc();
}

/// <summary>
/// Shared base for every concrete JAML clause. Owns the universal properties
/// (<see cref="Label"/>, <see cref="Score"/>, <see cref="Min"/>, <see cref="Max"/>,
/// <see cref="Antes"/>) and defines the four polymorphic dispatch members.
/// <para>
/// <see cref="Antes"/> defaults to an empty array; clauses that aren't ante-scoped
/// (event / logic combinators) simply leave it empty. <see cref="MaxAnte"/> derives
/// from <see cref="Antes"/> by default — override in clauses that draw their range
/// from somewhere else (event clauses use <c>Rolls</c>; logic clauses recurse over
/// children).
/// </para>
/// </summary>
public abstract class JamlClause : IJamlClause
{
    public string Label { get; init; } = "";
    public int Score { get; init; }
    public int Min { get; init; } = 1;
    public int? Max { get; init; }
    public int[] Antes { get; init; } = [];

    public virtual int MaxAnte
    {
        get
        {
            int n = Antes.Length;
            if (n == 0) return 0;
            int max = Antes[0];
            for (int i = 1; i < n; i++)
                if (Antes[i] > max) max = Antes[i];
            return max;
        }
    }

    public abstract int EstimatedCost { get; }
    public abstract string Describe();
    public abstract IMotelySeedFilterDesc CreateDesc();
}

/// <summary>
/// Shared base for event clauses driven by a roll-index array. Event clauses don't have
/// <c>antes</c> — they enumerate a PRNG stream by roll index — so they override
/// <see cref="JamlClause.MaxAnte"/> to derive from <see cref="Rolls"/> instead of
/// <see cref="JamlClause.Antes"/>. Cost is uniform across event clauses (3 + MaxAnte).
/// </summary>
public abstract class RollClause : JamlClause, IRollClause
{
    public required int[] Rolls { get; init; }

    public override int MaxAnte
    {
        get
        {
            int n = Rolls.Length;
            if (n == 0) return 0;
            int max = Rolls[0];
            for (int i = 1; i < n; i++)
                if (Rolls[i] > max) max = Rolls[i];
            return max;
        }
    }

    public override int EstimatedCost => 3 + MaxAnte;
}

/// <summary>
/// Shared base for boolean combinator clauses (<see cref="AndClause"/>, <see cref="OrClause"/>).
/// Owns the child clause array and overrides <see cref="JamlClause.MaxAnte"/> and
/// <see cref="JamlClause.EstimatedCost"/> to recurse over children.
/// </summary>
public abstract class LogicClause : JamlClause
{
    public required IJamlClause[] Clauses { get; init; }

    public override int MaxAnte
    {
        get
        {
            int max = 0;
            for (int i = 0; i < Clauses.Length; i++)
            {
                int childMax = Clauses[i].MaxAnte;
                if (childMax > max) max = childMax;
            }
            return max;
        }
    }

    public override int EstimatedCost
    {
        get
        {
            int total = 1;
            for (int i = 0; i < Clauses.Length; i++)
                total += Clauses[i].EstimatedCost;
            return total;
        }
    }
}
