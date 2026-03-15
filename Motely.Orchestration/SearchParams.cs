namespace Motely.Executors;

public sealed class MotelySearchRequest
{
    public required int ThreadCount { get; init; }
    public required int BatchCharCount { get; init; }
    public long? StartBatch { get; init; }
    public long? EndBatch { get; init; }
    public string[]? Seeds { get; init; }
    public string[]? Keywords { get; init; }
    public string? Padding { get; init; }
    public int? RandomSeeds { get; init; }
    public bool Palindrome { get; init; }
}

public static class MotelySearchRequestFactory
{
    public static (MotelySearchRequest? Request, string? Error) FromOptions(
        SearchOptionsDto options,
        int threadCount,
        int batchCharCount
    )
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalizedSeeds = new List<string>();
        var normalizedKeywords = new List<string>();

        var seedError = NormalizeSeedInput(
            value: options.SpecificSeed,
            fieldName: "specificSeed",
            target: normalizedSeeds
        );
        if (seedError != null)
            return (null, seedError);

        seedError = NormalizeSeedInputs(
            values: options.Seeds,
            fieldName: "seeds",
            target: normalizedSeeds
        );
        if (seedError != null)
            return (null, seedError);

        var keywordError = NormalizeKeywordInput(
            value: options.Keyword,
            fieldName: "keyword",
            target: normalizedKeywords
        );
        if (keywordError != null)
            return (null, keywordError);

        keywordError = NormalizeKeywordInputs(
            values: options.Keywords,
            fieldName: "keywords",
            target: normalizedKeywords
        );
        if (keywordError != null)
            return (null, keywordError);

        return (
            new MotelySearchRequest
            {
                ThreadCount = threadCount,
                BatchCharCount = batchCharCount,
                StartBatch = options.StartBatch,
                EndBatch = options.EndBatch,
                Seeds = normalizedSeeds.Count > 0 ? normalizedSeeds.ToArray() : null,
                Keywords = normalizedKeywords.Count > 0 ? normalizedKeywords.ToArray() : null,
                Padding = NormalizePadding(options.Padding),
                RandomSeeds = options.RandomSeeds,
                Palindrome = options.Palindrome == true,
            },
            null
        );
    }

    private static string? NormalizeSeedInput(
        string? value,
        string fieldName,
        List<string> target
    )
    {
        if (value == null)
            return null;

        if (string.IsNullOrWhiteSpace(value))
            return $"{fieldName} cannot be empty.";

        target.Add(value.Trim().ToUpperInvariant());
        return null;
    }

    private static string? NormalizeSeedInputs(
        string[]? values,
        string fieldName,
        List<string> target
    )
    {
        if (values == null)
            return null;

        if (values.Length == 0)
            return $"{fieldName} must contain at least one seed.";

        for (int i = 0; i < values.Length; i++)
        {
            var value = values[i];
            if (string.IsNullOrWhiteSpace(value))
                return $"{fieldName}[{i}] cannot be empty.";

            target.Add(value.Trim().ToUpperInvariant());
        }

        return null;
    }

    private static string? NormalizeKeywordInput(
        string? value,
        string fieldName,
        List<string> target
    )
    {
        if (value == null)
            return null;

        if (string.IsNullOrWhiteSpace(value))
            return $"{fieldName} cannot be empty.";

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > MotelyCore.MaxSeedLength)
            return $"{fieldName} '{normalized}' is too long (max {MotelyCore.MaxSeedLength} chars).";

        target.Add(normalized);
        return null;
    }

    private static string? NormalizeKeywordInputs(
        string[]? values,
        string fieldName,
        List<string> target
    )
    {
        if (values == null)
            return null;

        if (values.Length == 0)
            return $"{fieldName} must contain at least one keyword.";

        for (int i = 0; i < values.Length; i++)
        {
            var value = values[i];
            if (string.IsNullOrWhiteSpace(value))
                return $"{fieldName}[{i}] cannot be empty.";

            var normalized = value.Trim().ToUpperInvariant();
            if (normalized.Length > MotelyCore.MaxSeedLength)
                return $"{fieldName}[{i}] '{normalized}' is too long (max {MotelyCore.MaxSeedLength} chars).";

            target.Add(normalized);
        }

        return null;
    }

    private static string? NormalizePadding(string? padding)
    {
        if (padding == null)
            return null;

        if (string.IsNullOrWhiteSpace(padding))
            return null;

        return padding.Trim().ToUpperInvariant();
    }
}
