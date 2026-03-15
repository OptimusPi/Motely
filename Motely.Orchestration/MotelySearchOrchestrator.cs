using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Thin shared search setup for real entry points.
/// Normalizes public/runtime input at the boundary, then configures Motely once.
/// </summary>
public static class MotelySearchOrchestrator
{
    public static (JamlSearchPlan? Plan, string? FilterId, string? Error) PrepareSearch(
        JamlConfig config,
        MotelySearchRequest request
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(request);

        var validationError = ValidateRequest(request);
        if (validationError != null)
            return (null, null, validationError);

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(request.ThreadCount)
            .WithBatchCharacterCount(request.BatchCharCount);

        if (request.StartBatch.HasValue)
            settings = settings.WithStartBatchIndex(request.StartBatch.Value);

        if (request.EndBatch.HasValue)
            settings = settings.WithEndBatchIndex(request.EndBatch.Value);

        var options = new SearchOptionsDto
        {
            Seeds = request.Seeds,
            Keywords = request.Keywords,
            Padding = request.Padding,
            RandomSeeds = request.RandomSeeds,
            Palindrome = request.Palindrome ? true : null,
        };

        var (_, modeError) = settings.ApplySearchMode(options);
        if (modeError != null)
            return (null, null, modeError);

        return (plan, MotelyRuntimeIds.GenerateFilterId(config), null);
    }

    public static string? ValidateRequest(MotelySearchRequest request)
    {
        if (request.ThreadCount < 1)
            return "threadCount must be >= 1.";

        if (request.BatchCharCount is < 1 or > 7)
            return "batchCharCount must be in range 1..7.";

        if (request.StartBatch.HasValue && request.StartBatch.Value < 0)
            return "startBatch must be >= 0.";

        if (request.EndBatch.HasValue && request.EndBatch.Value < 0)
            return "endBatch must be >= 0.";

        if (
            request.StartBatch.HasValue
            && request.EndBatch.HasValue
            && request.StartBatch.Value >= request.EndBatch.Value
        )
            return "startBatch must be less than endBatch.";

        if (request.Seeds is { Length: 0 })
            return "seeds must contain at least one seed.";

        if (request.Keywords is { Length: 0 })
            return "keywords must contain at least one keyword.";

        if (request.Seeds != null)
        {
            for (int i = 0; i < request.Seeds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(request.Seeds[i]))
                    return $"seeds[{i}] cannot be empty.";
            }
        }

        if (request.Keywords != null)
        {
            for (int i = 0; i < request.Keywords.Length; i++)
            {
                var keyword = request.Keywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                    return $"keywords[{i}] cannot be empty.";

                if (keyword.Length > MotelyCore.MaxSeedLength)
                    return $"keywords[{i}] '{keyword}' is too long (max {MotelyCore.MaxSeedLength} chars).";
            }
        }

        if (request.RandomSeeds.HasValue && request.RandomSeeds.Value < 1)
            return "randomSeeds must be >= 1.";

        if (request.Padding != null)
        {
            if (request.Keywords is not { Length: > 0 })
                return "padding requires keyword search.";

            for (int i = 0; i < request.Padding.Length; i++)
            {
                var character = request.Padding[i];
                if (Array.IndexOf(MotelyCore.SeedDigits, character) < 0)
                    return $"padding contains invalid character '{character}'.";
            }
        }

        int explicitModeCount = 0;
        if (request.Seeds is { Length: > 0 })
            explicitModeCount++;
        if (request.Keywords is { Length: > 0 })
            explicitModeCount++;
        if (request.RandomSeeds.HasValue)
            explicitModeCount++;
        if (request.Palindrome)
            explicitModeCount++;

        if (explicitModeCount > 1)
            return "Choose only one search mode: seeds, keywords, randomSeeds, or palindrome.";

        return null;
    }

    public static string GenerateFilterId(JamlConfig config) =>
        MotelyRuntimeIds.GenerateFilterId(config);
}
