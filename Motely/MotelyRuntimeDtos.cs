using System.Text.Json.Serialization;

namespace Motely;

public class ErrorDto
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}

public class SeedSearchRowDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("tallies")]
    public int[] Tallies { get; set; } = [];
}

public class SearchStatusDto
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("seedsFound")]
    public int SeedsFound { get; set; }

    [JsonPropertyName("highestScore")]
    public double HighestScore { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; set; }

    [JsonPropertyName("results")]
    public SeedSearchRowDto[] Results { get; set; } = [];
}

public class SearchOptionsDto
{
    [JsonPropertyName("threads")]
    public int Threads { get; set; }

    [JsonPropertyName("batchCharCount")]
    public int BatchCharCount { get; set; }

    [JsonPropertyName("startBatch")]
    public long StartBatch { get; set; }

    [JsonPropertyName("endBatch")]
    public long EndBatch { get; set; }

    [JsonPropertyName("seeds")]
    public string[] Seeds { get; set; } = [];

    [JsonPropertyName("keywords")]
    public string[] Keywords { get; set; } = [];

    [JsonPropertyName("paddingChars")]
    public string? PaddingChars { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class JamlSearchPlanDto
{
    [JsonPropertyName("filterId")]
    public string FilterId { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("deck")]
    public string Deck { get; set; } = "";

    [JsonPropertyName("stake")]
    public string Stake { get; set; } = "";

    [JsonPropertyName("mustLabels")]
    public string[] MustLabels { get; set; } = [];

    [JsonPropertyName("shouldLabels")]
    public string[] ShouldLabels { get; set; } = [];

    [JsonPropertyName("shouldClauseCount")]
    public int ShouldClauseCount { get; set; }
}
