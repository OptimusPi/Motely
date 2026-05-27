using System.Text;
using Bootsharp;
using Bootsharp.FileSystem;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Native;

[assembly: Preferences(
    Space = [@"^Motely\.Wasm$", "index"],
    Name = [
        @"^Program$",
        "Motely",
        @"^WasmSearchSettings$",
        "SearchSettings",
        @"^WasmSeedRouter$",
        "SeedRouter",
    ]
)]

namespace Motely.Wasm;

public static partial class Program
{
    /// <summary>JS probe for <see cref="WasmSearchSettings.WithJimmolate"/> — assign before boot.</summary>
    [Import]
    public static partial bool JimmolateProbe(string seed, MotelyDeck deck, MotelyStake stake);

    internal static bool RunJimmolateImport(ref global::Motely.MotelySingleSearchContext ctx) =>
        JimmolateProbe(ctx.GetSeed(), ctx.Deck, ctx.Stake);

    private static IServiceProvider services = null!;
    private static readonly Dictionary<string, IFileSystem> MountedFileSystems = new(
        StringComparer.Ordinal
    );
    private static readonly MotelyFileWatcher FileWatcher = new();

    [Export]
    public static event Action<IReadOnlyList<Change>>? OnFileChanges;

    /// <summary>One callback set per WASM load (<c>bootsharp.boot()</c>). Wired into every search started from this module.</summary>
    [Export]
    public static event Action<MotelyProgress>? OnProgress;

    [Export]
    public static event Action<string>? OnSeedMatch;

    [Export]
    public static event Action<MotelyScoredSeedResult>? OnScoredResult;

    public static void Main()
    {
        services = new ServiceCollection().AddBootsharp().BuildServiceProvider();
    }

    [Export]
    public static string Version() => MotelyVersionConstant.Value;

    [Export]
    public static string ValidateJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            return error ?? "Invalid JAML.";
        try
        {
            JamlSearchBuilder.EnsureRunnablePlan(config);
            return "valid";
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // Exception messages crossing the JSExport boundary under NativeAOT-LLVM trim mode lose
    // their .Message and surface to JS as the opaque "C# exception from NativeAOT" husk. The
    // result-shaped Exports below catch C#-side so the diagnostic survives — mirrors the
    // existing pattern on MotelyJamlyzerResult.Error. FromJaml throws on bad input — call
    // ValidateJaml first. See README JAML API section.

    [Export]
    public static string ExplainJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            return $"# ERROR: {error ?? "Invalid JAML."}";
        try
        {
            return config.HasAnyClauses ? JamlSearchBuilder.ExplainPlan(config) : "";
        }
        catch (Exception ex)
        {
            return $"# ERROR: {ex.Message}";
        }
    }

    [Export]
    public static JamlSearchPlan CreatePlan(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            return new(0, "", []) { Error = error ?? "Invalid JAML." };
        try
        {
            return JamlSearchBuilder.CreatePlan(config);
        }
        catch (Exception ex)
        {
            return new(0, "", []) { Error = ex.Message };
        }
    }

    [Export]
    public static MotelyJamlyzerResult AnalyzeJamlSeeds(string jaml, string[]? seeds = null) =>
        MotelyJamlyzer.AnalyzeSeeds(new(jaml, seeds));

    [Export]
    public static WasmJamlConfig ParseJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error) || config is null)
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return new WasmJamlConfig(config);
    }

    [Export]
    public static string ExplainJamlConfig(WasmJamlConfig config) =>
        config.Config.HasAnyClauses ? JamlSearchBuilder.ExplainPlan(config.Config) : "";

    [Export]
    public static JamlSearchPlan CreatePlanFromConfig(WasmJamlConfig config) =>
        JamlSearchBuilder.CreatePlan(config.Config);

    [Export]
    public static MotelyJamlyzerResult AnalyzeJamlSeedsFromConfig(
        WasmJamlConfig config,
        string[]? seeds = null
    ) => MotelyJamlyzer.AnalyzeSeeds(config.Config, seeds);

    // ── Packed-int decoders ──────────────────────────────────────────────────
    // Return typed enums so Bootsharp emits MotelyItemType, MotelyItemTypeCategory,
    // MotelyJokerRarity, MotelyItemEdition, MotelyItemSeal, MotelyItemEnhancement
    // into the generated .g.mjs — consumers call these instead of manual bit-whacking.
    [Export]
    public static MotelyItemType DecodeItemType(int v) =>
        (MotelyItemType)(v & MotelyGlobals.ItemTypeMask);

    [Export]
    public static MotelyItemTypeCategory DecodeItemCategory(int v) =>
        (MotelyItemTypeCategory)(v & MotelyGlobals.ItemTypeCategoryMask);

    [Export]
    public static MotelyJokerRarity DecodeJokerRarity(int v) =>
        (MotelyJokerRarity)(v & MotelyGlobals.JokerRarityMask);

    [Export]
    public static MotelyItemEdition DecodeItemEdition(int v) =>
        (MotelyItemEdition)(v & MotelyGlobals.ItemEditionMask);

    [Export]
    public static MotelyItemSeal DecodeItemSeal(int v) =>
        (MotelyItemSeal)(v & MotelyGlobals.ItemSealMask);

    [Export]
    public static MotelyItemEnhancement DecodeItemEnhancement(int v) =>
        (MotelyItemEnhancement)(v & MotelyGlobals.ItemEnhancementMask);

    [Export]
    public static MotelyStandardcardSuit DecodeStandardcardSuit(int v) =>
        (MotelyStandardcardSuit)(v & MotelyGlobals.StandardcardSuitMask);

    [Export]
    public static MotelyStandardcardRank DecodeStandardcardRank(int v) =>
        (MotelyStandardcardRank)(v & MotelyGlobals.StandardcardRankMask);

    [Export]
    public static bool IsPerishable(int v) =>
        (v & (1 << MotelyGlobals.PerishableStickerOffset)) != 0;

    [Export]
    public static bool IsEternal(int v) => (v & (1 << MotelyGlobals.EternalStickerOffset)) != 0;

    [Export]
    public static bool IsRental(int v) => (v & (1 << MotelyGlobals.RentalStickerOffset)) != 0;

    /// <summary>
    /// Passthrough search settings (no JAML clauses). CLI requires <c>--jaml</c> or <c>--native</c>;
    /// this is the WASM equivalent of starting from an empty filter before attaching a mode.
    /// </summary>
    [Export]
    public static WasmSearchSettings CreateSearchSettings() =>
        new(
            AttachWasmCallbacks(
                new global::Motely.MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
                    new PassthroughFilterDesc()
                )
            )
        );

    /// <summary>Built-in native C# filters (CLI <c>--native</c> set).</summary>
    [Export]
    public static WasmSearchSettings CreateNativeSearchSettings(string name)
    {
        if (!MotelyNativeFilterNames.TryParse(name, out var filter))
            throw new ArgumentException(
                $"Unknown native filter '{name}'. Known: {string.Join(", ", MotelyNativeFilterNames.DisplayNames)}"
            );
        return new WasmSearchSettings(
            AttachWasmCallbacks(MotelyNativeFilterFactory.CreateSettings(filter))
        );
    }

    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;

    /// <summary>Apply a JAML document to search settings (validate with <see cref="ValidateJaml"/> first).</summary>
    [Export]
    public static WasmSearchSettings FromJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return new WasmSearchSettings(
            AttachWasmCallbacks(JamlSearchBuilder.CreateSettings(config))
        );
    }

    [Export]
    public static WasmSearchSettings FromJamlConfig(WasmJamlConfig config)
    {
        return new WasmSearchSettings(
            AttachWasmCallbacks(JamlSearchBuilder.CreateSettings(config.Config))
        );
    }

    private static IMotelySearchSettings AttachWasmCallbacks(IMotelySearchSettings settings)
    {
        if (OnProgress is not null)
            settings = settings.WithProgressCallback(p => OnProgress(p));
        if (OnSeedMatch is not null)
            settings = settings.WithSeedMatchCallback(seed => OnSeedMatch(seed));
        if (OnScoredResult is not null)
            settings = settings.WithScoredResultCallback(tally =>
                OnScoredResult(MotelyScoredSeedResult.FromTally(in tally))
            );
        return settings;
    }

    /// <summary>
    /// One-seed passthrough search. Use <see cref="CreateStreamCursor"/> for PRNG streams (shop, joker, …).
    /// Keep the router alive until you dispose it or finish stream reads.
    /// </summary>
    [Export]
    public static WasmSeedRouter CreateSeedRouter(string seed, MotelyDeck deck, MotelyStake stake)
    {
        return new WasmSeedRouter(seed, deck, stake);
    }

    [Export]
    public static async Task<string?> PickRoot(PickOptions? options = null) =>
        await Mounter().PickRoot(options);

    [Export]
    public static async Task<string> MountRoot(string root, MountOptions? options = null)
    {
        var fs = await Mounter().Mount(root, FileWatcher, options);
        MountedFileSystems[root] = fs;
        return root;
    }

    [Export]
    public static async Task UnmountRoot(string root)
    {
        MountedFileSystems.Remove(root);
        await Mounter().Unmount(root);
    }

    [Export]
    public static async Task<string> ReadTextFile(string root, string uri)
    {
        var bytes = await GetFileSystem(root).ReadFile(uri);
        return Encoding.UTF8.GetString(bytes);
    }

    [Export]
    public static async Task WriteTextFile(string root, string uri, string text) =>
        await GetFileSystem(root).WriteFile(uri, Encoding.UTF8.GetBytes(text));

    private static IFileMounter Mounter() => services.GetRequiredService<IFileMounter>();

    private static IFileSystem GetFileSystem(string root) =>
        MountedFileSystems.TryGetValue(root, out var fs)
            ? fs
            : throw new InvalidOperationException($"File system root '{root}' is not mounted.");

    private sealed class MotelyFileWatcher : IFileWatcher
    {
        public Task HandleFileChanges(IReadOnlyList<Change> changes)
        {
            OnFileChanges?.Invoke(changes);
            return Task.CompletedTask;
        }
    }
}
