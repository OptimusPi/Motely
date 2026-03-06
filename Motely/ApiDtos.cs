using System.Text.Json.Serialization;

namespace Motely;

/// <summary>Single set of API DTOs for all runtimes (Node addon, Browser WASM, Node WASM, SingleThread).</summary>
public sealed class CapabilitiesDto
{
    [JsonPropertyName("simd")]
    public bool Simd { get; set; }

    [JsonPropertyName("threads")]
    public bool Threads { get; set; }

    [JsonPropertyName("processorCount")]
    public int ProcessorCount { get; set; }

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;
}

public sealed class VersionDto
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = string.Empty;

    [JsonPropertyName("features")]
    public string[] Features { get; set; } = [];
}

public sealed class ErrorDto
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

public sealed class ValidateResultDto
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("deck")]
    public string? Deck { get; set; }

    [JsonPropertyName("stake")]
    public string? Stake { get; set; }
}

public sealed class SearchOptionsDto
{
    [JsonPropertyName("threadCount")]
    public int? ThreadCount { get; set; }

    [JsonPropertyName("batchCharCount")]
    public int? BatchCharCount { get; set; }

    [JsonPropertyName("cutoff")]
    public string? Cutoff { get; set; }

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

    [JsonPropertyName("randomSeeds")]
    public int? RandomSeeds { get; set; }

    [JsonPropertyName("palindrome")]
    public bool? Palindrome { get; set; }
}

public sealed class SearchStatusDto
{
    [JsonPropertyName("searchId")]
    public string SearchId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

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

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class SearchHitDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("tallies")]
    public string[] Tallies { get; set; } = [];
}
