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

    // Hide the C# bootstrap and any unmarshallable type from the JS surface:
    //  - ref structs (Span etc.) never marshal
    //  - byref `Type&` (from `ref`/`out` params like `ref MotelySingleBossStream`) emits an invalid
    //    JS node name (`export const Foo& = …`), so it must be erased too — exposing the context
    //    instance made Bootsharp walk these for the first time.
    [RenameNode]
    public static string? RenameNode(Type type, string @default) =>
        type == typeof(Boot) ? null
        : type.IsByRefLike ? null
        : type.IsByRef ? null
        : @default;

}

[SpecializeImport(typeof(MotelySingleSearchContext))]
public abstract class MotelySingleSearchContextImport(int id) : SpecializedImport(id)
{
    public abstract MotelyDeck Deck { get; }
    public abstract MotelyStake Stake { get; }
    public abstract string GetSeed();
    // First voucher of an ante — int in, enum out, marshals clean. Lets a JS predicate
    // DERIVE a fact about the seed (e.g. ante-1 voucher) instead of just reading its name.
    public abstract MotelyVoucher GetAnteFirstVoucher(int ante);
}

[SpecializeExport(typeof(MotelySingleSearchContext))]
public sealed class MotelySingleSearchContextExport(MotelySingleSearchContext ctx) : SpecializedExport(ctx)
{
    public MotelyDeck Deck => ctx.Deck;
    public MotelyStake Stake => ctx.Stake;
    public string GetSeed() => ctx.GetSeed();
    public MotelyVoucher GetAnteFirstVoucher(int ante) => ctx.GetAnteFirstVoucher(ante);
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

    [Export]
    public static string? ValidateLine(string line) =>
        JummyLine.TryToClause(line, out _, out var error) ? null : error;

    [Export]
    public static string? CanonicalizeLine(string line) =>
        JummyLine.TryToClause(line, out var clause, out var error)
            ? JummyLine.FromClause(clause!)
            : throw new InvalidOperationException(error ?? "Invalid JUMMY line.");

    /// <summary>Plan a search (keyword candidate counts etc.) for the given JamlConfig.</summary>
    [Export]
    public static JamlSearchPlan CreatePlan(JamlConfig jaml) =>
        JamlSearchBuilder.CreatePlan(jaml);

    /// <summary>Display names of the built-in native (C#) filters.</summary>
    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;
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
    /// <summary>
    /// Each analyzed seed, streamed as it's produced — the JAMLyzer twin of MotelySearch.OnScoredResult.
    /// Fires from the analyze path only (never the search loop), so a full analysis is built solely for
    /// seeds you actually ask to analyze — not for the spam the top-N heap would evict.
    /// </summary>
    [Export] public static event Action<MotelyJamlyzerSeedResult>? OnJamlyzedResult;

    /// <summary>Analyze each seed with the default window (20 rolls) from each stream's natural start.</summary>
    [Export]
    public static MotelyJamlyzerSeedResult[] AnalyzeSeeds(JamlConfig jaml) =>
        Emit(JamlyzerEngine.Analyze(jaml));

    /// <summary>Analyze each seed with an explicit roll window (the first page of a scroll).</summary>
    [Export]
    public static MotelyJamlyzerSeedResult[] AnalyzeSeedsPaged(JamlConfig jaml, int eventRolls) =>
        Emit(JamlyzerEngine.Analyze(jaml, eventRolls));

    /// <summary>
    /// Resume each seed from the <c>streamStates</c> bag handed back by a previous page, continuing
    /// exactly where it stopped. Single-seed only (the bag's PRNG state is seed-specific).
    /// </summary>
    [Export]
    public static MotelyJamlyzerSeedResult[] ResumeSeeds(
        JamlConfig jaml, MotelyJamlyzerStreamStates resumeFrom, int eventRolls) =>
        Emit(JamlyzerEngine.Analyze(jaml, resumeFrom, eventRolls));

    // Materialize the analysis, firing OnJamlyzedResult per seed as it lands, and still return the
    // full array (back-compat with callers that take the return value). When nobody's subscribed,
    // skip the per-item walk and just build the array.
    private static MotelyJamlyzerSeedResult[] Emit(IEnumerable<MotelyJamlyzerSeedResult> results)
    {
        var handler = OnJamlyzedResult;
        if (handler is null)
            return [.. results];

        var collected = new List<MotelyJamlyzerSeedResult>();
        foreach (var result in results)
        {
            collected.Add(result);
            handler.Invoke(result);
        }
        return [.. collected];
    }
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

        // A scoring search (has must/should clauses) ships its per-seed output — the
        // seed,score,tallies CSV string — out through the SAME seedMatchCallback the bare
        // no-scoring path uses. When a rich consumer is listening (OnScoredResult), that CSV is
        // redundant with the structured object and is the only source of the double-emit. So we
        // withhold the CSV courier in exactly that case. Non-scoring searches keep bare OnSeedMatch
        // regardless (the courier is the only way bare matches get out).
        bool isScoringSearch = jaml.Must.Count + jaml.Should.Count > 0;
        bool suppressRedundantCsv = isScoringSearch && OnScoredResult is not null;

        if (OnProgress is not null)
            settings = settings.WithProgressCallback(p => OnProgress.Invoke(p));
        if (OnSeedMatch is not null && !suppressRedundantCsv)
            settings = settings.WithSeedMatchCallback(s => OnSeedMatch.Invoke(s));
        if (OnScoredResult is not null)
            settings = settings.WithScoredResultCallback(t => OnScoredResult.Invoke(t));

        if (Jimmolate.FindSeed is { } findSeed)
        {
            settings = settings.WithJimmolate(
                (ref MotelySingleSearchContext ctx) => findSeed(ctx));
        }

        using var search = mode(settings).CreateSearch();
        await search.RunSearchAsync();
    }
}

// ── Jimmolate ────────────────────────────────────────────────────────────────
// Immolate model: one predicate = one kernel. Bound (Import) BEFORE boot(), compiled
// into the engine run — not a mutable post-boot slot. JS assigns
//   Jimmolate.findSeed = (ctx) => bool;
// BEFORE bootsharp.boot(). The predicate runs native in-engine on the live ctx.
public static partial class Jimmolate
{
    [Import]
    public static partial Func<MotelySingleSearchContext, bool>? FindSeed { get; set; }
}

// ── Per-search Jimmolate (the C# shape) ──────────────────────────────────────
// EXPERIMENT: prove the predicate can be passed AS AN ARGUMENT per search — matching
// C#'s `search.WithJimmolate(pred)` — instead of the global pre-boot slot above.
// A user interface crosses by instance binding (interop-instances): JS provides an
// object implementing it; C# uses it as the real interface. (A delegate can't be
// implemented by a JS object, so the predicate must be an interface, not a Func.)
public interface IJimmolatePredicate
{
    bool FindSeed(MotelySingleSearchContext ctx);
}

public static class MotelySearchWith
{
    /// <summary>Search the JAML's seed list, gating on a predicate handed in per call.</summary>
    [Export]
    public static async Task SearchList(JamlConfig jaml, IJimmolatePredicate predicate)
    {
        if (jaml.Seeds.Count == 0)
            throw new InvalidOperationException("JAML has no seeds to search.");

        var settings = JamlSearchBuilder.CreateSettings(jaml)
            .WithDeck(jaml.Deck)
            .WithStake(jaml.Stake)
            .WithThreadCount(1)
            .WithListSearch(jaml.Seeds, jaml.Seeds.Count)
            .WithJimmolate((ref MotelySingleSearchContext ctx) => predicate.FindSeed(ctx));

        using var search = settings.CreateSearch();
        await search.RunSearchAsync();
    }
}
