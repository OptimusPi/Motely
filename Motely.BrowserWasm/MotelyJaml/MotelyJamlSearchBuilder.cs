#nullable enable
using Motely;
using Motely.Filters;

namespace MotelyJaml;

/// <summary>
/// Static helpers that build <see cref="IMotelySearchSettings"/> from a
/// <see cref="JamlConfig"/> and search-mode parameters.
/// No state, no sessions, no interfaces — just settings.
/// </summary>
public static class MotelyJamlSearchHelper
{
    public static IMotelySearchSettings BuildConfigured(
        JamlConfig jaml, int batchCharCount, long startBatch, long endBatch)
    {
        IMotelySearchSettings settings;
        if (jaml.Aesthetics.Count > 0)
        {
            settings = PlanProviderSearch(jaml).WithAestheticSearch(jaml.Aesthetics[0]);
        }
        else
        {
            settings = PlanSequentialSearch(jaml, batchCharCount).WithSequentialSearch();
            if (startBatch > 0) settings = settings.WithStartBatchIndex(startBatch);
            if (endBatch > 0) settings = settings.WithEndBatchIndex(endBatch);
        }
        return settings;
    }

    public static IMotelySearchSettings BuildRandom(JamlConfig jaml, int count)
    {
        return PlanProviderSearch(jaml).WithRandomSearch(Math.Max(1, count));
    }

    public static IMotelySearchSettings BuildAesthetic(JamlConfig jaml, JamlAesthetic aesthetic)
    {
        if (!Enum.IsDefined(aesthetic))
            throw new ArgumentOutOfRangeException(nameof(aesthetic));
        return PlanProviderSearch(jaml).WithAestheticSearch(aesthetic);
    }

    public static IMotelySearchSettings BuildKeyword(
        JamlConfig jaml, string keywordsCsv, string paddingChars)
    {
        var keywords = keywordsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static k => k.Trim().ToUpperInvariant())
            .Where(static k => k.Length > 0)
            .ToArray();

        if (keywords.Length == 0)
            throw new ArgumentException("At least one keyword is required.", nameof(keywordsCsv));

        char[]? pad = string.IsNullOrEmpty(paddingChars) ? null
            : paddingChars.ToUpperInvariant()
                .Where(static c => MotelyGlobals.SeedDigits.Contains(c))
                .Distinct()
                .ToArray();

        var padded = MotelyGlobals.GeneratePaddedSeedsForKeywords(keywords, pad);
        long keywordSeedCount = MotelyGlobals.GetPaddedSeedCountForKeywordsLong(keywords, pad);
        return PlanProviderSearch(jaml)
            .WithProviderSearch(new MotelySeedListProvider(padded, keywordSeedCount));
    }

    public static IMotelySearchSettings BuildSeedList(JamlConfig jaml, string[] seeds)
    {
        var trimmed = seeds.Select(static s => s.Trim()).Where(static s => s.Length > 0).ToArray();
        if (trimmed.Length == 0)
            throw new ArgumentException("At least one non-empty seed is required.", nameof(seeds));
        return PlanProviderSearch(jaml).WithListSearch(trimmed, trimmed.Length);
    }

    public static IMotelySearchSettings PlanProviderSearch(JamlConfig jaml)
    {
        var built = JamlSearchBuilder.CreatePlan(jaml);
        return built.Settings
            .WithDeck(jaml.Deck)
            .WithStake(jaml.Stake)
            .WithThreadCount(1);
    }

    public static IMotelySearchSettings PlanSequentialSearch(JamlConfig jaml, int batchCharCount)
    {
        return PlanProviderSearch(jaml).WithBatchCharacterCount(batchCharCount);
    }
}
