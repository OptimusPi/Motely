using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Native;

namespace Motely.Wasm;

// Renamers keep the JS surface to exactly two nodes: Jimmolate + Motely.
public static class BootsharpRenamers
{
    // Fold the Motely.Wasm namespace (default module path "motely/wasm") into the root
    // "index" module so consumers import { Jimmolate, Motely } straight from the package root.
    [RenameModule]
    public static string RenameModule(Type type, string @default) =>
        type.Namespace == "Motely.Wasm" ? "" : @default;

    [RenameNode]
    public static string? RenameNode(Type type, string @default)
    {
        if (type == typeof(Program)) return null; // hide the C# bootstrap from JS
        if (type.IsByRefLike) return null;        // Span<T> / ref structs never marshal
        return @default;
    }
}

// C# entry point. Bootstrap only; hidden from JS by the renamer above.
public static class Program
{
    public static void Main() { }
}

// JS -> C#. Bind `Jimmolate.probe = (seed, deck, stake) => bool` BEFORE boot().
// Bootsharp snapshots [Import] bindings at boot(); assigning after boot is a no-op.
// Then flip `Jimmolate.enabled = true` to make the probe gate seeds during a search.
public static partial class Jimmolate
{
    [Import]
    public static partial bool Probe(string seed, MotelyDeck deck, MotelyStake stake);

    // Opt-in gate. When false (default) searches skip the probe entirely (no per-seed JS call).
    [Export]
    public static bool Enabled { get; set; }
}

// C# -> JS. The Motely node.
// JAML enters as text and is parsed internally; JamlConfig never crosses interop.
// Searches return the engine's own IMotelySearch (no mirror DTOs) and are awaitable +
// cancelable: JS does `const s = await Motely.runSeedListSearch(jaml)`, reads
// s.matchingSeeds / s.totalSeedsSearched / s.isCompleted, and passes a CancellationToken
// to stop a long grind.
public static partial class Motely
{
    [Export]
    public static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    [Export]
    public static string NormalizeSeed(string seed) => MotelyGlobals.NormalizeSeed(seed);

    // Live search stream (C# -> JS). Subscribe before starting a search.
    // Scored results ride out as the engine's own CSV row ("seed,score,t1,t2,…") — the
    // interop-safe payload the struct documents — avoiding its ReadOnlySpan member.
    [Export] public static event Action<MotelyProgress>? OnProgress;
    [Export] public static event Action<string>? OnSeedMatch;
    [Export] public static event Action<string>? OnScoredResult;

    // Validate JAML. Throws on invalid (this IS the engine's validation contract).
    [Export]
    public static void ParseJaml(string jaml) => JamlConfigLoader.FromYaml(jaml);

    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;

    [Export]
    public static Task<IMotelySearch> RunSeedListSearch(string jaml, CancellationToken ct = default)
    {
        var config = JamlConfigLoader.FromYaml(jaml);
        if (config.Seeds.Count == 0)
            throw new InvalidOperationException("JAML has no seeds to search.");
        return Run(Base(config).WithListSearch(config.Seeds, config.Seeds.Count), ct);
    }

    [Export]
    public static Task<IMotelySearch> RunSequentialSearch(
        string jaml,
        long startBatchIndex,
        long endBatchIndex,
        int batchCharacterCount,
        CancellationToken ct = default
    )
    {
        var config = JamlConfigLoader.FromYaml(jaml);
        return Run(
            Base(config)
                .WithSequentialSearch()
                .WithStartBatchIndex(startBatchIndex)
                .WithEndBatchIndex(endBatchIndex)
                .WithBatchCharacterCount(batchCharacterCount),
            ct
        );
    }

    [Export]
    public static Task<IMotelySearch> RunRandomSearch(string jaml, int count, CancellationToken ct = default)
    {
        var config = JamlConfigLoader.FromYaml(jaml);
        return Run(Base(config).WithRandomSearch(count), ct);
    }

    // CreateSettings builds the filter chain + scorer; deck/stake come from the document.
    // Live-stream callbacks are attached here so every search entry point reports uniformly.
    private static IMotelySearchSettings Base(JamlConfig config)
    {
        var s = JamlSearchBuilder.CreateSettings(config).WithDeck(config.Deck).WithStake(config.Stake);
        if (OnProgress is not null) s = s.WithProgressCallback(p => OnProgress.Invoke(p));
        if (OnSeedMatch is not null) s = s.WithSeedMatchCallback(seed => OnSeedMatch.Invoke(seed));
        if (OnScoredResult is not null) s = s.WithScoredResultCallback(t => OnScoredResult.Invoke(t.ToCsvRow()));
        // OG Immolate model: the JS predicate gates each surviving seed. Only seed/deck/stake
        // cross interop; the ref-struct context stays C#-side.
        if (Jimmolate.Enabled)
            s = s.WithJimmolate((ref MotelySingleSearchContext ctx) =>
                Jimmolate.Probe(ctx.GetSeed(), config.Deck, config.Stake));
        return s;
    }

    // WASM has one thread: the search runs to completion on the calling thread. Returning the
    // engine handle means JS reads final counters straight off it.
    private static async Task<IMotelySearch> Run(IMotelySearchSettings settings, CancellationToken ct)
    {
        var search = settings.WithThreadCount(1).CreateSearch();
        await search.RunSearchAsync(ct);
        return search;
    }
}
