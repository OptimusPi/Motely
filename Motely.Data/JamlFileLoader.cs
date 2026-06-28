using System.Diagnostics.CodeAnalysis;
using Motely.Filters.Jaml;

namespace Motely.Data;

public static class JamlFileLoader
{
    public static bool TryLoadFromPath(
        string path,
        [NotNullWhen(true)] out JamlConfig? config,
        out string? error
    )
    {
        config = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "No JAML path provided.";
            return false;
        }

        if (!Path.IsPathRooted(path) && !Path.HasExtension(path))
            path = Path.Combine("JamlFilters", path + ".jaml");

        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            error = $"Error reading JAML file '{path}': {ex.Message}";
            return false;
        }

        return string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
            ? JamlConfigLoader.TryLoadFromJson(content, out config, out error)
            : JamlConfigLoader.TryLoad(content, out config, out error);
    }
}
