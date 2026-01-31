using System.Text.Json.Serialization;

namespace Motely;

/// <summary>AOT-compatible DTOs for JSON (CLI, WASM, API).</summary>
public class SeedAnalysisDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = string.Empty;

    [JsonPropertyName("deck")]
    public string Deck { get; set; } = string.Empty;

    [JsonPropertyName("stake")]
    public string Stake { get; set; } = string.Empty;

    [JsonPropertyName("erraticDeckComposition")]
    public string[] ErraticDeckComposition { get; set; } = Array.Empty<string>();

    [JsonPropertyName("twos")]
    public int Twos { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("antes")]
    public AnteAnalysisDto[] Antes { get; set; } = Array.Empty<AnteAnalysisDto>();
}

public class AnteAnalysisDto
{
    [JsonPropertyName("ante")]
    public int Ante { get; set; }

    [JsonPropertyName("boss")]
    public string Boss { get; set; } = string.Empty;

    [JsonPropertyName("voucher")]
    public string Voucher { get; set; } = string.Empty;

    [JsonPropertyName("smallBlindTag")]
    public string SmallBlindTag { get; set; } = string.Empty;

    [JsonPropertyName("bigBlindTag")]
    public string BigBlindTag { get; set; } = string.Empty;

    [JsonPropertyName("drawOrder")]
    public string DrawOrder { get; set; } = string.Empty;

    [JsonPropertyName("shopQueue")]
    public ShopItemDto[] ShopQueue { get; set; } = Array.Empty<ShopItemDto>();

    [JsonPropertyName("packs")]
    public PackDto[] Packs { get; set; } = Array.Empty<PackDto>();
}

public class ShopItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class PackDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public string[] Items { get; set; } = Array.Empty<string>();
}

public class ErrorDto
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

public class SearchHitDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("tallies")]
    public int[]? Tallies { get; set; }
}

public class SearchResponseDto
{
    [JsonPropertyName("results")]
    public SearchHitDto[] Results { get; set; } = Array.Empty<SearchHitDto>();

    [JsonPropertyName("totalSearched")]
    public long TotalSearched { get; set; }

    [JsonPropertyName("foundCount")]
    public int FoundCount { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }
}

public class ProgressDto
{
    [JsonPropertyName("searchedCount")]
    public long SearchedCount { get; set; }

    [JsonPropertyName("foundCount")]
    public int FoundCount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("percentComplete")]
    public double PercentComplete { get; set; }

    [JsonPropertyName("seedsPerSecond")]
    public double SeedsPerSecond { get; set; }

    [JsonPropertyName("threadCount")]
    public int ThreadCount { get; set; }
}

public class VersionDto
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = string.Empty;

    [JsonPropertyName("features")]
    public string[] Features { get; set; } = Array.Empty<string>();
}

public class ValidateResultDto
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
