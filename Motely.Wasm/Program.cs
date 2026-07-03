using System.Reflection;
using Bootsharp;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Jummy;
using Motely.SeedProviders;
using JamlyzerEngine = Motely.Analysis.MotelyJamlyzer;

public static partial class Program
{
    public static void Main() { }
}

/// <summary>JavaScript cannot express C# byref: the generator renders those signatures as CLR
/// byref notation ("Type&"), which is not valid C# (CS1525 across Interop.g.cs). Erase exactly
/// the members whose signature needs byref/byref-like shapes; their state-threaded twins
/// (value in, value out — see JimmolateFilterTests) remain on the surface.</summary>
public static class MotelyWasmRenaming
{
    /// <summary>One flat import surface: every C# namespace folds into the root `index` module,
    /// so `import { MotelyVoucher, MotelySearch } from "motely-wasm"` works exactly as the
    /// README documents (renaming.md — module renamer).</summary>
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
    [Export]
    public static JamlConfig FromYaml(string content) => JamlConfigLoader.FromYaml(content);

    [Export]
    public static JamlConfig FromJson(string content) => JamlConfigLoader.FromJson(content);

    /// <summary>Null when the document loads clean; the loader's loud error otherwise.</summary>
    [Export]
    public static string? Validate(string content) =>
        JamlConfigLoader.TryLoad(content, out _, out string? error)
            ? null
            : error ?? "Invalid JAML.";

    [Export]
    public static string[] NativeFilterNames() => MotelyNativeFilterNames.DisplayNames;

    /// <summary>
    /// The JAML vocabulary, straight from the engine's own enums — the anti-hallucination
    /// tool. Editors complete from it, agents verify against it, and it can never drift
    /// from what the engine executes. Query filters by case-insensitive substring
    /// ("luck" finds LuckyCat).
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

    /// <summary>Null when the JUMMY line parses; the parser's loud error otherwise.</summary>
    [Export]
    public static string? ValidateLine(string line) => JummyLine.Validate(line);

    [Export]
    public static string CanonicalizeLine(string line) => JummyLine.Canonicalize(line);
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

public static partial class MotelySearch
{
    [Export]
    public static event Action<MotelyProgress>? OnProgress;

    [Export]
    public static event Action<string>? OnSeedMatch;

    [Export]
    public static event Action<MotelyScoredSeedResult>? OnScoredResult;

    /// <summary>The promise resolves with the scored results — call it, await it, use it.
    /// Events stream progress and incremental finds along the way for live UIs.</summary>
    [Export]
    public static Task<MotelyScoredSeedResult[]> SearchList(JamlConfig config) =>
        RunAsync(config, s => s.WithListSearch(config.Seeds, config.Seeds.Count));

    [Export]
    public static Task<MotelyScoredSeedResult[]> SearchRandom(JamlConfig config, int count) =>
        RunAsync(config, s => s.WithRandomSearch(count));

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
        settings = withMode(settings)
            .WithJimmolate(Jimmolate.Filter);
        using IMotelySearch search = settings.CreateSearch();
        await search.RunSearchAsync();
        return [.. results];
    }
}

public static partial class Jimmolate
{
    /// <summary>The predicate — the OG Immolate filter(inst) => keep? contract — bound from JavaScript before boot(): the live single-seed
    /// context crosses as an interop instance, so the predicate can drive every query a native
    /// filter can. Keep-all (<c>ctx => true</c>) is the neutral binding.</summary>
    [Import]
    public static partial int Filter(MotelySingleSearchContext ctx);
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

    /// <summary>[startBatchIndex, endBatchIndexExclusive] as a spreadable pair.</summary>
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
