using Motely.Filters.Jaml;

namespace Motely.Filters;

public abstract class LogicClause : IJamlClause
{
    /// <summary>Shared by AndClause/OrClause — this class's own clause-level keys beyond
    /// JamlClause.SharedKeys. "ante"/"antes" here even though LogicClause has no Antes property:
    /// the loader hoists a logic clause's antes down into any nested clause that didn't specify
    /// its own (see JamlConfigLoader.HoistAntes) — a real mechanism, not IAnteScopedClause.</summary>
    public static readonly string[] ClauseKeys = ["clauses", "ante", "antes"];

    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public IJamlClause[] Clauses { get; set; } = [];
}
