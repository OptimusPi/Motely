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
/// </summary>
public static partial class MotelyWasmApi
{
    /// <summary>Engine informational version, as stamped on the assembly.</summary>
    [Export]
    public static string Version() =>
        typeof(JamlConfigLoader).Assembly
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
    /// List-mode search: gate the given seeds through the filter's must clauses and rank the
    /// survivors by should score, best first. Runs on the engine's single-threaded browser pump,
    /// so the page stays responsive while it grinds.
    /// </summary>
    [Export]
    public static async Task<ScoreRun> ScoreSeeds(string jaml, string[] seeds)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            return new ScoreRun(false, error, 0, 0, 0, 0, []);

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

        return new ScoreRun(
            Ok: true,
            Error: null,
            TotalSeeds: search.TotalSeedsSearched,
            MatchingSeeds: search.MatchingSeeds,
            FilteredSeeds: search.FilteredSeeds,
            ElapsedMs: search.ElapsedMs,
            Results: [.. scored.OrderByDescending(s => s.Score)]);
    }

    /// <summary>
    /// Find matching seeds from an engine-owned search intent. Sequential and aesthetic searches
    /// require a match limit so a browser call cannot accidentally sweep the entire seed space.
    /// </summary>
    [Export]
    public static async Task<ScoreRun> FindSeeds(string jaml, MotelySearchIntent intent)
    {
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error))
            return new ScoreRun(false, error, 0, 0, 0, 0, []);

        if (
            intent.Mode is MotelySearchInputMode.Sequential or MotelySearchInputMode.Aesthetic
            && (!intent.StopAfterMatches.HasValue || intent.StopAfterMatches.Value < 1)
        )
        {
            return new ScoreRun(
                false,
                "Sequential and aesthetic searches require StopAfterMatches >= 1.",
                0,
                0,
                0,
                0,
                []
            );
        }

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

        return new ScoreRun(
            Ok: true,
            Error: null,
            TotalSeeds: search.TotalSeedsSearched,
            MatchingSeeds: search.MatchingSeeds,
            FilteredSeeds: search.FilteredSeeds,
            ElapsedMs: search.ElapsedMs,
            Results: [.. results.OrderByDescending(result => result.Score)]
        );
    }
}
