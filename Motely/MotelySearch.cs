using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely;

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

public interface IMotelySeedScore
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

public interface IMotelySeedFilter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VectorMask Filter(ref MotelyVectorSearchContext searchContext);
}

public enum MotelySearchMode
{
    Sequential,
    Provider,
}

public interface IMotelySeedProvider
{
    public int? SeedCount { get; }
    public ReadOnlySpan<char> NextSeed();

    /// <summary>
    /// Batch retrieve multiple seeds in one lock operation - much faster for multi-threaded access.
    /// Fills the provided array with seed strings, returns the number of seeds actually retrieved.
    /// </summary>
    public int NextSeeds(string[] seeds);
}

public sealed class MotelyRandomSeedProvider(int? count) : IMotelySeedProvider
{
    public int? SeedCount { get; } = count;

    private int _seedsGenerated;

    public ReadOnlySpan<char> NextSeed()
    {
        if (Interlocked.Increment(ref _seedsGenerated) > SeedCount)
            return [];

        // Random.Shared is thread-safe; string.Create writes directly into the
        // string's backing buffer — zero stackalloc, zero intermediate copies.
        return string.Create(
            MotelyCore.MaxSeedLength,
            (object?)null,
            static (buf, _) => Random.Shared.GetItems(MotelyCore.SeedDigits, buf)
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
                MotelyCore.MaxSeedLength,
                (object?)null,
                static (buf, _) => Random.Shared.GetItems(MotelyCore.SeedDigits, buf)
            );
            filled++;
        }
        return filled;
    }
}

/// <summary>
/// Generates palindrome seeds lazily (e.g., "12344321", "123454321").
/// Palindromes read the same forwards and backwards.
/// </summary>
public sealed class MotelyPalindromeSeedProvider : IMotelySeedProvider
{
    public int? SeedCount { get; } = -1; // Unknown - generates infinitely many palindromes

    private readonly IEnumerator<string> _palindromeEnumerator;
    private readonly object _enumeratorLock = new();

    public MotelyPalindromeSeedProvider()
    {
        _palindromeEnumerator = GeneratePalindromes().GetEnumerator();
    }

    private IEnumerable<string> GeneratePalindromes()
    {
        // Generate palindromes of increasing length
        // Length 1: single digits (0-9, A-C) = 13 possibilities
        // Length 2: "11", "22", etc. = 13 possibilities
        // Length 3: "121", "131", etc. = 13 * 13 = 169 possibilities
        // Length 4: "1221", "1331", etc. = 13 * 13 = 169 possibilities
        // Length 5: "12321", "12421", etc. = 13 * 13 * 13 = 2197 possibilities
        // etc.

        // Start with single-digit palindromes
        for (int len = 1; len <= MotelyCore.MaxSeedLength; len++)
        {
            foreach (var palindrome in GeneratePalindromesOfLength(len))
            {
                yield return palindrome;
            }
        }
    }

    private IEnumerable<string> GeneratePalindromesOfLength(int length)
    {
        if (length == 1)
        {
            // Single digit palindromes
            for (int i = 0; i < MotelyCore.SeedDigits.Length; i++)
            {
                yield return MotelyCore.SeedDigits[i].ToString();
            }
        }
        else
        {
            // Generate palindromes recursively
            int halfLen = (length + 1) / 2; // For even: 4->2, for odd: 5->3
            foreach (
                var palindrome in GeneratePalindromesRecursive(new char[length], 0, halfLen, length)
            )
            {
                yield return palindrome;
            }
        }
    }

    private IEnumerable<string> GeneratePalindromesRecursive(
        char[] buffer,
        int pos,
        int halfLen,
        int totalLen
    )
    {
        if (pos >= halfLen)
        {
            // Fill the second half as mirror of first half
            for (int i = 0; i < halfLen; i++)
            {
                buffer[totalLen - 1 - i] = buffer[i];
            }
            yield return new string(buffer, 0, totalLen);
            yield break;
        }

        // Try each digit at this position
        for (int i = 0; i < MotelyCore.SeedDigits.Length; i++)
        {
            buffer[pos] = MotelyCore.SeedDigits[i];
            foreach (var result in GeneratePalindromesRecursive(buffer, pos + 1, halfLen, totalLen))
            {
                yield return result;
            }
        }
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

public sealed class MotelySeedListProvider : IMotelySeedProvider
{
    // Keep seeds as enumerable - don't materialize! Seeds are used in the order provided.
    // For keyword generation, enumerable is lazy and avoids massive allocations.
    private readonly IEnumerator<string> _seedEnumerator;
    private string? _currentSeed;
    private long _seedIndex = -1;

    // Thread-safety: IEnumerator<T> is NOT thread-safe, so we need a lock
    // This is a lightweight lock for the hot path - contention should be minimal
    private readonly object _enumeratorLock = new object();

    public int? SeedCount { get; private set; } = -1; // Unknown for enumerables

    public MotelySeedListProvider(IEnumerable<string> seeds, int seedCount = -1)
    {
        // Don't materialize! Seeds are used in the order provided from generator/enumerator
        _seedEnumerator = seeds.GetEnumerator();
        SeedCount = seedCount; // Allow setting count for keyword generation
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
    IMotelySearchSettings WithListSearch(IEnumerable<string> seeds, int seedCount = -1);
    IMotelySearchSettings WithRandomSearch(int count);
    IMotelySearchSettings WithPalindromeSearch();
    IMotelySearchSettings WithProviderSearch(IMotelySeedProvider provider);
    IMotelySearchSettings WithSequentialSearch();
    IMotelySearchSettings WithDeck(MotelyDeck deck);
    IMotelySearchSettings WithStake(MotelyStake stake);
    IMotelySearchSettings WithProgressCallback(Action<MotelyProgress> callback);
    IMotelySearchSettings WithCsvOutput(bool csvOutput);
    IMotelySearchSettings WithQuietMode(bool quietMode);
    IMotelySearchSettings WithSeedMatchCallback(Action<string> callback);
    IMotelySearchSettings WithScoredResultCallback(Action<MotelySeedScoreTally> callback);
    IMotelySearchSettings WithProgressMessageCallback(Action<string> callback);
    /// <summary>Create a search instance without starting it. Call Start() on a background thread to allow progress polling.</summary>
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

    /// <summary>
    /// Callback invoked when a seed matches all filters (no score provider).
    /// Consumers provide their own output handler (e.g. Console.WriteLine for CLI).
    /// </summary>
    public Action<string>? SeedMatchCallback { get; set; }

    /// <summary>
    /// Callback invoked for scored result rows so callers can persist structured results.
    /// </summary>
    public Action<MotelySeedScoreTally>? ScoredResultCallback { get; set; }

    /// <summary>
    /// Callback for human-readable progress messages (e.g. "Progress: 12.3% | Found: 5/1000").
    /// Consumers provide their own output handler (e.g. Console.Error.WriteLine for CLI).
    /// </summary>
    public Action<string>? ProgressMessageCallback { get; set; }

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

    public MotelySearchSettings<TBaseFilter> WithPalindromeSearch()
    {
        return WithProviderSearch(new MotelyPalindromeSeedProvider());
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

    IMotelySearchSettings IMotelySearchSettings.WithListSearch(
        IEnumerable<string> seeds,
        int seedCount
    ) => WithListSearch(seeds, seedCount);

    IMotelySearchSettings IMotelySearchSettings.WithRandomSearch(int count) =>
        WithRandomSearch(count);

    IMotelySearchSettings IMotelySearchSettings.WithPalindromeSearch() => WithPalindromeSearch();

    IMotelySearchSettings IMotelySearchSettings.WithProviderSearch(IMotelySeedProvider provider) =>
        WithProviderSearch(provider);

    IMotelySearchSettings IMotelySearchSettings.WithSequentialSearch() => WithSequentialSearch();

    IMotelySearchSettings IMotelySearchSettings.WithDeck(MotelyDeck deck) => WithDeck(deck);

    IMotelySearchSettings IMotelySearchSettings.WithStake(MotelyStake stake) => WithStake(stake);

    IMotelySearchSettings IMotelySearchSettings.WithProgressCallback(
        Action<MotelyProgress> callback
    ) => WithProgressCallback(callback);

    IMotelySearchSettings IMotelySearchSettings.WithCsvOutput(bool csvOutput) =>
        WithCsvOutput(csvOutput);

    IMotelySearchSettings IMotelySearchSettings.WithQuietMode(bool quietMode) =>
        WithQuietMode(quietMode);

    IMotelySearchSettings IMotelySearchSettings.WithSeedMatchCallback(Action<string> callback) =>
        WithSeedMatchCallback(callback);

    IMotelySearchSettings IMotelySearchSettings.WithScoredResultCallback(
        Action<MotelySeedScoreTally> callback
    ) => WithScoredResultCallback(callback);

    IMotelySearchSettings IMotelySearchSettings.WithProgressMessageCallback(
        Action<string> callback
    ) => WithProgressMessageCallback(callback);

    IMotelySearch IMotelySearchSettings.CreateSearch() => CreateSearch();

    IMotelySearch IMotelySearchSettings.Start(CancellationToken cancellationToken) =>
        Start(cancellationToken);

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

    public MotelySearchSettings<TBaseFilter> WithProgressMessageCallback(Action<string> callback)
    {
        ProgressMessageCallback = callback;
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
    public TimeSpan ElapsedTime { get; }
    public long TotalSeedsSearched { get; }
    public long MatchingSeeds { get; }
    public long FilteredSeeds { get; }
    public bool IsCompleted { get; }
    public bool IsSequentialBatchSearch { get; }
    public long BatchIndex { get; }
    public long CompletedBatchCount { get; }

    public IMotelySearch Start(CancellationToken cancellationToken = default);
    public void AwaitCompletion();
    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default);
    public void Cancel();
    public void ForceProgressReport();
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

    // IInternalMotelySearch implementation
    int IInternalMotelySearch.PseudoHashKeyLengthCount => _pseudoHashKeyLengthCount;
    private readonly int* _pseudoHashKeyLengths;
    int* IInternalMotelySearch.PseudoHashKeyLengths => _pseudoHashKeyLengths;

    private readonly long _startBatchIndex;
    private long _completedBatchIndex;
    private readonly long _endBatchIndex;
    private long _batchIndex;
    private long _matchingSeeds;
    private long _actualBatchesCompleted; // Aggregated from thread-local counters
    private long _seedsSearched; // Provider-mode: actual seeds pulled (deterministic)

    // Plan-local counters to eliminate Interlocked contention
    private readonly long[] _planMatchingSeeds;
    private readonly long[] _planBatchesCompleted;
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
            // Aggregate from thread-local arrays (no Interlocked contention!)
            long totalBatches = 0;
            for (int i = 0; i < _planBatchesCompleted.Length; i++)
            {
                totalBatches += _planBatchesCompleted[i];
            }
            _actualBatchesCompleted = totalBatches; // Cache for other uses

            // Both modes track _actualBatchesCompleted - no need to recalculate from seeds
            // In provider mode, _startBatchIndex doesn't apply (batches aren't sequential)
            return _isProviderMode
                ? _actualBatchesCompleted
                : _startBatchIndex + _actualBatchesCompleted;
        }
    }

    public long TotalSeedsSearched
    {
        get
        {
            if (_isProviderMode)
            {
                return Interlocked.Read(ref _seedsSearched);
            }

            // Aggregate from thread-local arrays
            long totalBatches = 0;
            for (int i = 0; i < _planBatchesCompleted.Length; i++)
            {
                totalBatches += _planBatchesCompleted[i];
            }
            _actualBatchesCompleted = totalBatches;

            return _actualBatchesCompleted * _plans[0].SeedsPerBatch;
        }
    }
    public long MatchingSeeds
    {
        get
        {
            // Aggregate from thread-local arrays (no Interlocked contention!)
            long totalSeeds = 0;
            for (int i = 0; i < _planMatchingSeeds.Length; i++)
            {
                totalSeeds += _planMatchingSeeds[i];
            }
            _matchingSeeds = totalSeeds; // Cache for other uses
            return _matchingSeeds;
        }
    }
    public long FilteredSeeds => 0; // TODO: rebuild score desc on JamlConfig

    public TimeSpan ElapsedTime => _elapsedTime.Elapsed;

    /// <summary>
    /// Tries to get the score provider if one was configured.
    /// </summary>
    public bool TryGetScoreProvider([NotNullWhen(true)] out IMotelySeedScoreProvider? scoreProvider)
    {
        scoreProvider = _scoreProvider;
        return scoreProvider != null;
    }

    private long _lastReportMS;
    private const long ReportIntervalMS = 2000; // Report every 2 seconds

    private readonly Action<MotelyProgress>? _progressCallback;
    private readonly Action<string>? _seedMatchCallback;
    private readonly Action<MotelySeedScoreTally>? _scoredResultCallback;
    private readonly Action<string>? _progressMessageCallback;
    private readonly int _batchCharacterCount;
    private readonly bool _csvOutput;
    private readonly bool _quietMode;

    private readonly Stopwatch _elapsedTime = new();

    public MotelySearch(MotelySearchSettings<TBaseFilter> settings)
    {
        _isProviderMode = settings.Mode == MotelySearchMode.Provider;
        _searchParameters = new() { Deck = settings.Deck, Stake = settings.Stake };
        _progressCallback = settings.ProgressCallback;
        _seedMatchCallback = settings.SeedMatchCallback;
        _scoredResultCallback = settings.ScoredResultCallback;
        _progressMessageCallback = settings.ProgressMessageCallback;
        _batchCharacterCount = settings.SequentialBatchCharacterCount;
        _csvOutput = settings.CsvOutput;
        _quietMode = settings.QuietMode;

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

        _startBatchIndex = settings.StartBatchIndex;
        _endBatchIndex = settings.EndBatchIndex;

        // Initialize to one BEFORE start since ThreadMain increments BEFORE searching
        // StartBatchIndex is always >= 0 now (defaults to 0)
        _batchIndex = _startBatchIndex - 1;

        _completedBatchIndex = _startBatchIndex;

        int[] pseudohashKeyLengths = [.. filterCreationContext.CachedPseudohashKeyLengths];
        _pseudoHashKeyLengthCount = pseudohashKeyLengths.Length;

        _pseudoHashKeyLengths = (int*)Marshal.AllocHGlobal(sizeof(int) * _pseudoHashKeyLengthCount);
        for (int i = 0; i < _pseudoHashKeyLengthCount; i++)
        {
            _pseudoHashKeyLengths[i] = pseudohashKeyLengths[i];
        }

        _threadCount = Math.Max(1, settings.ThreadCount);
        _planMatchingSeeds = new long[_threadCount];
        _planBatchesCompleted = new long[_threadCount];
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
            // Multi-threaded: launch N threads, wait for all to finish
            using var countdown = new CountdownEvent(_threadCount);
            for (int i = 0; i < _threadCount; i++)
            {
                int threadIdx = i;
                Task.Factory.StartNew(
                    () =>
                    {
                        try
                        {
                            RunWorkerBody(_plans[threadIdx]);
                        }
                        finally
                        {
                            countdown.Signal();
                        }
                    },
                    _cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default
                );
            }
            countdown.Wait(_cancellationToken);
        }

        // Ensure all thread-local writes are visible before completing
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

            plan.SearchBatch(0);
            if (plan._providerExhausted)
            {
                plan.FlushLocalCounters();
                break;
            }

            plan._localBatchesCompleted++;

            // Flush counters
            if (plan._localMatchingSeeds > 0)
            {
                _planMatchingSeeds[plan.ThreadIndex] += plan._localMatchingSeeds;
                plan._localMatchingSeeds = 0;
            }
            _planBatchesCompleted[plan.ThreadIndex] += plan._localBatchesCompleted;
            plan._localBatchesCompleted = 0;

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

        plan.FlushLocalCounters();
    }

    public Task RunSearchAsync(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
        RunSearchUntilCompletion();
        return Task.CompletedTask;
    }

    public IMotelySearch Start(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        _cancellationToken = cancellationToken;

        RunSearchUntilCompletion();
        return this;
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

    public void ForceProgressReport()
    {
        PrintReport(force: true);
    }

    private void PrintReport(bool force = false)
    {
        long elapsedMS = _elapsedTime.ElapsedMilliseconds;

        if (!force)
        {
            // Atomic check-and-set to prevent multiple threads from printing simultaneously
            long lastReport = Volatile.Read(ref _lastReportMS);
            if (elapsedMS - lastReport < ReportIntervalMS)
                return;

            // Try to claim this report slot - if another thread beat us, skip
            // This ensures only ONE thread prints progress, even if multiple threads call this
            long expected = lastReport;
            if (Interlocked.CompareExchange(ref _lastReportMS, elapsedMS, expected) != expected)
                return;
        }
        else
        {
            Volatile.Write(ref _lastReportMS, elapsedMS);
        }

        // PERFORMANCE: Use calculated CompletedBatchCount (no extra state to maintain)
        long thisCompletedCount = CompletedBatchCount;
        long totalBatches = _plans[0].MaxBatch;
        long seedsSearched = TotalSeedsSearched;

        // Calculate progress percentage
        double percentComplete;
        double totalPortionFinished;
        double thisPortionFinished;

        if (_isProviderMode && _plans[0] is MotelyProviderSearchPlan providerPlan)
        {
            long totalSeeds = providerPlan.SeedProvider.SeedCount ?? -1;
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

        // Calculate seeds per millisecond (easier to read than per second for large numbers)
        double seedsPerMs = elapsedMS > 1 ? (double)seedsSearched / elapsedMS : 0;
        double seedsPerSecond = seedsPerMs * 1000.0; // Keep for backward compatibility in callback

        // Format speed as M/s (millions per second) for readability
        string speedFormatted = FormatSpeed(seedsPerSecond);

        // ALWAYS invoke progress callback if set (even in quiet mode) - needed for API speed stats
        if (_progressCallback != null)
        {
            var progress = new MotelyProgress
            {
                CompletedBatchCount = thisCompletedCount,
                TotalBatchCount = totalBatches,
                SeedsSearched = seedsSearched,
                SeedsPerMillisecond = seedsPerMs,
                PercentComplete = percentComplete,
                ElapsedTime = TimeSpan.FromMilliseconds(elapsedMS),
            };
            _progressCallback(progress);
        }

        // Suppress console progress output in quiet mode, unless forced (e.g. via ESC key)
        if (_quietMode && !force)
            return;

        string timeLeftFormatted;
        // Guard against unrealistic estimates early in search (when progress is < 0.01%)
        // Also guard against division by zero or near-zero
        if (thisPortionFinished < 0.0001)
        {
            timeLeftFormatted = "calculating...";
        }
        else
        {
            double totalTimeEstimate = elapsedMS / thisPortionFinished;
            double timeLeft = totalTimeEstimate - elapsedMS;

            bool invalid = double.IsNaN(timeLeft) || double.IsInfinity(timeLeft) || timeLeft < 0;
            // Clamp to max TimeSpan if too large - for very slow searches
            // Also cap at 30 days to avoid showing unrealistic estimates
            const double MAX_ESTIMATE_MS = 30.0 * 24 * 60 * 60 * 1000; // 30 days
            if (
                invalid
                || timeLeft > Math.Min(TimeSpan.MaxValue.TotalMilliseconds, MAX_ESTIMATE_MS)
            )
            {
                timeLeftFormatted = "--:--:--";
            }
            else
            {
                TimeSpan timeLeftSpan = TimeSpan.FromMilliseconds(
                    Math.Min(timeLeft, TimeSpan.MaxValue.TotalMilliseconds)
                );
                if (timeLeftSpan.Days == 0)
                    timeLeftFormatted = $"{timeLeftSpan:hh\\:mm\\:ss}";
                else
                    timeLeftFormatted = $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
            }
        }

        // Different progress display for CSV mode vs normal mode

        // In CSV mode, print progress on a NEW LINE (not overwriting) to avoid collision with results
        // Print at end of batch flush, so it appears after any results from that batch
        var progressMsg =
            $"# Progress: {totalPortionFinished * 100:F8}% | Found: {MatchingSeeds:N0}/{seedsSearched:N0} | ~{timeLeftFormatted} remaining ({speedFormatted})";

        _progressMessageCallback?.Invoke(progressMsg);
    }

    /// <summary>
    /// Format speed as M/s (millions per second) for readability.
    /// Examples: 2950678 → "2.95 M/s", 123456 → "123K seeds/s", 1234 → "1.23K seeds/s"
    /// </summary>
    private static string FormatSpeed(double seedsPerSecond)
    {
        if (seedsPerSecond >= 1_000_000)
        {
            return $"{seedsPerSecond / 1_000_000:F2} M/s";
        }
        else if (seedsPerSecond >= 1_000)
        {
            return $"{seedsPerSecond / 1_000:F2}K seeds/s";
        }
        else
        {
            return $"{seedsPerSecond:F0} seeds/s";
        }
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
        // Each thread accumulates locally, flushes to global at batch boundaries
        internal long _localMatchingSeeds = 0;
        internal long _localBatchesCompleted = 0;

        // Pre-allocated result buffer - ONE allocation per thread, reused forever
        // Old stale data is fine - mask controls which slots are valid
        protected readonly MotelySeedScoreTally[] _resultBuffer = new MotelySeedScoreTally[
            MotelyCore.MaxVectorWidth
        ];

        [InlineArray(MotelyCore.MaxSeedLength)]
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

            // Initialize the result buffer elements
            for (int i = 0; i < _resultBuffer.Length; i++)
            {
                _resultBuffer[i] = new MotelySeedScoreTally("", 0);
            }

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
                                        * MotelyCore.MaxCachedPseudoHashKeyLength
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

                SearchBatch(batchIdx);

                _localBatchesCompleted++;

                // Check for timed-out filter batches
                if (Search._additionalFilters.Length != 0)
                {
                    long currentMS = Search._elapsedTime.ElapsedMilliseconds;
                    for (int i = 0; i < Search._additionalFilters.Length; i++)
                    {
                        FilterSeedBatch* batch = &_filterSeedBatches[i];

                        if (batch->SeedCount != 0)
                        {
                            long batchWaitMS = currentMS - batch->WaitStartMS;

                            if (batchWaitMS >= MAX_SEED_WAIT_MS)
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

                // Flush counters
                if (_localMatchingSeeds > 0)
                {
                    Search._planMatchingSeeds[ThreadIndex] += _localMatchingSeeds;
                    _localMatchingSeeds = 0;
                }
                Search._planBatchesCompleted[ThreadIndex] += _localBatchesCompleted;
                _localBatchesCompleted = 0;

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

            FlushLocalCounters();
        }

        internal abstract void SearchBatch(long batchIdx);

        // PERFORMANCE: Flush thread-local counters to global state
        // Called periodically and at thread completion to aggregate thread-local data
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void FlushLocalCounters()
        {
            // Flush any remaining local counters to global (no Interlocked contention!)
            if (_localMatchingSeeds > 0)
            {
                Search._planMatchingSeeds[ThreadIndex] += _localMatchingSeeds;
                _localMatchingSeeds = 0;
            }
            if (_localBatchesCompleted > 0)
            {
                Search._planBatchesCompleted[ThreadIndex] += _localBatchesCompleted;
                _localBatchesCompleted = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SearchSeeds(in MotelySearchContextParams searchContextParams)
        {
            char* seed = stackalloc char[MotelyCore.MaxSeedLength];
            // This is the method for searching the base filter, we should not be searching additional filters
            Debug.Assert(!searchContextParams.IsAdditionalFilter);

            MotelyVectorSearchContext searchContext = new(
                in Search._searchParameters,
                in searchContextParams
            );

            VectorMask searchResultMask = Search._baseFilter.Filter(ref searchContext);

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

            searchContextParams.SeedHashCache->Reset();
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
                    searchResultMask,
                    0
                );

                // Report the scored results!
                ReportScoredResults(scoredMask, in searchParams);
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

            for (int lane = 0; lane < MotelyCore.MaxVectorWidth; lane++)
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
            char* seed = stackalloc char[MotelyCore.MaxSeedLength];

            for (int lane = 0; lane < MotelyCore.MaxVectorWidth; lane++)
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

                        if (partialHashLength >= MotelyCore.MaxCachedPseudoHashKeyLength)
                            continue;

                        if (searchParams.SeedHashCache->Cache[partialHashLength] == null)
                            continue;

                        // Cache[partialHashLength] already points to the correct Vector512<double>* for this key length
                        // Per GitHub issue fix: use [lane] directly, NOT [i * Vector512<double>.Count + lane]
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
                    // Bounds check before batching for next filter
                    if (nextFilterIndex < Search._additionalFilters.Length)
                    {
                        // Otherwise, we batch the seeds up for the next filter :3
                        BatchSeeds(nextFilterIndex, searchResultMask, in searchParams);
                    }
                    else
                    {
                        Debug.Assert(
                            false,
                            $"nextFilterIndex {nextFilterIndex} >= _additionalFilters.Length {Search._additionalFilters.Length}"
                        );
                    }
                }
            }

            // Reset the batch
            filterBatch->SeedCount = 0;
            filterBatch->SeedHashCache.Reset();
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
        private readonly string[] _seedBatchBuffer = new string[MotelyCore.MaxVectorWidth];

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
            int? seedCount = SeedProvider.SeedCount;
            MaxBatch = seedCount is >= 0
                ? (seedCount.Value + (long)(MotelyCore.MaxVectorWidth - 1))
                    / (long)MotelyCore.MaxVectorWidth
                : long.MaxValue / MotelyCore.MaxVectorWidth; // Large estimate for unknown count
            SeedsPerBatch = (long)MotelyCore.MaxVectorWidth;

            _hashes = (Vector512<double>*)
                Marshal.AllocHGlobal(sizeof(Vector512<double>) * search._pseudoHashKeyLengthCount);

            _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
            *_hashCache = new PartialSeedHashCache(search, _hashes);

            _seedCharacterMatrix = (Vector512<double>*)
                Marshal.AllocHGlobal(sizeof(Vector512<double>) * MotelyCore.MaxSeedLength);
        }

        internal override void SearchBatch(long batchIdx)
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
            int* seedLengths = stackalloc int[MotelyCore.MaxVectorWidth];

            // Are all the seeds the same length?
            bool homogeneousSeedLength = true;

            // Process the batched seeds
            for (int seedIdx = 0; seedIdx < actualSeedCount; seedIdx++)
            {
                ReadOnlySpan<char> seed = _seedBatchBuffer[seedIdx].AsSpan();

                if (
                    seed.IsEmpty
                    || seed.Length > MotelyCore.MaxSeedLength
                    || seed.IndexOf('0') >= 0
                )
                {
                    // Invalid seed - skip it
                    continue;
                }

                // Bounds check for seedLengths array
                if (seedIdx < MotelyCore.MaxVectorWidth)
                {
                    seedLengths[seedIdx] = seed.Length;

                    if (seedIdx > 0 && seedLengths[0] != seed.Length)
                        homogeneousSeedLength = false;
                }

                // Bounds check for seed length and matrix access
                int seedLen = Math.Min(seed.Length, MotelyCore.MaxSeedLength);
                for (int i = 0; i < seedLen; i++)
                {
                    int matrixIndex = i * MotelyCore.MaxVectorWidth + seedIdx;
                    if (
                        matrixIndex >= 0
                        && matrixIndex < MotelyCore.MaxSeedLength * MotelyCore.MaxVectorWidth
                    )
                    {
                        ((double*)_seedCharacterMatrix)[matrixIndex] = seed[i];
                    }
                }
                for (int i = seedLen; i < MotelyCore.MaxSeedLength; i++)
                {
                    int matrixIndex = i * MotelyCore.MaxVectorWidth + seedIdx;
                    if (
                        matrixIndex >= 0
                        && matrixIndex < MotelyCore.MaxSeedLength * MotelyCore.MaxVectorWidth
                    )
                    {
                        ((double*)_seedCharacterMatrix)[matrixIndex] = 0;
                    }
                }
            }
            if (actualSeedCount < MotelyCore.MaxVectorWidth)
            {
                for (int lane = actualSeedCount; lane < MotelyCore.MaxVectorWidth; lane++)
                {
                    for (int i = 0; i < MotelyCore.MaxSeedLength; i++)
                    {
                        ((double*)_seedCharacterMatrix)[i * MotelyCore.MaxVectorWidth + lane] = 0;
                    }
                }
            }

            // Provider-mode determinism: count actual seeds pulled, not (batches * vectorWidth).
            // This avoids nondeterministic totals caused by variable partial batches across threads.
            if (actualSeedCount > 0)
            {
                Interlocked.Add(ref Search._seedsSearched, actualSeedCount);
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
                Span<char> seed = stackalloc char[MotelyCore.MaxSeedLength];

                for (int i = 0; i < actualSeedCount; i++)
                {
                    int seedLength = seedLengths[i];

                    for (int j = 0; j < seedLength; j++)
                    {
                        seed[j] = (char)
                            ((double*)_seedCharacterMatrix)[j * MotelyCore.MaxVectorWidth + i];
                    }

                    SearchSingleSeed(seed[..seedLength]);
                }
            }
        }

        private void SearchSingleSeed(ReadOnlySpan<char> seed)
        {
            // Skip empty seeds (indicates we've run out of seeds in the list)
            if (seed.IsEmpty)
                return;

            char* seedLastCharacters = stackalloc char[MotelyCore.MaxSeedLength - 1];

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
            (MotelyCore.SeedDigits.Length + MotelyCore.MaxVectorWidth - 1)
                / MotelyCore.MaxVectorWidth
        ];

        static MotelySequentialSearchPlan()
        {
            Span<double> vector = stackalloc double[MotelyCore.MaxVectorWidth];

            for (int i = 0; i < SeedDigitVectors.Length; i++)
            {
                for (int j = 0; j < MotelyCore.MaxVectorWidth; j++)
                {
                    int index = i * MotelyCore.MaxVectorWidth + j;

                    if (index >= MotelyCore.SeedDigits.Length)
                    {
                        vector[j] = 0;
                    }
                    else
                    {
                        vector[j] = MotelyCore.SeedDigits[index];
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
            _digits = (char*)Marshal.AllocHGlobal(sizeof(char) * MotelyCore.MaxSeedLength);

            _batchCharCount = settings.SequentialBatchCharacterCount;
            SeedsPerBatch = (long)Math.Pow(MotelyCore.SeedDigits.Length, _batchCharCount);

            _nonBatchCharCount = MotelyCore.MaxSeedLength - _batchCharCount;
            MaxBatch = (long)Math.Pow(MotelyCore.SeedDigits.Length, _nonBatchCharCount);

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

        internal override void SearchBatch(long batchIdx)
        {
            // Figure out which digits this search is doing
            for (int i = _nonBatchCharCount - 1; i >= 0; i--)
            {
                int charIndex = (int)(batchIdx % MotelyCore.SeedDigits.Length);
                _digits[MotelyCore.MaxSeedLength - i - 1] = MotelyCore.SeedDigits[charIndex];
                batchIdx /= MotelyCore.SeedDigits.Length;
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

                for (int i = MotelyCore.MaxSeedLength - 1; i > _batchCharCount - 1; i--)
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
                        MotelyCore.MaxSeedLength,
                        MotelyCore.MaxSeedLength - 1,
                        &_digits[1],
                        &seedDigitVector
                    )
                );
            }
            else
            {
                for (int lane = 0; lane < MotelyCore.MaxVectorWidth; lane++)
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
