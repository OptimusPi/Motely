using System.Diagnostics.CodeAnalysis;
using Motely.Filters.Jaml;

namespace Motely.DataLake;

/// <summary>
/// Thin shim over the shared <see cref="MotelyJamlFile"/> gateway, kept for existing DataLake / TUI
/// call-sites. Resolution and loading route through the one core implementation shared with the CLI.
/// </summary>
public static class JamlFileSource
{
    public static bool TryLoadFromFile(
        string path,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error
    ) => MotelyJamlFile.TryLoad(path, out config, out error);

    /// <summary>Resolve to an existing file path, or <c>null</c> if none of the candidates exist.</summary>
    public static string? ResolvePath(string path) => MotelyJamlFile.TryResolveExisting(path);
}
