using Bootsharp;
using Bootsharp.FileSystem;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using System.Reflection;
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
    public static event Action<MotelyProgress>? OnProgress;

    public static void Main()
    {
        services = new ServiceCollection()
            .AddBootsharp()
            .BuildServiceProvider();
    }

    [Export]
    public static string Version() =>
        typeof(MotelyDeck).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

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
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return config.HasAnyClauses ? JamlSearchBuilder.ExplainPlan(config) : "";
    }

    [Export]
    public static JamlSearchPlan CreatePlan(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return JamlSearchBuilder.CreatePlan(config);
    }

    [Export]
    public static MotelyJamlyzerResult AnalyzeJamlSeeds(string jaml, string[] seeds) =>
        MotelyJamlyzer.AnalyzeSeeds(new(jaml, seeds));

    [Export]
    public static IMotelySearchSettings CreateSearch(string jaml)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            throw new InvalidOperationException(error ?? "Invalid JAML.");
        return JamlSearchBuilder.CreateSettings(config)
            .WithSeedMatchCallback(seed => OnSeedMatch?.Invoke(seed))
            .WithProgressCallback(p => OnProgress?.Invoke(p));
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
