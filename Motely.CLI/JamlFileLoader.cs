using System.Diagnostics.CodeAnalysis;
using Motely;
using Motely.Filters.Jaml;

namespace Motely.CLI;

public static class JamlFileLoader
{
    /// <summary>
    /// Turns a user-typed --jaml value into the real file path. A bare name like
    /// "Whimsy_Dicetricks314" (not rooted, no extension) resolves under JamlFilters/ with a .jaml
    /// extension; anything rooted or already-extensioned is used verbatim. Load and save-back both
    /// route through here so they can never disagree about where the file is.
    /// </summary>
    public static string ResolvePath(string path) =>
        !Path.IsPathRooted(path) && !Path.HasExtension(path)
            ? Path.Combine("JamlFilters", path + ".jaml")
            : path;

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

        path = ResolvePath(path);

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

        return JamlConfigLoader.TryLoad(content, out config, out error);
    }

    /// <summary>
    /// Rewrites the top-level seeds: list of the JAML at <paramref name="path"/> with the given
    /// seeds and writes it back. Resolves the path exactly like TryLoadFromPath, then reuses the
    /// validated text transform in Motely core (MotelyTopSeedSink).
    /// </summary>
    public static bool TrySaveSeeds(string path, IReadOnlyList<string> seeds, out string? error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "No JAML path provided.";
            return false;
        }

        path = ResolvePath(path);

        string original;
        try
        {
            original = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        if (!MotelyTopSeedSink.TryRewriteAndValidate(original, seeds, out var updated, out error))
            return false;

        try
        {
            File.WriteAllText(path, updated);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
