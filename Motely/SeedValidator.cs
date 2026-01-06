using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Motely;

/// <summary>
/// Validates and sanitizes Balatro seeds according to game rules:
/// - Seeds are base-35 (1-9, A-Z, no '0')
/// - Maximum length: 8 characters
/// - Can be empty string or 1-8 characters
/// </summary>
public static class SeedValidator
{
    /// <summary>
    /// Valid seed characters: 1-9 and A-Z (no '0', no lowercase)
    /// </summary>
    private static readonly HashSet<char> ValidSeedChars = new("123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");

    /// <summary>
    /// Check if a seed string is valid according to Balatro rules
    /// </summary>
    /// <param name="seed">Seed string to validate</param>
    /// <returns>True if seed is valid (A-Z1-9, ≤8 chars, no '0')</returns>
    public static bool IsValidSeed(string seed)
    {
        if (string.IsNullOrEmpty(seed))
            return true; // Empty seed is valid

        if (seed.Length > Motely.MaxSeedLength)
            return false;

        // Check for '0' (invalid in Balatro)
        if (seed.IndexOf('0') >= 0)
            return false;

        // Check all characters are valid (1-9, A-Z)
        foreach (char c in seed)
        {
            if (!ValidSeedChars.Contains(char.ToUpperInvariant(c)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Sanitize a raw seed string by:
    /// 1. Splitting on comma or whitespace (take first field)
    /// 2. Truncating to 8 characters max
    /// 3. Converting to uppercase
    /// 4. Filtering out invalid characters
    /// </summary>
    /// <param name="raw">Raw input string (may contain comma, score, whitespace, etc.)</param>
    /// <returns>Sanitized seed string, or empty string if no valid seed found</returns>
    public static string SanitizeSeed(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        // Step 1: Split on comma first (CSV format: "SEED,score")
        string firstField = raw;
        int commaIndex = raw.IndexOf(',');
        if (commaIndex >= 0)
        {
            firstField = raw.Substring(0, commaIndex).Trim();
        }
        else
        {
            // Step 2: Split on whitespace if no comma
            int spaceIndex = raw.IndexOfAny(new[] { ' ', '\t' });
            if (spaceIndex >= 0)
            {
                firstField = raw.Substring(0, spaceIndex).Trim();
            }
            else
            {
                firstField = raw.Trim();
            }
        }

        // Step 3: Truncate to max length
        if (firstField.Length > Motely.MaxSeedLength)
            firstField = firstField.Substring(0, Motely.MaxSeedLength);

        // Step 4: Convert to uppercase and filter invalid characters
        StringBuilder sb = new(firstField.Length);
        foreach (char c in firstField)
        {
            char upper = char.ToUpperInvariant(c);
            // Skip '0' and invalid characters, keep only 1-9 and A-Z
            if (upper != '0' && ValidSeedChars.Contains(upper))
                sb.Append(upper);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Validate and sanitize a batch of seeds, reporting detailed errors
    /// </summary>
    /// <param name="seeds">Raw seed strings to validate</param>
    /// <returns>Tuple: (validSeeds, invalidSeeds, errors)</returns>
    public static (List<string> ValidSeeds, List<string> InvalidSeeds, List<string> Errors) ValidateAndSanitizeSeeds(IEnumerable<string> seeds)
    {
        var validSeeds = new List<string>();
        var invalidSeeds = new List<string>();
        var errors = new List<string>();

        foreach (string raw in seeds)
        {
            string sanitized = SanitizeSeed(raw);
            
            if (string.IsNullOrEmpty(sanitized))
            {
                invalidSeeds.Add(raw);
                errors.Add($"Seed '{raw}' sanitized to empty (no valid characters)");
                continue;
            }

            if (!IsValidSeed(sanitized))
            {
                invalidSeeds.Add(raw);
                errors.Add($"Seed '{raw}' -> '{sanitized}' is invalid (contains '0', invalid chars, or >8 chars)");
                continue;
            }

            validSeeds.Add(sanitized);
        }

        return (validSeeds, invalidSeeds, errors);
    }
}
