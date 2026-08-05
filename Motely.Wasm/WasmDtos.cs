namespace Motely.Wasm;

// Result shapes only. No engine type is restated here.
//
// Spans, diagnostics, hover and completion cross as their real types — JamlSpan, JamlDiagnostic,
// JamlHoverInfo, JamlCompletionItem — because Bootsharp serializes records and enums itself. The
// four DTO twins that used to live in this file existed only to be renamed to camelCase and to
// stringify JamlDiagnosticSeverity, i.e. to lose type information on the way out.
//
// What remains are shapes with no engine equivalent: a parse verdict and a search run.

/// <summary>Outcome of validating a JAML document, with the header facts worth showing.</summary>
public sealed record ParseResult(
    bool Ok,
    string? Error,
    string? Name,
    string? Deck,
    string? Stake,
    int Must,
    int Should,
    int MustNot
);

/// <summary>One scored seed: its score and the per-should-clause tally behind it.</summary>
public sealed record ScoredSeed(string Seed, int Score, int[] Tally);

/// <summary>A completed list-mode run: counters, elapsed time, and the ranked survivors.</summary>
public sealed record ScoreRun(
    bool Ok,
    string? Error,
    long TotalSeeds,
    long MatchingSeeds,
    long FilteredSeeds,
    long ElapsedMs,
    ScoredSeed[] Results
);
