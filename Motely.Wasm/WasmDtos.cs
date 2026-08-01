using System.Text.Json.Serialization;

namespace Motely.Wasm;

// Every export returns one JSON string built from these DTOs through the source-generated
// context below — no reflection serialization, so trimming the browser bundle stays safe.

public sealed record ParseResultDto(
    bool Ok,
    string? Error,
    string? Name,
    string? Deck,
    string? Stake,
    int Must,
    int Should,
    int MustNot
);

public sealed record VocabularyDto(
    string[] Decks,
    string[] Stakes,
    string[] Jokers,
    string[] LegendaryJokers,
    string[] Vouchers,
    string[] TarotCards,
    string[] SpectralCards,
    string[] PlanetCards,
    string[] Bosses,
    string[] Tags,
    string[] Editions,
    string[] Enhancements,
    string[] Seals
);

public sealed record SpanDto(int StartLine, int StartColumn, int EndLine, int EndColumn);

public sealed record DiagnosticDto(SpanDto Span, string Message, string Severity, string Code);

public sealed record HoverDto(SpanDto Span, string Markdown);

public sealed record CompletionDto(string Label, string Kind, string? Detail, SpanDto? ReplaceSpan);

public sealed record ScoredSeedDto(string Seed, int Score, int[] Tally);

public sealed record ScoreRunDto(
    bool Ok,
    string? Error,
    long TotalSeeds,
    long MatchingSeeds,
    long FilteredSeeds,
    long ElapsedMs,
    ScoredSeedDto[] Results
);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ParseResultDto))]
[JsonSerializable(typeof(VocabularyDto))]
[JsonSerializable(typeof(DiagnosticDto[]))]
[JsonSerializable(typeof(HoverDto))]
[JsonSerializable(typeof(CompletionDto[]))]
[JsonSerializable(typeof(ScoreRunDto))]
internal sealed partial class WasmJson : JsonSerializerContext;
