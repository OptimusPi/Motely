using Motely.Filters.Jaml;

namespace Motely.Filters;

public abstract class LogicClause : IJamlClause
{
    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public IJamlClause[] Clauses { get; set; } = [];
}
