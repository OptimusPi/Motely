namespace Motely.Filters.Jaml;

/// <summary>
/// Marks a clause type with the JAML keys that select it, plus the shapes the grammar generator
/// needs. <see cref="Motely.Generators.JamlGrammarGenerator"/> reads this to emit JamlSchema.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class JamlDiscriminatorAttribute(params string[] names) : Attribute
{
    /// <summary>The <c>must:</c>/<c>should:</c> keys that select this clause ("joker", "jokers").</summary>
    public string[] Names { get; } = names;

    /// <summary>Enum the discriminator's value list is parsed as (e.g. MotelyJoker).</summary>
    public Type? ValueEnum { get; set; }

    /// <summary>The clause's <c>sources:</c> block shape, if it has one.</summary>
    public Type? SourceConfigType { get; set; }

    /// <summary>Roll-scoped clauses take their roll indices as the discriminator's own value
    /// (<c>glassDestroy: [1, 2]</c>) rather than a <c>rolls:</c> key.</summary>
    public bool RollsAreInlineValue { get; set; }
}

/// <summary>
/// Every JAML clause. WHAT to look for lives on the concrete type; these are the four keys every
/// clause carries regardless of kind.
/// </summary>
public interface IJamlClause
{
    /// <summary>Human name for reports and explain output. Optional.</summary>
    string? Label { get; set; }

    /// <summary>Occurrences required for the clause to count. Defaults to 1.</summary>
    int Min { get; set; }

    /// <summary>Upper bound on occurrences, null = unbounded.</summary>
    int? Max { get; set; }

    /// <summary>Points contributed when this clause matches (should-clauses).</summary>
    int Score { get; set; }
}

/// <summary>A clause that is evaluated per-ante rather than once per seed.</summary>
public interface IAnteScopedClause : IJamlClause
{
    /// <summary>Antes this clause applies to. Empty = the document's default window.</summary>
    int[] Antes { get; set; }
}

/// <summary>A clause scoped to numbered rolls of an event stream rather than to antes.</summary>
public interface IRollScopedClause : IJamlClause
{
    /// <summary>Roll indices this clause applies to.</summary>
    int[] Rolls { get; set; }
}

/// <summary>A clause that accepts a <c>with:</c> block — owned run-state modifiers.</summary>
public interface IWithScopedClause : IJamlClause
{
    /// <summary>Luck and assumed vouchers affecting this clause's rolls.</summary>
    JamlWith With { get; set; }
}

/// <summary>
/// One JAML scalar or sequence, still as text, with the typed reads the descs ask for. Every
/// Try* returns false rather than throwing — a bad value is a diagnostic, not an exception.
/// </summary>
public interface IJamlValueReader
{
    /// <summary>Raw scalar text. Null/empty/[] on a discriminator means "category match".</summary>
    string? Text { get; }

    bool TryInt(out int value);
    bool TryBool(out bool value);
    bool TryEnum<TEnum>(out TEnum value) where TEnum : struct, Enum;
    bool TryEnumArray<TEnum>(out TEnum[] value) where TEnum : struct, Enum;

    /// <summary>Playing-card rank, which is spelled with names the enum doesn't carry ("2", "J").</summary>
    bool TryRank(out MotelyStandardcardRank value);
}

/// <summary>
/// The static half of a clause: the keys it accepts and how to fill them. Implemented by each
/// FilterDesc so the loader never needs a switch over clause kinds.
/// </summary>
public interface IJamlClauseDesc<in TClause> where TClause : IJamlClause
{
    /// <summary>JAML keys that select this clause. Must match its <see cref="JamlDiscriminatorAttribute"/>.</summary>
    static abstract string[] Discriminators { get; }

    /// <summary>Every key accepted inside the clause body.</summary>
    static abstract string[] ClauseKeys { get; }

    /// <summary>Apply one clause-body key. False = key not handled here (loader reports it).</summary>
    static abstract bool Set(TClause clause, string key, IJamlValueReader value);

    /// <summary>
    /// Apply the discriminator's own value (the list after <c>joker:</c>). Virtual, not abstract:
    /// event clauses like <c>glassDestroy</c> carry no value list and never override it.
    /// </summary>
    static virtual bool SetDiscriminatorValue(TClause clause, IJamlValueReader value) => false;
}

/// <summary>Discriminator-value helpers. Empty means "any of this category", never a token.</summary>
public static class JamlDisc
{
    /// <summary>Null array reads as empty — clauses leave their value lists unset for "any".</summary>
    public static T[] OrEmpty<T>(T[]? values) => values ?? [];

    /// <summary>True when no specific values were named, i.e. the clause matches the whole category.</summary>
    public static bool IsCategoryAny<T>(T[]? values) => values is null || values.Length == 0;
}
