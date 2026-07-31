using System.Reflection;
using Bootsharp;

namespace Motely.Wasm;

/// <summary>
/// Interop shape for the wasm head. <see cref="IMotelySearchSettings"/> crosses as a real interop
/// instance, so JavaScript drives the engine's own <c>With*</c> chain — the same chain the CLI uses.
/// No DTO, no second grammar, no reimplemented Collect.
/// </summary>
/// <remarks>
/// Observed on Bootsharp 0.9.0, 2026-07-30, by building and reading the emitted output:
///
/// 1. A byref type reaches JavaScript under its CLR name and the trailing <c>&amp;</c> is not a legal
///    identifier — the bundle contained <c>export const MotelyVectorSearchContext&amp; = {</c> and the
///    page died with "Missing initializer in const declaration". Hence the node erasures below.
/// 2. Erasure cannot substitute for a specialization. <c>TypeInspector.Collect()</c> applies
///    renames *after* collecting types, so an erased node that another collected type references
///    leaves a dangling serializer entry (CS0103). A type that has a <c>[Specialize*]</c> pair must
///    therefore NOT also appear here — see <c>IMotelySeedFilterDesc</c>, deliberately absent.
///
/// Not verified: whether either is considered an upstream defect. Do not repeat that claim without
/// checking — it was carried in this comment for weeks and no one had.
/// </remarks>
public static class Interop
{
    /// <summary>
    /// The engine's desc / provider plumbing. Every member of these drives a <c>ref struct</c>
    /// context — <c>MotelyFilterCreationContext</c>, <c>MotelyVectorSearchContext</c>,
    /// <c>MotelySingleSearchContext</c> — or returns <c>ReadOnlySpan&lt;char&gt;</c>. Those types
    /// hold pointers into live SIMD lane state and the PRNG partial-hash cache; their validity is
    /// one stack frame, so a heap proxy cannot hold one and JavaScript can never implement them.
    ///
    /// Bootsharp emits a <c>JS_Import_*</c> proxy for every interface it discovers, so the erasure
    /// has to be node-level: a member-level erasure leaves a proxy class that still declares the
    /// interface and then fails CS0535.
    ///
    /// Named type by type on purpose. A sweep over "anything byref" would be a second, invisible
    /// API sitting next to <c>[Export]</c>; this list is nine lines and reviewable.
    ///
    /// Nothing a caller needs goes dark: the hunt surface stays whole — list / random / aesthetic /
    /// sequential seed modes, padding alphabet, provider batch, sequential batch chars, stopAfter,
    /// deck, stake, threads, batch indices, progress, scored results, csv, quiet, auto-cutoff.
    /// JAML authors filters; JavaScript picks seeds and knobs.
    /// </summary>
    private static readonly string[] EngineOnlyNodes =
    [
        // IMotelySeedFilterDesc is NOT erased: BoundarySpecializations.cs gives it a specialization
        // pair, which is what makes it cross as an opaque handle. Erasing it as well leaves the
        // collected Binary<IMotelySeedFilterDesc[]> pointing at an element id that Rename deleted
        // (TypeInspector.Collect renames after collecting) — CS0103 in Serializer.g.cs.
        // Verified empirically 2026-07-31: adding it here produces exactly that CS0103.
        "IMotelySeedFilter",
        "IMotelySeedScoreDesc",
        "IMotelySeedScoreProvider",
        "IMotelySeedAnalyzeDesc",
        "IMotelySeedAnalyzeProvider",
        "IMotelySeedRouterDesc",
        "IMotelySeedRouter",
        "MotelyIndividualSeedSearcher",
        // The ref-struct contexts themselves. 0.9 emits them as empty TypeScript interfaces named
        // with the CLR byref suffix — `export interface MotelyVectorSearchContext& { }` — which is
        // not a legal identifier and breaks `tsc`. They carry no members, so erasing costs nothing.
        "MotelyFilterCreationContext",
        "MotelyVectorSearchContext",
        "MotelySingleSearchContext",
    ];

    /// <summary>
    /// Settings members typed against the erased plumbing above. JavaScript reaches the same
    /// capability through the value-shaped doors: <c>WithSeedList</c>, <c>WithRandomSearch</c>,
    /// <c>WithAestheticSearch</c>, <c>WithSequentialSearch</c>.
    /// </summary>
    private static readonly (string Type, string Member)[] EngineOnlyMembers =
    [
        ("IMotelySearchSettings", "WithAdditionalFilter"),
        ("IMotelySearchSettings", "WithSeedScoreProvider"),
        ("IMotelySearchSettings", "WithSeedAnalyzeProvider"),
        ("IMotelySearchSettings", "WithSeedRouter"),
        // The engine's own remark on Jimmolate: there is no cross-boundary version and cannot
        // usefully be one — a JS predicate would marshal once per seed plus once per context read.
        ("IMotelySearchSettings", "WithJimmolate"),
        // WithSeedGenerator is declared on the interface (MotelySearch.cs:142), and Bootsharp walks
        // IMotelySearchSettings.GetMethods() — so the member it sees declares IMotelySearchSettings.
        // Matched by the class name it never fires.
        ("IMotelySearchSettings", "WithSeedGenerator"),
        // These two are declared only on the implementation (MotelySearch.cs:196), so they must be
        // matched by the class name: Bare() of MotelySearchSettings<TBaseFilter> is
        // "MotelySearchSettings". Aimed at the interface they matched nothing, silently.
        ("MotelySearchSettings", "AdditionalFilters"),
        ("MotelySearchSettings", "BaseFilterDescBase"),
    ];

    [RenameNode]
    public static string? Node(Type type, string @default) =>
        EngineOnlyNodes.Contains(Bare(type.Name)) ? null : @default;

    [RenameMember]
    public static string? Member(MemberInfo info, string @default)
    {
        string? declaring = Bare(info.DeclaringType?.Name);
        foreach ((string type, string member) in EngineOnlyMembers)
            if (declaring == type && info.Name == member)
                return null;
        return @default;
    }

    /// <summary>
    /// The plain type name behind a reflected one: <c>IFoo`1</c> reads as <c>IFoo</c>, and the CLR
    /// byref form <c>MotelyVectorSearchContext&amp;</c> reads as <c>MotelyVectorSearchContext</c>.
    /// The trailing <c>&amp;</c> matters — a byref type reflects under that name, and 0.9 carries it
    /// straight into the emitted JavaScript (<c>export const MotelyVectorSearchContext&amp; = {</c>),
    /// which is a syntax error in the shipped module.
    /// </summary>
    private static string? Bare(string? name)
    {
        if (name is null)
            return null;
        name = name.TrimEnd('&', '*');
        int tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }
}
