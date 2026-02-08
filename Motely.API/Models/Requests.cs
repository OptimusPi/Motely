namespace Motely.API.Models;

internal sealed record SearchStartRequest(
    string? FilterJaml,
    long? SeedCount,
    long? StartBatch,
    int? Cutoff,
    string? SeedSource
);

internal sealed record SearchStopRequest(string? SearchId);

internal sealed record FilterCloneRequest(string? FilterId, string? NewName);

internal sealed record FilterRenameRequest(string? FilterId, string? NewName);

internal sealed record FilterUpdateRequest(string? FilterId, string? FilterJaml);

internal sealed record FilterSaveRequest(
    string? FilterId,
    string? FilterJaml,
    bool CreateNew = false
);

internal sealed record FilterDeleteRequest(string? FilterId);

internal sealed record WordListUpsertRequest(string? Text);

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public sealed record McpPromptRequest(string? Prompt);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

internal sealed record FilterColumnsRequest(string? FilterJaml);

internal sealed record FilterUpdateColumnLabelRequest(
    string? FilterJaml,
    int ColumnIndex,
    string? NewLabel
);

internal sealed record MultiSourceHydrateRequest(
    string? FilterJaml,
    string[]? SeedSources,
    long? SeedCount,
    long? StartBatch,
    int? Cutoff
);
