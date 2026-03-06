using System.Text.Json.Serialization;

namespace Motely.DistributedWorker;

/// <summary>AOT-safe JSON context for all DTOs used by the coordination API.</summary>
[JsonSerializable(typeof(SubmitResultsDto))]
[JsonSerializable(typeof(SubmitResponseDto))]
[JsonSerializable(typeof(SeedResultDto))]
[JsonSerializable(typeof(SeedResultDto[]))]
[JsonSerializable(typeof(PoolClaimRequestDto))]
[JsonSerializable(typeof(PoolClaimResponseDto))]
[JsonSerializable(typeof(ErrorDto))]
internal partial class WorkerJsonContext : JsonSerializerContext { }

/// <summary>POST body for /api/search/sessions/{filterId}/results</summary>
internal sealed class SubmitResultsDto
{
    [JsonPropertyName("startBatch")]
    public long StartBatch { get; set; }

    [JsonPropertyName("endBatch")]
    public long EndBatch { get; set; }

    [JsonPropertyName("results")]
    public SeedResultDto[] Results { get; set; } = [];

    [JsonPropertyName("seedsSearched")]
    public long SeedsSearched { get; set; }
}

internal sealed class SubmitResponseDto
{
    [JsonPropertyName("accepted")]
    public int Accepted { get; set; }
}

internal sealed class SeedResultDto
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

internal sealed class ErrorDto
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>POST body for /api/search/pool/claim</summary>
internal sealed class PoolClaimRequestDto
{
    [JsonPropertyName("workerId")]
    public string? WorkerId { get; set; }
}

/// <summary>Response from /api/search/pool/claim</summary>
internal sealed class PoolClaimResponseDto
{
    [JsonPropertyName("idle")]
    public bool Idle { get; set; }

    [JsonPropertyName("retryAfterMs")]
    public int RetryAfterMs { get; set; } = 5000;

    [JsonPropertyName("filterId")]
    public string? FilterId { get; set; }

    [JsonPropertyName("jaml")]
    public string? Jaml { get; set; }

    [JsonPropertyName("deck")]
    public string Deck { get; set; } = "Red";

    [JsonPropertyName("stake")]
    public string Stake { get; set; } = "White";

    [JsonPropertyName("batchIndex")]
    public long BatchIndex { get; set; }

    [JsonPropertyName("remaining")]
    public long Remaining { get; set; }

    [JsonPropertyName("batchCharCount")]
    public int BatchCharCount { get; set; } = 5;
}
