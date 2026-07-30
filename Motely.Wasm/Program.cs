using System.Diagnostics;
using System.Reflection;
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Lsp.Core;
using Motely.SeedProviders;
using JamlyzerEngine = Motely.Analysis.MotelyJamlyzer;

// Bootsharp marshals an interface by reference as an interop instance, so JS drives the same
// chainable With* surface the CLI does.
[assembly: Export(typeof(IMotelySearchSettings), typeof(IMotelySearch))]

/// <summary>
/// WASM head for Motely: same engine path as native (JamlConfigLoader + JamlSearchBuilder +
/// MotelySearch). One grammar — JAML text only. Flat <c>index</c> module for JS imports.
/// Bootsharp.FileSystem registers <c>IFileMounter</c> via AddBootsharp (sponsor package).
/// </summary>
public static partial class Program
{
    public static void Main()
    {
        // AddBootsharp() registers extension interop (IFileMounter from Bootsharp.FileSystem).
        // MotelyServices holds the provider so static [Export] methods can resolve it.
        var services = new ServiceCollection().AddBootsharp().BuildServiceProvider();
        MotelyServices.Init(services);
    }
}

/// <summary>Static locator for Bootsharp's process-wide DI container (FileSystem IFileMounter).</summary>
public static class MotelyServices
{
    private static IServiceProvider? _services;

    public static void Init(IServiceProvider services) => _services = services;

    public static T Get<T>()
        where T : notnull =>
        (_services ?? throw new InvalidOperationException("MotelyServices.Init was never called."))
            .GetRequiredService<T>();
}

/// <summary>
/// JS cannot express C# byref; erase members whose signatures need byref/byref-like shapes.
/// State-threaded value-in/value-out twins stay on the surface.
/// </summary>
public static class MotelyWasmRenaming
{
    /// <summary>Fold every C# namespace into root <c>index</c> so
    /// <c>import { MotelySearch, MotelyJaml } from "motely-wasm"</c> works.</summary>
    [RenameModule]
    public static string Module(Type type, string @default) => "index";

    // Byref/ref-struct members are handled by MotelySingleSearchContextSpecialization, not a
    // shape sweep here: a global blocklist is a second, invisible API next to [Export], and it
    // deletes members silently the moment an engine type grows a ref member.
}

public static partial class MotelyWasm
{
    [Export]
    public static string GetVersion() =>
        typeof(MotelyWasm)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "0.0.0";
}

/// <summary>
/// The language brain (<see cref="JamlLanguageService"/>) that the stdio server hosts, exported
/// for browser and agent clients. An editor — or an agent writing JAML through MCP — gets the
/// engine's own diagnostics, hover and completion instead of guessing at the grammar.
/// </summary>
public static partial class MotelyLsp
{
    /// <summary>Errors and warnings for a whole document, from the real loader.</summary>
    [Export]
    public static IReadOnlyList<JamlDiagnostic> Diagnose(string text) =>
        JamlLanguageService.Diagnose(text);

    /// <summary>Markdown for the word at a cursor position, or null when there is nothing to say.</summary>
    [Export]
    public static JamlHoverInfo? Hover(string text, int line, int character) =>
        JamlLanguageService.Hover(text, line, character);

    /// <summary>Completion candidates at a cursor position, already filtered by the typed prefix.</summary>
    [Export]
    public static IReadOnlyList<JamlCompletionItem> Complete(string text, int line, int character) =>
        JamlLanguageService.Complete(text, line, character);

    /// <summary>Schema explanation of a discriminator, key or vocabulary word.</summary>
    [Export]
    public static string? Explain(string topic) => JamlLanguageService.Explain(topic);
}

public static partial class MotelyJaml
{
    /// <summary>Parse JAML text with the engine loader — the only filter grammar path.</summary>
    [Export]
    public static JamlConfig FromJaml(string content) => JamlConfigLoader.FromJaml(content);

    /// <summary>Null when the document loads clean; the loader's error otherwise.</summary>
    [Export]
    public static string? Validate(string content) =>
        JamlConfigLoader.TryLoad(content, out _, out string? error)
            ? null
            : error ?? "Invalid JAML.";

    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;

    /// <summary>Thin export of generated <see cref="JamlSchema.ListItems"/>.</summary>
    [Export]
    public static string[] ListItems(string kind, string? query = null) =>
        JamlSchema.ListItems(kind, query);

    [Export]
    public static string? ValidateLine(string line) => JamlLine.Validate(line);

    [Export]
    public static string CanonicalizeLine(string line) => JamlLine.Canonicalize(line);
}

public static partial class MotelyJamlyzer
{
    [Export]
    public static IReadOnlyList<MotelyJamlyzerSeedResult> AnalyzeSeeds(JamlConfig config) =>
        JamlyzerEngine.Analyze(config);

    [Export]
    public static IReadOnlyList<MotelyJamlyzerSeedResult> AnalyzeSeedsPaged(
        JamlConfig config,
        int eventRolls
    ) => JamlyzerEngine.Analyze(config, eventRolls);

    [Export]
    public static IReadOnlyList<MotelyJamlyzerSeedResult> ResumeSeeds(
        JamlConfig config,
        MotelyJamlyzerStreamStates resumeFrom,
        int eventRolls
    ) => JamlyzerEngine.Analyze(config, resumeFrom, eventRolls);
}

/// <summary>
/// Search surface: <see cref="JamlSearchBuilder.CreateSettings"/> + engine
/// <see cref="IMotelySearch.RunSearchAsync"/> — same chain as Motely.CLI.
/// </summary>
public static partial class MotelySearch
{
    [Export]
    public static event Action<MotelyProgress>? OnProgress;

    [Export]
    public static event Action<string>? OnSeedMatch;

    [Export]
    public static event Action<MotelyScoredSeedResult>? OnScoredResult;

    /// <summary>The engine's settings, for JS to drive directly. Config comes from MotelyJaml.FromJaml.</summary>
    [Export]
    public static IMotelySearchSettings Settings(JamlConfig config) =>
        JamlSearchBuilder.CreateSettings(config);

    [Export]
    public static Task<MotelyScoredSeedResult[]> SearchList(JamlConfig config) =>
        RunAsync(config, s => s.WithSeedList([.. config.Seeds]));

    [Export]
    public static Task<MotelyScoredSeedResult[]> SearchRandom(JamlConfig config, int count) =>
        RunAsync(config, s => s.WithRandomSearch(count));

    /// <summary>Sequential sweep. Pass batch indices as JS BigInt (C# <c>long</c>).</summary>
    [Export]
    public static Task<MotelyScoredSeedResult[]> SearchSequential(
        JamlConfig config,
        long startBatchIndex,
        long endBatchIndex,
        int batchCharacterCount
    ) =>
        RunAsync(
            config,
            s =>
                s.WithSequentialSearch()
                    .WithBatchCharacterCount(batchCharacterCount)
                    .WithStartBatchIndex(startBatchIndex)
                    .WithEndBatchIndex(endBatchIndex)
        );

    /// <summary>
    /// CLI <c>--collect N</c> shape: aesthetics first (all families, digit-pad free slots —
    /// same as CLI when <c>--padding</c> is omitted), then sequential for the remainder.
    /// <paramref name="stopAfter"/> is N (JS BigInt / C# <c>long</c>). SIMD may deliver a few over.
    /// </summary>
    [Export]
    public static async Task<MotelyScoredSeedResult[]> Collect(JamlConfig config, long stopAfter)
    {
        Debug.Assert(stopAfter >= 1, "stopAfter must be >= 1.");

        List<MotelyScoredSeedResult> results = [];
        var aesthetics = Enum.GetValues<JamlAesthetic>();
        char[] collectPad = JamlAesthetics.QuickPaddingChars;
        await RunIntoAsync(
            config,
            results,
            s =>
                s.WithProviderSearch(
                        new MotelySeedListProvider(
                            aesthetics.SelectMany(a =>
                                JamlAesthetics.EnumerateSeeds(a, collectPad)
                            ),
                            aesthetics.Sum(a => JamlAesthetics.GetSeedCount(a, collectPad))
                        )
                    )
                    .StopAfter(stopAfter)
        );

        long remaining = stopAfter - results.Count;
        if (remaining > 0)
        {
            await RunIntoAsync(
                config,
                results,
                s =>
                    s.WithSequentialSearch()
                        .WithBatchCharacterCount(4)
                        .StopAfter(remaining)
            );
        }

        return [.. results];
    }

    /// <summary>
    /// Collect with an explicit sequential range only (CLI <c>--collect N</c> + start/end batch).
    /// No aesthetic pass. Batch indices are JS BigInt.
    /// </summary>
    [Export]
    public static Task<MotelyScoredSeedResult[]> CollectSequential(
        JamlConfig config,
        long stopAfter,
        long startBatchIndex,
        long endBatchIndex,
        int batchCharacterCount
    )
    {
        Debug.Assert(stopAfter >= 1, "stopAfter must be >= 1.");

        return RunAsync(
            config,
            s =>
                s.WithSequentialSearch()
                    .WithBatchCharacterCount(batchCharacterCount)
                    .WithStartBatchIndex(startBatchIndex)
                    .WithEndBatchIndex(endBatchIndex)
                    .StopAfter(stopAfter)
        );
    }

    /// <summary>CLI <c>--collect 1</c> — <see cref="Collect"/>(config, 1).</summary>
    [Export]
    public static Task<MotelyScoredSeedResult[]> FindOne(JamlConfig config) =>
        Collect(config, stopAfter: 1);

    private static async Task<MotelyScoredSeedResult[]> RunAsync(
        JamlConfig config,
        Func<IMotelySearchSettings, IMotelySearchSettings> withMode
    )
    {
        List<MotelyScoredSeedResult> results = [];
        await RunIntoAsync(config, results, withMode);
        return [.. results];
    }

    private static async Task RunIntoAsync(
        JamlConfig config,
        List<MotelyScoredSeedResult> results,
        Func<IMotelySearchSettings, IMotelySearchSettings> withMode
    )
    {
        IMotelySearchSettings settings = JamlSearchBuilder
            .CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithProgressCallback(p => OnProgress?.Invoke(p))
            .WithSeedMatchCallback(s => OnSeedMatch?.Invoke(s))
            .WithScoredResultCallback(r =>
            {
                results.Add(r);
                OnScoredResult?.Invoke(r);
            });
        settings = withMode(settings);
        using IMotelySearch search = settings.CreateSearch();
        await search.RunSearchAsync();
    }
}

public static partial class MotelyUtilities
{
    [Export]
    public static long SeedToTotalIndex(string seed) => SeedMath.SeedToTotalIndex(seed);

    [Export]
    public static string TotalIndexToSeed(long index) => SeedMath.TotalIndexToSeed(index);

    [Export]
    public static long SeedToSearchIndex(string seed) => SeedMath.SeedToSearchIndex(seed);

    [Export]
    public static string SearchIndexToSeed(long index, int length) =>
        SeedMath.SearchIndexToSeed(index, length);

    [Export]
    public static long GetFirstSeedOfLength(int length) => SeedMath.GetFirstSeedOfLength(length);

    [Export]
    public static long MaxSearchIndexInclusive(int length) =>
        SeedMath.MaxSearchIndexInclusive(length);

    [Export]
    public static long SeedToBatchIndex(string seed, int batchSize) =>
        SeedMath.SeedToBatchIndex(seed, batchSize);

    [Export]
    public static string BatchIndexToSeedPrefix(long batchIndex, int batchSize) =>
        SeedMath.BatchIndexToSeedPrefix(batchIndex, batchSize);

    [Export]
    public static long[] SearchIndexRangeToBatchRange(
        long startSearchIndex,
        long endSearchIndexInclusive,
        int batchCharacterCount
    )
    {
        (long start, long endExclusive) = SeedMath.SearchIndexRangeToBatchRange(
            startSearchIndex,
            endSearchIndexInclusive,
            batchCharacterCount
        );
        return [start, endExclusive];
    }

    [Export]
    public static string[] RepeatCharKeywords(int repeatCount) =>
        [.. MotelySeedKeywordSequences.RepeatCharKeywords(repeatCount)];

    [Export]
    public static string[] AscendingDigitLetterKeywords(int length) =>
        [.. MotelySeedKeywordSequences.AscendingDigitLetterKeywords(length)];

    [Export]
    public static string[] DescendingDigitLetterKeywords(int length) =>
        [.. MotelySeedKeywordSequences.DescendingDigitLetterKeywords(length)];

    [Export]
    public static string[] MirrorPatternKeywords(int length) =>
        [.. MotelySeedKeywordSequences.MirrorPatternKeywords(length)];

    [Export]
    public static long GetAestheticSeedCount(JamlAesthetic aesthetic) =>
        MotelySeedKeywordSequences.GetAestheticSeedCount(aesthetic);

    [Export]
    public static string[] GrossKeywords() => MotelySeedKeywordSequences.GrossKeywords;

    [Export]
    public static string[] FunnyKeywords() => MotelySeedKeywordSequences.FunnyKeywords;

    [Export]
    public static string[] BalatroKeywords() => MotelySeedKeywordSequences.BalatroKeywords;
}
