using System.Reflection;
using Bootsharp;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Lsp.Core;

namespace Motely.Wasm;

/// <summary>
/// The browser surface of the Motely engine. Every member returns an engine type — Bootsharp
/// generates the TypeScript declarations from these signatures and serializes records, enums and
/// dictionaries across the boundary itself.
/// <para>
/// Nothing here restates the engine. The vocabulary is enumerated from the generated
/// <see cref="JamlSchema"/>, which Motely.Generators builds from the <c>[JamlDiscriminator]</c>
/// attributes on the FilterDescs — the descs that actually run the criteria. A desc added tomorrow
/// appears in the browser without this file changing. That is the only arrangement in which the
/// head cannot drift from the engine.
/// </para>
/// <para>
/// Async exports return plain <see cref="Task"/> and results are collected with the synchronous
/// <see cref="TakeRun"/>. Bootsharp packs serialized values into an <c>Int64</c>, and the .NET
/// runtime implements that marshaling for synchronous returns but not for <c>Task&lt;Int64&gt;</c>
/// (<c>ToJSNotImplemented</c> on Mono; opaque failure on NativeAOT-LLVM) — so an async export
/// returning a record can never reach JavaScript. The host recomposes the two calls into one
/// awaitable, so pages still write <c>await motely.scoreSeeds(...)</c>.
/// </para>
/// </summary>
public static partial class MotelyWasmApi
{
    /// <summary>
    /// Engine version. Motely.dll deliberately carries no assembly info, so the stamp is read
    /// from this assembly, which inherits the repo MotelyVersion via Directory.Build.props —
    /// the same arrangement Motely.HelperAPI uses.
    /// </summary>
    [Export]
    public static string Version() =>
        typeof(MotelyWasmApi).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

    /// <summary>Validate a JAML document with the one true loader. Never throws.</summary>
    [Export]
    public static ParseResult ParseJaml(string text) =>
        JamlConfigLoader.TryLoad(text, out var config, out var error)
            ? new ParseResult(
                Ok: true,
                Error: null,
                Name: config.Name,
                Deck: config.Deck.ToString(),
                Stake: config.Stake.ToString(),
                Must: config.Must.Count,
                Should: config.Should.Count,
                MustNot: config.MustNot.Count)
            : new ParseResult(false, error, null, null, null, 0, 0, 0);

    /// <summary>
    /// Every vocabulary kind the grammar knows, mapped to its member names. Enumerated from
    /// <see cref="JamlSchema.ValueEnumKinds"/> — generated from the FilterDescs, so this covers
    /// every enum the engine actually uses, including the ones nobody remembered to list.
    /// Crosses to JavaScript as a <c>Map</c>.
    /// </summary>
    [Export]
    public static IReadOnlyDictionary<string, string[]> Vocabulary() =>
        JamlSchema.ValueEnumKinds.ToDictionary(
            kind => kind.Kind,
            kind => JamlSchema.ListItems(kind.Kind));

    /// <summary>
    /// Every clause wire the loader will construct — "joker", "voucher", "luckyMoney", and the
    /// rest of the event clauses. This is the grammar's other axis: a discriminator is a desc, not
    /// a member of any enum, so <see cref="Vocabulary"/> can never surface one. Generated from the
    /// <c>[JamlDiscriminator]</c> attributes themselves.
    /// </summary>
    [Export]
    public static string[] Discriminators() => JamlSchema.Discriminators;

    /// <summary>The keys a given clause accepts, e.g. "min"/"max"/"score" — or empty when unknown.</summary>
    [Export]
    public static string[] ClauseKeys(string discriminator) =>
        JamlSchema.ClauseKeysFor(discriminator);

    /// <summary>Squiggles with spans and stable codes. Severity crosses as the enum it is.</summary>
    [Export]
    public static IReadOnlyList<JamlDiagnostic> Diagnostics(string text) =>
        JamlLanguageService.Diagnose(text);

    /// <summary>Hover content for the word at (line, character), or null.</summary>
    [Export]
    public static JamlHoverInfo? Hover(string text, int line, int character) =>
        JamlLanguageService.Hover(text, line, character);

    /// <summary>Completion candidates with kinds and replace spans.</summary>
    [Export]
    public static IReadOnlyList<JamlCompletionItem> Complete(string text, int line, int character) =>
        JamlLanguageService.Complete(text, line, character);

    /// <summary>Prose for a grammar topic, or null when the topic is unknown.</summary>
    [Export]
    public static string? Explain(string topic) => JamlLanguageService.Explain(topic);

    /// <summary>LSP semantic tokens, in the standard 5-int-per-token encoding.</summary>
    [Export]
    public static IReadOnlyList<int> SemanticTokens(string text) =>
        JamlLanguageService.SemanticTokens(text);

    /// <summary>The token types <see cref="SemanticTokens"/> indexes into.</summary>
    [Export]
    public static string[] SemanticTokenTypes() => JamlLanguageService.SemanticTokenTypes;

    /// <summary>
    /// Outcome of the most recently completed run, parked here because async exports cannot
    /// return serialized values (see class remarks). The engine pump is single-threaded in the
    /// browser, so one slot is the honest capacity — there is never a second run in flight.
    /// </summary>
    private static ScoreRun? _completedRun;

    /// <summary>
    /// Collect the outcome of the last run started by <see cref="RunScoreSeeds"/> or
    /// <see cref="RunFindSeeds"/>. Taking clears the slot; taking with nothing completed
    /// returns a not-ok run that says so.
    /// </summary>
    [Export]
    public static ScoreRun TakeRun()
    {
        var run = _completedRun
            ?? new ScoreRun(false, "No completed run to take. Await RunScoreSeeds or RunFindSeeds first.", 0, 0, 0, 0, []);
        _completedRun = null;
        return run;
    }

    /// <summary>
    /// List-mode search: gate the given seeds through the filter's must clauses and rank the
    /// survivors by should score, best first. Runs on the engine's single-threaded browser pump,
    /// so the page stays responsive while it grinds. Await, then <see cref="TakeRun"/>.
    /// </summary>
    [Export]
    public static async Task RunScoreSeeds(string jaml, string[] seeds)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
        {
            _completedRun = new ScoreRun(false, error, 0, 0, 0, 0, []);
            return;
        }

        try
        {
            List<ScoredSeed> scored = [];
            var settings = JamlSearchBuilder
                .CreateSettings(config)
                .WithSeedList([.. seeds.Where(s => !string.IsNullOrWhiteSpace(s))])
                .WithThreadCount(1)
                .WithQuietMode(true)
                .WithScoredResultCallback(tally =>
                    scored.Add(new ScoredSeed(tally.Seed, tally.Score, [.. tally.Tally.Select(b => (int)b)])));

            using var search = settings.CreateSearch();
            await search.Start().WaitForCompletionAsync();

            _completedRun = new ScoreRun(
                Ok: true,
                Error: null,
                TotalSeeds: search.TotalSeedsSearched,
                MatchingSeeds: search.MatchingSeeds,
                FilteredSeeds: search.FilteredSeeds,
                ElapsedMs: search.ElapsedMs,
                Results: [.. scored.OrderByDescending(s => s.Score)]);
        }
        catch (Exception searchException)
        {
            // Honest error over opaque interop crash: the browser gets the real reason.
            _completedRun = new ScoreRun(false, searchException.ToString(), 0, 0, 0, 0, []);
        }
    }

    /// <summary>
    /// Find matching seeds from an engine-owned search intent. Sequential and aesthetic searches
    /// require a match limit so a browser call cannot accidentally sweep the entire seed space.
    /// Await, then <see cref="TakeRun"/>.
    /// </summary>
    [Export]
    public static async Task RunFindSeeds(string jaml, MotelySearchIntent intent)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
        {
            _completedRun = new ScoreRun(false, error, 0, 0, 0, 0, []);
            return;
        }

        if (
            intent.Mode is MotelySearchInputMode.Sequential or MotelySearchInputMode.Aesthetic
            && (!intent.StopAfterMatches.HasValue || intent.StopAfterMatches.Value < 1)
        )
        {
            _completedRun = new ScoreRun(
                false,
                "Sequential and aesthetic searches require StopAfterMatches >= 1.",
                0,
                0,
                0,
                0,
                []
            );
            return;
        }

        try
        {
            List<ScoredSeed> results = [];
            object resultsLock = new();
            var settings = intent
                .ApplyTo(JamlSearchBuilder.CreateSettings(config))
                .WithQuietMode(true)
                .WithSeedMatchCallback(seed =>
                {
                    lock (resultsLock)
                        results.Add(new ScoredSeed(seed, 0, []));
                })
                .WithScoredResultCallback(tally =>
                {
                    lock (resultsLock)
                        results.Add(
                            new ScoredSeed(
                                tally.Seed,
                                tally.Score,
                                [.. tally.Tally.Select(value => (int)value)]
                            )
                        );
                });

            using var search = settings.CreateSearch();
            await search.Start().WaitForCompletionAsync();

            _completedRun = new ScoreRun(
                Ok: true,
                Error: null,
                TotalSeeds: search.TotalSeedsSearched,
                MatchingSeeds: search.MatchingSeeds,
                FilteredSeeds: search.FilteredSeeds,
                ElapsedMs: search.ElapsedMs,
                Results: [.. results.OrderByDescending(result => result.Score)]
            );
        }
        catch (Exception searchException)
        {
            // Honest error over opaque interop crash: the browser gets the real reason.
            _completedRun = new ScoreRun(false, searchException.ToString(), 0, 0, 0, 0, []);
        }
    }
}
