using Motely.Filters.Jaml;

namespace Motely.Filters;

public abstract class LogicClause : JamlClauseBase
{
    public JamlClauseBase[] Clauses { get; set; } = [];
}
