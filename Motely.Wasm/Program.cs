using System;
using System.Collections.Generic;
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

namespace Motely.Wasm;

// Bootsharp 0.8.0 replaced [assembly: Preferences(Space=…, Name=…)] with the renaming API
// (docs/guide/renaming.md). Space → RenameModule (fold every Motely namespace into the single
// `index` module); Name → RenameNode (project the `Program` node as `Motely`).
internal static class BootsharpRenamers
{
    [RenameModule]
    public static string RenameModule(Type type, string @default)
    {
        var ns = type.Namespace ?? "";
        return ns == "Motely" || ns == "Motely.Wasm" || ns.StartsWith("Motely.", StringComparison.Ordinal)
            ? "index"
            : @default;
    }

    [RenameNode]
    public static string RenameNode(Type type, string @default) =>
        @default == "Program" ? "Motely" : @default;
}

public static partial class Program
{
    private static IServiceProvider services = null!;
    private static readonly Dictionary<string, IFileSystem> MountedFileSystems = new(StringComparer.Ordinal);
    private static readonly MotelyFileWatcher FileWatcher = new();
    private static MotelySeedRouterDesc? _seedRouter;

    [Import]
    public static partial bool JimmolateProbe(MotelySingleSearchContext ctx);

    [Import]
    public static partial void ReportWasmError(string message);

    internal static bool RunJimmolateImport(ref global::Motely.MotelySingleSearchContext ctx) =>
        JimmolateProbe(ctx);

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
        JamlSearchBuilder.EnsureRunnablePlan(config);
        return config;
    }

    [Export]
    public static string ExplainJaml(JamlConfig config) =>
        config.Must.Count != 0
        || config.Should.Count != 0
        || config.MustNot.Count != 0
            ? JamlSearchBuilder.ExplainPlan(config)
            : "";

    [Export]
    public static JamlSearchPlan CreatePlan(JamlConfig config) =>
        JamlSearchBuilder.CreatePlan(config);

    [Export]
    public static MotelyJamlyzerResult Jamlyzer(JamlConfig config) =>
        MotelyJamlyzer.AnalyzeSeeds(config);

    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;

    // Returns the real MotelySingleSearchContext for (seed, deck, stake) — the same context
    // C# unit tests use directly. It's a `public partial class`, projected to JS as an instance proxy.
    [Export]
    public static MotelySingleSearchContext SeedContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _seedRouter?.Dispose();
        _seedRouter = new MotelySeedRouterDesc(seed, deck, stake);
        return _seedRouter.Instance();
    }

    // ── Search entry points ──
    // WASM has no pthreads, so every Run* call BLOCKS the calling thread until the search completes.
    // Consumers wanting a non-blocking UI should call from a Web Worker. Progress/match/scored events
    // fire on Motely.onProgress / onSeedMatch / onScoredResult during the run.

    [Export]
    public static IMotelySearch RunSequentialSearch(
        JamlConfig config,
        long startBatchIndex = 0,
        long endBatchIndex = long.MaxValue,
        int batchCharacterCount = 4,
        long progressReportIntervalMs = 500)
    {
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithSequentialSearch()
            .WithStartBatchIndex(startBatchIndex)
            .WithEndBatchIndex(endBatchIndex)
            .WithBatchCharacterCount(batchCharacterCount)
            .WithProgressReportIntervalMs(progressReportIntervalMs);
        return RunSearch(settings);
    }

    [Export]
    public static IMotelySearch RunRandomSearch(JamlConfig config, int count) =>
        RunSearch(JamlSearchBuilder.CreateSettings(config).WithRandomSearch(count));

    [Export]
    public static IMotelySearch RunSeedListSearch(JamlConfig config)
    {
        if (config.Seeds.Count == 0)
            throw new InvalidOperationException("JamlConfig.Seeds is empty; populate it before calling RunSeedListSearch.");
        var seeds = config.Seeds.ToArray();
        return RunSearch(JamlSearchBuilder.CreateSettings(config).WithListSearch(seeds, seeds.Length));
    }

    [Export]
    public static IMotelySearch RunAestheticSearch(JamlConfig config, JamlAesthetic aesthetic) =>
        RunSearch(JamlSearchBuilder.CreateSettings(config).WithAestheticSearch(aesthetic));

    [Export]
    public static IMotelySearch RunNativeListSearch(string filterName, string[] seeds)
    {
        if (!MotelyNativeFilterNames.TryParse(filterName, out var filter))
            throw new ArgumentException(
                $"Unknown native filter '{filterName}'. Known: {string.Join(", ", MotelyNativeFilterNames.DisplayNames)}"
            );
        return RunSearch(MotelyNativeFilterFactory.CreateSettings(filter).WithListSearch(seeds, seeds.Length));
    }

    [Export]
    public static IMotelySearch RunPassthroughListSearch(string[] seeds) =>
        RunSearch(new global::Motely.MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
            new PassthroughFilterDesc()
        ).WithListSearch(seeds, seeds.Length));

    private static IMotelySearch RunSearch(IMotelySearchSettings settings)
    {
        settings = AttachWasmCallbacks(settings).WithThreadCount(1);
        if (MotelyWasmInterop.JimmolateSearcher is not null)
            settings = settings.WithJimmolate();

        // WASM + threadCount=1: Start() pokes through the pthread path and runs the search
        // synchronously on the calling thread. By the time it returns, the search is done —
        // counters on the returned IMotelySearch handle are ready to read from JS.
        return settings.Start();
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

}
