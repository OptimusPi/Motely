using Bootsharp;
using Bootsharp.FileSystem;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely.Filters;
using System.Reflection;
using System.Text;

[assembly: Preferences(Space = [".+", "Motely"])]

namespace Motely.Wasm;

public static partial class Program
{
    private const int WasmThreadCount = 1;
    private static IServiceProvider services = null!;
    private static readonly object SearchLock = new();
    private static readonly Dictionary<string, IFileSystem> MountedFileSystems = new(StringComparer.Ordinal);
    private static readonly MotelyFileWatcher FileWatcher = new();
    private static IMotelySearch? currentSearch;

    [Export] public static event Action<ScoredSeed>? OnSeedScored;
    [Export] public static event Action<ProgressDto>? OnProgress;
    [Export] public static event Action<SearchSummary>? OnSearchComplete;
    [Export] public static event Action<string>? OnError;
    [Export] public static event Action<IReadOnlyList<Change>>? OnFileChanges;

    public static void Main()
    {
        services = new ServiceCollection().AddBootsharp().BuildServiceProvider();
    }

    [Export]
    public static string Version() =>
        typeof(MotelyDeck).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

    [Export]
    public static JamlLoadResult LoadJaml(string yaml)
    {
        var ok = JamlConfigLoader.TryLoad(yaml, out _, out var error);
        return new JamlLoadResult(ok, error);
    }

    [Export]
    public static JamlPlanResult ExplainJaml(string yaml)
    {
        if (!JamlConfigLoader.TryLoad(yaml, out var config, out var error) || config is null)
            return new(false, error ?? "Invalid JAML.", null);
        try
        {
            var explanation = config.HasAnyClauses ? JamlSearchBuilder.ExplainPlan(config) : "";
            if (config.HasAnyClauses) _ = JamlSearchBuilder.CreatePlan(config);
            return new(true, null, explanation);
        }
        catch (Exception ex) { return new(false, ex.Message, null); }
    }

    [Export]
    public static void StartJamlPageSearch(string yaml, long startBatch = 0, long endBatch = 1, int batchCharacterCount = 4)
    {
        ValidatePage(startBatch, endBatch, batchCharacterCount);
        StartSearch(() =>
        {
            var (config, plan) = ResolveJaml(yaml);
            return plan.Settings
                .WithDeck(config.Deck).WithStake(config.Stake)
                .WithThreadCount(WasmThreadCount)
                .WithBatchCharacterCount(batchCharacterCount)
                .WithStartBatchIndex(startBatch).WithEndBatchIndex(endBatch)
                .WithSequentialSearch()
                .WithProgressCallback(EmitProgress)
                .WithScoredResultCallback(EmitScored)
                .CreateSearch();
        });
    }

    [Export]
    public static void StartJamlSeedListSearch(string yaml, IReadOnlyList<string> seeds)
    {
        StartSearch(() =>
        {
            var (config, plan) = ResolveJaml(yaml);
            var normalized = NormalizeSeeds(seeds);
            return plan.Settings
                .WithDeck(config.Deck).WithStake(config.Stake)
                .WithThreadCount(WasmThreadCount)
                .WithListSearch(normalized, normalized.Count)
                .WithProgressCallback(EmitProgress)
                .WithScoredResultCallback(EmitScored)
                .CreateSearch();
        });
    }

    [Export]
    public static async Task StartJamlFilePageSearch(string root, string uri, long startBatch = 0, long endBatch = 1, int batchCharacterCount = 4) =>
        StartJamlPageSearch(await ReadTextFile(root, uri), startBatch, endBatch, batchCharacterCount);

    [Export]
    public static void CancelSearch() { lock (SearchLock) currentSearch?.Cancel(); }

    [Export]
    public static bool IsSearchRunning() { lock (SearchLock) return currentSearch is { IsCompleted: false }; }

    [Export] public static async Task<string?> PickRoot(PickOptions? options = null) => await Mounter().PickRoot(options);
    [Export] public static async Task<string> MountRoot(string root, MountOptions? options = null)
    {
        var fs = await Mounter().Mount(root, FileWatcher, options);
        MountedFileSystems[root] = fs;
        return root;
    }
    [Export] public static async Task UnmountRoot(string root) { MountedFileSystems.Remove(root); await Mounter().Unmount(root); }
    [Export] public static async Task<string> ReadTextFile(string root, string uri) => Encoding.UTF8.GetString(await GetFs(root).ReadFile(uri));
    [Export] public static async Task WriteTextFile(string root, string uri, string text) => await GetFs(root).WriteFile(uri, Encoding.UTF8.GetBytes(text));
    [Export] public static async Task<JamlLoadResult> LoadJamlFile(string root, string uri) => LoadJaml(await ReadTextFile(root, uri));
    [Export] public static async Task<JamlPlanResult> ExplainJamlFile(string root, string uri) => ExplainJaml(await ReadTextFile(root, uri));

    private static void StartSearch(Func<IMotelySearch> createSearch)
    {
        lock (SearchLock)
        {
            if (currentSearch is { IsCompleted: false })
                throw new InvalidOperationException("A Motely search is already running.");
            currentSearch?.Dispose();
            currentSearch = createSearch();
        }
        try
        {
            currentSearch.Start();
            _ = currentSearch.WaitForCompletionAsync().ContinueWith(static task =>
            {
                if (task.Exception is { } ex) OnError?.Invoke(ex.GetBaseException().Message);
                CompleteCurrentSearch();
            }, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(ex.Message);
            CompleteCurrentSearch(ex.Message);
        }
    }

    private static void CompleteCurrentSearch(string? error = null)
    {
        IMotelySearch? s;
        lock (SearchLock) s = currentSearch;
        if (s is null) return;
        OnSearchComplete?.Invoke(new(s.TotalSeedsSearched, s.MatchingSeeds, s.FilteredSeeds, s.CompletedBatchCount, s.ElapsedMs, error));
    }

    private static void EmitScored(MotelySeedScoreTally tally) =>
        OnSeedScored?.Invoke(new(tally.Seed, tally.Score, tally.TallyValuesSpan.ToArray()));

    private static void EmitProgress(MotelyProgress p) =>
        OnProgress?.Invoke(new(p.CompletedBatchCount, p.TotalBatchCount, p.SeedsSearched, p.MatchingSeeds, p.SeedsPerMillisecond, p.PercentComplete, p.ElapsedMilliseconds, p.EstimatedTimeRemainingMilliseconds));

    private static (JamlConfig Config, JamlSearchPlan Plan) ResolveJaml(string yaml)
    {
        if (!JamlConfigLoader.TryLoad(yaml, out var config, out var error) || config is null)
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        if (!config.HasAnyClauses) throw new InvalidOperationException("JAML has no clauses.");
        return (config, JamlSearchBuilder.CreatePlan(config));
    }

    private static IReadOnlyList<string> NormalizeSeeds(IReadOnlyList<string> seeds)
    {
        var n = new List<string>(seeds.Count);
        for (int i = 0; i < seeds.Count; i++)
            if (!string.IsNullOrWhiteSpace(seeds[i]))
                n.Add(seeds[i].Trim().ToUpperInvariant().Replace('0', 'O'));
        return n;
    }

    private static void ValidatePage(long startBatch, long endBatch, int batchCharacterCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startBatch);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endBatch, startBatch);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchCharacterCount, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batchCharacterCount, 7);
    }

    private static IFileMounter Mounter() => services.GetRequiredService<IFileMounter>();
    private static IFileSystem GetFs(string root) =>
        MountedFileSystems.TryGetValue(root, out var fs) ? fs : throw new InvalidOperationException($"Root '{root}' not mounted.");

    private sealed class MotelyFileWatcher : IFileWatcher
    {
        public Task HandleFileChanges(IReadOnlyList<Change> changes) { OnFileChanges?.Invoke(changes); return Task.CompletedTask; }
    }
}

public sealed record JamlLoadResult(bool Ok, string? Error);
public sealed record JamlPlanResult(bool Ok, string? Error, string? Explanation);
public sealed record ScoredSeed(string Seed, int Score, int[] Tallies);
public sealed record ProgressDto(long CompletedBatches, long TotalBatches, long SeedsSearched, long MatchingSeeds, double SeedsPerMillisecond, double PercentComplete, long ElapsedMilliseconds, long? EstimatedTimeRemainingMilliseconds);
public sealed record SearchSummary(long TotalSeedsSearched, long MatchingSeeds, long FilteredSeeds, long CompletedBatchCount, long ElapsedMilliseconds, string? Error);
