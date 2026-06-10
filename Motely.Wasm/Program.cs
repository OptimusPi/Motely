using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Bootsharp;
using Bootsharp.FileSystem;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Native;
using Motely.SeedProviders;

namespace Motely.Wasm;

// Bootsharp 0.8.0 replaced [assembly: Preferences(Space=…, Name=…)] with the renaming API
// (docs/guide/renaming.md). We keep Bootsharp's DEFAULT module/node mapping so the published
// surface stays stable for consumers (jaml-ui imports `motely-wasm/motely/wasm`,
// `motely/analysis`, `motely/enums`, `motely/filters/jaml`, and the `Program` node). Folding
// everything into `index` or renaming `Program` → `Motely` is a breaking API change; don't.
public static class BootsharpRenamers
{
    [RenameNode]
    public static string? RenameNode(Type type, string @default) =>
        // Ref-struct types (MotelyRunState, Span<T>) can never marshal; erase them from the
        // surface. They linger as serialized types because they were registered while inspecting
        // members that RenameMember later erased. Everything else keeps its default node name.
        type.IsByRefLike ? null : @default;

    // MotelySingleSearchContext crosses to JS (the JimmolateProbe import), so Bootsharp tries to
    // instance-bind its whole surface. Most of that surface is SIMD value/ref-struct types
    // (per-seed streams, MotelyRunState, MotelySingleItemSet, Span) that cannot marshal — they
    // generate Resolve<T&> / un-serializable / non-instance errors. Returning null erases the
    // member from the generated JS, so Bootsharp never emits interop for it. Marshallable members
    // (e.g. GetSeed) survive; the unmarshallable per-seed stream surface is dropped.
    [RenameMember]
    public static string? RenameMember(MemberInfo info, string @default) =>
        info is MethodInfo m
        && m.DeclaringType == typeof(MotelySingleSearchContext)
        && (m.GetParameters().Any(p => Unmarshallable(p.ParameterType)) || Unmarshallable(m.ReturnType))
            ? null
            : @default;

    private static bool Unmarshallable(Type t)
    {
        if (t.IsByRef)
            return true; // ref/in/out — Bootsharp would emit Instances.Resolve<T&>
        if (t.IsByRefLike)
            return true; // ref structs: MotelyRunState, Span<T>
        var n = t.Name;
        return n.StartsWith("MotelySingle", StringComparison.Ordinal)
            && (n.EndsWith("Stream", StringComparison.Ordinal) || n == "MotelySingleItemSet");
    }
}

public static partial class Program
{
    private static IServiceProvider services = null!;
    private static readonly Dictionary<string, IFileSystem> MountedFileSystems = new(
        StringComparer.Ordinal
    );
    private static readonly MotelyFileWatcher FileWatcher = new();

    [Import]
    public static partial void ReportWasmError(string message); // TODO this doesnt seem right

    // Jimmolate = the OG Immolate `filter(seed) => keep?` model, in the browser. JS assigns
    // `Motely.jimmolatePredicate = (result) => bool` before boot; it runs per SCORED seed on
    // the marshallable scored result (Seed/Score/Tallies). No engine driving, no ref-struct
    // streams across the boundary (that was the 65-wrapper trap) — C# does the work, the
    // predicate decides. A seed reaches JS only if the predicate keeps it.
    [Import]
    public static partial bool JimmolatePredicate(MotelyScoredSeedResult result);

    // Gate: a bare unassigned [Import] throws when invoked, so consumers set this true after
    // wiring `jimmolatePredicate`. When false, every scored seed is reported (no filtering).
    [Export]
    public static bool JimmolateEnabled { get; set; }

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
        services = new ServiceCollection().AddBootsharp().BuildServiceProvider();
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
        if (!JamlConfigLoader.TryParseRootJson(json, out var doc, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML JSON.");
        return JamlConfigLoader.SerializeRoot(doc);
    }

    // Pure forwards: Bootsharp [Export] must live in this assembly (the engine never
    // references Bootsharp), but the logic is the loader's.
    [Export]
    public static JamlConfig FromJaml(string jaml) => JamlConfigLoader.FromYaml(jaml);

    [Export]
    public static JamlConfig FromJson(string json) => JamlConfigLoader.FromJson(json);

    [Export]
    public static string ExplainJaml(JamlConfig config) =>
        config.Must.Count != 0 || config.Should.Count != 0 || config.MustNot.Count != 0
            ? JamlSearchBuilder.ExplainPlan(config)
            : "";

    [Export]
    public static JamlSearchPlan CreatePlan(JamlConfig config) =>
        JamlSearchBuilder.CreatePlan(config);

    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;

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
        long progressReportIntervalMs = 500
    )
    {
        var settings = JamlSearchBuilder
            .CreateSettings(config)
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
            throw new InvalidOperationException(
                "JamlConfig.Seeds is empty; populate it before calling RunSeedListSearch."
            );
        var seeds = config.Seeds.ToArray();
        return RunSearch(
            JamlSearchBuilder.CreateSettings(config).WithListSearch(seeds, seeds.Length)
        );
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
        return RunSearch(
            MotelyNativeFilterFactory.CreateSettings(filter).WithListSearch(seeds, seeds.Length)
        );
    }

    [Export]
    public static IMotelySearch RunPassthroughListSearch(string[] seeds) =>
        RunSearch(
            new global::Motely.MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
                new PassthroughFilterDesc()
            ).WithListSearch(seeds, seeds.Length)
        );

    private static IMotelySearch RunSearch(IMotelySearchSettings settings)
    {
        settings = AttachWasmCallbacks(settings).WithThreadCount(1);

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
            {
                var result = MotelyScoredSeedResult.FromTally(in tally);
                // Jimmolate: the JS `filter(result) => keep?` predicate. Drop what it rejects.
                if (JimmolateEnabled && !JimmolatePredicate(result))
                    return;
                OnScoredResult(result);
            });
        return settings;
    }
}
