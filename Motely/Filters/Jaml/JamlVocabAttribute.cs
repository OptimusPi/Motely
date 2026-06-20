namespace Motely.Filters.Jaml;

/// <summary>
/// Marks an enum as part of the JAML language vocabulary. The JAML vocabulary source
/// generator reads every enum carrying this attribute and emits its members as the valid
/// names for <paramref name="category"/>, so the vocabulary is derived from the enums at
/// compile time rather than dumped by a runtime command.
/// </summary>
[AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
public sealed class JamlVocabAttribute(string category) : Attribute
{
    /// <summary>The JAML category this enum supplies names for (e.g. "joker", "voucher").</summary>
    public string Category { get; } = category;
}
