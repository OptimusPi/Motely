using Motely.Filters.Jaml;

namespace Motely.Filters;

public abstract class LogicClause : IJamlClause
{
    /// <summary>Shared by AndClause/OrClause — this class's own clause-level keys beyond
    /// JamlClause.SharedKeys.</summary>
    public static readonly string[] ClauseKeys = ["clauses"];

    public string? Label { get; set; }
    public int Min { get; set; } = 1;
    public int? Max { get; set; }
    public int Score { get; set; }
    public IJamlClause[] Clauses { get; set; } = [];
}
