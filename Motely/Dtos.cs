using System.Text.Json.Serialization;

namespace Motely;

public sealed class VersionDto
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = "";

    [JsonPropertyName("features")]
    public string[] Features { get; set; } = [];
}

public sealed class CapabilitiesDto
{
    [JsonPropertyName("simd")]
    public bool Simd { get; set; }

    [JsonPropertyName("threads")]
    public bool Threads { get; set; }

    [JsonPropertyName("availableThreadCount")]
    public int AvailableThreadCount { get; set; }

    [JsonPropertyName("processorCount")]
    public int ProcessorCount { get; set; }

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "";
}

public sealed class ValidateResultDto
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("deck")]
    public string? Deck { get; set; }

    [JsonPropertyName("stake")]
    public string? Stake { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class SearchOptionsDto
{
    [JsonPropertyName("threadCount")]
    public int? ThreadCount { get; set; }

    [JsonPropertyName("batchCharCount")]
    public int? BatchCharCount { get; set; }

    [JsonPropertyName("cutoff")]
    public int? Cutoff { get; set; }

    [JsonPropertyName("startBatch")]
    public long? StartBatch { get; set; }

    [JsonPropertyName("endBatch")]
    public long? EndBatch { get; set; }

    [JsonPropertyName("specificSeed")]
    public string? SpecificSeed { get; set; }

    [JsonPropertyName("seeds")]
    public string[]? Seeds { get; set; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; set; }

    [JsonPropertyName("keywords")]
    public string[]? Keywords { get; set; }

    [JsonPropertyName("padding")]
    public string? Padding { get; set; }

    [JsonPropertyName("randomSeeds")]
    public int? RandomSeeds { get; set; }

    [JsonPropertyName("palindrome")]
    public bool? Palindrome { get; set; }
}

public sealed class BlockSeedResultDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

public sealed class BlockSearchResultDto
{
    [JsonPropertyName("blockId")]
    public int BlockId { get; set; }

    [JsonPropertyName("seedsSearched")]
    public long SeedsSearched { get; set; }

    [JsonPropertyName("seedsFound")]
    public int SeedsFound { get; set; }

    [JsonPropertyName("seeds")]
    public BlockSeedResultDto[] Seeds { get; set; } = [];
}

public sealed class SearchHitDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("tallies")]
    public string[] Tallies { get; set; } = [];
}

public sealed class SearchStatusDto
{
    [JsonPropertyName("filterId")]
    public string FilterId { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; set; }

    [JsonPropertyName("totalSeedsSearched")]
    public long TotalSeedsSearched { get; set; }

    [JsonPropertyName("matchingSeeds")]
    public long MatchingSeeds { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; set; }

    [JsonPropertyName("results")]
    public SearchHitDto[] Results { get; set; } = [];
}

public sealed class ErrorDto
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
