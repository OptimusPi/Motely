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

    /// <summary>
    /// <paramref name="eventRolls"/> sizes the pull and shop-source roll queues.
    /// <paramref name="shopSlots"/> is a separate dial: how deep to walk each ante's real,
    /// interleaved shop. 0 keeps the defaults (15 on antes 0-1, 50 beyond). The shop stream never
    /// runs dry, so pass whatever depth the caller needs -- an endless ante-1 shop is
    /// <c>seedsPaged(jaml, 0, 5000)</c>, which costs 5000 shop items and no oversized roll queues.
    /// </summary>
    [Export]
    public static IReadOnlyList<MotelyJamlyzerSeedResult> SeedsPaged(
        string jaml,
        int eventRolls,
        int shopSlots = 0
    ) => MotelyJamlyzer.Analyze(JamlConfigLoader.FromJaml(jaml), eventRolls, shopSlots);

    /// <summary>
    /// Continue a scroll: <paramref name="resumeFrom"/> is the <c>streamStates</c> off the previous
    /// result, so the shop and every roll queue pick up exactly where the last window stopped --
    /// no duplicated items, none skipped. Single seed only; the state bag is seed-specific.
    /// </summary>
    [Export]
    public static IReadOnlyList<MotelyJamlyzerSeedResult> SeedsResume(
        string jaml,
        MotelyJamlyzerStreamStates resumeFrom,
        int eventRolls,
        int shopSlots = 0
    ) => MotelyJamlyzer.Analyze(JamlConfigLoader.FromJaml(jaml), resumeFrom, eventRolls, shopSlots);
}
