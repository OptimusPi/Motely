using System.Linq;
using Motely.Executors;

namespace Motely;

/// <summary>
/// Applies search mode from a <see cref="MotelySearchRequest"/> to search settings.
/// ALL search-mode business logic lives here — once, correct, shared.
/// </summary>
public static class SearchOptionsExtensions
{
    public static (IMotelySearchSettings settings, string? error) ApplySearchMode(
        this IMotelySearchSettings settings,
        MotelySearchRequest request
    )
    {
        char[]? paddingChars = MotelyCore.ParsePaddingChars(request.Padding);

        if (request.Seeds is { Length: > 0 })
            return (settings.WithListSearch(request.Seeds), null);

        if (request.Keywords is { Length: > 0 })
        {
            var validKeywords = request.Keywords
                .Select(k => k.Trim().ToUpperInvariant())
                .Where(k => !string.IsNullOrEmpty(k) && k.Length <= MotelyCore.MaxSeedLength)
                .ToArray();

            if (validKeywords.Length == 0)
                return (settings, "All keywords were empty or too long.");

            ulong count = MotelyCore.GetPaddedSeedCountForKeywords(validKeywords, paddingChars);
            int seedCount = count > int.MaxValue ? int.MaxValue : (int)count;
            return (settings.WithListSearch(MotelyCore.GeneratePaddedSeedsForKeywords(validKeywords, paddingChars), seedCount), null);
        }

        if (request.RandomSeeds.HasValue)
            return (settings.WithRandomSearch(request.RandomSeeds.Value), null);

        if (request.Palindrome)
            return (settings.WithPalindromeSearch(), null);

        return (settings.WithSequentialSearch(), null);
    }
}
