#nullable enable
using System.Diagnostics.CodeAnalysis;
using Motely.DB.SeedSource;
using Motely.Filters;

namespace Motely.CLI;

/// <summary>
/// Shared CLI wiring for list / keyword / random / aesthetic / sequential search modes (native + JAML).
/// </summary>
internal static class CliSearchMode
{
    public readonly record struct Input(
        string? SourcePath,
        string? SeedsArgument,
        IReadOnlyList<string> KeywordInputs,
        string? PaddingCharsOption,
        int? RandomCount,
        string? AestheticName,
        long? StartBatch,
        long? EndBatch,
        double? StartPercent,
        int BatchCharacterCount,
        IReadOnlyList<JamlAesthetic>? JamlAestheticFallback
    );

    public static bool TryApplySearchMode(
        IMotelySearchSettings settings,
        in Input input,
        Action<string>? writeWarning,
        [NotNullWhen(false)] out string? error,
        out IMotelySearchSettings updated
    )
    {
        updated = settings;
        error = null;

        bool hasSource = !string.IsNullOrWhiteSpace(input.SourcePath);
        bool hasSeedsArg = !string.IsNullOrWhiteSpace(input.SeedsArgument);

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

        bool hasSeedListMode = hasSource || hasSeedsArg;
        bool hasKeywordMode = input.KeywordInputs.Count > 0;

        int explicitSearchModeCount = 0;
        if (hasSeedListMode)
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
                "Error: choose only one search input mode: --source, --seeds, --keyword, --keywords, --random, or --aesthetic.";
            return false;
        }

        string[]? explicitSeeds = null;
        if (hasSource)
        {
            try
            {
                var sourceSeeds = SeedReader.ReadSeeds(input.SourcePath!);
                if (sourceSeeds.Count == 0)
                {
                    error = "Error: resolved source contained no seeds.";
                    return false;
                }

                explicitSeeds = sourceSeeds.ToArray();
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
                    var sourceSeeds = SeedReader.ReadSeeds(seedsValue);
                    if (sourceSeeds.Count == 0)
                    {
                        error = "Error: resolved seed source contained no seeds.";
                        return false;
                    }

                    writeWarning?.Invoke("Warning: --seeds <path> is deprecated; use --source <path>.");
                    explicitSeeds = sourceSeeds.ToArray();
                }
                catch (Exception ex)
                {
                    error = $"Error: {ex.Message}";
                    return false;
                }
            }
            else
            {
                var inlineSeeds = SeedReader.ParseInlineSeeds(seedsValue);
                if (inlineSeeds.Count == 0)
                {
                    error = "Error: --seeds requires at least one inline seed.";
                    return false;
                }

                explicitSeeds = inlineSeeds.ToArray();
            }
        }

        if (explicitSeeds != null)
        {
            updated = updated.WithListSearch(explicitSeeds, explicitSeeds.Length);
        }
        else if (hasKeywordMode)
        {
            char[]? paddingChars =
                !string.IsNullOrWhiteSpace(input.PaddingCharsOption)
                    ? input
                        .PaddingCharsOption!.ToUpperInvariant()
                        .Where(static c => MotelyGlobals.SeedDigits.Contains(c))
                        .Distinct()
                        .ToArray()
                    : null;
            var prov = MotelyGlobals.GeneratePaddedSeedsForKeywords(input.KeywordInputs, paddingChars);
            long keywordSeedCount = MotelyGlobals.GetPaddedSeedCountForKeywordsLong(
                input.KeywordInputs,
                paddingChars
            );
            updated = updated.WithProviderSearch(new MotelySeedListProvider(prov, keywordSeedCount));
        }
        else if (input.RandomCount.HasValue)
        {
            updated = updated.WithRandomSearch(input.RandomCount.Value);
        }
        else if (explicitAesthetic.HasValue)
        {
            updated = updated.WithAestheticSearch(explicitAesthetic.Value);
        }
        else if (input.JamlAestheticFallback is { Count: > 0 })
        {
            updated = updated.WithAestheticSearch(input.JamlAestheticFallback[0]);
            if (input.JamlAestheticFallback.Count > 1)
            {
                writeWarning?.Invoke(
                    $"Warning: JAML has {input.JamlAestheticFallback.Count} aesthetics; using first only."
                );
            }
        }
        else
        {
            updated = updated.WithSequentialSearch();

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

                int nonBatchChars = MotelyGlobals.MaxSeedLength - input.BatchCharacterCount;
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

        return true;
    }
}
