using System.Text.RegularExpressions;

namespace Motely.API;

/// <summary>
/// Helper methods for seed source parsing and validation
/// </summary>
public static class SeedSourceHelper
{
    // Valid Balatro seed dictionary: [ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789] (35 chars, no 0, no lowercase)
    private static readonly HashSet<char> ValidSeedChars = new("ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789");
    
    /// <summary>
    /// Parse category and display name from filename using _CATEGORY__filename.ext convention
    /// </summary>
    public static (string? category, string displayName) ParseCategoryFromFileName(string fileName)
    {
        if (!fileName.StartsWith("_"))
        {
            // No category prefix - strip extension for cleaner display
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            return (null, nameWithoutExt);
        }

        var doubleUnderscoreIndex = fileName.IndexOf("__", StringComparison.Ordinal);
        if (doubleUnderscoreIndex < 0)
        {
            // Has underscore but no double underscore - treat as uncategorized, strip extension
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            return (null, nameWithoutExt);
        }

        var category = fileName.Substring(1, doubleUnderscoreIndex - 1); // Skip leading _
        var filename = fileName.Substring(doubleUnderscoreIndex + 2); // Skip __

        // Replace underscores in category with spaces for display
        var displayCategory = category.Replace('_', ' ');

        // Strip file extension from display name for cleaner organization
        var displayName = Path.GetFileNameWithoutExtension(filename);

        return (displayCategory, displayName);
    }

    /// <summary>
    /// Get icon for file type
    /// </summary>
    public static string GetIconForFileType(string kind, string? extension = null)
    {
        return kind switch
        {
            "builtin" => "⭐",
            "action" => "➕",
            "db" => "🦆",
            "csv" => "📊",
            "txt" => "📄",
            _ => extension?.ToLowerInvariant() switch
            {
                ".db" => "🦆",
                ".csv" => "📊",
                ".txt" => "📄",
                _ => "📄"
            }
        };
    }

    /// <summary>
    /// Validate and normalize a seed string
    /// Rules: 0-8 chars, dictionary only, convert 0→O, lowercase→uppercase
    /// </summary>
    public static string? ValidateAndNormalizeSeed(string seed)
    {
        if (string.IsNullOrWhiteSpace(seed))
            return null;

        // Trim whitespace
        seed = seed.Trim();

        // Convert 0 to O (Balatro quirk)
        seed = seed.Replace('0', 'O');

        // Convert to uppercase
        seed = seed.ToUpperInvariant();

        // Check length (0-8 chars)
        if (seed.Length == 0 || seed.Length > 8)
            return null;

        // Check all characters are in valid dictionary
        foreach (var c in seed)
        {
            if (!ValidSeedChars.Contains(c))
                return null; // Invalid character
        }

        return seed;
    }

    /// <summary>
    /// Parse CSV file and extract valid seeds
    /// </summary>
    public static List<string> ParseCsvSeeds(string csvContent)
    {
        var seeds = new List<string>();
        var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Handle comma-separated values
            var parts = line.Split(',');
            foreach (var part in parts)
            {
                var normalized = ValidateAndNormalizeSeed(part.Trim());
                if (normalized != null)
                {
                    seeds.Add(normalized);
                }
            }
        }

        return seeds.Distinct().ToList(); // Remove duplicates
    }
}

