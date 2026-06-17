using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Motely.Filters.Jaml;

public static partial class JamlConfigLoader
{
    /// <summary>
    /// Load a JAML config from a file path. A bare name with no directory and no extension
    /// resolves to <c>JamlFilters/&lt;name&gt;.jaml</c> (the same convention the search path uses);
    /// a <c>.json</c> extension takes the JSON load path, anything else the YAML path. Read or
    /// parse failures return <c>false</c> with a human-readable <paramref name="error"/>.
    /// </summary>
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

        // Bare name (no directory, no extension) → JamlFilters/<name>.jaml
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
            ? TryLoadFromJson(content, out config, out error)
            : TryLoad(content, out config, out error);
    }
}
