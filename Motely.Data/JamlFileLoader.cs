using System.Diagnostics.CodeAnalysis;
using Motely.Filters.Jaml;

namespace Motely.Data;

/// <summary>
/// File-system entry point for JAML configs. Core never touches <c>System.IO</c>.
/// A bare name with no directory and no extension resolves to
/// <c>JamlFilters/&lt;name&gt;.jaml</c>; a <c>.json</c> extension takes the JSON
/// load path, anything else the YAML path.
/// </summary>
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
