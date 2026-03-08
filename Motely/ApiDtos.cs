using System.Text.Json.Serialization;
using Motely.Analysis;
using Motely.Filters;

namespace Motely;

/// <summary>Single set of API DTOs for all runtimes (Node addon, Browser WASM, Node WASM, SingleThread).</summary>
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
    [JsonPropertyName("filterId")]
    public string FilterId { get; set; } = string.Empty;

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

public sealed class ProgressCallbackDto
{
    [JsonPropertyName("seedsSearched")]
    public long SeedsSearched { get; set; }

    [JsonPropertyName("matchingSeeds")]
    public long MatchingSeeds { get; set; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }
}

public static class MotelyRuntimeIds
{
    public static string GenerateFilterId(JamlConfig config)
    {
        var name = SanitizeForId(config.Name ?? "Unknown", maxLength: 30);
        var deck = config.Deck.ToString();
        var stake = config.Stake.ToString();
        return $"{name}_{deck}_{stake}";
    }

    public static string SanitizeForId(string input, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unknown";

        var firstPart = input.Split(
            new[] { ",", " - ", "–", "—", ";", ". " },
            StringSplitOptions.None
        )[0];
        var sanitized = firstPart.Trim().Replace(" ", "");
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '_');
        }

        if (sanitized.Length > maxLength)
            sanitized = sanitized[..maxLength];

        return sanitized;
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(CapabilitiesDto))]
[JsonSerializable(typeof(VersionDto))]
[JsonSerializable(typeof(ErrorDto))]
[JsonSerializable(typeof(ValidateResultDto))]
[JsonSerializable(typeof(SearchOptionsDto))]
[JsonSerializable(typeof(SearchStatusDto))]
[JsonSerializable(typeof(SearchHitDto[]))]
[JsonSerializable(typeof(ProgressCallbackDto))]
[JsonSerializable(typeof(SeedAnalysisDto))]
[JsonSerializable(typeof(AnteAnalysisDto))]
[JsonSerializable(typeof(ShopItemDto))]
[JsonSerializable(typeof(PackDto))]
public partial class MotelyJsonContext : JsonSerializerContext { }
