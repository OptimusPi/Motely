using System.Text.Json;
using Motely;
using Motely.API;

namespace Motely.API.Services;

public static class FilterService
{
    /// <summary>
    /// Validates that a file path is within the expected base directory.
    /// Uses defense-in-depth with both StartsWith and GetRelativePath checks.
    /// </summary>
    /// <param name="filePath">The file path to validate</param>
    /// <param name="baseDirectory">The base directory that the file must be within</param>
    /// <param name="fullFilePath">The normalized full file path if validation succeeds</param>
    /// <returns>True if the file path is within the base directory, false otherwise</returns>
    public static bool IsPathWithinDirectory(string filePath, string baseDirectory, out string fullFilePath)
    {
        fullFilePath = Path.GetFullPath(filePath);
        var fullBaseDir = Path.GetFullPath(baseDirectory);
        
        // Normalize the directory path to end with a separator for accurate StartsWith check
        // This prevents false positives like "/app/JamlFilters" matching "/app/JamlFiltersEvil"
        fullBaseDir = Path.TrimEndingDirectorySeparator(fullBaseDir) + Path.DirectorySeparatorChar;
        
        // Defense in depth: Check using both StartsWith and GetRelativePath
        // StartsWith check: Verify the full path is within the expected directory
        // Using Ordinal comparison for case-sensitive security on case-sensitive file systems
        if (!fullFilePath.StartsWith(fullBaseDir, StringComparison.Ordinal))
        {
            return false;
        }
        
        // GetRelativePath check: Verify no ".." path traversal attempts
        var relativePath = Path.GetRelativePath(fullBaseDir, fullFilePath);
        if (relativePath.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Sanitizes a filter name to prevent path traversal attacks.
    /// Extracts just the filename stem (no path separators, no extension) and replaces invalid characters.
    /// </summary>
    /// <param name="name">The filter name to sanitize</param>
    /// <param name="safeName">The sanitized filename stem, or null if the name is invalid</param>
    /// <returns>True if the name is valid and was sanitized, false otherwise</returns>
    public static bool TrySanitizeFilterName(string? name, out string? safeName)
    {
        safeName = null;
        
        if (string.IsNullOrEmpty(name))
            return false;
        
        // Extract just the filename stem (no path separators, no extension)
        var sanitized = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(sanitized))
            return false;
        
        // Remove any remaining path separators or invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }
        
        safeName = sanitized;
        return true;
    }
    
    public static string GetFilterJaml(string? filterId)
    {
        if (!TrySanitizeFilterName(filterId, out var safeName))
            return string.Empty;
        
        var filterPath = Path.Combine(MotelyPaths.JamlFiltersDir, $"{safeName}.jaml");
        
        // Validate that the resolved path is within the expected directory
        if (!IsPathWithinDirectory(filterPath, MotelyPaths.JamlFiltersDir, out var fullFilterPath))
        {
            return string.Empty;
        }
        
        if (!File.Exists(fullFilterPath))
            return string.Empty;
            
        return File.ReadAllText(fullFilterPath);
    }

    public static List<object> LoadFiltersFromDisk(string filtersPath, Func<global::Motely.Filters.MotelyJsonConfig?, bool> hasErraticFilters)
    {
        var filters = new List<object>();
        if (!Directory.Exists(filtersPath)) return filters;
        
        var allFiles = Directory.GetFiles(filtersPath, "*.jaml");
        
        var filterFiles = allFiles
            .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (var file in filterFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            string filterJaml;
            try
            {
                filterJaml = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            var deck = "Red";
            var stake = "White";
            var columns = new List<string> { "seed", "score" };
            string? displayName = name;
            string? author = null;
            string? jamlErr;
            if (JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out jamlErr) && cfg != null)
            {
                if (!string.IsNullOrWhiteSpace(cfg.Name))
                    displayName = cfg.Name;
                if (!string.IsNullOrWhiteSpace(cfg.Author))
                    author = cfg.Author;
                if (!string.IsNullOrWhiteSpace(cfg.Deck)) deck = cfg.Deck;
                if (!string.IsNullOrWhiteSpace(cfg.Stake)) stake = cfg.Stake;
                
                if (string.IsNullOrWhiteSpace(cfg.Deck) && hasErraticFilters(cfg))
                    deck = "Erratic";

                try
                {
                    columns = cfg.GetColumnNames();
                }
                catch
                {
                    columns = new List<string> { "seed", "score" };
                }
            }

            var filterName = displayName ?? "UnknownFilter";
            var searchId = $"{SearchManager.SanitizeFilterFileStem(filterName)}_{deck}_{stake}";
            var fileName = Path.GetFileName(file);
            var filterId = Path.GetFileNameWithoutExtension(fileName);

            filters.Add(new
            {
                id = filterId,
                name = displayName,
                author = author ?? "Default",
                filterId,
                filterJaml,
                filePath = fileName,
                searchId,
                columns
            });
        }
        
        return filters
            .OrderBy(f =>
            {
                var nameProp = f.GetType().GetProperty("name");
                return nameProp?.GetValue(f) as string ?? "";
            }, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

