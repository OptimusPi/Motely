#if BOOTSHARP_FILESYSTEM
using System.Text;
using Bootsharp;
using Bootsharp.FileSystem;

/// <summary>
/// Real local-file access for JAML filters via the browser's File System Access API
/// (Bootsharp.FileSystem — sponsor-exclusive extension, see
/// D:\bootsharp\docs\guide\extensions\file-system.md). Lets a user pick a folder once,
/// then save/load/list .jaml files in it directly — no copy-paste, no localStorage.
///
/// The JS half of this extension, @rewaffle/bootsharp-file-system, lives on a private
/// sponsor-only registry, so motely-wasm declares it as an OPTIONAL peer dependency and
/// never as a hard one. A public MIT package that hard-depends on a paid private package
/// 404s on install for every consumer who is not a sponsor — which is exactly what
/// motely-wasm 24.1.1 did to everyone who tried to install it. Sponsors who add the peer
/// get these methods; everyone else gets the whole engine and simply no folder picker.
/// </summary>
public static partial class MotelyFileSystem
{
    private static string? _rootId;
    private static IFileSystem? _fs;

    // No live UI to notify of external changes yet — MotelyFileSystemWatcher.OnChange
    // (below) is what a consumer wires up if/when one is built.
    private class Watcher : IFileWatcher
    {
        public Task HandleFileChanges(IReadOnlyList<Change> changes)
        {
            foreach (var c in changes)
                OnChange?.Invoke($"{c.Type}:{c.Entry.Uri}");
            return Task.CompletedTask;
        }
    }

    [Export]
    public static event Action<string>? OnChange;

    /// <summary>Prompts the user to pick a local folder and mounts it. Returns false if the
    /// user cancelled the picker. Must be called (and awaited) before any other method here.</summary>
    [Export]
    public static async Task<bool> PickAndMountFolder()
    {
        var mounter = MotelyServices.Get<IFileMounter>();
        var root = await mounter.PickRoot(new PickOptions { Id = "jaml-filters" });
        if (root is null)
            return false;
        _rootId = root;
        _fs = await mounter.Mount(root, new Watcher());
        return true;
    }

    [Export]
    public static async Task Unmount()
    {
        if (_rootId is null)
            return;
        await MotelyServices.Get<IFileMounter>().Unmount(_rootId);
        _rootId = null;
        _fs = null;
    }

    private static IFileSystem Fs =>
        _fs ?? throw new InvalidOperationException("No folder mounted — call PickAndMountFolder first.");

    private static string JamlUri(string fileName) =>
        fileName.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase) ? $"/{fileName}" : $"/{fileName}.jaml";

    [Export]
    public static Task SaveJamlFilter(string fileName, string jaml) =>
        Fs.WriteFile(JamlUri(fileName), Encoding.UTF8.GetBytes(jaml));

    [Export]
    public static async Task<string> LoadJamlFilter(string fileName) =>
        Encoding.UTF8.GetString(await Fs.ReadFile(JamlUri(fileName)));

    [Export]
    public static Task DeleteJamlFilter(string fileName) => Fs.DeleteFile(JamlUri(fileName));
}
#endif // BOOTSHARP_FILESYSTEM
