using System.Linq;

namespace Motely;

/// <summary>
/// Applies a <see cref="SearchOptionsDto"/> to any <see cref="IMotelySearchSettings"/>,
/// translating the JSON-serializable options into the correct search mode.
///
/// RULE: ALL search-mode business logic lives here — once, correct, shared.
/// CLI, BrowserWasm, NodeAddon, and any future consumer entry point MUST call
/// <see cref="ApplySearchMode"/> instead of duplicating this switch.
/// </summary>
public static class SearchOptionsExtensions
{
    /// <summary>
    /// Configure the search mode on <paramref name="settings"/> from <paramref name="options"/>.
    ///
    /// Priority order:
    ///   1. specificSeed  — single-seed verification list
    ///   2. seeds[]       — explicit seed list
    ///   3. keyword       — all 8-char padded variations containing the keyword
    ///   4. keywords[]    — same as keyword but for multiple words (comma-sep in CLI)
    ///   5. randomSeeds   — N random seeds
    ///   6. palindrome    — all palindrome seeds
    ///   7. (default)     — sequential scan
    ///
    /// --padding (optional with keyword/keywords):
    ///   Restricts which characters are used as padding. e.g. "67Z" means only
    ///   '6', '7', 'Z' are valid padding chars. Seeds like "6STAR77" or "STAR7Z7"
    ///   are generated; "GSTAR67" is NOT (because 'G' was not in --padding).
    ///   If omitted, all 35 valid seed characters are used as padding.
    ///
    /// Returns <paramref name="settings"/> for fluent chaining.
    /// Returns an error string (non-null) if a validation error occurred; null on success.
    /// </summary>
    public static (IMotelySearchSettings settings, string? error) ApplySearchMode(
        this IMotelySearchSettings settings,
        SearchOptionsDto options
    )
    {
        char[]? paddingChars = MotelyCore.ParsePaddingChars(options.Padding);

        if (options.SpecificSeed != null)
        {
            string seed = options.SpecificSeed.ToUpperInvariant();
            return (settings.WithListSearch([seed], 1), null);
        }

        if (options.Seeds is { Length: > 0 })
            return (settings.WithListSearch(options.Seeds), null);

        if (!string.IsNullOrEmpty(options.Keyword))
        {
            string kw = options.Keyword.Trim().ToUpperInvariant();
            int padLen = MotelyCore.MaxSeedLength - kw.Length;
            if (padLen < 0)
                return (settings, $"Keyword '{kw}' is too long (max {MotelyCore.MaxSeedLength} chars).");

            int count = MotelyCore.GetPaddedSeedCount(kw, padLen, paddingChars);
            return (settings.WithListSearch(MotelyCore.GeneratePaddedSeeds(kw, padLen, paddingChars), count), null);
        }

        if (options.Keywords is { Length: > 0 })
        {
            var validKeywords = options.Keywords
                .Select(k => k.Trim().ToUpperInvariant())
                .Where(k => !string.IsNullOrEmpty(k) && k.Length <= MotelyCore.MaxSeedLength)
                .ToArray();

            if (validKeywords.Length == 0)
                return (settings, "All keywords were empty or too long.");

            int count = MotelyCore.GetPaddedSeedCountForKeywords(validKeywords, paddingChars);
            return (settings.WithListSearch(MotelyCore.GeneratePaddedSeedsForKeywords(validKeywords, paddingChars), count), null);
        }

        if (options.RandomSeeds.HasValue)
            return (settings.WithRandomSearch(options.RandomSeeds.Value), null);

        if (options.Palindrome == true)
            return (settings.WithPalindromeSearch(), null);

        return (settings.WithSequentialSearch(), null);
    }
}
