using System.Text.Json.Serialization;

namespace Motely.BrowserWasm;

/// <summary>WASM-only DTOs that supplement the shared MotelyAotJsonContext types.</summary>

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
