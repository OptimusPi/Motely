using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;
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
    public string Seed { get; }
}

public interface IMotelySeedScoreProvider
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public VectorMask Score(
        ref MotelyVectorSearchContext searchContext,
        MotelySeedScoreTally[] buffer,
        VectorMask baseFilterMask = default,
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
    public int SeedCount { get; }
    public ReadOnlySpan<char> NextSeed();

    /// <summary>
    /// Batch retrieve multiple seeds in one lock operation - much faster for multi-threaded access.
    /// Fills the provided array with seed strings, returns the number of seeds actually retrieved.
    /// </summary>
    public int NextSeeds(string[] seeds);
}

public sealed class MotelyRandomSeedProvider(int count) : IMotelySeedProvider
{
    public int SeedCount { get; } = count;

    private readonly ThreadLocal<Random> _randomInstances = new();
    private int _seedsGenerated = 0;

    public ReadOnlySpan<char> NextSeed()
    {
        // Check if we've generated enough seeds
        if (Interlocked.Increment(ref _seedsGenerated) > SeedCount)
        {
            return ReadOnlySpan<char>.Empty;
        }

        Random? random = _randomInstances.Value ??= new();

        Span<char> seed = stackalloc char[Motely.MaxSeedLength];

        for (int i = 0; i < seed.Length; i++)
        {
            seed[i] = Motely.SeedDigits[random.Next(Motely.SeedDigits.Length)];
        }

        return new string(seed);
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < seeds.Length; i++)
        {
            var seed = NextSeed();
            if (seed.IsEmpty)
                break;
            seeds[i] = seed.ToString(); // Convert span to string for storage
            count++;
        }
        return count;
    }
}

/// <summary>
/// Generates palindrome seeds lazily (e.g., "12344321", "123454321").
/// Palindromes read the same forwards and backwards.
/// </summary>
public sealed class MotelyPalindromeSeedProvider : IMotelySeedProvider
{
    public int SeedCount { get; } = -1; // Unknown - generates infinitely many palindromes

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
        for (int len = 1; len <= Motely.MaxSeedLength; len++)
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
            for (int i = 0; i < Motely.SeedDigits.Length; i++)
            {
                yield return Motely.SeedDigits[i].ToString();
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
        for (int i = 0; i < Motely.SeedDigits.Length; i++)
        {
            buffer[pos] = Motely.SeedDigits[i];
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

    public int SeedCount { get; private set; } = -1; // Unknown for enumerables

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

    IMotelySearch IMotelySearchSettings.Start(CancellationToken cancellationToken) =>
        Start(cancellationToken);

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

    public IMotelySearch Start(CancellationToken cancellationToken = default)
    {
        MotelySearch<TBaseFilter> search = new(this);

        search.Start(cancellationToken);

        return search;
    }
}

public interface IMotelySearch : IDisposable
{
    public MotelySearchStatus Status { get; }

    /// <summary>True when measuring progress by batches (sequential seed-space search). False for provider/list search.</summary>
    public bool IsSequentialBatchSearch { get; }
    public long BatchIndex { get; }
    public long CompletedBatchCount { get; }
    public TimeSpan ElapsedTime { get; }
    public long TotalSeedsSearched { get; }
    public long MatchingSeeds { get; }
    public long FilteredSeeds { get; }

    public void Start(CancellationToken cancellationToken = default);
    public void AwaitCompletion();
    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default);
    public void Pause();
    public void Cancel();
    public void ForceProgressReport();
}

internal unsafe interface IInternalMotelySearch : IMotelySearch
{
    internal int PseudoHashKeyLengthCount { get; }
    internal int* PseudoHashKeyLengths { get; }
}

public enum MotelySearchStatus
{
    Paused,
    Running,
    Completed,
    Disposed,
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

    private readonly MotelySearchThread[] _threads;
    private readonly IPauseSync _pauseSync;
    private readonly IPauseSync _unpauseSync;
    private volatile MotelySearchStatus _status;
    public MotelySearchStatus Status => _status;
    public bool IsSequentialBatchSearch => !_isProviderMode;

    internal CancellationToken _cancellationToken = CancellationToken.None;
    private readonly TaskCompletionSource<bool> _completionSource = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    private readonly TBaseFilter _baseFilter;
    private readonly IMotelySeedFilter[] _additionalFilters;
    private readonly int _pseudoHashKeyLengthCount;
    private int _activeProviderThreads; // For provider-mode: threads still processing seeds
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

    public long BatchIndex => _batchIndex;

    // Batches actually completed (aggregated from thread-local counters)
    public long CompletedBatchCount
    {
        get
        {
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

            return _actualBatchesCompleted * (_threads.Length > 0 ? _threads[0].SeedsPerBatch : 0);
        }
    }
    public long MatchingSeeds => _matchingSeeds;
    public long FilteredSeeds => Filters.MotelyJsonSeedScoreDesc.FilteredSeedCount;

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
    private readonly int _batchCharacterCount;
    private readonly bool _csvOutput;
    private readonly bool _quietMode;

    private readonly Stopwatch _elapsedTime = new();

    public MotelySearch(MotelySearchSettings<TBaseFilter> settings)
    {
        _isProviderMode = settings.Mode == MotelySearchMode.Provider;
        _searchParameters = new() { Deck = settings.Deck, Stake = settings.Stake };
        _progressCallback = settings.ProgressCallback;
        _batchCharacterCount = settings.SequentialBatchCharacterCount;
        _csvOutput = settings.CsvOutput;
        _quietMode = settings.QuietMode;

        MotelyFilterCreationContext filterCreationContext = new(in _searchParameters)
        {
            IsAdditionalFilter = false,
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

        _pauseSync = MotelySearchPlatform.CreatePauseSync(settings.ThreadCount + 1);
        _unpauseSync = MotelySearchPlatform.CreateUnpauseSync(settings.ThreadCount + 1);
        _status = MotelySearchStatus.Paused;

        // Initialize provider-mode thread counter
        _activeProviderThreads =
            settings.Mode == MotelySearchMode.Provider ? settings.ThreadCount : 0;

        _threads = new MotelySearchThread[settings.ThreadCount];
        for (int i = 0; i < _threads.Length; i++)
        {
            _threads[i] = settings.Mode switch
            {
                MotelySearchMode.Sequential => new MotelySequentialSearchThread(this, settings, i),
                MotelySearchMode.Provider => new MotelyProviderSearchThread(this, settings, i),
                _ => throw new InvalidEnumArgumentException(nameof(settings.Mode)),
            };
        }

        // The threads all immediatly enter a paused state
        _pauseSync.SignalAndWait();
    }

    public void Start(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_status == MotelySearchStatus.Disposed, this);

        // Set cancellation token before starting
        _cancellationToken = cancellationToken;

        // Atomically replace paused status with running
        if (
            Interlocked.CompareExchange(
                ref _status,
                MotelySearchStatus.Running,
                MotelySearchStatus.Paused
            ) != MotelySearchStatus.Paused
        )
            return;

        // Clear bottom line if in CSV mode to prevent interference
        if (_csvOutput && !_quietMode)
        {
            // Clear any bottom-line state (no-op now that FancyConsole is removed)
            // Notify that progress goes to stderr in CSV mode
            Console.Error.WriteLine("# Progress updates will appear here every 2 seconds...");
        }

        _elapsedTime.Start();
        _unpauseSync.SignalAndWait();
    }

    public void AwaitCompletion()
    {
        // Check periodically for cancellation with timeout intervals
        const int timeoutMs = 100; // Check every 100ms

        foreach (MotelySearchThread searchThread in _threads)
        {
            // Wait with timeout to check status periodically
            while (!searchThread.Worker.Join(TimeSpan.FromMilliseconds(timeoutMs)))
            {
                // Check if search was cancelled, disposed, or cancellation token was signaled
                // If cancelled, break immediately - threads will exit due to cancellation check in ThreadMain
                if (
                    _status == MotelySearchStatus.Disposed
                    || _status == MotelySearchStatus.Paused
                    || _cancellationToken.IsCancellationRequested
                )
                {
                    // Search was cancelled - don't wait for threads, they'll exit cleanly
                    return;
                }
            }
        }
    }

    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        // Already completed - return immediately
        if (_status == MotelySearchStatus.Completed || _status == MotelySearchStatus.Disposed)
            return Task.CompletedTask;

        // Register cancellation callback
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                Cancel(); // Signal search to stop
                _completionSource.TrySetCanceled(cancellationToken);
            });
        }

        return _completionSource.Task;
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_status == MotelySearchStatus.Disposed, this);
        // Atomically replace running status with paused
        if (
            Interlocked.CompareExchange(
                ref _status,
                MotelySearchStatus.Paused,
                MotelySearchStatus.Running
            ) != MotelySearchStatus.Running
        )
            return;

        // Wait for all threads to reach the pause barrier
        // Threads check status in their loop and will signal when they see Paused
        try
        {
            _pauseSync.SignalAndWait(TimeSpan.FromSeconds(2));
        }
        catch (BarrierPostPhaseException)
        {
            // Barrier already broken; treat pause as best-effort.
        }
        catch (TimeoutException)
        {
            // One or more threads didn't reach the pause barrier in time; treat pause as best-effort.
        }

        _elapsedTime.Stop();
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
        long totalBatches = _threads[0].MaxBatch;
        long seedsSearched = TotalSeedsSearched;

        // Calculate progress percentage
        double percentComplete;
        double totalPortionFinished;
        double thisPortionFinished;

        if (_isProviderMode && _threads[0] is MotelyProviderSearchThread providerThread)
        {
            long totalSeeds = providerThread.SeedProvider.SeedCount;
            totalPortionFinished = totalSeeds > 0 ? (double)seedsSearched / totalSeeds : 0;
            percentComplete = totalPortionFinished * 100.0;
            thisPortionFinished = totalPortionFinished;
        }
        else
        {
            long batchesSinceStart = thisCompletedCount - _startBatchIndex;
            long totalBatchesToDo = _threads[0].MaxBatch - _startBatchIndex;
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
        if (_csvOutput)
        {
            // In CSV mode, print progress on a NEW LINE (not overwriting) to avoid collision with results
            // Print at end of batch flush, so it appears after any results from that batch
            var progressMsg =
                $"# Progress: {totalPortionFinished * 100:F8}% | Found: {MatchingSeeds:N0}/{seedsSearched:N0} | ~{timeLeftFormatted} remaining ({speedFormatted})";
            lock (ConsoleLock)
            {
                Console.Error.WriteLine(progressMsg);
            }
        }
        else
        {
            // Normal mode - use fancy bottom line
            // DEBUG: Show raw values to verify calculation accuracy
            // Simple bottom-line progress to Console (no cursor tricks)
            lock (ConsoleLock)
            {
                Console.WriteLine(
                    $"{totalPortionFinished * 100:F8}% | Found: {MatchingSeeds:N0}/{seedsSearched:N0} | ~{timeLeftFormatted} remaining ({speedFormatted}) [batches:{thisCompletedCount}/{totalBatches} t:{elapsedMS}ms]"
                );
            }
        }
    }

    /// <summary>
    /// Fast-path cancellation for Ctrl+C: signal token and set status without waiting for threads.
    /// Used for immediate responsiveness on Ctrl+C. Threads will exit cleanly on next status check.
    /// Call Dispose() later for full cleanup if needed.
    /// </summary>
    public void Cancel()
    {
        // Atomically mark as disposed to signal threads to exit
        Interlocked.Exchange(ref _status, MotelySearchStatus.Disposed);

        // Signal completion so WaitForCompletionAsync resolves (threads exit on next status check)
        _completionSource.TrySetResult(false);
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
        // If cancellation was requested, don't try to pause - let threads exit naturally
        // Trying to pause after cancellation causes delays due to barrier timeouts
        if (_status == MotelySearchStatus.Running && !_cancellationToken.IsCancellationRequested)
        {
            Pause();
        }

        // Atomically replace current state with Disposed state
        MotelySearchStatus oldStatus = Interlocked.Exchange(
            ref _status,
            MotelySearchStatus.Disposed
        );

        // If we were paused, threads are waiting on unpauseBarrier
        // Signal it so they wake up and see Disposed status, then exit
        if (oldStatus == MotelySearchStatus.Paused)
        {
            // Threads will see Disposed status in their loop and exit
            // Signal barrier to wake them up - if it fails, threads will still exit on next status check
            try
            {
                _unpauseSync.SignalAndWait(TimeSpan.FromSeconds(5));
            }
            catch (BarrierPostPhaseException)
            {
                // Barrier already broken - threads will exit when they check status
            }
            catch (TimeoutException)
            {
                // Threads didn't respond in time - they'll exit when they check Disposed status
            }
        }
        else if (oldStatus == MotelySearchStatus.Running)
        {
            // Threads are running - they'll see Disposed status and exit
            // No barrier synchronization needed
        }

        // Wait for threads to finish (they should exit when they see Disposed status)
        foreach (MotelySearchThread thread in _threads)
        {
            if (thread.Worker.IsAlive)
            {
                // Give threads a moment to see the Disposed status and exit
                // Use shorter timeout since threads should already be exiting due to cancellation
                if (!thread.Worker.Join(TimeSpan.FromMilliseconds(500)))
                {
                    // Thread didn't exit in time - this shouldn't happen but handle gracefully
                    // The thread should exit when it checks _status in ThreadMain
                }
            }
            thread.Dispose();
        }

        Marshal.FreeHGlobal((nint)_pseudoHashKeyLengths);

        GC.SuppressFinalize(this);
    }

    ~MotelySearch()
    {
        if (_status != MotelySearchStatus.Disposed)
        {
            Dispose();
        }
    }

    private abstract class MotelySearchThread : IDisposable
    {
        public const int MAX_SEED_WAIT_MS = 5000;

        public readonly MotelySearch<TBaseFilter> Search;
        public readonly int ThreadIndex;
        public readonly IWorkerHandle Worker;

        public long MaxBatch { get; internal set; }
        public long SeedsPerBatch { get; internal set; }

        // ========== THREAD-LOCAL PERFORMANCE ARCHITECTURE ==========
        // PATTERN: Thread-local accumulate → Batch-boundary pull/clear → Global aggregate
        // This eliminates hot-path Interlocked operations and I/O bottlenecks

        // Thread-local counters - NO Interlocked in hot path!
        // Each thread accumulates locally, flushes to global at batch boundaries
        protected long _localMatchingSeeds = 0;
        protected long _localBatchesCompleted = 0;

        // Pre-allocated result buffer - ONE allocation per thread, reused forever
        // Old stale data is fine - mask controls which slots are valid
        protected readonly MotelySeedScoreTally[] _resultBuffer = new MotelySeedScoreTally[
            Motely.MaxVectorWidth
        ];

        [InlineArray(Motely.MaxSeedLength)]
        private struct FilterSeedBatchCharacters
        {
            public Vector512<double> Character;
        }

        private struct FilterSeedBatch
        {
            public FilterSeedBatchCharacters SeedCharacters;
            public Vector512<double>* SeedHashes;
            public PartialSeedHashCache SeedHashCache;
            public int SeedLength;
            public int SeedCount;
            public long WaitStartMS;
        }

        private readonly FilterSeedBatch* _filterSeedBatches;

        // Provider-mode: this thread has exhausted the seed provider and should idle.
        // IMPORTANT: keep the thread alive so Pause() barriers still work.
        protected bool _providerExhausted;

        public MotelySearchThread(MotelySearch<TBaseFilter> search, int threadIndex)
        {
            Search = search;
            ThreadIndex = threadIndex;

            Worker = MotelySearchPlatform.CreateWorker(ThreadMain);

            // Initialize the result buffer elements BEFORE starting thread to avoid race condition
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
                                    sizeof(Vector512<double>) * Motely.MaxCachedPseudoHashKeyLength
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

            Worker.Start();
        }

        private void ThreadMain()
        {
            // PERFORMANCE: Keep thread alive until Disposed to prevent barrier deadlocks
            while (Search._status != MotelySearchStatus.Disposed)
            {
                var status = Search._status;

                if (status == MotelySearchStatus.Paused)
                {
                    Search._pauseSync.SignalAndWait();
                    // ...PAUSED...
                    Search._unpauseSync.SignalAndWait();
                    continue;
                }

                if (status == MotelySearchStatus.Completed)
                {
                    FlushLocalCounters();
                    return;
                }

                // Check for cancellation BEFORE starting work batch
                if (Search._cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Provider-mode: threads that are exhausted should remain alive but idle.
                // This prevents repeated decrements and keeps Pause() barriers consistent.
                if (_providerExhausted)
                {
                    // Check for cancellation even while idling!
                    if (Search._cancellationToken.IsCancellationRequested)
                        break;
                    Thread.Yield();
                    continue;
                }

                if (Search._isProviderMode)
                {
                    // Provider-mode: do NOT use global batch index or MaxBatch termination.
                    // SeedProvider.NextSeed() determines exhaustion, and only the last exhausted
                    // thread sets the search status to Completed.
                    SearchBatch(0);

                    if (_providerExhausted)
                    {
                        // CRITICAL: Flush counters before idling!
                        // Without this, single-seed searches never report their results.
                        FlushLocalCounters();

                        if (Search._cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        Thread.Yield();
                        continue;
                    }
                }
                else
                {
                    long batchIdx = Interlocked.Increment(ref Search._batchIndex);

                    // FIX: Check against BOTH MaxBatch AND _endBatchIndex
                    if (batchIdx >= Search._endBatchIndex || batchIdx >= MaxBatch)
                    {
                        // Don't process this batch - we're done
                        Search._status = MotelySearchStatus.Completed;
                        Search._completionSource.TrySetResult(true);
                        continue;
                    }

                    SearchBatch(batchIdx);
                }

                _localBatchesCompleted++; // Thread-local increment (no Interlocked!)

                // PERFORMANCE: ALL batch-end processing happens HERE in sequence
                // 1. Check for timed-out filter batches
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

                // 2. Flush counters periodically
                if (_localMatchingSeeds > 0)
                {
                    Interlocked.Add(ref Search._matchingSeeds, _localMatchingSeeds);
                    _localMatchingSeeds = 0;
                }
                Interlocked.Add(ref Search._actualBatchesCompleted, _localBatchesCompleted);
                _localBatchesCompleted = 0;

                // 3. Report progress (uses aggregated state from above) - throttled internally
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

            // Loop exited due to cancellation - flush any remaining state
            FlushLocalCounters();
        }

        protected abstract void SearchBatch(long batchIdx);

        // PERFORMANCE: Flush thread-local counters to global state
        // Called periodically and at thread completion to aggregate thread-local data
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FlushLocalCounters()
        {
            // Flush any remaining local counters to global
            if (_localMatchingSeeds > 0)
            {
                Interlocked.Add(ref Search._matchingSeeds, _localMatchingSeeds);
                _localMatchingSeeds = 0;
            }
            if (_localBatchesCompleted > 0)
            {
                Interlocked.Add(ref Search._actualBatchesCompleted, _localBatchesCompleted);
                _localBatchesCompleted = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SearchSeeds(in MotelySearchContextParams searchContextParams)
        {
            char* seed = stackalloc char[Motely.MaxSeedLength];
            // This is the method for searching the base filter, we should not be searching additional filters
            Debug.Assert(!searchContextParams.IsAdditionalFilter);

            MotelyVectorSearchContext searchContext = new(
                in Search._searchParameters,
                in searchContextParams
            );

            VectorMask searchResultMask = Search._baseFilter.Filter(ref searchContext);

            if (searchResultMask.IsPartiallyTrue())
            {
                DebugLogger.Log($"[BASE FILTER] Mask has partial results - routing to next stage");
                if (Search._additionalFilters.Length == 0)
                {
                    // If we have no additional filters, we can just report the results from the base filter
                    DebugLogger.Log($"[BASE FILTER] No additional filters - reporting directly");
                    ReportSeeds(searchResultMask, in searchContextParams);
                }
                else
                {
                    // Otherwise, we need to queue up the seeds for the first additional filter.
                    DebugLogger.Log($"[BASE FILTER] Batching seeds for additional filter 0");
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
                DebugLogger.Log($"[REPORT] Using score provider path (CSV={Search._csvOutput})");
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

                DebugLogger.Log(
                    $"[REPORT] Score provider returned mask with {(scoredMask.IsPartiallyTrue() ? "matches" : "NO matches")}"
                );
                // Report the scored results!
                ReportScoredResults(scoredMask, in searchParams);
            }
            else
            {
                DebugLogger.Log($"[REPORT] Using basic seeds path (no score provider or CSV off)");
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
            // The score provider (MotelyJsonSeedScoreDesc) invokes the callback which writes to Console.
            // Writing here causes DUPLICATE output (every seed printed twice).
            //
            // This method now ONLY updates counters for statistics tracking.
            // The callback flow is:
            //   1. scoreProvider.Score() -> invokes callback -> Console.WriteLine (FIRST OUTPUT)
            //   2. ReportScoredResults() -> ONLY increment counter (NO OUTPUT)

            for (int lane = 0; lane < Motely.MaxVectorWidth; lane++)
            {
                if (resultMask[lane] && searchParams.IsLaneValid(lane))
                {
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
            char* seed = stackalloc char[Motely.MaxSeedLength];

            for (int lane = 0; lane < Motely.MaxVectorWidth; lane++)
            {
                if (searchResultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    int length = searchParams.GetSeed(lane, seed);

                    // Increment thread-local counter
                    _localMatchingSeeds++;

                    // Write directly to console ONLY if:
                    // 1. Not in CSV output mode (CSV/DB output goes to file, not console)
                    // Note: Quiet mode should NOT suppress seed output, only progress!
                    if (!Search._csvOutput)
                    {
                        string seedStr = new Span<char>(seed, length).ToString();
                        Console.WriteLine(seedStr);
                    }
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

            // Validate searchParams
            if (searchParams.SeedHashCache == null)
            {
                DebugLogger.Log($"[BATCH] ERROR: SeedHashCache is null");
                return;
            }

            FilterSeedBatch* filterBatch = &_filterSeedBatches[filterIndex];

            // Validate filterBatch->SeedHashes is allocated
            if (filterBatch->SeedHashes == null)
            {
                DebugLogger.Log(
                    $"[BATCH] ERROR: filterBatch->SeedHashes is null for filterIndex {filterIndex}"
                );
                return;
            }

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

                        if (partialHashLength >= Motely.MaxCachedPseudoHashKeyLength)
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
        private void SearchFilterBatch(int filterIndex, FilterSeedBatch* filterBatch)
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

            DebugLogger.Log($"[BATCH] About to call additional filter {filterIndex}");
            VectorMask searchResultMask = Search
                ._additionalFilters[filterIndex]
                .Filter(ref searchContext);
            DebugLogger.Log(
                $"[BATCH] Additional filter {filterIndex} returned mask: {searchResultMask.Value:X}"
            );

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
                        DebugLogger.Log(
                            $"[BATCH] ERROR: nextFilterIndex {nextFilterIndex} >= _additionalFilters.Length {Search._additionalFilters.Length}"
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
            Worker.Join(TimeSpan.FromSeconds(5));

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

    private sealed unsafe class MotelyProviderSearchThread : MotelySearchThread
    {
        public readonly IMotelySeedProvider SeedProvider;

        private readonly Vector512<double>* _hashes;
        private readonly PartialSeedHashCache* _hashCache;

        private readonly Vector512<double>* _seedCharacterMatrix;

        // Thread-local seed batch buffer to avoid allocations per batch
        private readonly string[] _seedBatchBuffer = new string[Motely.MaxVectorWidth];

        public MotelyProviderSearchThread(
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
            MaxBatch =
                SeedProvider.SeedCount >= 0
                    ? (SeedProvider.SeedCount + (long)(Motely.MaxVectorWidth - 1))
                        / (long)Motely.MaxVectorWidth
                    : long.MaxValue / Motely.MaxVectorWidth; // Large estimate for unknown count
            SeedsPerBatch = (long)Motely.MaxVectorWidth;

            _hashes = (Vector512<double>*)
                Marshal.AllocHGlobal(sizeof(Vector512<double>) * search._pseudoHashKeyLengthCount);

            _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
            *_hashCache = new PartialSeedHashCache(search, _hashes);

            _seedCharacterMatrix = (Vector512<double>*)
                Marshal.AllocHGlobal(sizeof(Vector512<double>) * Motely.MaxSeedLength);
        }

        protected override void SearchBatch(long batchIdx)
        {
            // NOTE: With global seed index (DuckDBSeedProvider), batchIdx doesn't correspond
            // to actual seeds processed. Just try to get seeds and process them - NextSeed()
            // will return empty when all seeds are exhausted.

            // Batch retrieve seeds in one lock operation - much faster!
            // Use thread-local buffer to avoid allocations per batch
            int actualSeedCount = SeedProvider.NextSeeds(_seedBatchBuffer);

            // If we got no seeds at all, this thread is exhausted
            if (actualSeedCount == 0)
            {
                // Mark this thread exhausted so ThreadMain idles it (and we only decrement once)
                _providerExhausted = true;

                // Decrement active provider threads; if this was the last one, mark search completed
                if (Interlocked.Decrement(ref Search._activeProviderThreads) == 0)
                {
                    Search._status = MotelySearchStatus.Completed;
                    Search._completionSource.TrySetResult(true);
                }
                return;
            }

            // The length of all the seeds
            int* seedLengths = stackalloc int[Motely.MaxVectorWidth];

            // Are all the seeds the same length?
            bool homogeneousSeedLength = true;

            // Process the batched seeds
            for (int seedIdx = 0; seedIdx < actualSeedCount; seedIdx++)
            {
                ReadOnlySpan<char> seed = _seedBatchBuffer[seedIdx].AsSpan();

                if (seed.IsEmpty || seed.Length > Motely.MaxSeedLength || seed.IndexOf('0') >= 0)
                {
                    // Invalid seed - skip it
                    continue;
                }

                // Bounds check for seedLengths array
                if (seedIdx < Motely.MaxVectorWidth)
                {
                    seedLengths[seedIdx] = seed.Length;

                    if (seedIdx > 0 && seedLengths[0] != seed.Length)
                        homogeneousSeedLength = false;
                }

                // Bounds check for seed length and matrix access
                int seedLen = Math.Min(seed.Length, Motely.MaxSeedLength);
                for (int i = 0; i < seedLen; i++)
                {
                    int matrixIndex = i * Motely.MaxVectorWidth + seedIdx;
                    if (
                        matrixIndex >= 0
                        && matrixIndex < Motely.MaxSeedLength * Motely.MaxVectorWidth
                    )
                    {
                        ((double*)_seedCharacterMatrix)[matrixIndex] = seed[i];
                    }
                }
                for (int i = seedLen; i < Motely.MaxSeedLength; i++)
                {
                    int matrixIndex = i * Motely.MaxVectorWidth + seedIdx;
                    if (
                        matrixIndex >= 0
                        && matrixIndex < Motely.MaxSeedLength * Motely.MaxVectorWidth
                    )
                    {
                        ((double*)_seedCharacterMatrix)[matrixIndex] = 0;
                    }
                }
            }
            if (actualSeedCount < Motely.MaxVectorWidth)
            {
                for (int lane = actualSeedCount; lane < Motely.MaxVectorWidth; lane++)
                {
                    for (int i = 0; i < Motely.MaxSeedLength; i++)
                    {
                        ((double*)_seedCharacterMatrix)[i * Motely.MaxVectorWidth + lane] = 0;
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
                Span<char> seed = stackalloc char[Motely.MaxSeedLength];

                for (int i = 0; i < actualSeedCount; i++)
                {
                    int seedLength = seedLengths[i];

                    for (int j = 0; j < seedLength; j++)
                    {
                        seed[j] = (char)
                            ((double*)_seedCharacterMatrix)[j * Motely.MaxVectorWidth + i];
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

            char* seedLastCharacters = stackalloc char[Motely.MaxSeedLength - 1];

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

    private sealed unsafe class MotelySequentialSearchThread : MotelySearchThread
    {
        // A cache of vectors containing all the seed's digits.
        private static readonly Vector512<double>[] SeedDigitVectors = new Vector512<double>[
            (Motely.SeedDigits.Length + Motely.MaxVectorWidth - 1) / Motely.MaxVectorWidth
        ];

        static MotelySequentialSearchThread()
        {
            Span<double> vector = stackalloc double[Motely.MaxVectorWidth];

            for (int i = 0; i < SeedDigitVectors.Length; i++)
            {
                for (int j = 0; j < Motely.MaxVectorWidth; j++)
                {
                    int index = i * Motely.MaxVectorWidth + j;

                    if (index >= Motely.SeedDigits.Length)
                    {
                        vector[j] = 0;
                    }
                    else
                    {
                        vector[j] = Motely.SeedDigits[index];
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

        public MotelySequentialSearchThread(
            MotelySearch<TBaseFilter> search,
            MotelySearchSettings<TBaseFilter> settings,
            int index
        )
            : base(search, index)
        {
            _digits = (char*)Marshal.AllocHGlobal(sizeof(char) * Motely.MaxSeedLength);

            _batchCharCount = settings.SequentialBatchCharacterCount;
            SeedsPerBatch = (long)Math.Pow(Motely.SeedDigits.Length, _batchCharCount);

            _nonBatchCharCount = Motely.MaxSeedLength - _batchCharCount;
            MaxBatch = (long)Math.Pow(Motely.SeedDigits.Length, _nonBatchCharCount);

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

        protected override void SearchBatch(long batchIdx)
        {
            // Figure out which digits this search is doing
            for (int i = _nonBatchCharCount - 1; i >= 0; i--)
            {
                int charIndex = (int)(batchIdx % Motely.SeedDigits.Length);
                _digits[Motely.MaxSeedLength - i - 1] = Motely.SeedDigits[charIndex];
                batchIdx /= Motely.SeedDigits.Length;
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

                for (int i = Motely.MaxSeedLength - 1; i > _batchCharCount - 1; i--)
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
                Search._status == MotelySearchStatus.Disposed
                || Search._status == MotelySearchStatus.Paused
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
                        Motely.MaxSeedLength,
                        Motely.MaxSeedLength - 1,
                        &_digits[1],
                        &seedDigitVector
                    )
                );
            }
            else
            {
                for (int lane = 0; lane < Motely.MaxVectorWidth; lane++)
                {
                    if (seedDigitVector[lane] == 0)
                        break;

                    _digits[i] = (char)seedDigitVector[lane];

                    for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
                    {
                        SearchVector(i - 1, SeedDigitVectors[vectorIndex], hashes, lane);
                        // Abort loop immediately if cancellation occurred in recursive call
                        if (
                            Search._status == MotelySearchStatus.Disposed
                            || Search._status == MotelySearchStatus.Paused
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
