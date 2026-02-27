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

    [JsonPropertyName("batchSize")]
    public int? BatchSize { get; set; }

    [JsonPropertyName("cutoff")]
    public string? Cutoff { get; set; }

    [JsonPropertyName("startBatch")]
    public long? StartBatch { get; set; }

    [JsonPropertyName("endBatch")]
    public long? EndBatch { get; set; }

    [JsonPropertyName("specificSeed")]
    public string? SpecificSeed { get; set; }

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

public sealed class SeedAnalysisDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = string.Empty;

    [JsonPropertyName("deck")]
    public string Deck { get; set; } = string.Empty;

    [JsonPropertyName("stake")]
    public string Stake { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("erraticDeckComposition")]
    public string[] ErraticDeckComposition { get; set; } = [];

    [JsonPropertyName("antes")]
    public AnteAnalysisDto[] Antes { get; set; } = [];
}

public sealed class AnteAnalysisDto
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
    public ShopItemDto[] ShopQueue { get; set; } = [];

    [JsonPropertyName("packs")]
    public PackDto[] Packs { get; set; } = [];
}

public sealed class ShopItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class PackDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public string[] Items { get; set; } = [];
}
