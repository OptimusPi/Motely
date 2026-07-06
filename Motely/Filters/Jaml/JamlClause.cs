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

public static class JamlClause
{
    /// <summary>Keys every IJamlClause implementation accepts — exactly the interface's own
    /// members. NOT "sources", NOT "with"/"luck"/"vouchers" — those apply only to the specific
    /// concrete types that actually have a matching property (see each class's own ClauseKeys).</summary>
    public static readonly string[] SharedKeys = ["min", "max", "score", "label"];

    /// <summary>Extra keys granted by IAnteScopedClause on top of SharedKeys.</summary>
    public static readonly string[] AnteScopedExtraKeys = ["ante", "antes"];

    /// <summary>Allowed keys inside a clause's own <c>with:</c> block (JamlWith modifiers) —
    /// only meaningful on the clause types that list "with" in their own ClauseKeys.</summary>
    public static readonly string[] WithBlockKeys = ["luck", "vouchers"];

    /// <summary>Allowed keys inside a with-having event clause's <c>sources:</c> block, used as
    /// an alternate spelling for luck — only meaningful on clause types that list "sources" in
    /// their own ClauseKeys (distinct from the item-clause "sources:" block, which is a full
    /// SourceConfig object; the same key name means something different for these two families).</summary>
    public static readonly string[] EventSourcesLuckKey = ["luck"];
}
