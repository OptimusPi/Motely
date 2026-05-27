using Motely.Filters.Jaml;

namespace Motely.Filters;

public abstract class LogicClause : IJamlClause
{
    public string? Label { get; init; }
    public IJamlClause[] Clauses { get; init; } = [];
    public int Min { get; init; } = 1;
    public int? Max { get; init; }
    public int Score { get; init; }

    public virtual int EstimatedCost => 1;
    public abstract string Describe();
    public abstract IMotelySeedFilterDesc CreateDesc();
}
