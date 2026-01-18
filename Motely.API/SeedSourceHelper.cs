using System.Text.RegularExpressions;

namespace Motely.API;

/// <summary>
/// Helper methods for seed source parsing and validation
/// </summary>
public static class SeedSourceHelper
{
    // Valid Balatro seed dictionary: [ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789] (35 chars, no 0, no lowercase)
    private static readonly HashSet<char> ValidSeedChars = new(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789"
    );

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
                _ => "📄",
            },
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

    /// <summary>
    /// Generate NSFW/funny seed names for testing the content filter.
    /// Uses valid Balatro seed characters only (A-Z, 1-9).
    /// </summary>
    public static List<string> GenerateNsfwSeeds()
    {
        var seeds = new HashSet<string>();

        // Base NSFW words (already valid chars, max 8 chars)
        string[] nsfwWords =
        {
            // Profanity
            "FUK",
            "FUKK",
            "FUKKK",
            "SHIT",
            "SHT",
            "ASS",
            "ASSS",
            "DAMN",
            "HELL",
            "CRAP",
            "BITCH",
            // Sexual
            "SEX",
            "SEXX",
            "SEXY",
            "SEKS",
            "CUM",
            "CUMM",
            "CUMMY",
            "DIK",
            "DIKK",
            "DIKS",
            "COK",
            "COKK",
            "KOCK",
            "COCK",
            "TIT",
            "TITS",
            "TITT",
            "BOOB",
            "MILF",
            "SLUT",
            "HOE",
            "PORN",
            "PRN",
            "LEWD",
            "NUDE",
            "PAWG",
            "DTF",
            "BWC",
            "BBC",
            "VAG",
            "PUS",
            "NUT",
            "NUTS",
            "JERK",
            // Slurs (for detection testing)
            "FAG",
            "KUNT",
            "CVNT",
            "TWAT",
            // Drugs
            "WEED",
            "POT",
            "LSD",
            "DANK",
            "BLUNT",
            "HIGH",
            "BAKED",
            "STONER",
            "DOPE",
            "CRACK",
            // Other
            "KILL",
            "EVIL",
            "SATAN",
            "DEMON",
            "NAZI",
            // Silly/meme
            "LOL",
            "LMAO",
            "YEET",
            "BASED",
            "CHAD",
            "SIMP",
            "CRINGE",
        };

        // Number suffixes for padding
        string[] numSuffixes =
        {
            "1",
            "11",
            "69",
            "88",
            "99",
            "111",
            "123",
            "321",
            "420",
            "666",
            "777",
            "888",
            "999",
            "1234",
            "4321",
        };

        // Letter suffixes
        string[] letterSuffixes = { "X", "XX", "XXX", "Z", "ZZ", "S" };

        // Add base words
        foreach (var word in nsfwWords)
        {
            if (word.Length <= 8)
                seeds.Add(word);
        }

        // Add words with number suffixes
        foreach (var word in nsfwWords)
        {
            foreach (var suffix in numSuffixes)
            {
                var combined = word + suffix;
                if (combined.Length <= 8)
                    seeds.Add(combined);
            }
        }

        // Add words with letter suffixes
        foreach (var word in nsfwWords)
        {
            foreach (var suffix in letterSuffixes)
            {
                var combined = word + suffix;
                if (combined.Length <= 8)
                    seeds.Add(combined);
            }
        }

        // Add number prefixes
        foreach (var word in nsfwWords)
        {
            foreach (var suffix in numSuffixes)
            {
                var combined = suffix + word;
                if (combined.Length <= 8)
                    seeds.Add(combined);
            }
        }

        // Special combinations
        string[] specialSeeds =
        {
            // Repeating patterns
            "69696969",
            "42069",
            "80085",
            "8008135",
            "58008",
            // Word combos
            "BIGDIK",
            "BIGCOK",
            "HOTMILF",
            "SEXGOD",
            "CUMKING",
            "DIKHEAD",
            "ASSHAT",
            "FUKFACE",
            "SHITASS",
            "CUMSHOT",
            "FUKBOY",
            "FUKBOI",
            "CUMSLUT",
            // X-padded
            "XSEXXX",
            "XXXPORN",
            "CUMXXX",
            "DIKXXX",
            // Meme combos
            "LOLCUM",
            "YEETDIK",
            "CHADCOK",
            "SIMPFUK",
            // Numbers mixed
            "CUM69",
            "SEX420",
            "DIK666",
            "FUK777",
            "69CUM",
            "420SEX",
            "666DIK",
            "777FUK",
            "CUM4ME",
            "DIK4U",
            "SEX4ALL",
            // All numbers that spell stuff
            "8008",
            "80085",
            "55378008", // BOOB, BOOBS (upside down calc)
        };

        foreach (var seed in specialSeeds)
        {
            var normalized = ValidateAndNormalizeSeed(seed);
            if (normalized != null)
                seeds.Add(normalized);
        }

        return seeds.OrderBy(s => s).ToList();
    }

    /// <summary>
    /// Check if a seed contains NSFW content (for UI warnings)
    /// </summary>
    public static bool IsNsfwSeed(string seed)
    {
        if (string.IsNullOrWhiteSpace(seed))
            return false;

        seed = seed.ToUpperInvariant();

        // Quick patterns to check
        string[] patterns =
        {
            "FUK",
            "FCK",
            "SHT",
            "SHIT",
            "ASS",
            "DAMN",
            "HELL",
            "CRAP",
            "BITCH",
            "SEX",
            "SEKS",
            "CUM",
            "DIK",
            "COK",
            "COCK",
            "KOCK",
            "TIT",
            "BOOB",
            "MILF",
            "SLUT",
            "HOE",
            "PORN",
            "PRN",
            "LEWD",
            "NUDE",
            "PAWG",
            "DTF",
            "BWC",
            "BBC",
            "VAG",
            "PUS",
            "NUT",
            "JERK",
            "FAG",
            "KUNT",
            "CVNT",
            "CUNT",
            "TWAT",
            "WEED",
            "POT",
            "LSD",
            "DANK",
            "BLUNT",
            "CRACK",
            "KILL",
            "EVIL",
            "SATAN",
            "DEMON",
            "NAZI",
            "69",
            "420",
            "666",
        };

        foreach (var pattern in patterns)
        {
            if (seed.Contains(pattern))
                return true;
        }

        return false;
    }
}
