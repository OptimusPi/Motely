namespace Motely.API.Models;

internal sealed record SearchStartRequest(
    string? FilterJaml,
    long? SeedCount,
    long? StartBatch,
    int? Cutoff,
    string? SeedSource
);

internal sealed record SearchStopRequest(string? SearchId);

internal sealed record FilterSaveRequest(
    string? FilterId,
    string? FilterJaml,
    bool CreateNew = false
);

public sealed record McpPromptRequest(string? Prompt);
