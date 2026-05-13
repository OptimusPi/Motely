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
    private static IServiceProvider services = null!;
    private static readonly Dictionary<string, IFileSystem> MountedFileSystems = new(StringComparer.Ordinal);
    private static readonly MotelyFileWatcher FileWatcher = new();

    [Export]
    public static event Action<IReadOnlyList<Change>>? OnFileChanges;

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
    public static JamlLoadResult LoadJaml(string yaml)
    {
        var ok = JamlConfigLoader.TryLoad(yaml, out _, out var error);
        return new JamlLoadResult(ok, error);
    }

    [Export]
    public static JamlPlanResult ExplainJaml(string yaml)
    {
        if (!JamlConfigLoader.TryLoad(yaml, out var config, out var error))
            return new(false, error ?? "Invalid JAML.", null);
        if (!config.HasAnyClauses)
            return new(true, null, "");
        try
        {
            JamlSearchBuilder.CreatePlan(config);
            return new(true, null, JamlSearchBuilder.ExplainPlan(config));
        }
        catch (Exception ex)
        {
            return new(false, ex.Message, null);
        }
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

public sealed record JamlLoadResult(bool Ok, string? Error);
public sealed record JamlPlanResult(bool Ok, string? Error, string? Explanation);
