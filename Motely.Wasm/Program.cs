using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Jummy;
using Motely.SeedProviders;
using JamlyzerEngine = Motely.Analysis.MotelyJamlyzer;

namespace Motely.Wasm;

// ── Bootsharp interop shaping ────────────────────────────────────────────────
// One flat import surface. JS does:  import { MotelySearch, MotelyJamlyzer, ... } from "motely-wasm"
// A string param is YAML (or JSON) *text*; parsing it yields the JAML (a JamlConfig). So params are
// `yaml`, and the parsed value is `jaml`. Nothing fancy crosses the boundary: text in, plain data out —
// the engine's fluent IMotelySearchSettings / IMotelySearch never reach JS.
public static class BootsharpRenamers
{
    // Fold every Motely.* + Motely.Wasm type into the root "index" module.
    [RenameModule]
    public static string RenameModule(Type type, string @default)
    {
        var ns = type.Namespace ?? "";
        return ns == "Motely.Wasm" || ns == "Motely" || ns.StartsWith("Motely.", StringComparison.Ordinal)
            ? "index"
            : @default;
    }

    // Hide the C# bootstrap and any ref-struct (Span/ref struct never marshals) from the JS surface.
    [RenameNode]
    public static string? RenameNode(Type type, string @default) =>
        type == typeof(Boot) ? null
        : type.IsByRefLike ? null
        : @default;

    // Jimmolate's seed finder hands JS a MotelySingleSearchContext. Most of its surface is SIMD /
    // ref-struct streams that can't marshal — erase those members so Bootsharp never emits invalid
    // `Type&` syntax for them. The seed finder only ever needs ctx.GetSeed().
    [RenameMember]
    public static string? RenameMember(MemberInfo info, string @default) =>
        info is MethodInfo m
        && m.DeclaringType == typeof(MotelySingleSearchContext)
        && (m.GetParameters().Any(p => Unmarshallable(p.ParameterType)) || Unmarshallable(m.ReturnType))
            ? null
            : @default;

    private static bool Unmarshallable(Type t) =>
        t.IsByRef
        || t.IsByRefLike
        || (t.Name.StartsWith("MotelySingle", StringComparison.Ordinal)
            && (t.Name.EndsWith("Stream", StringComparison.Ordinal) || t.Name == "MotelySingleItemSet"));
}

// C# entry point — bootstrap only, hidden from JS by RenameNode above.
public static class Boot
{
    public static void Main() { }
}

// ── Library bridge ───────────────────────────────────────────────────────────
// (No NormalizeSeed export: the engine normalizes seeds it's given, and trim/upper-case is a JS
//  string op — no need to cross interop for it.)
public static class MotelyWasm
{
    [Export]
    public static string GetVersion() =>
        typeof(MotelyWasm).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(MotelyWasm).Assembly.GetName().Version?.ToString()
        ?? "unknown";
}

// ── JAML ─────────────────────────────────────────────────────────────────────
// JAML is a real language with a real type. FromYaml/FromJson parse its two concrete syntaxes into
// the same JamlConfig — that typed object is what JS gets and hands back, not a string to re-parse.
public static class MotelyJaml
{
    /// <summary>Parse a JAML document (YAML syntax) into its JamlConfig.</summary>
    [Export]
    public static JamlConfig FromYaml(string jaml) => JamlConfigLoader.FromYaml(jaml);

    /// <summary>Parse a JAML document (JSON syntax) into its JamlConfig.</summary>
    [Export]
    public static JamlConfig FromJson(string json) => JamlConfigLoader.FromJson(json);

    /// <summary>Validate JAML. Returns null when valid, otherwise the error message.</summary>
    [Export]
    public static string? Validate(string jaml) =>
        JamlConfigLoader.TryLoad(jaml, out _, out var error) ? null : (error ?? "Invalid JAML.");

    /// <summary>Plan a search (keyword candidate counts etc.) for the given JamlConfig.</summary>
    [Export]
    public static JamlSearchPlan CreatePlan(JamlConfig jaml) =>
        JamlSearchBuilder.CreatePlan(jaml);

    /// <summary>Display names of the built-in native (C#) filters.</summary>
    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;
}

// ── JUMMY ────────────────────────────────────────────────────────────────────
public static class MotelyJummy
{
    [Export]
    public static string? Validate(string line) =>
        JummyLine.TryToClause(line, out _, out var error) ? null : error;

    [Export]
    public static string? Canonicalize(string line) =>
        JummyLine.TryToClause(line, out var clause, out var error)
            ? JummyLine.FromClause(clause!)
            : throw new InvalidOperationException(error ?? "Invalid JUMMY line.");
}

// ── Utilities ────────────────────────────────────────────────────────────────
public static class MotelyUtilities
{
    [Export] public static long SeedToTotalIndex(string seed) => SeedMath.SeedToTotalIndex(seed);
    [Export] public static string TotalIndexToSeed(long index) => SeedMath.TotalIndexToSeed(index);
    [Export] public static long SeedToSearchIndex(string seed) => SeedMath.SeedToSearchIndex(seed);
    [Export] public static string SearchIndexToSeed(long index, int length) => SeedMath.SearchIndexToSeed(index, length);
    [Export] public static long GetFirstSeedOfLength(int length) => SeedMath.GetFirstSeedOfLength(length);
    [Export] public static long MaxSearchIndexInclusive(int length) => SeedMath.MaxSearchIndexInclusive(length);
    [Export] public static long SeedToBatchIndex(string seed, int batchSize) => SeedMath.SeedToBatchIndex(seed, batchSize);
    [Export] public static string BatchIndexToSeedPrefix(long batchIndex, int batchSize) => SeedMath.BatchIndexToSeedPrefix(batchIndex, batchSize);
    [Export] public static long[] SearchIndexRangeToBatchRange(long startSearchIndexInclusive, long stopSearchIndexInclusive, int batchCharCount)
    {
        var (start, end) = SeedMath.SearchIndexRangeToBatchRange(startSearchIndexInclusive, stopSearchIndexInclusive, batchCharCount);
        return [start, end];
    }

    [Export] public static string[] RepeatCharKeywords(int repeatCount) => [.. MotelySeedKeywordSequences.RepeatCharKeywords(repeatCount)];
    [Export] public static string[] AscendingDigitLetterKeywords(int length) => [.. MotelySeedKeywordSequences.AscendingDigitLetterKeywords(length)];
    [Export] public static string[] DescendingDigitLetterKeywords(int length) => [.. MotelySeedKeywordSequences.DescendingDigitLetterKeywords(length)];
    [Export] public static string[] MirrorPatternKeywords(int length) => [.. MotelySeedKeywordSequences.MirrorPatternKeywords(length)];
    [Export] public static long GetAestheticSeedCount(JamlAesthetic aesthetic) => MotelySeedKeywordSequences.GetAestheticSeedCount(aesthetic);
    [Export] public static string[] GrossKeywords() => MotelySeedKeywordSequences.GrossKeywords;
    [Export] public static string[] FunnyKeywords() => MotelySeedKeywordSequences.FunnyKeywords;
    [Export] public static string[] BalatroKeywords() => MotelySeedKeywordSequences.BalatroKeywords;
}

// ── JAMLyzer ─────────────────────────────────────────────────────────────────
// Analyzer (not scoring): per-ante boss/voucher/tags/shop/packs + every stream for each seed.
// (Each result also carries a `score` when the JAML has should-clauses — score-by-JAMLyzer.)
public static class MotelyJamlyzer
{
    /// <summary>Analyze each seed with the default window (20 rolls) from each stream's natural start.</summary>
    [Export]
    public static MotelyJamlyzerSeedResult[] AnalyzeSeeds(JamlConfig jaml) =>
        [.. JamlyzerEngine.Analyze(jaml)];

    /// <summary>Analyze each seed with an explicit roll window (the first page of a scroll).</summary>
    [Export]
    public static MotelyJamlyzerSeedResult[] AnalyzeSeedsPaged(JamlConfig jaml, int eventRolls) =>
        [.. JamlyzerEngine.Analyze(jaml, eventRolls)];

    /// <summary>
    /// Resume each seed from the <c>streamStates</c> bag handed back by a previous page, continuing
    /// exactly where it stopped. Single-seed only (the bag's PRNG state is seed-specific).
    /// </summary>
    [Export]
    public static MotelyJamlyzerSeedResult[] ResumeSeeds(
        JamlConfig jaml, MotelyJamlyzerStreamStates resumeFrom, int eventRolls) =>
        [.. JamlyzerEngine.Analyze(jaml, resumeFrom, eventRolls)];
}

// ── Search ───────────────────────────────────────────────────────────────────
// Flat search wrappers: each runs the engine to completion on the (single) WASM thread. Results
// flow through the events — subscribe before starting:
//   OnProgress      → MotelyProgress (carries SeedsSearched / MatchingSeeds count / % / ETA …)
//   OnScoredResult  → each scored result { seed, score, tallies } (searches with a score provider)
//   OnSeedMatch     → each matching seed, bare (searches with no should/scoring)
// The awaited Task just signals completion; there is no return payload to keep in sync.
public static class MotelySearch
{
    [Export] public static event Action<MotelyProgress>? OnProgress;
    [Export] public static event Action<string>? OnSeedMatch;
    [Export] public static event Action<MotelyScoredSeedResult>? OnScoredResult;

    /// <summary>Search the explicit seed list in the JAML.</summary>
    [Export]
    public static Task SearchList(JamlConfig jaml)
    {
        if (jaml.Seeds.Count == 0)
            throw new InvalidOperationException("JAML has no seeds to search.");
        return Run(jaml, s => s.WithListSearch(jaml.Seeds, jaml.Seeds.Count));
    }

    /// <summary>Sequentially walk the seed space across the given batch range.</summary>
    [Export]
    public static Task SearchSequential(
        JamlConfig jaml, long startBatchIndex, long endBatchIndex, int batchCharacterCount)
    {
        return Run(jaml, s => s
            .WithSequentialSearch()
            .WithStartBatchIndex(startBatchIndex)
            .WithEndBatchIndex(endBatchIndex)
            .WithBatchCharacterCount(batchCharacterCount));
    }

    /// <summary>Search a number of random seeds.</summary>
    [Export]
    public static Task SearchRandom(JamlConfig jaml, int count)
    {
        return Run(jaml, s => s.WithRandomSearch(count));
    }

    private static async Task Run(
        JamlConfig jaml, Func<IMotelySearchSettings, IMotelySearchSettings> mode)
    {
        var settings = JamlSearchBuilder.CreateSettings(jaml)
            .WithDeck(jaml.Deck)
            .WithStake(jaml.Stake)
            .WithThreadCount(1); // WASM has no pthreads — one thread, runs on the caller.

        if (OnProgress is not null)
            settings = settings.WithProgressCallback(p => OnProgress.Invoke(p));
        if (OnSeedMatch is not null)
            settings = settings.WithSeedMatchCallback(s => OnSeedMatch.Invoke(s));
        if (OnScoredResult is not null)
            settings = settings.WithScoredResultCallback(t => OnScoredResult.Invoke(t));

        // Jimmolate: opt-in JS-authored seed finder, slotted as a link in the engine's filter chain.
        if (Jimmolate.Enabled)
        {
            MotelyDeck deck = jaml.Deck;
            MotelyStake stake = jaml.Stake;
            settings = settings.WithJimmolate(
                (ref MotelySingleSearchContext ctx) => Jimmolate.FindSeed(ctx.GetSeed(), deck, stake));
        }

        using var search = mode(settings).CreateSearch();
        await search.RunSearchAsync();
    }
}

// ── Jimmolate ────────────────────────────────────────────────────────────────
// A seed finder you write in JS and drop into the engine's filter chain — the imperative
// "is this the seed?" mental model the old-head seed gods know from Immolate. Bind FindSeed
// BEFORE boot() (Bootsharp snapshots [Import] bindings at boot; assigning after is a no-op),
// then flip Enabled to make it gate seeds during a search.
public static partial class Jimmolate
{
    [Import]
    public static partial bool FindSeed(string seed, MotelyDeck deck, MotelyStake stake);

    [Export]
    public static bool Enabled { get; set; }
}
