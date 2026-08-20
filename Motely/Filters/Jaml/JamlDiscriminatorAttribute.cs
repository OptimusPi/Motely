namespace Motely.Filters.Jaml;

/// <summary>
/// Marks a clause type as a JAML wire family. The grammar generator walks these into
/// <c>JamlSchema</c> — wire names, value enums, source configs, rolls defaults.
/// <see cref="AllowMultipleAttribute"/> is on so one clause type can own several wire
/// spellings with different rolls defaults (e.g. <c>tag</c> vs <c>smallBlindTag</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class JamlDiscriminatorAttribute : Attribute
{
    public string[] Wires { get; }
    public Type? ValueEnum { get; init; }
    public Type? SourceConfigType { get; init; }
    public bool RollsAreInlineValue { get; init; }
    public int[]? RollsDefault { get; init; }

    public JamlDiscriminatorAttribute(params string[] wires) => Wires = wires;
}
