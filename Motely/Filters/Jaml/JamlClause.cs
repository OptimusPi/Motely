namespace Motely.Filters.Jaml;

/// <summary>Common scalar contract shared by all flat clause types.</summary>
public interface IJamlClause
{
    string? Label { get; set; }
    int Min { get; set; }
    int? Max { get; set; }
    int Score { get; set; }
}

/// <summary>
/// Capability for clauses scoped to specific antes (cards/features). Event clauses do NOT
/// implement this — they are roll-scoped, not ante-scoped. This is a capability interface a
/// clause opts into, never a base class: there is no "all clauses have antes" — events don't.
/// </summary>
public interface IAnteScopedClause : IJamlClause
{
    int[] Antes { get; set; }
}
