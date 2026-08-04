using System.Text.Json.Serialization;

namespace Motely.DistributedWorker;

// Community Search Party wire shapes — these mirror the zod schemas served by
// seedfinder.app (app/api/party/*.ts + lib/party/coordinator.ts) exactly.
// The protocol contract is documented in README.md, and the server keeps its
// mirrored copy at lib/party/PROTOCOL.md.

[JsonSerializable(typeof(PartyLeaseEnvelopeDto))]
[JsonSerializable(typeof(PartyReportRequestDto))]
[JsonSerializable(typeof(PartyReportResponseDto))]
public partial class WorkerJsonContext;

/// <summary>GET /api/party/next?partyId= — exactly one of Lease / Done / Error is meaningful.</summary>
public sealed class PartyLeaseEnvelopeDto
{
    [JsonPropertyName("lease")] public PartyLeaseDto? Lease { get; set; }

    [JsonPropertyName("done")] public bool Done { get; set; }

    /// <summary>Set with Done: "fully leased", "complete", "cancelled", "exhausted".</summary>
    [JsonPropertyName("reason")] public string? Reason { get; set; }

    [JsonPropertyName("error")] public string? Error { get; set; }
}

public sealed class PartyLeaseDto
{
    [JsonPropertyName("partyId")] public string PartyId { get; set; } = "";

    /// <summary>The full JAML filter document the party is grinding.</summary>
    [JsonPropertyName("jaml")] public string Jaml { get; set; } = "";

    [JsonPropertyName("deck")] public string? Deck { get; set; }

    [JsonPropertyName("stake")] public string? Stake { get; set; }

    /// <summary>Party blocks are engine batch indices at this character count.</summary>
    [JsonPropertyName("batchChars")] public int BatchChars { get; set; }

    /// <summary>First block of this lease. The lease covers [StartBlock, StartBlock + BlockCount).</summary>
    [JsonPropertyName("startBlock")] public long StartBlock { get; set; }

    [JsonPropertyName("blockCount")] public long BlockCount { get; set; }

    [JsonPropertyName("totalBlocks")] public long TotalBlocks { get; set; }

    /// <summary>Identifies this lease to /api/party/report — heartbeats and the final report both need it.</summary>
    [JsonPropertyName("workerToken")] public string WorkerToken { get; set; } = "";

    /// <summary>ISO-8601. The server-side TTL is 60s; heartbeat well inside it.</summary>
    [JsonPropertyName("expiresAt")] public string? ExpiresAt { get; set; }
}

/// <summary>POST /api/party/report — a heartbeat (HeartbeatOnly=true) or the lease's final report.</summary>
public sealed class PartyReportRequestDto
{
    [JsonPropertyName("partyId")] public string PartyId { get; set; } = "";

    [JsonPropertyName("workerToken")] public string WorkerToken { get; set; } = "";

    [JsonPropertyName("startBlock")] public long StartBlock { get; set; }

    /// <summary>Candidate seeds only (max 1000; each 1-8 chars). The server re-verifies
    /// and scores every candidate itself — a worker's local scores are never trusted.</summary>
    [JsonPropertyName("seeds")] public string[] Seeds { get; set; } = [];

    [JsonPropertyName("heartbeatOnly")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HeartbeatOnly { get; set; }
}

public sealed class PartyReportResponseDto
{
    /// <summary>Heartbeat acknowledgement.</summary>
    [JsonPropertyName("ok")] public bool Ok { get; set; }

    [JsonPropertyName("confirmed")] public int Confirmed { get; set; }

    [JsonPropertyName("rejected")] public int Rejected { get; set; }

    [JsonPropertyName("recorded")] public int Recorded { get; set; }

    [JsonPropertyName("error")] public string? Error { get; set; }
}
