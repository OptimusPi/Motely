using Bootsharp;
using Motely.Analysis;
using Motely.Filters.Jaml;

/// <summary>
/// Analyze host. Jamlyzer — not search. JAML text in, records out.
/// Do not put <see cref="JamlConfig"/> on the boundary: it is a class, so Bootsharp
/// would pass it by ref as an instance (serialization.md / interop-instances.md) and
/// drag <c>IJamlClause</c> into JS. Same door as <c>Search.scoreList</c>.
/// </summary>
public static partial class Analyze
{
    [Export]
    public static IReadOnlyList<MotelyJamlyzerSeedResult> Seeds(string jaml) =>
        MotelyJamlyzer.Analyze(JamlConfigLoader.FromJaml(jaml));

    [Export]
    public static IReadOnlyList<MotelyJamlyzerSeedResult> SeedsPaged(string jaml, int eventRolls) =>
        MotelyJamlyzer.Analyze(JamlConfigLoader.FromJaml(jaml), eventRolls);
}
