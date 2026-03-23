using System.Text.Json.Serialization;

namespace Motely.Executors;

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
