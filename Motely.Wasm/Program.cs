using System.Reflection;
using Bootsharp;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Filters.Jaml;

public static partial class Program
{
    public static void Main() { }

    /// <summary>Parses JAML into the real <see cref="JamlConfig"/>, passed to JS by reference —
    /// read/tweak deck, stake, seeds, name live, then hand it to <see cref="CreateSearch"/>.</summary>
    [Export]
    public static JamlConfig LoadJaml(string jamlYaml) => JamlConfigLoader.FromYaml(jamlYaml);

    /// <summary>The engine's own settings object, filter and scoring wired (same path as CLI
    /// <c>--jaml</c>). Chain the fluent <c>with*</c> calls, then <c>start()</c> and await
    /// <c>waitForCompletionAsync()</c> — in the browser that rides the engine's async pump.
    /// A JAML <c>seeds:</c> list front-runs as a seed list, same default as the CLI.
    /// <paramref name="jimmolate"/> chains a JS-authored per-seed predicate as a REAL
    /// in-engine filter: it receives the live <see cref="MotelySingleSearchContext"/> — the
    /// same drivable instance C# predicates get — and returns keep/drop.</summary>
    [Export]
    public static IMotelySearchSettings CreateSearch(
        JamlConfig config,
        int minScore = 0,
        Func<MotelySingleSearchContext, bool>? jimmolate = null
    )
    {
        var settings = JamlSearchBuilder
            .CreateSettings(config, minScore)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1);
        if (config.Seeds.Count > 0)
            settings = settings.WithListSearch(config.Seeds, config.Seeds.Count);
        if (jimmolate is not null)
            settings = settings.WithJimmolate((ref MotelySingleSearchContext ctx) =>
                jimmolate(ctx)
            );
        return settings;
    }

    /// <summary>Jamlyzer: full structured per-seed breakdown (antes, shops, packs, pulls,
    /// event streams) for every seed in the config.</summary>
    [Export]
    public static MotelyJamlyzerSeedResult[] Analyze(JamlConfig config, int eventRolls = 20) =>
        [.. MotelyJamlyzer.Analyze(config, eventRolls)];

    /// <summary>Jamlyzer for one seed — e.g. per search match, straight off the callback.</summary>
    [Export]
    public static MotelyJamlyzerSeedResult AnalyzeSeed(
        JamlConfig config,
        string seed,
        int eventRolls = 20
    )
    {
        // Swap the seed list in place (JamlConfig has no clone); restore so the JS-held
        // config instance comes back exactly as it went in.
        var prior = config.Seeds.ToList();
        config.Seeds.Clear();
        config.Seeds.Add(seed);
        try
        {
            return MotelyJamlyzer.Analyze(config, eventRolls)[0];
        }
        finally
        {
            config.Seeds.Clear();
            config.Seeds.AddRange(prior);
        }
    }

    /// <summary>Scrolls a single seed's event streams forward from the
    /// <see cref="MotelyJamlyzerStreamStates"/> bag carried by a previous result.</summary>
    [Export]
    public static MotelyJamlyzerSeedResult AnalyzeNext(
        JamlConfig config,
        MotelyJamlyzerStreamStates resumeFrom,
        int eventRolls = 20
    ) => MotelyJamlyzer.Analyze(config, resumeFrom, eventRolls)[0];
}

/// <summary>
/// Bootsharp renamers. Motely's real interfaces cross the boundary; these erase (return null)
/// ONLY the members whose types cannot physically cross a JS boundary — descriptor plumbing
/// built on <c>ref MotelyFilterCreationContext</c>/SIMD providers — plus the blocking
/// <c>AwaitCompletion</c> (would deadlock the browser's single thread; the async pump path is
/// <c>start()</c> + <c>waitForCompletionAsync()</c>). Requires Bootsharp >= 0.9.1-alpha.1,
/// where erased members are pruned BEFORE the type crawl.
/// </summary>
public static class Prefs
{
    private static readonly HashSet<string> SealedSettings =
    [
        nameof(IMotelySearchSettings.BaseFilterDescBase),
        nameof(IMotelySearchSettings.AdditionalFilters),
        nameof(IMotelySearchSettings.WithAdditionalFilter),
        nameof(IMotelySearchSettings.WithSeedScoreProvider),
        nameof(IMotelySearchSettings.WithSeedAnalyzeProvider),
        nameof(IMotelySearchSettings.WithSeedRouter),
        nameof(IMotelySearchSettings.WithProviderSearch),
        nameof(IMotelySearchSettings.WithAestheticSearch),
        nameof(IMotelySearchSettings.WithJimmolate), // ref-delegate form; Program.WithJimmolate is the JS door
        nameof(IMotelySearchSettings.WithListSearch), // IEnumerable isn't marshalable; lists go via config.seeds
    ];

    // The live per-seed context handed to Jimmolate predicates: default-deny, since most of its
    // surface drives ref-struct PRNG streams. Whitelist grows as bridgeable queries are needed.
    private static readonly HashSet<string> OpenSingleContext =
    [
        nameof(MotelySingleSearchContext.GetSeed),
        nameof(MotelySingleSearchContext.GetAnteFirstVoucher),
    ];

    [RenameModule]
    public static string? RenameModule(Type type, string @default) =>
        // Flatten Motely namespaces into the root module: `import { Program, ... } from "motely"`.
        type.Namespace?.StartsWith("Motely", StringComparison.Ordinal) == true ? null : @default;

    [RenameMember]
    public static string? RenameMember(MemberInfo info, string @default)
    {
        if (info.DeclaringType == typeof(IMotelySearchSettings))
            return SealedSettings.Contains(info.Name) ? null : @default;
        if (info.DeclaringType == typeof(IMotelySearch))
            return info.Name == nameof(IMotelySearch.AwaitCompletion) ? null : @default;
        if (info.DeclaringType == typeof(MotelySingleSearchContext))
            return OpenSingleContext.Contains(info.Name) && HasNoByRefParams(info)
                ? @default
                : null;
        return @default;
    }

    // Whitelisting is by name; overloads that thread ref state (e.g. ref MotelyRunState)
    // can't cross the boundary and are dropped even when their name is open.
    private static bool HasNoByRefParams(MemberInfo info) =>
        info is not MethodInfo method
        || Array.TrueForAll(method.GetParameters(), static p => !p.ParameterType.IsByRef);
}
