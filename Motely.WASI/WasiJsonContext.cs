using System.Text.Json.Serialization;

namespace Motely.Wasi;

/// <summary>
/// AOT-compatible JSON context for WASI DTOs.
/// Same pattern as WasmJsonContext in BrowserWasm — required for trimming safety.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(RpcRequest))]
[JsonSerializable(typeof(RpcResponse))]
[JsonSerializable(typeof(WasiValidateResultDto))]
[JsonSerializable(typeof(WasiSeedAnalysisDto))]
[JsonSerializable(typeof(WasiCapabilitiesDto))]
[JsonSerializable(typeof(WasiErrorDto))]
[JsonSerializable(typeof(WasiSearchProgressDto))]
[JsonSerializable(typeof(WasiSearchResultDto))]
[JsonSerializable(typeof(WasiSearchCompleteDto))]
public partial class WasiJsonContext : JsonSerializerContext { }

// ── DTOs ──────────────────────────────────────────────────────────

public sealed class RpcRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonParamsDto? Params { get; set; }
}

public sealed class JsonParamsDto
{
    [JsonPropertyName("jaml")]
    public string? Jaml { get; set; }

    [JsonPropertyName("seed")]
    public string? Seed { get; set; }

    [JsonPropertyName("deck")]
    public string? Deck { get; set; }

    [JsonPropertyName("stake")]
    public string? Stake { get; set; }

    [JsonPropertyName("randomSeeds")]
    public int? RandomSeeds { get; set; }

    [JsonPropertyName("cutoff")]
    public string? Cutoff { get; set; }
}

public sealed class RpcResponse
{
    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class WasiValidateResultDto
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

public sealed class WasiCapabilitiesDto
{
    [JsonPropertyName("runtime")]
    public string Runtime { get; set; } = "";

    [JsonPropertyName("simd")]
    public bool Simd { get; set; }

    [JsonPropertyName("threads")]
    public bool Threads { get; set; }

    [JsonPropertyName("processorCount")]
    public int ProcessorCount { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

public sealed class WasiErrorDto
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}

public sealed class WasiSeedAnalysisDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("deck")]
    public string Deck { get; set; } = "";

    [JsonPropertyName("stake")]
    public string Stake { get; set; } = "";

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("antes")]
    public WasiAnteDto[] Antes { get; set; } = [];
}

public sealed class WasiAnteDto
{
    [JsonPropertyName("ante")]
    public int Ante { get; set; }

    [JsonPropertyName("boss")]
    public string Boss { get; set; } = "";

    [JsonPropertyName("voucher")]
    public string Voucher { get; set; } = "";

    [JsonPropertyName("smallBlindTag")]
    public string SmallBlindTag { get; set; } = "";

    [JsonPropertyName("bigBlindTag")]
    public string BigBlindTag { get; set; } = "";

    [JsonPropertyName("shopQueue")]
    public WasiShopItemDto[] ShopQueue { get; set; } = [];

    [JsonPropertyName("packs")]
    public WasiPackDto[] Packs { get; set; } = [];
}

public sealed class WasiShopItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class WasiPackDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("items")]
    public string[] Items { get; set; } = [];
}

public sealed class WasiSearchProgressDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "progress";

    [JsonPropertyName("seedsSearched")]
    public long SeedsSearched { get; set; }

    [JsonPropertyName("matchingSeeds")]
    public long MatchingSeeds { get; set; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }
}

public sealed class WasiSearchResultDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "result";

    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

public sealed class WasiSearchCompleteDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "done";

    [JsonPropertyName("searchId")]
    public string SearchId { get; set; } = "";
}
