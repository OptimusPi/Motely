using System.Diagnostics.CodeAnalysis;
using Motely.Filters;

namespace Motely.DataLake;

public static class JamlFileSource
{
    public static bool TryLoadFromFile(
        string path,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error
    )
    {
        config = null;
        var resolved = ResolvePath(path);
        if (resolved == null)
        {
            error = $"File not found: {path}";
            return false;
        }
        return JamlConfigLoader.TryLoad(File.ReadAllText(resolved), out config, out error);
    }

    public static string? ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.Trim();

        if (File.Exists(path)) return path;

        var withExt = Path.ChangeExtension(path, ".jaml");
        if (File.Exists(withExt)) return withExt;

        var inFilters = Path.Combine("JamlFilters", path);
        if (File.Exists(inFilters)) return inFilters;

        var inFiltersExt = Path.Combine("JamlFilters", withExt);
        if (File.Exists(inFiltersExt)) return inFiltersExt;

        return null;
    }
}
