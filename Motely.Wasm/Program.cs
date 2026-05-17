using Bootsharp;
using Bootsharp.FileSystem;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.Native;
using System.Text;

[assembly: Preferences(Space = [@"^Motely\.Wasm\.Program$", "Motely"])]

namespace Motely.Wasm;

public static partial class Program
{
    private static IServiceProvider services = null!;
    private static readonly Dictionary<string, IFileSystem> MountedFileSystems = new(StringComparer.Ordinal);
    private static readonly MotelyFileWatcher FileWatcher = new();

    [Export]
    public static event Action<IReadOnlyList<Change>>? OnFileChanges;

    [Export]
    public static event Action<string>? OnSeedMatch;

    [Export]
    public static event Action<MotelyScoredSeedResult>? OnScoredResult;

    [Export]
    public static event Action<MotelyProgress>? OnProgress;

    public static void Main()
    {
        services = new ServiceCollection()
            .AddBootsharp()
            .BuildServiceProvider();
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
    // existing pattern on MotelyJamlyzerResult.Error. CreateSearch must still throw (instance-
    // proxied return), so its contract is "call ValidateJaml first." See README JAML API section.

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
    public static MotelyJamlyzerResult AnalyzeJamlSeeds(string jaml, string[] seeds) =>
        MotelyJamlyzer.AnalyzeSeeds(new(jaml, seeds));

    [Export]
    public static IMotelySearchSettingsInterop CreateSearch(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return AttachInteropCallbacks(JamlSearchBuilder.CreateSettings(config));
    }

    [Export]
    public static IMotelySearchSettingsInterop CreateSearchSettings() =>
        AttachInteropCallbacks(
            new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(
                new PassthroughFilterDesc()
            )
        );

    // One-shot convenience: parse, configure, and start a search in a single call. Same JAML
    // throw contract as CreateSearch (call ValidateJaml first). Options are serializable
    // primitives only so the record crosses the Bootsharp boundary cleanly.
    [Export]
    public static IMotelySearch StartSearch(string jaml, JamlSearchOptions? options = null)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        var settings = AttachInteropCallbacks(JamlSearchBuilder.CreateSettings(config))
            .WithDeck(options?.Deck ?? MotelyDeck.Red)
            .WithStake(options?.Stake ?? MotelyStake.White)
            .WithStartBatchIndex(options?.StartBatchIndex ?? 0);
        if (options?.ThreadCount > 0)
            settings = settings.WithThreadCount(options.ThreadCount);
        if (options?.Seeds is { Length: > 0 } seeds)
            settings = settings.WithListSearch(seeds);
        return settings.Start();
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

    private static IMotelySearchSettingsInterop AttachInteropCallbacks(IMotelySearchSettings settings) =>
        (IMotelySearchSettingsInterop)settings
            .WithSeedMatchCallback(seed => OnSeedMatch?.Invoke(seed))
            .WithScoredResultCallback(tally =>
                OnScoredResult?.Invoke(MotelyScoredSeedResult.FromTally(in tally))
            )
            .WithProgressCallback(p => OnProgress?.Invoke(p));

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

// Bootsharp-safe primitives only — no ref structs, no engine types that don't round-trip
// across the JS boundary. Mirrors the StartSearch builder chain in JS-call shape.
public sealed record JamlSearchOptions(
    MotelyDeck Deck = MotelyDeck.Red,
    MotelyStake Stake = MotelyStake.White,
    int ThreadCount = 0,
    string[]? Seeds = null,
    long StartBatchIndex = 0
);
