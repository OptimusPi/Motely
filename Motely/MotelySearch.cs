using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely;

public interface IMotelySeedFilter
{
    public VectorMask Filter(ref MotelyVectorSearchContext searchContext);
}

public interface IMotelySeedFilterDesc
{
    public IMotelySeedFilter CreateFilter(ref MotelyFilterCreationContext ctx);
}

public interface IMotelySeedFilterDesc<TFilter> : IMotelySeedFilterDesc
    where TFilter : struct, IMotelySeedFilter
{
    public new TFilter CreateFilter(ref MotelyFilterCreationContext ctx);

    IMotelySeedFilter IMotelySeedFilterDesc.CreateFilter(ref MotelyFilterCreationContext ctx)
    {
        return CreateFilter(ref ctx);
    }
}

public interface IMotelySeedScoreDesc
{
    public IMotelySeedScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx);
}

public interface IMotelySeedScoreDesc<TScoreProvider> : IMotelySeedScoreDesc
    where TScoreProvider : struct, IMotelySeedScoreProvider
{
    public new TScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx);

    IMotelySeedScoreProvider IMotelySeedScoreDesc.CreateScoreProvider(
        ref MotelyFilterCreationContext ctx
    )
    {
        return CreateScoreProvider(ref ctx);
    }
}

public interface IMotelySeedScores
{
    string Seed { get; }
    int Score { get; }
    byte[] Tally { get; }
}

public interface IMotelySeedScoreProvider
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VectorMask Score(
        ref MotelyVectorSearchContext searchContext,
        MotelySeedScoreTally[] buffer,
        VectorMask baseFilterMask,
        int scoreThreshold = 0
    );
}

public interface IMotelySeedRouter
{
    public void InjectSingleSeedContext(in MotelySingleSearchContext ctx);
}

public interface IMotelySeedRouterDesc
{
    public IMotelySeedRouter CreateSeedRouter(ref MotelyFilterCreationContext ctx);
}

public interface IMotelySeedRouterDesc<TProvider> : IMotelySeedRouterDesc
    where TProvider : struct, IMotelySeedRouter
{
    public new TProvider CreateSeedRouter(ref MotelyFilterCreationContext ctx);

    IMotelySeedRouter IMotelySeedRouterDesc.CreateSeedRouter(
        ref MotelyFilterCreationContext ctx
    )
    {
        return CreateSeedRouter(ref ctx);
    }
}


public enum MotelySearchMode
{
    Sequential,
    Provider,
}

public interface IMotelySeedProvider
{
    public long SeedCount { get; }
    public ReadOnlySpan<char> NextSeed();

    /// <summary>
    /// Batch retrieve multiple seeds in one lock operation - much faster for multi-threaded access.
    /// Fills the provided array with seed strings, returns the number of seeds actually retrieved.
    /// </summary>
    public int NextSeeds(string[] seeds);
}

public sealed class MotelyRandomSeedProvider(int seedCount) : IMotelySeedProvider
{
    public long SeedCount { get; } = seedCount;
    private int _seedsGenerated;

    public ReadOnlySpan<char> NextSeed()
    {
        if (Interlocked.Increment(ref _seedsGenerated) > SeedCount)
            return [];

        return string.Create(
            MotelyGlobals.MaxSeedLength,
            (object?)null,
            static (buf, _) => Random.Shared.GetItems(MotelyGlobals.SeedDigits, buf)
        );
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds is not { Length: > 0 })
            return 0;

        int filled = 0;
        for (int i = 0; i < seeds.Length; i++)
        {
            if (Interlocked.Increment(ref _seedsGenerated) > SeedCount)
                break;

            seeds[i] = string.Create(
                MotelyGlobals.MaxSeedLength,
                (object?)null,
                static (buf, _) => Random.Shared.GetItems(MotelyGlobals.SeedDigits, buf)
            );
            filled++;
        }
        return filled;
    }
}

/// <summary>
/// Generates palindrome seeds lazily via <see cref="JamlAesthetics.EnumerateSeeds"/>.
/// </summary>
public sealed class MotelyPalindromeSeedProvider : IMotelySeedProvider
{
    public long SeedCount { get; } = JamlAesthetics.GetSeedCount(JamlAesthetic.Palindrome);

    private readonly IEnumerator<string> _palindromeEnumerator;
    private readonly object _enumeratorLock = new();

    public MotelyPalindromeSeedProvider()
    {
        _palindromeEnumerator = JamlAesthetics
            .EnumerateSeeds(JamlAesthetic.Palindrome)
            .GetEnumerator();
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_palindromeEnumerator.MoveNext())
            {
                return _palindromeEnumerator.Current.AsSpan();
            }
            return ReadOnlySpan<char>.Empty;
        }
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return 0;

        lock (_enumeratorLock)
        {
            int count = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                if (!_palindromeEnumerator.MoveNext())
                    break;
                seeds[i] = _palindromeEnumerator.Current;
                count++;
            }
            return count;
        }
    }
}

/// <summary>
/// Generates psychosis seeds lazily via <see cref="JamlAesthetics.EnumerateSeeds"/> (echo pattern: ABAxBxxx, ~1 billion seeds).
/// </summary>
public sealed class MotelyPsychosisSeedProvider : IMotelySeedProvider
{
    public long SeedCount { get; } = JamlAesthetics.GetSeedCount(JamlAesthetic.Psychosis);

    private readonly IEnumerator<string> _psychosisEnumerator;
    private readonly object _enumeratorLock = new();

    public MotelyPsychosisSeedProvider()
    {
        _psychosisEnumerator = JamlAesthetics
            .EnumerateSeeds(JamlAesthetic.Psychosis)
            .GetEnumerator();
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_psychosisEnumerator.MoveNext())
            {
                return _psychosisEnumerator.Current.AsSpan();
            }
            return ReadOnlySpan<char>.Empty;
        }
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return 0;

        lock (_enumeratorLock)
        {
            int count = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                if (!_psychosisEnumerator.MoveNext())
                    break;
                seeds[i] = _psychosisEnumerator.Current;
                count++;
            }
            return count;
        }
    }
}

public sealed class MotelyAestheticSeedProvider : IMotelySeedProvider
{
    public long SeedCount { get; }

    private readonly IEnumerator<string> _enumerator;
    private readonly object _enumeratorLock = new();

    public MotelyAestheticSeedProvider(JamlAesthetic aesthetic)
    {
        SeedCount = JamlAesthetics.GetSeedCount(aesthetic);
        _enumerator = JamlAesthetics.EnumerateSeeds(aesthetic).GetEnumerator();
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_enumerator.MoveNext())
                return _enumerator.Current.AsSpan();
            return ReadOnlySpan<char>.Empty;
        }
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return 0;

        lock (_enumeratorLock)
        {
            int count = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                if (!_enumerator.MoveNext())
                    break;
                seeds[i] = _enumerator.Current;
                count++;
            }
            return count;
        }
    }
}

public sealed class MotelyKeywordSeedProvider : IMotelySeedProvider
{
    public long SeedCount { get; }

    private readonly IEnumerator<string> _enumerator;
    private readonly object _enumeratorLock = new();

    public MotelyKeywordSeedProvider(IEnumerable<string> keywords, char[]? paddingChars = null)
    {
        SeedCount = MotelyGlobals.GetPaddedSeedCountForKeywordsLong(keywords, paddingChars);
        _enumerator = MotelyGlobals.GeneratePaddedSeedsForKeywords(keywords, paddingChars).GetEnumerator();
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_enumerator.MoveNext())
            {
                return _enumerator.Current.AsSpan();
            }
            return ReadOnlySpan<char>.Empty;
        }
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return 0;

        lock (_enumeratorLock)
        {
            int count = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                if (!_enumerator.MoveNext())
                    break;
                seeds[i] = _enumerator.Current;
                count++;
            }
            return count;
        }
    }
}

public sealed class MotelySeedListProvider : IMotelySeedProvider
{
    // Keep seeds as enumerable - don't materialize! Seeds are used in the order provided.
    // For keyword generation, enumerable is lazy and avoids massive allocations.
    private readonly IEnumerator<string> _seedEnumerator;
    private string? _currentSeed;
    private long _seedIndex = -1;

    // Thread-safety: IEnumerator<T> is NOT thread-safe, so we need a lock
    // This is a lightweight lock for the hot path - contention should be minimal
    private readonly object _enumeratorLock = new();

    public long SeedCount { get; private set; } = -1; // Unknown for enumerables

    public MotelySeedListProvider(IEnumerable<string> seeds, long seedCount = -1)
    {
        // Don't materialize! Seeds are used in the order provided from generator/enumerator
        _seedEnumerator = seeds.GetEnumerator();
        SeedCount = ResolveSeedCount(seeds, seedCount);
    }

    private static long ResolveSeedCount(IEnumerable<string> seeds, long seedCount)
    {
        if (seedCount >= 0)
            return seedCount;

        if (seeds is ICollection<string> collection)
            return collection.Count;

        if (seeds is IReadOnlyCollection<string> readOnlyCollection)
            return readOnlyCollection.Count;

        if (seeds is System.Collections.ICollection nonGenericCollection)
            return nonGenericCollection.Count;

        return -1;
    }

    public ReadOnlySpan<char> NextSeed()
    {
        // Thread-safe access to enumerator - multiple threads may call this concurrently
        lock (_enumeratorLock)
        {
            _seedIndex++;
            if (_seedEnumerator.MoveNext())
            {
                _currentSeed = _seedEnumerator.Current;
                // Create a copy of the string to avoid issues if Current is modified
                // (though it shouldn't be, this is defensive)
                return _currentSeed.AsSpan();
            }
            return ReadOnlySpan<char>.Empty;
        }
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return 0;

        lock (_enumeratorLock)
        {
            int count = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                _seedIndex++;
                if (!_seedEnumerator.MoveNext())
                    break;

                _currentSeed = _seedEnumerator.Current;
                seeds[i] = _currentSeed; // Store string directly
                count++;
            }
            return count;
        }
    }

    public void Dispose()
    {
        lock (_enumeratorLock)
        {
            _seedEnumerator?.Dispose();
        }
    }
}

/// <summary>
/// Optional <see cref="IMotelySeedProvider"/> for <see cref="IAsyncEnumerable{T}"/> sources.
/// Pass to <see cref="MotelySearchSettings{TBaseFilter}.WithProviderSearch"/>; do not use unless you
/// truly need async streaming — prefer <see cref="MotelySeedListProvider"/> / <see cref="MotelySearchSettings{TBaseFilter}.WithListSearch"/>.
/// </summary>
public sealed class MotelyAsyncSeedListProvider : IMotelySeedProvider, IDisposable, IAsyncDisposable
{
    private readonly IAsyncEnumerable<string> _seeds;
    private readonly CancellationToken _cancellationToken;

    private IAsyncEnumerator<string>? _enumerator;
    private string? _currentSeed;
    private readonly object _enumeratorLock = new();
    private bool _disposed;

    public long SeedCount { get; }

    public MotelyAsyncSeedListProvider(
        IAsyncEnumerable<string> seeds,
        long seedCount = -1,
        CancellationToken cancellationToken = default
    )
    {
        _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
        SeedCount = seedCount;
        _cancellationToken = cancellationToken;
    }

    private IAsyncEnumerator<string> EnsureEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _enumerator ??= _seeds.GetAsyncEnumerator(_cancellationToken);
    }

    private static bool MoveNextSync(IAsyncEnumerator<string> enumerator)
    {
        return enumerator.MoveNextAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_disposed)
                return ReadOnlySpan<char>.Empty;

            var enumerator = EnsureEnumerator();
            if (!MoveNextSync(enumerator))
                return ReadOnlySpan<char>.Empty;

            _currentSeed = enumerator.Current;
            return _currentSeed.AsSpan();
        }
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds is not { Length: > 0 })
            return 0;

        lock (_enumeratorLock)
        {
            if (_disposed)
                return 0;

            var enumerator = EnsureEnumerator();
            int count = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                if (!MoveNextSync(enumerator))
                    break;
                seeds[i] = enumerator.Current;
                count++;
            }

            return count;
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        IAsyncEnumerator<string>? enumerator;
        lock (_enumeratorLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            enumerator = _enumerator;
            _enumerator = null;
        }

        if (enumerator != null)
            await enumerator.DisposeAsync().ConfigureAwait(false);
    }
}

public interface IMotelySearchSettings
{
    IMotelySeedFilterDesc BaseFilterDescBase { get; }
    IList<IMotelySeedFilterDesc>? AdditionalFilters { get; }
    IMotelySearchSettings WithAdditionalFilter(IMotelySeedFilterDesc filterDesc);
    IMotelySearchSettings WithThreadCount(int threadCount);
    IMotelySearchSettings WithBatchCharacterCount(int batchCharacterCount);
    IMotelySearchSettings WithStartBatchIndex(long startBatchIndex);
    IMotelySearchSettings WithEndBatchIndex(long endBatchIndex);
    IMotelySearchSettings WithSeedScoreProvider(IMotelySeedScoreDesc seedScoreDesc);
    IMotelySearchSettings WithSeedRouter(IMotelySeedRouterDesc desc);
    IMotelySearchSettings WithListSearch(IEnumerable<string> seeds, int seedCount = -1);
    IMotelySearchSettings WithRandomSearch(int count);
    IMotelySearchSettings WithAestheticSearch(JamlAesthetic aesthetic);
    IMotelySearchSettings WithProviderSearch(IMotelySeedProvider provider);
    IMotelySearchSettings WithSequentialSearch();
    IMotelySearchSettings WithDeck(MotelyDeck deck);
    IMotelySearchSettings WithStake(MotelyStake stake);
    IMotelySearchSettings WithProgressCallback(Action<MotelyProgress> callback);
    IMotelySearchSettings WithProgressReportIntervalMs(long intervalMs);
    IMotelySearchSettings WithCsvOutput(bool csvOutput);
    IMotelySearchSettings WithQuietMode(bool quietMode);
    IMotelySearchSettings WithSeedMatchCallback(Action<string> callback);
    IMotelySearchSettings WithScoredResultCallback(Action<MotelySeedScoreTally> callback);

    IMotelySearch CreateSearch();
    IMotelySearch Start(CancellationToken cancellationToken = default);
}

public sealed class MotelySearchSettings<TBaseFilter>(
    IMotelySeedFilterDesc<TBaseFilter> baseFilterDesc
) : IMotelySearchSettings
    where TBaseFilter : struct, IMotelySeedFilter
{
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
    public long StartBatchIndex { get; set; } = 0;
    public long EndBatchIndex { get; set; } = long.MaxValue;

    public IMotelySeedFilterDesc<TBaseFilter> BaseFilterDesc { get; set; } = baseFilterDesc;

    // Interface implementation
    IMotelySeedFilterDesc IMotelySearchSettings.BaseFilterDescBase => BaseFilterDesc;

    public IList<IMotelySeedFilterDesc>? AdditionalFilters { get; set; } = null;

    public IMotelySeedScoreDesc? SeedScoreDesc { get; set; } = null;

    public IMotelySeedRouterDesc? SeedRouterDesc { get; set; } = null;

    public MotelySearchMode Mode { get; set; }

    /// <summary>
    /// The object which provides seeds to search. Should only be non-null if
    /// `Mode` is set to `Provider`.
    /// </summary>
    public IMotelySeedProvider? SeedProvider { get; set; }

    /// <summary>
    /// The number of seed characters each batch contains.
    ///
    /// For example, with a value of 3 one batch would go through 35^3 seeds.
    /// Only meaningful when `Mode` is set to `Sequential`.
    /// </summary>
    public int SequentialBatchCharacterCount { get; set; } = 3;

    public MotelyDeck Deck { get; set; } = MotelyDeck.Red;
    public MotelyStake Stake { get; set; } = MotelyStake.White;

    public bool CsvOutput { get; set; } = false;
    public bool QuietMode { get; set; } = false;

    /// <summary>
    /// Callback for progress updates - useful for UI progress bars and logging
    /// Receives MotelyProgress object with all progress data
    /// </summary>
    public Action<MotelyProgress>? ProgressCallback { get; set; }

    public long ProgressReportIntervalMs { get; set; } = 800;

    /// <summary>
    /// Callback invoked when a seed matches all filters (no score provider).
    /// Consumers provide their own output handler (e.g. Console.WriteLine for CLI).
    /// </summary>
    public Action<string>? SeedMatchCallback { get; set; }

    /// <summary>
    /// Callback invoked for scored result rows so callers can persist structured results.
    /// </summary>
    public Action<MotelySeedScoreTally>? ScoredResultCallback { get; set; }

    public MotelySearchSettings<TBaseFilter> WithThreadCount(int threadCount)
    {
        ThreadCount = threadCount;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithStartBatchIndex(long startBatchIndex)
    {
        StartBatchIndex = startBatchIndex;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithEndBatchIndex(long endBatchIndex)
    {
        EndBatchIndex = endBatchIndex;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithBatchCharacterCount(int batchCharacterCount)
    {
        SequentialBatchCharacterCount = batchCharacterCount;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithListSearch(
        IEnumerable<string> seeds,
        int seedCount = -1
    )
    {
        return WithProviderSearch(new MotelySeedListProvider(seeds, seedCount));
    }

    public MotelySearchSettings<TBaseFilter> WithRandomSearch(int count)
    {
        return WithProviderSearch(new MotelyRandomSeedProvider(count));
    }

    public MotelySearchSettings<TBaseFilter> WithAestheticSearch(JamlAesthetic aesthetic)
    {
        return WithProviderSearch(new MotelyAestheticSeedProvider(aesthetic));
    }

    public MotelySearchSettings<TBaseFilter> WithProviderSearch(IMotelySeedProvider provider)
    {
        SeedProvider = provider;
        Mode = MotelySearchMode.Provider;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithSequentialSearch()
    {
        SeedProvider = null;
        Mode = MotelySearchMode.Sequential;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithAdditionalFilter(IMotelySeedFilterDesc filterDesc)
    {
        AdditionalFilters ??= [];
        AdditionalFilters.Add(filterDesc);
        return this;
    }

    // Interface implementation for chained calls
    IMotelySearchSettings IMotelySearchSettings.WithAdditionalFilter(
        IMotelySeedFilterDesc filterDesc
    )
    {
        return WithAdditionalFilter(filterDesc);
    }

    public MotelySearchSettings<TBaseFilter> WithSeedScoreProvider(
        IMotelySeedScoreDesc seedScoreDesc
    )
    {
        SeedScoreDesc = seedScoreDesc;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithSeedRouter(IMotelySeedRouterDesc desc)
    {
        SeedRouterDesc = desc;
        return this;
    }

    // Explicit interface implementations for chaining
    IMotelySearchSettings IMotelySearchSettings.WithThreadCount(int threadCount) =>
        WithThreadCount(threadCount);

    IMotelySearchSettings IMotelySearchSettings.WithBatchCharacterCount(int count) =>
        WithBatchCharacterCount(count);

    IMotelySearchSettings IMotelySearchSettings.WithStartBatchIndex(long index) =>
        WithStartBatchIndex(index);

    IMotelySearchSettings IMotelySearchSettings.WithEndBatchIndex(long index) =>
        WithEndBatchIndex(index);

    IMotelySearchSettings IMotelySearchSettings.WithSeedScoreProvider(IMotelySeedScoreDesc desc) =>
        WithSeedScoreProvider(desc);

    IMotelySearchSettings IMotelySearchSettings.WithSeedRouter(IMotelySeedRouterDesc desc) =>
        WithSeedRouter(desc);

    IMotelySearchSettings IMotelySearchSettings.WithListSearch(
        IEnumerable<string> seeds,
        int seedCount
    ) => WithListSearch(seeds, seedCount);

    IMotelySearchSettings IMotelySearchSettings.WithRandomSearch(int count) =>
        WithRandomSearch(count);

    IMotelySearchSettings IMotelySearchSettings.WithAestheticSearch(JamlAesthetic aesthetic) =>
        WithAestheticSearch(aesthetic);

    IMotelySearchSettings IMotelySearchSettings.WithProviderSearch(IMotelySeedProvider provider) =>
        WithProviderSearch(provider);

    IMotelySearchSettings IMotelySearchSettings.WithSequentialSearch() => WithSequentialSearch();

    IMotelySearchSettings IMotelySearchSettings.WithDeck(MotelyDeck deck) => WithDeck(deck);

    IMotelySearchSettings IMotelySearchSettings.WithStake(MotelyStake stake) => WithStake(stake);

    IMotelySearchSettings IMotelySearchSettings.WithProgressCallback(
        Action<MotelyProgress> callback
    ) => WithProgressCallback(callback);

    IMotelySearchSettings IMotelySearchSettings.WithProgressReportIntervalMs(long intervalMs) =>
        WithProgressReportIntervalMs(intervalMs);

    IMotelySearchSettings IMotelySearchSettings.WithCsvOutput(bool csvOutput) =>
        WithCsvOutput(csvOutput);

    IMotelySearchSettings IMotelySearchSettings.WithQuietMode(bool quietMode) =>
        WithQuietMode(quietMode);

    IMotelySearchSettings IMotelySearchSettings.WithSeedMatchCallback(Action<string> callback) =>
        WithSeedMatchCallback(callback);

    IMotelySearchSettings IMotelySearchSettings.WithScoredResultCallback(
        Action<MotelySeedScoreTally> callback
    ) => WithScoredResultCallback(callback);

    IMotelySearch IMotelySearchSettings.Start(CancellationToken cancellationToken) =>
        Start(cancellationToken);

    /// <inheritdoc cref="IMotelySearchSettings.CreateSearch" />
    public IMotelySearch CreateSearch() => new MotelySearch<TBaseFilter>(this);

    public MotelySearchSettings<TBaseFilter> WithDeck(MotelyDeck deck)
    {
        Deck = deck;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithStake(MotelyStake stake)
    {
        Stake = stake;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithProgressCallback(Action<MotelyProgress> callback)
    {
        ProgressCallback = callback;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithProgressReportIntervalMs(long intervalMs)
    {
        ProgressReportIntervalMs = Math.Max(0, intervalMs);
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithCsvOutput(bool csvOutput)
    {
        CsvOutput = csvOutput;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithQuietMode(bool quietMode)
    {
        QuietMode = quietMode;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithSeedMatchCallback(Action<string> callback)
    {
        SeedMatchCallback = callback;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithScoredResultCallback(
        Action<MotelySeedScoreTally> callback
    )
    {
        ScoredResultCallback = callback;
        return this;
    }

    public IMotelySearch Start(CancellationToken cancellationToken = default)
    {
        MotelySearch<TBaseFilter> search = new(this);

        return search.Start(cancellationToken);
    }
}

public interface IMotelySearch : IDisposable
{
    long ElapsedMs { get; }
    long TotalSeedsSearched { get; }
    long MatchingSeeds { get; }
    long FilteredSeeds { get; }
    bool IsCompleted { get; }
    bool IsSequentialBatchSearch { get; }
    long BatchIndex { get; }
    long CompletedBatchCount { get; }

    IMotelySearch Start(CancellationToken cancellationToken = default);
    Task RunSearchAsync(CancellationToken cancellationToken = default);
    void AwaitCompletion();
    Task WaitForCompletionAsync(CancellationToken cancellationToken = default);
    void Cancel();
}

internal unsafe interface IInternalMotelySearch : IMotelySearch
{
    internal int PseudoHashKeyLengthCount { get; }
    internal int* PseudoHashKeyLengths { get; }
}

public struct MotelySearchParameters
{
    public MotelyStake Stake;
    public MotelyDeck Deck;
}

public sealed unsafe class MotelySearch<TBaseFilter> : IInternalMotelySearch
    where TBaseFilter : struct, IMotelySeedFilter
{
    /// <summary>Shared lock for console output (replaces removed FancyConsole.ConsoleLock).</summary>
    internal static readonly object ConsoleLock = new();

    private readonly MotelySearchParameters _searchParameters;

    internal CancellationToken _cancellationToken = CancellationToken.None;
    private readonly TaskCompletionSource<bool> _completionSource = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private int _isDisposed;

    private readonly TBaseFilter _baseFilter;
    private readonly IMotelySeedFilter[] _additionalFilters;
    private readonly int _pseudoHashKeyLengthCount;
    private readonly bool _isProviderMode;

    private readonly IMotelySeedScoreProvider? _scoreProvider;
    private readonly IMotelySeedRouter? _seedRouter;

    // IInternalMotelySearch implementation
    int IInternalMotelySearch.PseudoHashKeyLengthCount => _pseudoHashKeyLengthCount;
    private readonly int* _pseudoHashKeyLengths;
    int* IInternalMotelySearch.PseudoHashKeyLengths => _pseudoHashKeyLengths;

    private readonly long _startBatchIndex;
    private readonly long _endBatchIndex;
    private long _batchIndex;
    private readonly MotelySearchPlan[] _plans;
    private readonly int _threadCount;

    public bool IsCompleted => _completionSource.Task.IsCompleted;
    public bool IsSequentialBatchSearch => !_isProviderMode;

    public long BatchIndex => _batchIndex;

    // Batches actually completed (aggregated from thread-local counters)
    public long CompletedBatchCount
    {
        get
        {
            long totalBatches = 0;
            for (int i = 0; i < _plans.Length; i++)
            {
                totalBatches += _plans[i].SnapshotBatchesCompleted();
            }

            // In provider mode, _startBatchIndex doesn't apply (batches aren't sequential)
            return _isProviderMode ? totalBatches : _startBatchIndex + totalBatches;
        }
    }

    public long TotalSeedsSearched
    {
        get
        {
            if (_isProviderMode)
            {
                long totalSeeds = 0;
                for (int i = 0; i < _plans.Length; i++)
                {
                    totalSeeds += _plans[i].SnapshotSeedsSearched();
                }
                return totalSeeds;
            }

            long totalBatches = 0;
            for (int i = 0; i < _plans.Length; i++)
            {
                totalBatches += _plans[i].SnapshotBatchesCompleted();
            }

            return totalBatches * _plans[0].SeedsPerBatch;
        }
    }
    public long MatchingSeeds
    {
        get
        {
            long totalSeeds = 0;
            for (int i = 0; i < _plans.Length; i++)
            {
                totalSeeds += _plans[i].SnapshotMatchingSeeds();
            }
            return totalSeeds;
        }
    }
    /// <summary>
    /// Seeds searched that did not match (base filter rejected, additional filter rejected,
    /// or below score cutoff). Equal to <see cref="TotalSeedsSearched"/> minus
    /// <see cref="MatchingSeeds"/>. WASM consumers rely on this being non-zero during a
    /// run to render rejection bars / throughput stats.
    /// </summary>
    public long FilteredSeeds
    {
        get
        {
            long total = TotalSeedsSearched;
            long matched = MatchingSeeds;
            long diff = total - matched;
            // Snapshots race with workers, so on rare reads matched can momentarily
            // exceed total by a handful of seeds. Clamp to 0 so consumers never see negatives.
            return diff > 0 ? diff : 0;
        }
    }

    public long ElapsedMs => _elapsedTime.ElapsedMilliseconds;
    public TimeSpan ElapsedTime => _elapsedTime.Elapsed;

    /// <summary>
    /// Tries to get the score provider if one was configured.
    /// </summary>
    public bool TryGetScoreProvider([NotNullWhen(true)] out IMotelySeedScoreProvider? scoreProvider)
    {
        scoreProvider = _scoreProvider;
        return scoreProvider != null;
    }

    /// <summary>
    /// Tries to get the single seed router if configured
    /// </summary>
    public bool TryGetSingleSeedRouter([NotNullWhen(true)] out IMotelySeedRouter? seedRouter)
    {
        seedRouter = _seedRouter;
        return seedRouter != null;
    }

    private readonly Action<MotelyProgress>? _progressCallback;
    private readonly Action<string>? _seedMatchCallback;
    private readonly Action<MotelySeedScoreTally>? _scoredResultCallback;
    private readonly long _progressReportIntervalMs;

    private readonly Stopwatch _elapsedTime = new();
    private long _lastProgressReportElapsedMs = -1;

    public MotelySearch(MotelySearchSettings<TBaseFilter> settings)
    {
        _isProviderMode = settings.Mode == MotelySearchMode.Provider;
        _searchParameters = new() { Deck = settings.Deck, Stake = settings.Stake };
        _progressCallback = settings.ProgressCallback;
        _progressReportIntervalMs = settings.ProgressReportIntervalMs;
        _seedMatchCallback = settings.SeedMatchCallback;
        _scoredResultCallback = settings.ScoredResultCallback;

        MotelyFilterCreationContext filterCreationContext = new(in _searchParameters)
        {
            IsAdditionalFilter = false,
            SeedMatchCallback = _seedMatchCallback,
        };

        _baseFilter = settings.BaseFilterDesc.CreateFilter(ref filterCreationContext);

        if (settings.AdditionalFilters == null)
        {
            _additionalFilters = [];
        }
        else
        {
            _additionalFilters = new IMotelySeedFilter[settings.AdditionalFilters.Count];
            for (int i = 0; i < _additionalFilters.Length; i++)
            {
                filterCreationContext.IsAdditionalFilter = true;
                _additionalFilters[i] = settings
                    .AdditionalFilters[i]
                    .CreateFilter(ref filterCreationContext);
            }
        }

        // Create the score provider if one was specified
        if (settings.SeedScoreDesc != null)
        {
            _scoreProvider = settings.SeedScoreDesc.CreateScoreProvider(ref filterCreationContext);
        }

        // Create the context provider if one was specified
        if (settings.SeedRouterDesc != null)
        {
            _seedRouter = settings.SeedRouterDesc.CreateSeedRouter(ref filterCreationContext);
        }

        _startBatchIndex = settings.StartBatchIndex;
        _endBatchIndex = settings.EndBatchIndex;

        // Initialize to one BEFORE start since ThreadMain increments BEFORE searching
        // StartBatchIndex is always >= 0 now (defaults to 0)
        _batchIndex = _startBatchIndex - 1;

        int[] pseudohashKeyLengths = [.. filterCreationContext.CachedPseudohashKeyLengths];
        _pseudoHashKeyLengthCount = pseudohashKeyLengths.Length;

        _pseudoHashKeyLengths = (int*)Marshal.AllocHGlobal(sizeof(int) * _pseudoHashKeyLengthCount);
        for (int i = 0; i < _pseudoHashKeyLengthCount; i++)
        {
            _pseudoHashKeyLengths[i] = pseudohashKeyLengths[i];
        }

        _threadCount = Math.Max(1, settings.ThreadCount);
        _plans = new MotelySearchPlan[_threadCount];
        for (int i = 0; i < _threadCount; i++)
        {
            _plans[i] = settings.Mode switch
            {
                MotelySearchMode.Sequential => new MotelySequentialSearchPlan(this, settings, i),
                MotelySearchMode.Provider => new MotelyProviderSearchPlan(this, settings, i),
                _ => throw new InvalidEnumArgumentException(nameof(settings.Mode)),
            };
        }
    }

    private void RunWorkerBody(MotelySearchPlan plan)
    {
        if (_isProviderMode)
        {
            RunProviderPlan(plan);
        }
        else
        {
            plan.ExecuteSequentialPlan();
        }
    }

    public void RunSearchUntilCompletion()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        _elapsedTime.Start();

        if (_threadCount == 1)
        {
            // Single-threaded: run directly on this thread
            RunWorkerBody(_plans[0]);
        }
        else
        {
            // Multi-threaded: launch real threads (maps to pthreads in NativeAOT-LLVM WASM)
            var threads = new Thread[_threadCount];
            for (int i = 0; i < _threadCount; i++)
            {
                int threadIdx = i;
                threads[i] = new Thread(() => RunWorkerBody(_plans[threadIdx]))
                {
                    Name = $"Motely Search Thread {threadIdx}",
                    IsBackground = true
                };
                threads[i].Start();
            }

            for (int i = 0; i < _threadCount; i++)
            {
                threads[i].Join();
            }
        }

        SignalSearchCompleted();
    }

    /// <summary>Single place to complete <see cref="_completionSource"/> after worker(s) finish (sync path).</summary>
    private void SignalSearchCompleted()
    {
        Thread.MemoryBarrier();
        bool completed =
            Volatile.Read(ref _isDisposed) == 0 && !_cancellationToken.IsCancellationRequested;
        _completionSource.TrySetResult(completed);
    }

    private void RunProviderPlan(MotelySearchPlan plan)
    {
        while (Volatile.Read(ref _isDisposed) == 0 && !_cancellationToken.IsCancellationRequested)
        {
            if (plan._providerExhausted)
            {
                break;
            }

            plan.SearchProviderBatch();
            if (plan._providerExhausted)
            {
                break;
            }

            plan._localBatchesCompleted++;

            // Report progress
            PrintReport();
        }

        // Force flush any remaining seeds in filter batches
        if (_additionalFilters.Length != 0 && plan._filterSeedBatches != null)
        {
            for (int i = 0; i < _additionalFilters.Length; i++)
            {
                var batch = &plan._filterSeedBatches[i];
                if (batch->SeedCount != 0)
                {
                    plan.SearchFilterBatch(i, batch);
                }
            }
        }
    }

    public Task RunSearchAsync(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        StartSearchThreads();
        return _completionSource.Task;
    }

    public IMotelySearch Start(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        _cancellationToken = cancellationToken;

        RunSearchUntilCompletion();
        return this;
    }

    /// <summary>
    /// Starts search threads without blocking the caller.
    /// Completion is signaled via <see cref="_completionSource"/>.
    /// </summary>
    private void StartSearchThreads()
    {
        // what the fuck - pifreak
        // //ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);

        _elapsedTime.Start();

        if (_threadCount == 1)
        {
            // No System.Threading.Thread: matches single-threaded WASM (no pthread) and mirrors
            // <see cref="RunSearchUntilCompletion"/> for ThreadCount == 1.
            RunWorkerBody(_plans[0]);


            // twhat the fuick is this shit guys?   -pifreak :(
            SignalSearchCompleted();
        }
        else
        {
            int remaining = _threadCount;
            for (int i = 0; i < _threadCount; i++)
            {
                int threadIdx = i;
                var thread = new Thread(() =>
                {
                    RunWorkerBody(_plans[threadIdx]);
                    if (Interlocked.Decrement(ref remaining) == 0)
                    {
                        Thread.MemoryBarrier();
                        bool completed =
                            Volatile.Read(ref _isDisposed) == 0 && !_cancellationToken.IsCancellationRequested;
                        _completionSource.TrySetResult(completed);
                    }
                })
                {
                    Name = $"Motely Search Thread {threadIdx}",
                    IsBackground = true
                };
                thread.Start();
            }
        }
    }

    public void Cancel()
    {
        Interlocked.Exchange(ref _isDisposed, 1);
        _completionSource.TrySetResult(false);
    }

    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        return _completionSource.Task.WaitAsync(cancellationToken);
    }

    public void AwaitCompletion()
    {
        _completionSource.Task.GetAwaiter().GetResult();
    }

    /// <summary>After each batch: fire the progress callback with <see cref="MotelyProgress"/> (callers format strings).</summary>
    private void PrintReport()
    {
        if (_progressCallback == null)
            return;

        long elapsedMS = _elapsedTime.ElapsedMilliseconds;
        if (_progressReportIntervalMs > 0
            && _lastProgressReportElapsedMs >= 0
            && elapsedMS - _lastProgressReportElapsedMs < _progressReportIntervalMs)
            return;
        _lastProgressReportElapsedMs = elapsedMS;

        long thisCompletedCount = CompletedBatchCount;
        long totalBatches = _plans[0].MaxBatch;
        long seedsSearched = TotalSeedsSearched;

        double percentComplete;
        double totalPortionFinished;
        double thisPortionFinished;

        if (_isProviderMode && _plans[0] is MotelyProviderSearchPlan providerPlan)
        {
            long totalSeeds = providerPlan.SeedProvider.SeedCount;
            totalPortionFinished = totalSeeds > 0 ? (double)seedsSearched / totalSeeds : 0;
            percentComplete = totalPortionFinished * 100.0;
            thisPortionFinished = totalPortionFinished;
        }
        else
        {
            long batchesSinceStart = thisCompletedCount - _startBatchIndex;
            long totalBatchesToDo = _plans[0].MaxBatch - _startBatchIndex;
            totalPortionFinished = totalBatches > 0 ? (double)thisCompletedCount / totalBatches : 0;
            percentComplete = totalPortionFinished * 100.0;
            thisPortionFinished =
                totalBatchesToDo > 0 ? (double)batchesSinceStart / totalBatchesToDo : 0.0;
        }

        double seedsPerMs = elapsedMS > 1 ? (double)seedsSearched / elapsedMS : 0;

        long? etaMs = null;
        if (thisPortionFinished >= 0.0001)
        {
            double totalTimeEstimate = elapsedMS / thisPortionFinished;
            double timeLeftMs = totalTimeEstimate - elapsedMS;
            if (
                !double.IsNaN(timeLeftMs)
                && !double.IsInfinity(timeLeftMs)
                && timeLeftMs >= 0
                && timeLeftMs <= 30.0 * 24 * 60 * 60 * 1000
            )
                etaMs = (long)timeLeftMs;
        }

        var progress = new MotelyProgress
        {
            CompletedBatchCount = thisCompletedCount,
            TotalBatchCount = totalBatches,
            SeedsSearched = seedsSearched,
            MatchingSeeds = MatchingSeeds,
            SeedsPerMillisecond = seedsPerMs,
            PercentComplete = percentComplete,
            ElapsedMilliseconds = elapsedMS,
            EstimatedTimeRemainingMilliseconds = etaMs,
        };
        _progressCallback(progress);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        for (int i = 0; i < _plans.Length; i++)
            _plans[i].Dispose();
        Marshal.FreeHGlobal((nint)_pseudoHashKeyLengths);
        _completionSource.TrySetResult(false);

        GC.SuppressFinalize(this);
    }

    ~MotelySearch()
    {
        if (Volatile.Read(ref _isDisposed) == 0)
        {
            Dispose();
        }
    }

    private abstract class MotelySearchPlan : IDisposable
    {
        public const int MAX_SEED_WAIT_MS = 200;

        public readonly MotelySearch<TBaseFilter> Search;
        public readonly int ThreadIndex;

        public long MaxBatch { get; internal set; }
        public long SeedsPerBatch { get; internal set; }

        // ========== THREAD-LOCAL PERFORMANCE ARCHITECTURE ==========
        // PATTERN: Thread-local accumulate → Batch-boundary pull/clear → Global aggregate
        // This eliminates hot-path Interlocked operations and I/O bottlenecks

        // Thread-local counters - NO Interlocked in hot path!
        // Each thread owns these counters; reporting aggregates snapshots.
        internal long _localMatchingSeeds = 0;
        internal long _localBatchesCompleted = 0;
        internal long _localSeedsSearched = 0;

        // Pre-allocated result buffer - ONE allocation per thread, reused forever
        // Old stale data is fine - mask controls which slots are valid
        protected readonly MotelySeedScoreTally[] _resultBuffer = new MotelySeedScoreTally[
            MotelyGlobals.MaxVectorWidth
        ];

        [InlineArray(MotelyGlobals.MaxSeedLength)]
        internal struct FilterSeedBatchCharacters
        {
            public Vector512<double> Character;
        }

        internal struct FilterSeedBatch
        {
            public FilterSeedBatchCharacters SeedCharacters;
            public Vector512<double>* SeedHashes;
            public PartialSeedHashCache SeedHashCache;
            public int SeedLength;
            public int SeedCount;
            public long WaitStartMS;
        }

        internal readonly FilterSeedBatch* _filterSeedBatches;

        // Provider-mode: this thread has exhausted the seed provider and should idle.
        internal bool _providerExhausted;

        public MotelySearchPlan(MotelySearch<TBaseFilter> search, int threadIndex)
        {
            Search = search;
            ThreadIndex = threadIndex;

            if (search._additionalFilters.Length != 0)
            {
                _filterSeedBatches = (FilterSeedBatch*)
                    Marshal.AllocHGlobal(
                        sizeof(FilterSeedBatch) * search._additionalFilters.Length
                    );

                int allocatedCount = 0;
                try
                {
                    for (int i = 0; i < search._additionalFilters.Length; i++)
                    {
                        FilterSeedBatch* batch = &_filterSeedBatches[i];

                        *batch = new()
                        {
                            SeedHashes = (Vector512<double>*)
                                Marshal.AllocHGlobal(
                                    sizeof(Vector512<double>)
                                        * MotelyGlobals.MaxCachedPseudoHashKeyLength
                                ),
                        };
                        allocatedCount = i + 1; // Track successful allocations

                        batch->SeedHashCache = new(search, batch->SeedHashes);
                    }
                }
                catch
                {
                    // Clean up any allocated memory on exception
                    for (int i = 0; i < allocatedCount; i++)
                    {
                        if (_filterSeedBatches[i].SeedHashes != null)
                        {
                            Marshal.FreeHGlobal((nint)_filterSeedBatches[i].SeedHashes);
                        }
                    }
                    Marshal.FreeHGlobal((nint)_filterSeedBatches);
                    _filterSeedBatches = null;
                    throw;
                }
            }
        }

        internal void ExecuteSequentialPlan()
        {
            while (Volatile.Read(ref Search._isDisposed) == 0)
            {
                if (Search._cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                long batchIdx = Interlocked.Increment(ref Search._batchIndex);

                if (batchIdx >= Search._endBatchIndex || batchIdx >= MaxBatch)
                {
                    break;
                }

                SearchSequentialBatch(batchIdx);

                _localBatchesCompleted++;

                // Check for timed-out filter batches
                if (Search._additionalFilters.Length != 0)
                {
                    for (int i = 0; i < Search._additionalFilters.Length; i++)
                    {
                        FilterSeedBatch* batch = &_filterSeedBatches[i];

                        if (batch->SeedCount != 0)
                        {

                            if (Search._elapsedTime.ElapsedMilliseconds - batch->WaitStartMS >= MAX_SEED_WAIT_MS)
                            {
                                SearchFilterBatch(i, batch);
                                Debug.Assert(
                                    batch->SeedCount == 0,
                                    "Batch should be reset after SearchFilterBatch"
                                );
                            }
                        }
                    }
                }

                // Report progress
                Search.PrintReport();
            }

            // Force flush any remaining seeds in filter batches
            if (Search._additionalFilters.Length != 0 && _filterSeedBatches != null)
            {
                for (int i = 0; i < Search._additionalFilters.Length; i++)
                {
                    FilterSeedBatch* batch = &_filterSeedBatches[i];
                    if (batch->SeedCount != 0)
                    {
                        SearchFilterBatch(i, batch);
                    }
                }
            }

        }

        internal abstract void SearchProviderBatch();
        internal abstract void SearchSequentialBatch(long batchIdx);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long SnapshotMatchingSeeds() => Volatile.Read(ref _localMatchingSeeds);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long SnapshotBatchesCompleted() => Volatile.Read(ref _localBatchesCompleted);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal long SnapshotSeedsSearched() => Volatile.Read(ref _localSeedsSearched);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SearchSeeds(in MotelySearchContextParams searchContextParams)
        {
            // This is the method for searching the base filter, we should not be searching additional filters
            Debug.Assert(!searchContextParams.IsAdditionalFilter);

            MotelyVectorSearchContext searchContext = new(
                in Search._searchParameters,
                in searchContextParams
            );

            VectorMask searchResultMask = Search._baseFilter.Filter(ref searchContext);

            searchContextParams.SeedHashCache->Reset();
            if (searchResultMask.IsPartiallyTrue())
            {
                if (Search._additionalFilters.Length == 0)
                {
                    ReportSeeds(searchResultMask, in searchContextParams);
                }
                else
                {
                    BatchSeeds(0, searchResultMask, in searchContextParams);
                }
            }
        }

        // Extracts the actual seed characters from a search context and reports that seed
        private void ReportSeeds(
            VectorMask searchResultMask,
            in MotelySearchContextParams searchParams
        )
        {
            Debug.Assert(
                searchResultMask.IsPartiallyTrue(),
                "Mask should be checked for partial truth before calling report seeds (for performance)."
            );

            // If we have a score provider, ALWAYS use it (it handles scoring, cutoff AND printing)
            // Previously this was gated by _csvOutput, which caused normal runs to bypass scoring
            // and fall back to ReportBasicSeeds (printing every raw seed).
            if (Search.TryGetScoreProvider(out var scoreProvider))
            {
                // Create search context for scoring
                MotelyVectorSearchContext searchContext = new(
                    in Search._searchParameters,
                    in searchParams
                );

                // Call the score provider with the mask of seeds that passed filters
                // The score provider will handle scoring and calling the callback
                VectorMask scoredMask = scoreProvider.Score(
                    ref searchContext,
                    _resultBuffer,
                    searchResultMask
                );

                // Report the scored results!
                ReportScoredResults(scoredMask, in searchParams);
            }
            else if (Search._seedRouter != null)
            {
                for (int lane = 0; lane < MotelyGlobals.MaxVectorWidth; lane++)
                {
                    if (searchParams.IsLaneValid(lane))
                    {
                        MotelySingleSearchContext singleCtx = new(
                            in Search._searchParameters,
                            in searchParams,
                            lane
                        );
                        Search._seedRouter.InjectSingleSeedContext(in singleCtx);
                    }
                }
            }
            else
            {
                // No score provider - report basic seeds
                ReportBasicSeeds(searchResultMask, in searchParams);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReportScoredResults(
            VectorMask resultMask,
            in MotelySearchContextParams searchParams
        )
        {
            // Do NOT write to console here - the callback already handled output!
            // The score provider invokes the callback which writes to Console.
            // This method ONLY updates counters for statistics tracking.
            // The callback flow is:
            //   1. scoreProvider.Score() -> invokes callback -> Console.WriteLine (FIRST OUTPUT)
            //   2. ReportScoredResults() -> ONLY increment counter (NO OUTPUT)

            for (int lane = 0; lane < MotelyGlobals.MaxVectorWidth; lane++)
            {
                if (resultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    Search._scoredResultCallback?.Invoke(_resultBuffer[lane]);
                    _localMatchingSeeds++;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReportBasicSeeds(
            VectorMask searchResultMask,
            in MotelySearchContextParams searchParams
        )
        {
            char* seed = stackalloc char[MotelyGlobals.MaxSeedLength];

            for (int lane = 0; lane < MotelyGlobals.MaxVectorWidth; lane++)
            {
                if (searchResultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    int length = searchParams.GetSeed(lane, seed);

                    // Increment thread-local counter
                    _localMatchingSeeds++;

                    string seedStr = new Span<char>(seed, length).ToString();
                    Search._seedMatchCallback?.Invoke(seedStr);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BatchSeeds(
            int filterIndex,
            VectorMask searchResultMask,
            in MotelySearchContextParams searchParams
        )
        {
            Debug.Assert(
                _filterSeedBatches != null
                    && Search._additionalFilters != null
                    && filterIndex >= 0
                    && filterIndex < Search._additionalFilters.Length,
                $"Invalid filterIndex {filterIndex}, _additionalFilters={(Search._additionalFilters == null ? "NULL" : $"Length={Search._additionalFilters.Length}")}"
            );

            Debug.Assert(searchParams.SeedHashCache != null, "SeedHashCache is null");

            FilterSeedBatch* filterBatch = &_filterSeedBatches[filterIndex];

            Debug.Assert(
                filterBatch->SeedHashes != null,
                $"filterBatch->SeedHashes is null for filterIndex {filterIndex}"
            );

            Debug.Assert(
                searchResultMask.IsPartiallyTrue(),
                "Mask should be checked for partial truth before calling enqueue seeds (for performance)."
            );

            for (int lane = 0; lane < Vector512<double>.Count; lane++)
            {
                if (searchResultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    int seedBatchIndex = filterBatch->SeedCount;

                    if (seedBatchIndex == 0)
                    {
                        filterBatch->SeedLength = searchParams.SeedLength;
                    }
                    else
                    {
                        // Each batch can only contain seeds of the same length, we should check if this seed can go into the batch
                        if (filterBatch->SeedLength != searchParams.SeedLength)
                        {
                            // This seed is a different length to the ones already in the batch :c
                            // Let's flush the batch and start again.
                            SearchFilterBatch(filterIndex, filterBatch);

                            Debug.Assert(
                                filterBatch->SeedCount == 0,
                                "Searching the batch should have reset it."
                            );
                            seedBatchIndex = 0;

                            filterBatch->SeedLength = searchParams.SeedLength;
                        }
                        // else: Same length - seedBatchIndex already equals current SeedCount, ready to use
                    }

                    ++filterBatch->SeedCount;

                    // Store the seed digits
                    {
                        int i = 0;
                        for (; i < searchParams.SeedLastCharactersLength; i++)
                        {
                            ((double*)&filterBatch->SeedCharacters)[
                                i * Vector512<double>.Count + seedBatchIndex
                            ] = ((double*)searchParams.SeedLastCharacters)[
                                i * Vector512<double>.Count + lane
                            ];
                        }

                        for (
                            int firstCharIndex = 0;
                            firstCharIndex < searchParams.SeedFirstCharactersLength;
                            firstCharIndex++
                        )
                        {
                            ((double*)&filterBatch->SeedCharacters)[
                                (searchParams.SeedLastCharactersLength + firstCharIndex)
                                    * Vector512<double>.Count
                                    + seedBatchIndex
                            ] = searchParams.SeedFirstCharacters[firstCharIndex];
                        }
                    }

                    // Store the cached hashes
                    // The cache structure: Cache[partialHashLength] already points to the correct Vector512<double>*
                    // for that key length. We just need to copy lane 'lane' from source to lane 'seedBatchIndex' in target.
                    if (
                        Search._pseudoHashKeyLengths == null
                        || Search._pseudoHashKeyLengthCount <= 0
                    )
                        return;

                    for (int i = 0; i < Search._pseudoHashKeyLengthCount; i++)
                    {
                        int partialHashLength = Search._pseudoHashKeyLengths[i];

                        // Ensure cache entry exists before accessing
                        if (searchParams.SeedHashCache == null)
                            continue;

                        if (partialHashLength >= MotelyGlobals.MaxCachedPseudoHashKeyLength)
                            Console.WriteLine($"partialHashLength {partialHashLength} >= MotelyGlobals.MaxCachedPseudoHashKeyLength {MotelyGlobals.MaxCachedPseudoHashKeyLength}");

                        if (searchParams.SeedHashCache->Cache[partialHashLength] == null)
                            continue;

                        // Cache[partialHashLength] already points to the correct Vector512<double>*
                        // for this key length (set up by PartialSeedHashCache ctor:
                        //     InitialCache[keyLength] = &partialSeedHashes[i]
                        // ). So the source vector is one Vector512 and we just need lane `lane`.
                        // Using `[i * Vector512<double>.Count + lane]` here reads `_hashes[2*i]`
                        // — correct only when i == 0, wrong vector or OOB for i >= 1 — which
                        // silently drops seeds from multi-keylength additional-filter chains.
                        // Regression covered by Tacodiva/Motely#5 and
                        // Motely.Tests/ChainedMustClauseSeedTests.cs.
                        double sourceValue = (
                            (double*)searchParams.SeedHashCache->Cache[partialHashLength]
                        )[lane];

                        // Write to target: filterBatch->SeedHashes is an array of Vector512<double>
                        ((double*)filterBatch->SeedHashes)[
                            i * Vector512<double>.Count + seedBatchIndex
                        ] = sourceValue;
                    }

                    if (seedBatchIndex == Vector512<double>.Count - 1)
                    {
                        // The queue if full of seeds! We can run the search
                        SearchFilterBatch(filterIndex, filterBatch);
                    }
                }
            }
        }

        // Searches a batch with a filter then resets that batch
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SearchFilterBatch(int filterIndex, FilterSeedBatch* filterBatch)
        {
            Debug.Assert(filterBatch->SeedCount != 0);

            // Zero unused lanes so IsLaneValid returns false for lane >= SeedCount.
            // Without this, stale data from the previous batch makes those lanes appear
            // valid, causing the same seeds to be re-processed and reported as duplicates.
            //
            // We also zero the cached partial-hash vectors for those padding lanes: BatchSeeds
            // writes only to lane = seedBatchIndex, so any lane >= SeedCount retains a stale
            // hash from the last partial batch that touched this slot. Filter code that reads
            // the hash cache across all 8 SIMD lanes (voucher / tarot / planet / tag / spectral
            // resample loops) will otherwise produce garbage PRNG output for padding lanes and
            // can spin forever trying to reroll them into a legal value. See Tacodiva/Motely#5
            // for the source-side handoff bug; this is the destination-side hygiene pass.
            int count = filterBatch->SeedCount;
            if (count < MotelyGlobals.MaxVectorWidth)
            {
                double* chars = (double*)&filterBatch->SeedCharacters;
                for (int lane = count; lane < MotelyGlobals.MaxVectorWidth; lane++)
                {
                    chars[lane] = 0; // First char of each lane; enough for IsLaneValid
                }

                double* hashes = (double*)filterBatch->SeedHashes;
                int keyCount = Search._pseudoHashKeyLengthCount;
                for (int i = 0; i < keyCount; i++)
                {
                    for (int lane = count; lane < MotelyGlobals.MaxVectorWidth; lane++)
                    {
                        hashes[i * Vector512<double>.Count + lane] = 0;
                    }
                }
            }

            MotelySearchContextParams searchParams = new(
                &filterBatch->SeedHashCache,
                filterBatch->SeedLength,
                0,
                null,
                (Vector512<double>*)&filterBatch->SeedCharacters,
                isAdditionalFilter: true
            );

            MotelyVectorSearchContext searchContext = new(
                in Search._searchParameters,
                in searchParams
            );

            VectorMask searchResultMask = Search
                ._additionalFilters[filterIndex]
                .Filter(ref searchContext);

            if (searchResultMask.IsPartiallyTrue())
            {
                int nextFilterIndex = filterIndex + 1;

                if (nextFilterIndex == Search._additionalFilters.Length)
                {
                    // If this was the last filter, we can report the seeds
                    ReportSeeds(searchResultMask, in searchParams);
                }
                else
                {
                    Debug.Assert(
                        nextFilterIndex < Search._additionalFilters.Length && nextFilterIndex >= 0,
                        $"nextFilterIndex {nextFilterIndex} >= _additionalFilters.Length {Search._additionalFilters.Length} or nextFilterIndex < 0   "
                    );
                    BatchSeeds(nextFilterIndex, searchResultMask, in searchParams);
                }
            }

            filterBatch->SeedCount = 0;
        }

        public void Dispose()
        {
            // FIX: Check if _filterSeedBatches is not null before freeing
            if (_filterSeedBatches != null)
            {
                for (int i = 0; i < Search._additionalFilters.Length; i++)
                {
                    _filterSeedBatches[i].SeedHashCache.Dispose();
                    if (_filterSeedBatches[i].SeedHashes != null)
                    {
                        Marshal.FreeHGlobal((nint)_filterSeedBatches[i].SeedHashes);
                    }
                }

                Marshal.FreeHGlobal((nint)_filterSeedBatches);
            }
        }
    }

    private sealed unsafe class MotelyProviderSearchPlan : MotelySearchPlan
    {
        public readonly IMotelySeedProvider SeedProvider;

        private readonly Vector512<double>* _hashes;
        private readonly PartialSeedHashCache* _hashCache;

        private readonly Vector512<double>* _seedCharacterMatrix;

        // Thread-local seed batch buffer to avoid allocations per batch
        private readonly string[] _seedBatchBuffer = new string[MotelyGlobals.MaxVectorWidth];

        public MotelyProviderSearchPlan(
            MotelySearch<TBaseFilter> search,
            MotelySearchSettings<TBaseFilter> settings,
            int index
        )
            : base(search, index)
        {
            if (settings.SeedProvider == null)
                throw new ArgumentException(
                    "Cannot create a provider search without a seed provider."
                );

            SeedProvider = settings.SeedProvider;

            // Calculate MaxBatch - handle unknown seed count (-1) by using a large estimate
            // This is only used for progress reporting, not actual batch termination
            long seedCount = SeedProvider.SeedCount;
            MaxBatch = seedCount >= 0
                ? (seedCount + (long)(MotelyGlobals.MaxVectorWidth - 1))
                    / (long)MotelyGlobals.MaxVectorWidth
                : long.MaxValue / MotelyGlobals.MaxVectorWidth; // Large estimate for unknown count
            SeedsPerBatch = (long)MotelyGlobals.MaxVectorWidth;

            _hashes = (Vector512<double>*)
                Marshal.AllocHGlobal(sizeof(Vector512<double>) * search._pseudoHashKeyLengthCount);

            _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
            *_hashCache = new PartialSeedHashCache(search, _hashes);

            _seedCharacterMatrix = (Vector512<double>*)
                Marshal.AllocHGlobal(sizeof(Vector512<double>) * MotelyGlobals.MaxSeedLength);
        }

        internal override void SearchProviderBatch()
        {
            // Batch retrieve seeds in one lock operation - much faster!
            // Use thread-local buffer to avoid allocations per batch
            int actualSeedCount = SeedProvider.NextSeeds(_seedBatchBuffer);

            // If we got no seeds at all, this thread is exhausted
            if (actualSeedCount == 0)
            {
                // Mark this thread exhausted so ThreadMain idles it (and we only decrement once)
                _providerExhausted = true;

                return;
            }

            // The length of all the seeds
            int* seedLengths = stackalloc int[MotelyGlobals.MaxVectorWidth];

            // Are all the seeds the same length?
            bool homogeneousSeedLength = true;

            // Process the batched seeds
            for (int seedIdx = 0; seedIdx < actualSeedCount; seedIdx++)
            {
                ReadOnlySpan<char> seed = _seedBatchBuffer[seedIdx].AsSpan();

                if (
                    seed.IsEmpty
                    || seed.Length > MotelyGlobals.MaxSeedLength
                    || seed.IndexOf('0') >= 0
                )
                {
                    // Invalid seed - skip it
                    continue;
                }

                // Bounds check for seedLengths array
                if (seedIdx < MotelyGlobals.MaxVectorWidth)
                {
                    seedLengths[seedIdx] = seed.Length;

                    if (seedIdx > 0 && seedLengths[0] != seed.Length)
                        homogeneousSeedLength = false;
                }

                // Bounds check for seed length and matrix access
                int seedLen = Math.Min(seed.Length, MotelyGlobals.MaxSeedLength);
                for (int i = 0; i < seedLen; i++)
                {
                    int matrixIndex = i * MotelyGlobals.MaxVectorWidth + seedIdx;
                    if (
                        matrixIndex >= 0
                        && matrixIndex < MotelyGlobals.MaxSeedLength * MotelyGlobals.MaxVectorWidth
                    )
                    {
                        ((double*)_seedCharacterMatrix)[matrixIndex] = seed[i];
                    }
                }
                for (int i = seedLen; i < MotelyGlobals.MaxSeedLength; i++)
                {
                    int matrixIndex = i * MotelyGlobals.MaxVectorWidth + seedIdx;
                    if (
                        matrixIndex >= 0
                        && matrixIndex < MotelyGlobals.MaxSeedLength * MotelyGlobals.MaxVectorWidth
                    )
                    {
                        ((double*)_seedCharacterMatrix)[matrixIndex] = 0;
                    }
                }
            }
            if (actualSeedCount < MotelyGlobals.MaxVectorWidth)
            {
                for (int lane = actualSeedCount; lane < MotelyGlobals.MaxVectorWidth; lane++)
                {
                    for (int i = 0; i < MotelyGlobals.MaxSeedLength; i++)
                    {
                        ((double*)_seedCharacterMatrix)[i * MotelyGlobals.MaxVectorWidth + lane] = 0;
                    }
                }
            }

            if (actualSeedCount > 0)
            {
                _localSeedsSearched += actualSeedCount;
            }

            if (homogeneousSeedLength)
            {
                // If all the seeds are the same length, we can be fast and vectorize!
                int seedLength = seedLengths[0];

                // Calculate the partial psuedohash cache
                for (
                    int pseudohashKeyIdx = 0;
                    pseudohashKeyIdx < Search._pseudoHashKeyLengthCount;
                    pseudohashKeyIdx++
                )
                {
                    int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                    Vector512<double> numVector = Vector512<double>.One;

                    for (int i = seedLength - 1; i >= 0; i--)
                    {
                        numVector = Vector512.Divide(Vector512.Create(1.1239285023), numVector);

                        numVector = Vector512.Multiply(numVector, _seedCharacterMatrix[i]);

                        numVector = Vector512.Multiply(numVector, Math.PI);
                        numVector = Vector512.Add(
                            numVector,
                            Vector512.Create((i + pseudohashKeyLength + 1) * Math.PI)
                        );

                        Vector512<double> intPart = Vector512.Floor(numVector);
                        numVector = Vector512.Subtract(numVector, intPart);
                    }

                    _hashes[pseudohashKeyIdx] = numVector;
                }

                SearchSeeds(
                    new MotelySearchContextParams(
                        _hashCache,
                        seedLength,
                        0,
                        null,
                        _seedCharacterMatrix
                    )
                );
            }
            else
            {
                // Otherwise, we need to search all the seeds individually
                Span<char> seed = stackalloc char[MotelyGlobals.MaxSeedLength];

                for (int i = 0; i < actualSeedCount; i++)
                {
                    int seedLength = seedLengths[i];

                    for (int j = 0; j < seedLength; j++)
                    {
                        seed[j] = (char)
                            ((double*)_seedCharacterMatrix)[j * MotelyGlobals.MaxVectorWidth + i];
                    }

                    SearchSingleSeed(seed[..seedLength]);
                }
            }
        }

        internal override void SearchSequentialBatch(long batchIdx)
        {
            throw new InvalidOperationException(
                $"{nameof(MotelyProviderSearchPlan)} does not support sequential batch search."
            );
        }

        private void SearchSingleSeed(ReadOnlySpan<char> seed)
        {
            // Skip empty seeds (indicates we've run out of seeds in the list)
            if (seed.IsEmpty)
                return;

            char* seedLastCharacters = stackalloc char[MotelyGlobals.MaxSeedLength - 1];

            // Calculate the partial psuedohash cache
            for (
                int pseudohashKeyIdx = 0;
                pseudohashKeyIdx < Search._pseudoHashKeyLengthCount;
                pseudohashKeyIdx++
            )
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                double num = 1;

                for (int i = seed.Length - 1; i >= 0; i--)
                {
                    num =
                        (
                            1.1239285023 / num * seed[i] * Math.PI
                            + (i + pseudohashKeyLength + 1) * Math.PI
                        ) % 1;
                }

                _hashes[pseudohashKeyIdx] = Vector512.Create(num);
            }

            for (int i = 0; i < seed.Length - 1; i++)
            {
                seedLastCharacters[i] = seed[i + 1];
            }

            Vector512<double> firstCharacterVector = Vector512.CreateScalar((double)seed[0]);

            SearchSeeds(
                new MotelySearchContextParams(
                    _hashCache,
                    seed.Length,
                    seed.Length - 1,
                    seedLastCharacters,
                    &firstCharacterVector
                )
            );
        }

        public new void Dispose()
        {
            base.Dispose();

            _hashCache->Dispose();
            Marshal.FreeHGlobal((nint)_hashCache);

            Marshal.FreeHGlobal((nint)_hashes);
            Marshal.FreeHGlobal((nint)_seedCharacterMatrix);
        }
    }

    private sealed unsafe class MotelySequentialSearchPlan : MotelySearchPlan
    {
        // A cache of vectors containing all the seed's digits.
        private static readonly Vector512<double>[] SeedDigitVectors = new Vector512<double>[
            (MotelyGlobals.SeedDigits.Length + MotelyGlobals.MaxVectorWidth - 1)
                / MotelyGlobals.MaxVectorWidth
        ];

        static MotelySequentialSearchPlan()
        {
            Span<double> vector = stackalloc double[MotelyGlobals.MaxVectorWidth];

            for (int i = 0; i < SeedDigitVectors.Length; i++)
            {
                for (int j = 0; j < MotelyGlobals.MaxVectorWidth; j++)
                {
                    int index = i * MotelyGlobals.MaxVectorWidth + j;

                    if (index >= MotelyGlobals.SeedDigits.Length)
                    {
                        vector[j] = 0;
                    }
                    else
                    {
                        vector[j] = MotelyGlobals.SeedDigits[index];
                    }
                }

                SeedDigitVectors[i] = Vector512.Create<double>(vector);
            }
        }

        private readonly int _batchCharCount;
        private readonly int _nonBatchCharCount;

        private readonly char* _digits;
        private readonly Vector512<double>* _hashes;
        private readonly PartialSeedHashCache* _hashCache;

        public MotelySequentialSearchPlan(
            MotelySearch<TBaseFilter> search,
            MotelySearchSettings<TBaseFilter> settings,
            int index
        )
            : base(search, index)
        {
            _digits = (char*)Marshal.AllocHGlobal(sizeof(char) * MotelyGlobals.MaxSeedLength);

            _batchCharCount = settings.SequentialBatchCharacterCount;
            SeedsPerBatch = (long)Math.Pow(MotelyGlobals.SeedDigits.Length, _batchCharCount);

            _nonBatchCharCount = MotelyGlobals.MaxSeedLength - _batchCharCount;
            MaxBatch = (long)Math.Pow(MotelyGlobals.SeedDigits.Length, _nonBatchCharCount);

            // Safety check for pseudoHashKeyLengthCount to prevent null pointer issues
            if (Search._pseudoHashKeyLengthCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid pseudoHashKeyLengthCount: {Search._pseudoHashKeyLengthCount}. Search may not be properly initialized."
                );
            }

            _hashes = (Vector512<double>*)
                Marshal.AllocHGlobal(
                    sizeof(Vector512<double>)
                        * Search._pseudoHashKeyLengthCount
                        * (_batchCharCount + 1)
                );

            _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
            *_hashCache = new PartialSeedHashCache(search, &_hashes[0]);
        }

        internal override void SearchSequentialBatch(long batchIdx)
        {
            // Figure out which digits this search is doing
            for (int i = _nonBatchCharCount - 1; i >= 0; i--)
            {
                int charIndex = (int)(batchIdx % MotelyGlobals.SeedDigits.Length);
                _digits[MotelyGlobals.MaxSeedLength - i - 1] = MotelyGlobals.SeedDigits[charIndex];
                batchIdx /= MotelyGlobals.SeedDigits.Length;
            }

            Vector512<double>* hashes = &_hashes[
                _batchCharCount * Search._pseudoHashKeyLengthCount
            ];

            // Calculate hash for the first digits at all the required pseudohash lengths
            for (
                int pseudohashKeyIdx = 0;
                pseudohashKeyIdx < Search._pseudoHashKeyLengthCount;
                pseudohashKeyIdx++
            )
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                double num = 1;

                for (int i = MotelyGlobals.MaxSeedLength - 1; i > _batchCharCount - 1; i--)
                {
                    num =
                        (
                            1.1239285023 / num * _digits[i] * Math.PI
                            + (i + pseudohashKeyLength + 1) * Math.PI
                        ) % 1;
                }

                // We only need to write to the first lane because that's the only one that we need
                *(double*)&hashes[pseudohashKeyIdx] = num;
            }

            // Start searching
            for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
            {
                SearchVector(_batchCharCount - 1, SeedDigitVectors[vectorIndex], hashes, 0);
            }
        }

        internal override void SearchProviderBatch()
        {
            throw new InvalidOperationException(
                $"{nameof(MotelySequentialSearchPlan)} does not support provider batch search."
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SearchVector(
            int i,
            Vector512<double> seedDigitVector,
            Vector512<double>* nums,
            int numsLaneIndex
        )
        {
            // Check for cancellation/disposal periodically to make large batches responsive
            if (
                Volatile.Read(ref Search._isDisposed) != 0
                || Search._cancellationToken.IsCancellationRequested
            )
            {
                return;
            }

            Vector512<double>* hashes = &_hashes[i * Search._pseudoHashKeyLengthCount];

            for (
                int pseudohashKeyIdx = 0;
                pseudohashKeyIdx < Search._pseudoHashKeyLengthCount;
                pseudohashKeyIdx++
            )
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];
                Vector512<double> calcVector = Vector512.Create(
                    1.1239285023 / ((double*)&nums[pseudohashKeyIdx])[numsLaneIndex]
                );

                calcVector = Vector512.Multiply(calcVector, seedDigitVector);

                calcVector = Vector512.Multiply(calcVector, Math.PI);
                calcVector = Vector512.Add(
                    calcVector,
                    Vector512.Create((i + pseudohashKeyLength + 1) * Math.PI)
                );

                Vector512<double> intPart = Vector512.Floor(calcVector);
                calcVector = Vector512.Subtract(calcVector, intPart);

                hashes[pseudohashKeyIdx] = calcVector;
            }

            if (i == 0)
            {
                SearchSeeds(
                    new MotelySearchContextParams(
                        _hashCache,
                        MotelyGlobals.MaxSeedLength,
                        MotelyGlobals.MaxSeedLength - 1,
                        &_digits[1],
                        &seedDigitVector
                    )
                );
            }
            else
            {
                for (int lane = 0; lane < MotelyGlobals.MaxVectorWidth; lane++)
                {
                    if (seedDigitVector[lane] == 0)
                        break;

                    _digits[i] = (char)seedDigitVector[lane];

                    for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
                    {
                        SearchVector(i - 1, SeedDigitVectors[vectorIndex], hashes, lane);
                        // Abort loop immediately if cancellation occurred in recursive call
                        if (
                            Volatile.Read(ref Search._isDisposed) != 0
                            || Search._cancellationToken.IsCancellationRequested
                        )
                        {
                            return;
                        }
                    }
                }
            }
        }

        public new void Dispose()
        {
            base.Dispose();

            _hashCache->Dispose();
            Marshal.FreeHGlobal((nint)_hashCache);

            Marshal.FreeHGlobal((nint)_digits);
            Marshal.FreeHGlobal((nint)_hashes);
        }
    }
}
