using System.Text.Json.Serialization;

namespace Motely.BrowserWasm;

/// <summary>Progress payload for AOT-safe JSON serialization in SingleThread WASM.</summary>
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

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProgressCallbackDto))]
public partial class WasmProgressJsonContext : JsonSerializerContext { }
