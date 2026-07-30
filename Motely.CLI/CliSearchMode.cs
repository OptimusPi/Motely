#nullable enable
using System.Diagnostics.CodeAnalysis;
using Motely.DataLake;
using Motely.Filters;
using Motely.SeedProviders;

namespace Motely.CLI;

/// <summary>
/// Shared CLI wiring for list / keyword / random / aesthetic / sequential search modes (native + JAML).
/// </summary>
internal static class CliSearchMode
{
    public readonly record struct Input(
        string? SourcePath,
        string? SeedsArgument,
        bool Drown,
        string? ResultsRootPath,
        string? FilterId,
        IReadOnlyList<string>? JamlSeeds,
        IReadOnlyList<string> KeywordInputs,
        string? PaddingCharsOption,
        int? RandomCount,
        string? AestheticName,
        long? StartBatch,
        long? EndBatch,
        double? StartPercent,
        long? StartSeedSearchIndex,
        long? StopSeedSearchIndex,
        int? BatchCharacterCount
    );

    /// <summary>Default batch character count when the caller didn't pass one explicitly.</summary>
    private const int DefaultBatchCharacterCount = 4;

    public static bool TryApplySearchMode(
        IMotelySearchSettings settings,
        in Input input,
        Action<string>? writeWarning,
        [NotNullWhen(false)] out string? error,
        out IMotelySearchSettings updated,
        out IDisposable? sourceLifetime
    )
    {
        updated = settings;
        error = null;
        sourceLifetime = null;

        bool hasSource = !string.IsNullOrWhiteSpace(input.SourcePath);
        bool hasSeedsArg = !string.IsNullOrWhiteSpace(input.SeedsArgument);
        bool hasDrownMode = input.Drown;

        if (hasSource && hasSeedsArg)
        {
            error = "Error: choose only one explicit seed input: --source or --seeds.";
            return false;
        }

        JamlAesthetic? explicitAesthetic = null;
        if (!string.IsNullOrWhiteSpace(input.AestheticName))
        {
            var trimmed = input.AestheticName.Trim();
            if (!JamlAestheticParser.TryParse(trimmed, out var aesthetic))
            {
                error =
                    $"Error: unknown --aesthetic value '{trimmed}'. Known: {JamlAestheticParser.KnownJamlStringsDescription()}.";
                return false;
            }
            explicitAesthetic = aesthetic;
        }

        bool hasSeedIndexOptions =
            input.StartSeedSearchIndex.HasValue || input.StopSeedSearchIndex.HasValue;
        if (hasSeedIndexOptions)
        {
            if (
                hasSource
                || hasSeedsArg
                || hasDrownMode
                || input.KeywordInputs.Count > 0
                || input.RandomCount.HasValue
                || explicitAesthetic.HasValue
            )
            {
                error = "Error: --startSeed/--stopSeed apply only to default sequential search.";
                return false;
            }
        }

        bool hasSeedListMode = hasSource || hasSeedsArg;
        bool hasKeywordMode = input.KeywordInputs.Count > 0;

        int explicitSearchModeCount = 0;
        if (hasSeedListMode)
            explicitSearchModeCount++;
        if (hasDrownMode)
            explicitSearchModeCount++;
        if (hasKeywordMode)
            explicitSearchModeCount++;
        if (input.RandomCount.HasValue)
            explicitSearchModeCount++;
        if (explicitAesthetic.HasValue)
            explicitSearchModeCount++;

        if (explicitSearchModeCount > 1)
        {
            error =
                "Error: choose only one search input mode: --source, --seeds, --makeitrain, --keyword, --keywords, --random, or --aesthetic.";
            return false;
        }

        string[]? explicitSeeds = null;
        SeedSourceProvider? streamingProvider = null;

        if (hasDrownMode)
        {
            if (string.IsNullOrWhiteSpace(input.FilterId))
            {
                error = "Error: --makeitrain requires a resolved filterId (use --jaml).";
                return false;
            }

            string lakeFile = SeedLakeSink.LakePath(input.ResultsRootPath, input.FilterId!);
            if (!File.Exists(lakeFile))
            {
                error = $"Error: no saved seeds at '{lakeFile}'. Run a search first.";
                return false;
            }

            var drownProvider = SeedSourceProvider.FromLake(lakeFile, input.FilterId!);
            updated = updated.WithProviderSearch(drownProvider);
            sourceLifetime = drownProvider;
            return true;
        }

        if (hasSource)
        {
            try
            {
                streamingProvider = new SeedSourceProvider(input.SourcePath!);
                if (streamingProvider.SeedCount == 0)
                {
                    streamingProvider.Dispose();
                    streamingProvider = null;
                    error = "Error: resolved source contained no seeds.";
                    return false;
                }
                sourceLifetime = streamingProvider;
            }
            catch (Exception ex)
            {
                error = $"Error: {ex.Message}";
                return false;
            }
        }
        else if (hasSeedsArg)
        {
            var seedsValue = input.SeedsArgument!;
            bool looksLikeSourcePath =
                seedsValue.Contains(Path.DirectorySeparatorChar)
                || seedsValue.Contains(Path.AltDirectorySeparatorChar)
                || Path.HasExtension(seedsValue);

            if (looksLikeSourcePath)
            {
                try
                {
                    streamingProvider = new SeedSourceProvider(seedsValue);
                    if (streamingProvider.SeedCount == 0)
                    {
                        streamingProvider.Dispose();
                        streamingProvider = null;
                        error = "Error: resolved seed source contained no seeds.";
                        return false;
                    }

                    writeWarning?.Invoke(
                        "Warning: --seeds <path> is deprecated; use --source <path>."
                    );
                    sourceLifetime = streamingProvider;
                }
                catch (Exception ex)
                {
                    error = $"Error: {ex.Message}";
                    return false;
                }
            }
            else
            {
                var inlineSeeds = ParseInlineSeeds(seedsValue);
                if (inlineSeeds.Count == 0)
                {
                    error = "Error: --seeds requires at least one inline seed.";
                    return false;
                }

                explicitSeeds = inlineSeeds.ToArray();
            }
        }

        if (streamingProvider != null)
        {
            updated = updated.WithProviderSearch(streamingProvider);
        }
        else if (explicitSeeds != null)
        {
            updated = updated.WithSeedGenerator(explicitSeeds, explicitSeeds.Length);
        }
        else if (hasKeywordMode)
        {
            char[]? paddingChars = !string.IsNullOrWhiteSpace(input.PaddingCharsOption)
                ? input
                    .PaddingCharsOption!.ToUpperInvariant()
                    .Where(static c => MotelyGlobals.SeedDigits.Contains(c))
                    .Distinct()
                    .ToArray()
                : null;
            var prov = MotelyGlobals.GeneratePaddedSeedsForKeywords(
                input.KeywordInputs,
                paddingChars
            );
            long keywordSeedCount = MotelyGlobals.GetPaddedSeedCountForKeywordsLong(
                input.KeywordInputs,
                paddingChars
            );
            updated = updated.WithProviderSearch(
                new MotelySeedListProvider(prov, keywordSeedCount)
            );
        }
        else if (input.RandomCount.HasValue)
        {
            updated = updated.WithRandomSearch(input.RandomCount.Value);
        }
        else if (explicitAesthetic.HasValue)
        {
            // --padding mixes with --aesthetic: free slots / keyword pads use that charset.
            // Default when omitted: full alphabet (explicit single-family hunt). Collect's
            // multi-family prepass defaults to digit pad separately in Program.
            char[]? aestheticPad = !string.IsNullOrWhiteSpace(input.PaddingCharsOption)
                ? MotelyGlobals.ParsePaddingChars(input.PaddingCharsOption)
                : null;
            updated = updated.WithAestheticSearch(explicitAesthetic.Value, aestheticPad);
        }
        // The JAML seeds: replay and the sequential sweep are the *default* modes — they apply
        // only when the caller picked no explicit search input above. An explicit mode
        // (--keyword, --random, --aesthetic, --source, --seeds) already installed its provider;
        // reaching the block below would silently stomp it back to sequential.
        if (explicitSearchModeCount > 0)
            return true;

        // An explicit batch/range option means the caller asked for a real sequential sweep —
        // don't let a JAML file's saved `seeds:` list silently replace it with just those seeds.
        bool hasExplicitSequentialRange =
            input.BatchCharacterCount.HasValue
            || input.StartBatch.HasValue
            || input.EndBatch.HasValue
            || input.StartPercent.HasValue
            || hasSeedIndexOptions;

        if (input.JamlSeeds is { Count: > 0 } && !hasExplicitSequentialRange)
        {
            // A JAML `seeds:` array front-runs the search as a seed list by default.
            updated = updated.WithSeedGenerator(input.JamlSeeds, input.JamlSeeds.Count);
        }
        else
        {
            int batchCharacterCount = input.BatchCharacterCount ?? DefaultBatchCharacterCount;
            updated = updated.WithSequentialSearch();
            updated = updated.WithBatchCharacterCount(batchCharacterCount);

            bool hasSeedRange =
                input.StartSeedSearchIndex.HasValue || input.StopSeedSearchIndex.HasValue;
            if (hasSeedRange)
            {
                if (
                    input.StartBatch.HasValue
                    || input.EndBatch.HasValue
                    || input.StartPercent.HasValue
                )
                {
                    error =
                        "Error: do not combine --startSeed/--stopSeed with --startBatch, --endBatch, or --startPercent.";
                    return false;
                }

                long maxIdx = SeedMath.MaxSearchIndexInclusive(MotelyGlobals.MaxSeedLength);
                long startIdx = input.StartSeedSearchIndex ?? 0;
                long stopIdx = input.StopSeedSearchIndex ?? maxIdx;
                if (startIdx < 0 || stopIdx < startIdx || stopIdx > maxIdx)
                {
                    error =
                        $"Error: --startSeed/--stopSeed must satisfy 0 <= start <= stop <= {maxIdx} (Motely search index for length-{MotelyGlobals.MaxSeedLength} seeds).";
                    return false;
                }

                var (sb, ebExclusive) = SeedMath.SearchIndexRangeToBatchRange(
                    startIdx,
                    stopIdx,
                    batchCharacterCount
                );
                updated = updated.WithStartBatchIndex(sb).WithEndBatchIndex(ebExclusive);
            }
            else
            {
                if (input.StartBatch.HasValue)
                    updated = updated.WithStartBatchIndex(input.StartBatch.Value);
                else if (input.StartPercent.HasValue)
                {
                    double pct = input.StartPercent.Value;
                    if (pct < 0 || pct > 100)
                    {
                        error = "Error: --startPercent must be between 0 and 100.";
                        return false;
                    }

                    int nonBatchChars = MotelyGlobals.MaxSeedLength - batchCharacterCount;
                    long maxBatch = (long)Math.Pow(MotelyGlobals.SeedDigits.Length, nonBatchChars);
                    long startBatch = (long)(maxBatch * (pct / 100.0));
                    if (startBatch < 0)
                        startBatch = 0;
                    if (maxBatch > 0 && startBatch >= maxBatch)
                        startBatch = maxBatch - 1;
                    updated = updated.WithStartBatchIndex(startBatch);
                }

                if (input.EndBatch.HasValue)
                    updated = updated.WithEndBatchIndex(input.EndBatch.Value);
            }
        }

        return true;
    }

    private static List<string> ParseInlineSeeds(string value)
    {
        var seeds = new List<string>();
        foreach (
            var part in value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            if (!string.IsNullOrWhiteSpace(part))
                seeds.Add(MotelyGlobals.NormalizeSeed(part));
        }
        return seeds;
    }
}
