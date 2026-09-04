using Bootsharp;
using Motely;
using Motely.Filters;
using Motely.Filters.Jaml;
using Motely.Filters.Native;
using Motely.SeedProviders;

/// <summary>
/// Search host. <c>Search.settings(jaml)</c> hands JS the engine's fluent settings as an interop
/// instance; chain <c>with*</c> calls, then <c>start(cancellationToken)</c>. Finds arrive on
/// <see cref="OnScored"/>, progress on <see cref="OnProgress"/>. Cancel through the token.
/// Jimmolate is the live context.
/// </summary>
public static partial class Search
{
    [Export]
    public static event Action<MotelyProgress>? OnProgress;

    [Export]
    public static event Action<MotelySeedScore>? OnScored;

    /// <summary>
    /// JS predicate. Gets the live <see cref="MotelySingleSearchContext"/> (specialization rail),
    /// not a seed string. Return score; 0 drops. Same contract as
    /// <see cref="MotelyIndividualSeedSearcher"/> / JimmolateFilterTests.
    /// </summary>
    [Import]
    public static partial int Jimmolate(MotelySingleSearchContext ctx);

    /// <summary>The engine's settings for this JAML, fluent. Default seed input is the sequential space.</summary>
    [Export]
    public static SearchSettings Settings(string jaml) =>
        new(JamlSearchBuilder.CreateSettings(JamlConfigLoader.FromJaml(jaml)));

    /// <summary>Passthrough filter + <see cref="Jimmolate"/> as the only predicate.</summary>
    [Export]
    public static SearchSettings JimmolateSettings() =>
        new(new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(new PassthroughFilterDesc())
            .WithJimmolate(static ctx => Jimmolate(ctx)));

    internal static void Progress(MotelyProgress p) => OnProgress?.Invoke(p);
    internal static void Scored(MotelySeedScore s) => OnScored?.Invoke(s);
}

/// <summary>
/// <see cref="IMotelySearchSettings"/> for JS. Callbacks are the two events on <see cref="Search"/>.
/// Thread count is always 1 in the browser. Cancel with the token passed to <see cref="Start"/>.
/// </summary>
public sealed class SearchSettings
{
    private readonly IMotelySearchSettings _settings;
    private IMotelySearch? _search;

    internal SearchSettings(IMotelySearchSettings settings)
    {
        // Browser WASM is single-threaded. Not a JS choice.
        _settings = settings
            .WithThreadCount(1)
            .WithQuietMode(true)
            .WithProgressCallback(Search.Progress)
            .WithScoredResultCallback(static t =>
                Search.Scored(new MotelySeedScore(t.Seed, t.Score, t.TallyValuesSpan.ToArray())))
            .WithSeedMatchCallback(static s => Search.Scored(new MotelySeedScore(s, 1, [])));
    }

    public SearchSettings WithBatchCharacterCount(int batchCharacterCount) { _settings.WithBatchCharacterCount(batchCharacterCount); return this; }
    public SearchSettings WithProviderBatchSeedCount(int seedCount) { _settings.WithProviderBatchSeedCount(seedCount); return this; }
    public SearchSettings WithStartBatchIndex(long startBatchIndex) { _settings.WithStartBatchIndex(startBatchIndex); return this; }
    public SearchSettings WithEndBatchIndex(long endBatchIndex) { _settings.WithEndBatchIndex(endBatchIndex); return this; }
    public SearchSettings WithSeedList(string[] seeds) { _settings.WithSeedList(seeds); return this; }
    public SearchSettings WithRandomSearch(int count) { _settings.WithRandomSearch(count); return this; }
    public SearchSettings WithKeywordSearch(string[] keywords, bool quickPad) { _settings.WithKeywordSearch(keywords, quickPad ? JamlAesthetics.QuickPaddingChars : null); return this; }
    public SearchSettings WithAestheticSearch(JamlAesthetic aesthetic, bool quickPad) { _settings.WithAestheticSearch(aesthetic, quickPad ? JamlAesthetics.QuickPaddingChars : null); return this; }
    public SearchSettings WithSequentialSearch() { _settings.WithSequentialSearch(); return this; }
    public SearchSettings WithDeck(MotelyDeck deck) { _settings.WithDeck(deck); return this; }
    public SearchSettings WithStake(MotelyStake stake) { _settings.WithStake(stake); return this; }
    public SearchSettings WithProgressReportIntervalMs(long intervalMs) { _settings.WithProgressReportIntervalMs(intervalMs); return this; }
    public SearchSettings WithAutoScoreCutoff(bool enabled) { _settings.WithAutoScoreCutoff(enabled); return this; }
    public SearchSettings StopAfter(long matchCount) { _settings.StopAfter(matchCount); return this; }

    /// <summary>Run to completion. Resolves when the space is exhausted, the match limit hits, or
    /// <paramref name="cancellationToken"/> fires. A cancelled run resolves too;
    /// <see cref="StoppedOnMatchLimit"/> and <see cref="TotalSeedsSearched"/> say what happened.</summary>
    public async Task Start(CancellationToken cancellationToken)
    {
        if (_search is not null && !_search.IsCompleted)
            throw new InvalidOperationException("This search is already running.");
        _search?.Dispose();
        _search = _settings.Start(cancellationToken);
        await _search.WaitForCompletionAsync();
    }

    public bool IsCompleted => _search?.IsCompleted ?? false;
    public bool StoppedOnMatchLimit => _search?.StoppedOnMatchLimit ?? false;
    public long TotalSeedsSearched => _search?.TotalSeedsSearched ?? 0;
    public long MatchingSeeds => _search?.MatchingSeeds ?? 0;
    public long ElapsedMs => _search?.ElapsedMs ?? 0;
    public double SeedsPerSecond => _search?.SeedsPerSecond ?? 0;
    public long TotalBatchCount => _search?.TotalBatchCount ?? 0;
    public long CompletedBatchCount => _search?.CompletedBatchCount ?? 0;
    public long ResumeBatchIndex => _search?.ResumeBatchIndex ?? -1;
    public MotelySeedScore[] Results => _search is null ? [] : [.. _search.Results];
}
