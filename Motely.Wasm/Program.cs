using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bootsharp;
using Bootsharp.FileSystem;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Native;
using Motely.SeedProviders;
using System.Text.Json;

[assembly: Preferences(
    Space = [
        @"^Motely\.Wasm$", "index",
        @"^Motely(\..*)?$", "index"
    ],
    Name = [
        @"^Program$",
        "Motely",
    ]
)]



namespace Motely.Wasm;

public static partial class Program
{
    private static IServiceProvider services = null!;
    private static readonly Dictionary<string, IFileSystem> MountedFileSystems = new(StringComparer.Ordinal);
    private static readonly MotelyFileWatcher FileWatcher = new();

    /// <summary>JS probe for WithJimmolate — assign before boot.</summary>
    [Import]
    public static partial bool JimmolateProbe(string seed, MotelyDeck deck, MotelyStake stake);

    [Import]
    public static partial void ReportWasmError(string message);

    internal static bool RunJimmolateImport(ref global::Motely.MotelySingleSearchContext ctx) =>
        JimmolateProbe(ctx.GetSeed(), ctx.Deck, ctx.Stake);

    /// <summary>One callback set per WASM load (<c>bootsharp.boot()</c>). Wired into every search started from this module.</summary>
    [Export]
    public static event Action<MotelyProgress>? OnProgress;

    [Export]
    public static event Action<string>? OnSeedMatch;

    [Export]
    public static event Action<MotelyScoredSeedResult>? OnScoredResult;

    [Export]
    public static event Action<IReadOnlyList<Change>>? OnFileChanges;

    public static void Main()
    {
        services = new ServiceCollection()
            .AddBootsharp()
            .BuildServiceProvider();
    }

    [Export]
    public static void EnableJimmolate()
    {
        MotelyWasmInterop.JimmolateSearcher = RunJimmolateImport;
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
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    [Export]
    public static async Task WriteTextFile(string root, string uri, string text) =>
        await GetFileSystem(root).WriteFile(uri, System.Text.Encoding.UTF8.GetBytes(text));

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

    [Export]
    public static string ExplainJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            return $"# ERROR: {error ?? "Invalid JAML."}";
        try
        {
            return config.HasAnyClauses() ? JamlSearchBuilder.ExplainPlan(config) : "";
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
    public static string JamlToJson(string jaml)
    {
        if (!JamlConfigLoader.TryParseRoot(jaml, out var doc, out var error) || doc is null)
            throw new InvalidOperationException(error ?? "Failed to parse JAML.");
        return JsonSerializer.Serialize(doc, JamlJsonContext.Default.JamlRootDocument);
    }

    [Export]
    public static string JsonToJaml(string json)
    {
        var doc = JsonSerializer.Deserialize<JamlRootDocument>(json, JamlJsonContext.Default.JamlRootDocument);
        if (doc is null)
            throw new InvalidOperationException("Failed to deserialize JamlRootDocument from JSON.");
        return JamlConfigLoader.SerializeRoot(doc);
    }

    [Export]
    public static JamlConfig ParseJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error) || config is null)
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return config;
    }

    [Export]
    public static string ExplainJamlConfig(JamlConfig config) =>
        config.HasAnyClauses() ? JamlSearchBuilder.ExplainPlan(config) : "";

    [Export]
    public static JamlSearchPlan CreatePlanFromConfig(JamlConfig config) =>
        JamlSearchBuilder.CreatePlan(config);

    [Export]
    public static MotelyJamlyzerResult AnalyzeJamlSeedsFromConfig(
        JamlConfig config,
        string[]? seeds = null
    ) => MotelyJamlyzer.AnalyzeSeeds(config, seeds);

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

    private static IMotelySearch CreateWasmSearch(IMotelySearchSettings settings)
    {
        settings = AttachWasmCallbacks(settings)
            .WithThreadCount(1);
        
        // Auto-attach Jimmolate if JS registered the import probe
        if (MotelyWasmInterop.JimmolateSearcher is not null)
        {
            settings = settings.WithJimmolate();
        }

        return settings.Start();
    }

    [Export]
    public static IMotelyWasmSearchSettings FromJaml(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        var settings = JamlSearchBuilder.CreateSettings(config);
        var s = settings.WithThreadCount(1);
        if (MotelyWasmInterop.JimmolateSearcher is not null)
        {
            s = s.WithJimmolate();
        }
        return new MotelyWasmSearchSettings(s);
    }

    [Export]
    public static IMotelyWasmSearchSettings CreateSearchSettings()
    {
        var settings = new global::Motely.MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
            new PassthroughFilterDesc()
        );
        var s = settings.WithThreadCount(1);
        if (MotelyWasmInterop.JimmolateSearcher is not null)
        {
            s = s.WithJimmolate();
        }
        return new MotelyWasmSearchSettings(s);
    }

    [Export]
    public static IMotelyWasmSearchSettings CreateNativeSearchSettings(string filterName)
    {
        if (!MotelyNativeFilterNames.TryParse(filterName, out var filter))
            throw new ArgumentException(
                $"Unknown native filter '{filterName}'. Known: {string.Join(", ", MotelyNativeFilterNames.DisplayNames)}"
            );
        var settings = MotelyNativeFilterFactory.CreateSettings(filter);
        var s = settings.WithThreadCount(1);
        if (MotelyWasmInterop.JimmolateSearcher is not null)
        {
            s = s.WithJimmolate();
        }
        return new MotelyWasmSearchSettings(s);
    }

    [Export]
    public static IMotelySearch StartRandomSearch(string jaml, int count)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithRandomSearch(count);
        return CreateWasmSearch(settings);
    }

    [Export]
    public static IMotelySearch StartSequentialSearch(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithSequentialSearch();
        return CreateWasmSearch(settings);
    }

    [Export]
    public static IMotelySearch StartSeedListSearch(string jaml, string[] seeds)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithListSearch(seeds, seeds.Length);
        return CreateWasmSearch(settings);
    }

    [Export]
    public static IMotelySearch StartAestheticSearch(string jaml, JamlAesthetic aesthetic)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithAestheticSearch(aesthetic);
        return CreateWasmSearch(settings);
    }

    [Export]
    public static IMotelySearch StartNativeListSearch(string filterName, string[] seeds)
    {
        if (!MotelyNativeFilterNames.TryParse(filterName, out var filter))
            throw new ArgumentException(
                $"Unknown native filter '{filterName}'. Known: {string.Join(", ", MotelyNativeFilterNames.DisplayNames)}"
            );
        var settings = MotelyNativeFilterFactory.CreateSettings(filter)
            .WithListSearch(seeds, seeds.Length);
        return CreateWasmSearch(settings);
    }

    [Export]
    public static IMotelySearch StartPassthroughListSearch(string[] seeds)
    {
        var settings = new global::Motely.MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
            new PassthroughFilterDesc()
        ).WithListSearch(seeds, seeds.Length);
        return CreateWasmSearch(settings);
    }

    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;

    internal static IMotelySearchSettings AttachWasmCallbacks(IMotelySearchSettings settings)
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

    [Export]
    public static IMotelyStreamCursor CreateStreamCursor(string seed, MotelyDeck deck, MotelyStake stake, int ante, MotelyStreamKind kind)
    {
        return new MotelyStreamCursor(seed, deck, stake, ante, kind);
    }

    /// <summary>
    /// Placeholder for required API tests.
    /// </summary>
    [Export]
    public static string Seed() => "";
}

public enum MotelyStreamKind
{
    Shop = 0,
    Joker = 1,
    Tarot = 2,
    Planet = 3,
    Spectral = 4,
    LegendaryJoker = 5,
    RareTagJoker = 6
}

public class MotelyStreamCursor : IMotelyStreamCursor
{
    private readonly MotelySeedRouterDesc _routerDesc;
    private readonly MotelySingleSearchContext _context;
    private readonly MotelyStreamKind _kind;

    private MotelySingleShopItemStream _shopStream;
    private MotelySingleJokerStream _jokerStream;
    private MotelySingleTarotStream _tarotStream;
    private MotelySinglePlanetStream _planetStream;
    private MotelySingleSpectralStream _spectralStream;
    private MotelySingleJokerFixedRarityStream _fixedRarityStream;

    public MotelyStreamCursor(string seed, MotelyDeck deck, MotelyStake stake, int ante, MotelyStreamKind kind)
    {
        _kind = kind;
        _routerDesc = new MotelySeedRouterDesc(seed, deck, stake);
        _context = _routerDesc.Instance();

        switch (kind)
        {
            case MotelyStreamKind.Shop:
                _shopStream = _context.CreateShopItemStream(ante);
                break;
            case MotelyStreamKind.Joker:
                _jokerStream = _context.CreateShopJokerStream(ante);
                break;
            case MotelyStreamKind.Tarot:
                _tarotStream = _context.CreateShopTarotStream(ante);
                break;
            case MotelyStreamKind.Planet:
                _planetStream = _context.CreateShopPlanetStream(ante);
                break;
            case MotelyStreamKind.Spectral:
                _spectralStream = _context.CreateShopSpectralStream(ante);
                break;
            case MotelyStreamKind.LegendaryJoker:
                _fixedRarityStream = _context.CreateLegendaryJokerStream(ante);
                break;
            case MotelyStreamKind.RareTagJoker:
                _fixedRarityStream = _context.CreateRareTagJokerStream(ante);
                break;
        }
    }

    public int GetNext()
    {
        switch (_kind)
        {
            case MotelyStreamKind.Shop:
                return _context.GetNextShopItem(ref _shopStream).Value;
            case MotelyStreamKind.Joker:
                return _context.GetNextJoker(ref _jokerStream).Value;
            case MotelyStreamKind.Tarot:
                return _context.GetNextTarot(ref _tarotStream).Value;
            case MotelyStreamKind.Planet:
                return _context.GetNextPlanet(ref _planetStream).Value;
            case MotelyStreamKind.Spectral:
                return _context.GetNextSpectral(ref _spectralStream).Value;
            case MotelyStreamKind.LegendaryJoker:
            case MotelyStreamKind.RareTagJoker:
                return _context.GetNextJoker(ref _fixedRarityStream).Value;
            default:
                throw new NotSupportedException();
        }
    }

    public int[] GetNextChunk(int count)
    {
        var chunk = new int[count];
        for (int i = 0; i < count; i++)
        {
            chunk[i] = GetNext();
        }
        return chunk;
    }

    public void Dispose() => _routerDesc.Dispose();
}

public interface IMotelyWasmSearchSettings
{
    IMotelyWasmSearchSettings WithSequentialSearch();
    IMotelyWasmSearchSettings WithRandomSearch(int count);
    IMotelyWasmSearchSettings WithListSearch(string[] seeds, int count);
    IMotelyWasmSearchSettings WithAestheticSearch(JamlAesthetic aesthetic);
    IMotelyWasmSearchSettings WithDeck(MotelyDeck deck);
    IMotelyWasmSearchSettings WithStake(MotelyStake stake);
    IMotelyWasmSearchSettings WithJimmolate();
    IMotelyWasmSearchSettings WithThreadCount(int count);
    IMotelyWasmSearchSettings WithProgressReportIntervalMs(long intervalMs);
    IMotelyWasmSearchSettings WithBatchCharacterCount(int count);
    IMotelyWasmSearchSettings WithStartBatchIndex(long startIndex);
    IMotelyWasmSearchSettings WithEndBatchIndex(long endIndex);
    IMotelySearch Start();
}

public class MotelyWasmSearchSettings : IMotelyWasmSearchSettings
{
    private readonly IMotelySearchSettings _settings;

    public MotelyWasmSearchSettings(IMotelySearchSettings settings)
    {
        _settings = settings;
    }

    public IMotelyWasmSearchSettings WithSequentialSearch()
    {
        _settings.WithSequentialSearch();
        return this;
    }

    public IMotelyWasmSearchSettings WithRandomSearch(int count)
    {
        _settings.WithRandomSearch(count);
        return this;
    }

    public IMotelyWasmSearchSettings WithListSearch(string[] seeds, int count)
    {
        _settings.WithListSearch(seeds, count);
        return this;
    }

    public IMotelyWasmSearchSettings WithAestheticSearch(JamlAesthetic aesthetic)
    {
        _settings.WithAestheticSearch(aesthetic);
        return this;
    }

    public IMotelyWasmSearchSettings WithDeck(MotelyDeck deck)
    {
        _settings.WithDeck(deck);
        return this;
    }

    public IMotelyWasmSearchSettings WithStake(MotelyStake stake)
    {
        _settings.WithStake(stake);
        return this;
    }

    public IMotelyWasmSearchSettings WithJimmolate()
    {
        _settings.WithJimmolate();
        return this;
    }

    public IMotelyWasmSearchSettings WithThreadCount(int count)
    {
        _settings.WithThreadCount(count);
        return this;
    }

    public IMotelyWasmSearchSettings WithProgressReportIntervalMs(long intervalMs)
    {
        _settings.WithProgressReportIntervalMs(intervalMs);
        return this;
    }

    public IMotelyWasmSearchSettings WithBatchCharacterCount(int count)
    {
        _settings.WithBatchCharacterCount(count);
        return this;
    }

    public IMotelyWasmSearchSettings WithStartBatchIndex(long startIndex)
    {
        _settings.WithStartBatchIndex(startIndex);
        return this;
    }

    public IMotelyWasmSearchSettings WithEndBatchIndex(long endIndex)
    {
        _settings.WithEndBatchIndex(endIndex);
        return this;
    }

    public IMotelySearch Start()
    {
        var s = Program.AttachWasmCallbacks(_settings);
        return s.Start();
    }
}
