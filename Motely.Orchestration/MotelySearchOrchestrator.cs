using Motely.Analysis;
using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Search orchestration: validate, prepare, and run searches.
/// </summary>
public static class MotelySearchOrchestrator
{
    /// <summary>
    /// Run a complete search with callbacks. Loads JAML, prepares, executes.
    /// </summary>
    public static (string Status, int SeedsFound, int HighestScore) RunSearch(
        string jamlContent, MotelySearchRequest request,
        Action<long, long, long>? onProgress = null,
        Action<string, int>? onResult = null,
        CancellationToken cancellationToken = default)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error) || config == null)
            throw new InvalidOperationException(error ?? "Invalid JAML.");

        var (plan, _, prepareError) = PrepareSearch(config, request);
        if (prepareError != null || plan == null)
            throw new InvalidOperationException(prepareError ?? "Search could not be prepared.");

        int seedsFound = 0;
        int highestScore = 0;
        var settings = plan.Settings;

        if (onProgress != null)
        {
            settings = settings.WithProgressCallback(prog =>
                onProgress(prog.SeedsSearched, prog.MatchingSeeds, (long)prog.ElapsedTime.TotalMilliseconds));
        }

        if (plan.ShouldClauseCount > 0)
        {
            settings = settings.WithScoredResultCallback(tally =>
            {
                Interlocked.Increment(ref seedsFound);
                if (tally.Score > highestScore) highestScore = tally.Score;
                onResult?.Invoke(tally.Seed, tally.Score);
            });
        }
        else
        {
            settings = settings.WithSeedMatchCallback(seed =>
            {
                Interlocked.Increment(ref seedsFound);
                onResult?.Invoke(seed, 0);
            });
        }

        using var search = settings.CreateSearch();
        search.Start(cancellationToken);

        return (cancellationToken.IsCancellationRequested ? "cancelled" :
                search.IsCompleted ? "ok" : "cancelled",
                seedsFound, highestScore);
    }

    public static (JamlSearchPlan? Plan, string? FilterId, string? Error) PrepareSearch(
        JamlConfig config,
        MotelySearchRequest request
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(request);

        var (effectiveRequest, mergeError) = MergeJamlAesthetics(config, request);
        if (mergeError != null)
            return (null, null, mergeError);

        var validationError = ValidateRequest(effectiveRequest);
        if (validationError != null)
            return (null, null, validationError);

        var plan = JamlSearchBuilder.CreatePlan(config);
        var settings = plan.Settings
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(effectiveRequest.ThreadCount)
            .WithBatchCharacterCount(effectiveRequest.BatchCharCount);

        if (effectiveRequest.StartBatch.HasValue)
            settings = settings.WithStartBatchIndex(effectiveRequest.StartBatch.Value);

        if (effectiveRequest.EndBatch.HasValue)
            settings = settings.WithEndBatchIndex(effectiveRequest.EndBatch.Value);

        var (_, modeError) = settings.ApplySearchMode(effectiveRequest);
        if (modeError != null)
            return (null, null, modeError);

        return (plan, config.FilterId, null);
    }

    public static async Task<BlockSearchResultDto> ProcessBlockAsync(string jamlContent, int blockId)
    {
        var result = await ProcessBlockRunner.ProcessBlockAsync(jamlContent, blockId);
        if (result == null)
            return new BlockSearchResultDto { BlockId = blockId };

        return new BlockSearchResultDto
        {
            BlockId = result.BlockId,
            SeedsFound = result.SeedsFound,
            HighestScore = result.HighestScore,
            Seeds = result.Seeds.ToArray(),
        };
    }

    public static BlockSearchResultDto RunSearchCollecting(string jamlContent, MotelySearchRequest request, int blockId = 0)
    {
        var seeds = new List<string>();
        int highestScore = 0;

        var (_, seedsFound, _) = RunSearch(jamlContent, request,
            onResult: (seed, score) =>
            {
                seeds.Add(seed);
                if (score > highestScore) highestScore = score;
            });

        return new BlockSearchResultDto
        {
            BlockId = blockId,
            SeedsFound = seedsFound,
            HighestScore = highestScore,
            Seeds = seeds.ToArray(),
        };
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

    /// <summary>
    /// Applies JAML <c>aesthetics</c> when the host did not already pick a conflicting search mode.
    /// </summary>
    public static (MotelySearchRequest Request, string? Error) MergeJamlAesthetics(
        JamlConfig config,
        MotelySearchRequest request
    )
    {
        if (config.Aesthetics is not { Count: > 0 })
            return (request, null);

        var wantsPalindrome = config.Aesthetics.Contains(JamlAesthetic.Palindrome);
        if (!wantsPalindrome)
            return (request, null);

        var hasSeeds = request.Seeds is { Length: > 0 };
        var hasKeywords = request.Keywords is { Length: > 0 };
        var hasRandom = request.RandomSeeds.HasValue;
        if (hasSeeds || hasKeywords || hasRandom)
        {
            return (
                request,
                "JAML aesthetics include 'palindrome', which conflicts with seeds, keywords, or random search mode."
            );
        }

        if (request.Palindrome)
            return (request, null);

        return (
            new MotelySearchRequest
            {
                ThreadCount = request.ThreadCount,
                BatchCharCount = request.BatchCharCount,
                StartBatch = request.StartBatch,
                EndBatch = request.EndBatch,
                Seeds = request.Seeds,
                Keywords = request.Keywords,
                Padding = request.Padding,
                RandomSeeds = request.RandomSeeds,
                Palindrome = true,
            },
            null
        );
    }
}
