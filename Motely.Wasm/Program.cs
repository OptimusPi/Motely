using System.Reflection;
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.SeedProviders;
using JamlyzerEngine = Motely.Analysis.MotelyJamlyzer;

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

    [RenameMember]
    public static string? Member(MemberInfo info, string @default) =>
        info switch
        {
            MethodInfo m
                when m.ReturnType.IsByRef
                    || m.ReturnType.IsByRefLike
                    || m.GetParameters()
                        .Any(p => p.ParameterType.IsByRef || p.ParameterType.IsByRefLike) => null,
            PropertyInfo p when p.PropertyType.IsByRef || p.PropertyType.IsByRefLike => null,
            _ => @default,
        };
}

public static partial class MotelyWasm
{
    [Export]
    public static string GetVersion() =>
        typeof(MotelyWasm)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "0.0.0";
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

    /// <summary>
    /// Engine enum vocabulary for editors/agents. Case-insensitive substring filter
    /// ("luck" finds LuckyCat). Same names the SIMD path executes.
    /// </summary>
    [Export]
    public static string[] ListItems(string kind, string? query = null)
    {
        string[] names = kind.ToLowerInvariant() switch
        {
            "joker" or "jokers" => Enum.GetNames<MotelyJoker>(),
            "voucher" or "vouchers" => Enum.GetNames<MotelyVoucher>(),
            "tag" or "tags" => Enum.GetNames<MotelyTag>(),
            "boss" or "bosses" => Enum.GetNames<MotelyBossBlind>(),
            "deck" or "decks" => Enum.GetNames<MotelyDeck>(),
            "stake" or "stakes" => Enum.GetNames<MotelyStake>(),
            "edition" or "editions" => Enum.GetNames<MotelyItemEdition>(),
            "seal" or "seals" => Enum.GetNames<MotelyItemSeal>(),
            "tarotcard" or "tarotcards" or "tarot" => Enum.GetNames<MotelyTarotCard>(),
            "spectralcard" or "spectralcards" or "spectral" => Enum.GetNames<MotelySpectralCard>(),
            "planetcard" or "planetcards" or "planet" => Enum.GetNames<MotelyPlanetCard>(),
            _ => throw new ArgumentException(
                $"Unknown vocabulary kind '{kind}'. Kinds: joker, voucher, tag, boss, deck, stake, edition, seal, tarotCard, spectralCard, planetCard."
            ),
        };

        if (string.IsNullOrWhiteSpace(query))
            return names;

        return [.. names.Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase))];
    }

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

    [Export]
    public static Task<MotelyScoredSeedResult[]> SearchList(JamlConfig config) =>
        RunAsync(config, s => s.WithListSearch(config.Seeds, config.Seeds.Count));

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

    private static async Task<MotelyScoredSeedResult[]> RunAsync(
        JamlConfig config,
        Func<IMotelySearchSettings, IMotelySearchSettings> withMode
    )
    {
        List<MotelyScoredSeedResult> results = [];
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
        return [.. results];
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
