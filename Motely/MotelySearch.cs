using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely;

public interface IMotelySeedFilterDesc
{
    public IMotelySeedFilter CreateFilter(ref MotelyFilterCreationContext ctx);
}

public interface IMotelySeedFilterDesc<TFilter> : IMotelySeedFilterDesc where TFilter : struct, IMotelySeedFilter
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

public interface IMotelySeedScoreDesc<TScoreProvider> : IMotelySeedScoreDesc where TScoreProvider : struct, IMotelySeedScoreProvider
{
    public new TScoreProvider CreateScoreProvider(ref MotelyFilterCreationContext ctx);

    IMotelySeedScoreProvider IMotelySeedScoreDesc.CreateScoreProvider(ref MotelyFilterCreationContext ctx)
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
    public VectorMask Score(ref MotelyVectorSearchContext searchContext, MotelySeedScoreTally[] buffer, VectorMask baseFilterMask = default, int scoreThreshold = 0);
}

public interface IMotelySeedFilter
{
    public VectorMask Filter(ref MotelyVectorSearchContext searchContext);
}

public enum MotelySearchMode
{
    Sequential,
    Provider
}

public interface IMotelySeedProvider
{
    public int SeedCount { get; }
    public ReadOnlySpan<char> NextSeed();
}

public sealed class MotelyRandomSeedProvider(int count) : IMotelySeedProvider
{
    public int SeedCount { get; } = count;

    private readonly ThreadLocal<Random> _randomInstances = new();

    public ReadOnlySpan<char> NextSeed()
    {
        Random? random = _randomInstances.Value ??= new();

        Span<char> seed = stackalloc char[Motely.MaxSeedLength];

        for (int i = 0; i < seed.Length; i++)
        {
            seed[i] = Motely.SeedDigits[random.Next(Motely.SeedDigits.Length)];
        }

        return new string(seed);
    }
}

public sealed class MotelySeedListProvider(IEnumerable<string> seeds) : IMotelySeedProvider
{
    // Sort the seeds by length to increase vectorization potential
    public readonly string[] Seeds = [.. seeds.OrderBy(seed => seed.Length)];

    public int SeedCount => Seeds.Length;

    private long _currentSeed = -1;
    public ReadOnlySpan<char> NextSeed()
    {
        long index = Interlocked.Increment(ref _currentSeed);
        if (index >= Seeds.Length)
            return ReadOnlySpan<char>.Empty;
        return Seeds[index];
    }
}

public sealed class MotelySearchSettings<TBaseFilter>(IMotelySeedFilterDesc<TBaseFilter> baseFilterDesc)
    where TBaseFilter : struct, IMotelySeedFilter
{
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
    public long StartBatchIndex { get; set; } = 0;
    public long EndBatchIndex { get; set; } = long.MaxValue;

    public IMotelySeedFilterDesc<TBaseFilter> BaseFilterDesc { get; set; } = baseFilterDesc;

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
    
    /// <summary>
    /// Callback for progress updates - useful for UI progress bars
    /// Parameters: (batchesProcessed, totalBatches, seedsFound, elapsedMs)
    /// </summary>
    public Action<long, long, long, double>? ProgressCallback { get; set; }

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

    public MotelySearchSettings<TBaseFilter> WithListSearch(IEnumerable<string> seeds)
    {
        return WithProviderSearch(new MotelySeedListProvider(seeds));
    }
    
    public MotelySearchSettings<TBaseFilter> WithRandomSearch(int count)
    {
        return WithProviderSearch(new MotelyRandomSeedProvider(count));
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

    public MotelySearchSettings<TBaseFilter> WithSeedScoreProvider(IMotelySeedScoreDesc seedScoreDesc)
    {
        SeedScoreDesc = seedScoreDesc;
        return this;
    }

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

    public MotelySearchSettings<TBaseFilter> WithProgressCallback(Action<long, long, long, double> callback)
    {
        ProgressCallback = callback;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithCsvOutput(bool csvOutput)
    {
        CsvOutput = csvOutput;
        return this;
    }

    public IMotelySearch Start()
    {
        MotelySearch<TBaseFilter> search = new(this);

        search.Start();

        return search;
    }
}


public interface IMotelySearch : IDisposable
{
    public MotelySearchStatus Status { get; }
    public long BatchIndex { get; }
    public long CompletedBatchCount { get; }
    public TimeSpan ElapsedTime { get; }
    public long TotalSeedsSearched { get; }
    public long MatchingSeeds { get; }
    public long FilteredSeeds { get; }

    public void Start();
    public void AwaitCompletion();
    public void Pause();
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
    Disposed
}

public struct MotelySearchParameters
{
    public MotelyStake Stake;
    public MotelyDeck Deck;
}

public unsafe sealed class MotelySearch<TBaseFilter> : IInternalMotelySearch
    where TBaseFilter : struct, IMotelySeedFilter
{

    private readonly MotelySearchParameters _searchParameters;

    private readonly MotelySearchThread[] _threads;
    private readonly Barrier _pauseBarrier;
    private readonly Barrier _unpauseBarrier;
    private volatile MotelySearchStatus _status;
    public MotelySearchStatus Status => _status;

    private readonly TBaseFilter _baseFilter;

    private readonly IMotelySeedFilter[] _additionalFilters;
    // Current Motely filters usually do not have a score provider. They just print and/or return a SEED e.g. "ALEEB"
    private readonly IMotelySeedScoreProvider? _scoreProvider;

    /// <summary>
    /// Sets the score provider, if it is provided.
    /// </summary>
    /// <param name="scoreProvider"></param>
    /// <returns></returns>
    private bool TryGetScoreProvider([NotNullWhen(true)] out IMotelySeedScoreProvider? scoreProvider)
    {
        scoreProvider = _scoreProvider;
        return scoreProvider != null;
    }

    private readonly int _pseudoHashKeyLengthCount;
    int IInternalMotelySearch.PseudoHashKeyLengthCount => _pseudoHashKeyLengthCount;
    private readonly int* _pseudoHashKeyLengths;
    int* IInternalMotelySearch.PseudoHashKeyLengths => _pseudoHashKeyLengths;

    private readonly long _startBatchIndex;
    private long _completedBatchIndex;
    private readonly long _endBatchIndex;
    private long _batchIndex;
    private long _matchingSeeds;
    private long _actualBatchesCompleted; // Aggregated from thread-local counters

    public long BatchIndex => _batchIndex;
    // Batches actually completed (aggregated from thread-local counters)
    public long CompletedBatchCount => _actualBatchesCompleted;
    // Calculate total seeds searched from completed batches
    // Each batch processes SeedsPerBatch seeds
    // Sequential mode: 35^batchSize (e.g., 1225 for batchSize=2)
    // Provider mode: 8 (Vector512 width)
    public long TotalSeedsSearched => CompletedBatchCount * (_threads.Length > 0 ? _threads[0].SeedsPerBatch : 0);
    public long MatchingSeeds => _matchingSeeds;
    public long FilteredSeeds => Filters.MotelyJsonSeedScoreDesc.GetFilteredSeeds();
    
    
    public TimeSpan ElapsedTime => _elapsedTime.Elapsed;

    private double _lastReportMS;
    private readonly double reportInterval = 2000; // Report every 2 seconds
    
    private readonly Action<long, long, long, double>? _progressCallback;
    private readonly int _batchCharacterCount;
    private readonly bool _csvOutput;

    private readonly Stopwatch _elapsedTime = new();

    public MotelySearch(MotelySearchSettings<TBaseFilter> settings)
    {
        _searchParameters = new()
        {
            Deck = settings.Deck,
            Stake = settings.Stake
        };
        _progressCallback = settings.ProgressCallback;
        _batchCharacterCount = settings.SequentialBatchCharacterCount;
        _csvOutput = settings.CsvOutput;

        MotelyFilterCreationContext filterCreationContext = new(in _searchParameters)
        {
            IsAdditionalFilter = false
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
                _additionalFilters[i] = settings.AdditionalFilters[i].CreateFilter(ref filterCreationContext);
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
        // REMOVED: _completedBatchCount - calculated from _batchIndex instead

        int[] pseudohashKeyLengths = [.. filterCreationContext.CachedPseudohashKeyLengths];
        _pseudoHashKeyLengthCount = pseudohashKeyLengths.Length;
        _pseudoHashKeyLengths = (int*)Marshal.AllocHGlobal(sizeof(int) * _pseudoHashKeyLengthCount);

        for (int i = 0; i < _pseudoHashKeyLengthCount; i++)
        {
            _pseudoHashKeyLengths[i] = pseudohashKeyLengths[i];
        }

        _pauseBarrier = new(settings.ThreadCount + 1);
        _unpauseBarrier = new(settings.ThreadCount + 1);
        _status = MotelySearchStatus.Paused;

        _threads = new MotelySearchThread[settings.ThreadCount];
        for (int i = 0; i < _threads.Length; i++)
        {
            _threads[i] = settings.Mode switch
            {
                MotelySearchMode.Sequential => new MotelySequentialSearchThread(this, settings, i),
                MotelySearchMode.Provider => new MotelyProviderSearchThread(this, settings, i),
                _ => throw new InvalidEnumArgumentException(nameof(settings.Mode))
            };
        }

        // The threads all immediatly enter a paused state
        _pauseBarrier.SignalAndWait();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_status == MotelySearchStatus.Disposed, this);
        // Atomically replace paused status with running
        if (Interlocked.CompareExchange(ref _status, MotelySearchStatus.Running, MotelySearchStatus.Paused) != MotelySearchStatus.Paused)
            return;

        // Clear bottom line if in CSV mode to prevent interference
        if (_csvOutput)
        {
            FancyConsole.SetBottomLine(null);
            // Notify that progress goes to stderr in CSV mode
            Console.Error.WriteLine("# Progress updates will appear here every 2 seconds...");
        }

        _elapsedTime.Start();
        _unpauseBarrier.SignalAndWait();
    }

    public void AwaitCompletion()
    {
        foreach (MotelySearchThread searchThread in _threads)
            searchThread.Thread.Join();
    }

    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_status == MotelySearchStatus.Disposed, this);
        // Atomically replace running status with paused
        if (Interlocked.CompareExchange(ref _status, MotelySearchStatus.Paused, MotelySearchStatus.Running) != MotelySearchStatus.Running)
            return;

        _pauseBarrier.SignalAndWait();
        _elapsedTime.Stop();
    }

    // REMOVED: Old ReportSeed() method with Interlocked.Increment - DEAD CODE
    // All seed reporting now uses thread-local ReportSeedThreadLocal() for zero-overhead reporting
    // Thread-local counters are flushed at batch boundaries only

    private void PrintReport()
    {
        double elapsedMS = _elapsedTime.ElapsedMilliseconds;

        if (elapsedMS - _lastReportMS < reportInterval) return;

        _lastReportMS = elapsedMS;

        // PERFORMANCE: Use calculated CompletedBatchCount (no extra state to maintain)
        long thisCompletedCount = CompletedBatchCount;

        double totalPortionFinished = (double)thisCompletedCount / (double)_threads[0].MaxBatch;
        double thisPortionFinished = thisCompletedCount / (double)_threads[0].MaxBatch;
        double totalTimeEstimate = elapsedMS / thisPortionFinished;
        double timeLeft = totalTimeEstimate - elapsedMS;

        string timeLeftFormatted;
        bool invalid = double.IsNaN(timeLeft) || double.IsInfinity(timeLeft) || timeLeft < 0;
        // Clamp to max TimeSpan if too large - for very slow searches
        if (invalid || timeLeft > TimeSpan.MaxValue.TotalMilliseconds)
        {
            timeLeftFormatted = "--:--:--";
        }
        else
        {
            TimeSpan timeLeftSpan = TimeSpan.FromMilliseconds(Math.Min(timeLeft, TimeSpan.MaxValue.TotalMilliseconds));
            if (timeLeftSpan.Days == 0) timeLeftFormatted = $"{timeLeftSpan:hh\\:mm\\:ss}";
            else timeLeftFormatted = $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
        }

        // Calculate seeds per millisecond
        // Avoid divide by zero for a very fast find
        double seedsPerMS = 0;
        if (elapsedMS > 1)
            seedsPerMS = thisCompletedCount * (double)_threads[0].SeedsPerBatch / elapsedMS;

        // Different progress display for CSV mode vs normal mode
        if (_csvOutput)
        {
            // In CSV mode, write progress to stderr with carriage return (erase and redraw)
            // Clear the line first, then write new progress
            var progressMsg = $"# Progress: {Math.Round(totalPortionFinished * 100, 2):F2}% ~{timeLeftFormatted} remaining ({Math.Round(seedsPerMS)} seeds/ms)";
            Console.Error.Write($"\r{progressMsg}{new string(' ', Math.Max(0, 100 - progressMsg.Length))}");
        }
        else
        {
            // Normal mode - use fancy bottom line
            FancyConsole.SetBottomLine($"{Math.Round(totalPortionFinished * 100, 2):F2}% ~{timeLeftFormatted} remaining ({Math.Round(seedsPerMS)} seeds/ms)");
        }

    }

    public void Dispose()
    {
        Pause();

        // Atomically replace paused state with Disposed state

        MotelySearchStatus oldStatus = Interlocked.Exchange(ref _status, MotelySearchStatus.Disposed);

        if (oldStatus == MotelySearchStatus.Paused)
        {
            _unpauseBarrier.SignalAndWait();
        }
        else
        {
            Debug.Assert(oldStatus == MotelySearchStatus.Completed);
        }


        foreach (MotelySearchThread thread in _threads)
        {
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

        public const int MAX_SEED_WAIT_MS = 50000;

        public readonly MotelySearch<TBaseFilter> Search;
        public readonly int ThreadIndex;
        public readonly Thread Thread;

        public long MaxBatch { get; internal set; }
        public long SeedsPerBatch { get; internal set; }

        // ========== THREAD-LOCAL PERFORMANCE ARCHITECTURE ==========
        // PATTERN: Thread-local accumulate → Batch-boundary pull/clear → Global aggregate
        // This eliminates hot-path Interlocked operations and I/O bottlenecks

        // Thread-local counters - NO Interlocked in hot path!
        // Each thread accumulates locally, flushes to global at batch boundaries
        private long _localMatchingSeeds = 0;
        private long _localBatchesCompleted = 0;
        private const int SEED_COUNT_FLUSH_THRESHOLD = 128; // Flush every N seeds
        private const int BATCH_COUNT_FLUSH_THRESHOLD = 10; // Flush every N batches

        // Thread-local output buffer - NO I/O in hot path!
        // Seeds buffered locally, written in batches to reduce syscall overhead
        private readonly List<string> _seedOutputBuffer = new(256);
        private const int SEED_OUTPUT_FLUSH_THRESHOLD = 100; // Flush every N seeds

        // Pre-allocated result buffer - ONE allocation per thread, reused forever
        // Old stale data is fine - mask controls which slots are valid
        protected readonly MotelySeedScoreTally[] _resultBuffer = new MotelySeedScoreTally[8];

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

        public MotelySearchThread(MotelySearch<TBaseFilter> search, int threadIndex)
        {
            Search = search;
            ThreadIndex = threadIndex;

            Thread = new(ThreadMain)
            {
                Name = $"Motely Search Thread {ThreadIndex}"
            };

            // Initialize the result buffer elements BEFORE starting thread to avoid race condition
            for (int i = 0; i < _resultBuffer.Length; i++)
            {
                _resultBuffer[i] = new MotelySeedScoreTally("", 0);
            }

            if (search._additionalFilters.Length != 0)
            {
                _filterSeedBatches = (FilterSeedBatch*)Marshal.AllocHGlobal(sizeof(FilterSeedBatch) * search._additionalFilters.Length);
                
                int allocatedCount = 0;
                try
                {
                    for (int i = 0; i < search._additionalFilters.Length; i++)
                    {
                        FilterSeedBatch* batch = &_filterSeedBatches[i];

                        *batch = new()
                        {
                            SeedHashes = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Motely.MaxCachedPseudoHashKeyLength),
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

            Thread.Start();
        }

        private void ThreadMain()
        {
            while (true)
            {

                switch (Search._status)
                {
                    case MotelySearchStatus.Paused:
                        Search._pauseBarrier.SignalAndWait();
                        // ...Paused
                        Search._unpauseBarrier.SignalAndWait();
                        continue;

                    case MotelySearchStatus.Completed:

                        // Search any batches which have yet to be fully searched
                        for (int i = 0; i < Search._additionalFilters.Length; i++)
                        {
                            FilterSeedBatch* batch = &_filterSeedBatches[i];

                            if (batch->SeedCount != 0)
                            {
                                SearchFilterBatch(i, batch);
                            }
                        }

                        // PERFORMANCE: Flush any remaining thread-local counts and buffers on completion
                        FlushLocalCounters();

                        Debug.Assert(Search._batchIndex >= MaxBatch || Search._batchIndex >= Search._endBatchIndex);
                        return;

                    case MotelySearchStatus.Disposed:
                        // CRITICAL FIX: Flush counters before exiting to ensure stats are accurate
                        FlushLocalCounters();
                        return;
                }

                long batchIdx = Interlocked.Increment(ref Search._batchIndex);

                // FIX: Check against BOTH MaxBatch AND _endBatchIndex
                if (batchIdx >= Search._endBatchIndex || batchIdx >= MaxBatch)
                {
                    // Don't process this batch - we're done
                    Search._status = MotelySearchStatus.Completed;
                    continue;
                }

                SearchBatch(batchIdx);
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
                                Debug.Assert(batch->SeedCount == 0, "Batch should be reset after SearchFilterBatch");
                            }
                        }
                    }
                }

                // 2. Flush thread-local counters periodically (reduces Interlocked ops)
                if (_localMatchingSeeds >= SEED_COUNT_FLUSH_THRESHOLD || _localBatchesCompleted >= BATCH_COUNT_FLUSH_THRESHOLD)
                {
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

                // 3. Flush buffered seed output (reduces I/O ops)
                if (_seedOutputBuffer.Count >= SEED_OUTPUT_FLUSH_THRESHOLD)
                {
                    FlushSeedOutputBuffer();
                }

                // 4. Report progress (LAST - uses aggregated state from above)
                Search.PrintReport();
            }

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

            // Flush any remaining buffered seeds to console
            if (_seedOutputBuffer.Count > 0)
            {
                FlushSeedOutputBuffer();
            }
        }

        // PERFORMANCE: Batch console writes - write all buffered seeds at once
        // This dramatically reduces I/O overhead compared to per-seed WriteLine
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FlushSeedOutputBuffer()
        {
            if (_seedOutputBuffer.Count == 0) return;

            // Write all buffered seeds to console in one go
            foreach (string seed in _seedOutputBuffer)
            {
                FancyConsole.WriteLine(seed);
            }

            _seedOutputBuffer.Clear();
        }

        // PERFORMANCE: Thread-local seed reporting - NO Interlocked, NO immediate I/O!
        // Increment local counter and buffer output for later batch flush
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReportSeedThreadLocal(ReadOnlySpan<char> seed)
        {
            // Increment thread-local counter (no Interlocked overhead!)
            _localMatchingSeeds++;

            // Buffer the seed for batched console write (no I/O overhead!)
            _seedOutputBuffer.Add(seed.ToString());
        }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        protected void SearchSeeds(in MotelySearchContextParams searchContextParams)
        {
            char* seed = stackalloc char[Motely.MaxSeedLength];
            // This is the method for searching the base filter, we should not be searching additional filters
            Debug.Assert(!searchContextParams.IsAdditionalFilter);

            MotelyVectorSearchContext searchContext = new(in Search._searchParameters, in searchContextParams);

            VectorMask searchResultMask = Search._baseFilter.Filter(ref searchContext);
            
            // DEBUG: Log base filter results
            if (DebugLogger.IsEnabled)
            {
                DebugLogger.Log($"[BASE FILTER] Returned mask: 0x{searchResultMask.Value:X} (partially true: {searchResultMask.IsPartiallyTrue()})");
                for (int lane = 0; lane < 8; lane++)
                {
                    if (searchResultMask[lane] && searchContextParams.IsLaneValid(lane))
                    {
                        int length = searchContextParams.GetSeed(lane, seed);
                        string seedStr = new Span<char>(seed, length).ToString();
                        DebugLogger.Log($"[BASE FILTER] Lane {lane} PASSED: {seedStr}");
                    }
                }
            }

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
            else
            {
                DebugLogger.Log($"[BASE FILTER] No partial results - nothing to process");
            }

            searchContextParams.SeedHashCache->Reset();
        }

        // Extracts the actual seed characters from a search context and reports that seed
        private void ReportSeeds(VectorMask searchResultMask, in MotelySearchContextParams searchParams)
        {
            Debug.Assert(searchResultMask.IsPartiallyTrue(), "Mask should be checked for partial truth before calling report seeds (for performance).");

            // If CSV output is enabled and we have a score provider, use it
            if (Search._csvOutput && Search.TryGetScoreProvider(out var scoreProvider))
            {
                // Create search context for scoring
                MotelyVectorSearchContext searchContext = new(in Search._searchParameters, in searchParams);

                // Call the score provider with the mask of seeds that passed filters
                // The score provider will handle scoring and calling the callback
                // The score provider internally tracks filtered seeds (passed all filters before cutoff)
                VectorMask scoredMask = scoreProvider.Score(ref searchContext, _resultBuffer, searchResultMask, 0);

                // Report the scored results!
                ReportScoredResults(scoredMask, in searchParams);
            }
            else
            {
                // No score provider - report basic seeds
                ReportBasicSeeds(searchResultMask, in searchParams);
            }
        }
        
        private void ReportScoredResults(VectorMask resultMask, in MotelySearchContextParams searchParams)
        {
            char* seed = stackalloc char[Motely.MaxSeedLength];
            
            for (int lane = 0; lane < Motely.MaxVectorWidth; lane++)
            {
                if (resultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    Console.Write("\r");

                    var bufferedResult = _resultBuffer[lane];
                    DebugLogger.Log($"🔍 Lane {lane}: Seed='{bufferedResult.Seed}', Score={bufferedResult.Score}");

                    if (!string.IsNullOrEmpty(bufferedResult.Seed))
                    {
                        var tallies = bufferedResult.TallyColumns != null ? string.Join(",", bufferedResult.TallyColumns) : "";
                        DebugLogger.Log($"🎉 OUTPUT: {bufferedResult.Seed},{bufferedResult.Score},{tallies}");
                        // PERFORMANCE: Use thread-local counter instead of Interlocked
                        _localMatchingSeeds++;

                        // Use colorized output if tallies are present
                        if (bufferedResult.TallyColumns != null && bufferedResult.TallyColumns.Count > 0)
                        {
                            // DIRECT CONSOLE OUTPUT - DO NOT USE FANCYCONSOLE WITH TALLIES
                            Console.WriteLine(TallyColorizer.FormatResultLine(bufferedResult.Seed, bufferedResult.Score, bufferedResult.TallyColumns));
                        }
                        else
                        {
                            // Regular output can still use FancyConsole
                            FancyConsole.WriteLine($"{bufferedResult.Seed},{bufferedResult.Score},{tallies}");
                        }
                    }
                    else
                    {
                        DebugLogger.Log($"⚠️ Empty buffer at lane {lane} - using basic seed");
                        int length = searchParams.GetSeed(lane, seed);
                        // PERFORMANCE: Use thread-local counter instead of Interlocked
                        _localMatchingSeeds++;
                        Console.WriteLine(new Span<char>(seed, length).ToString());
                    }
                }
            }
        }
        
        private void ReportBasicSeeds(VectorMask searchResultMask, in MotelySearchContextParams searchParams)
        {
            char* seed = stackalloc char[Motely.MaxSeedLength];

            for (int lane = 0; lane < Motely.MaxVectorWidth; lane++)
            {
                if (searchResultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    int length = searchParams.GetSeed(lane, seed);
                    // PERFORMANCE: Use thread-local reporting instead of Interlocked + immediate I/O
                    ReportSeedThreadLocal(new Span<char>(seed, length));
                }
            }
        }

        private void BatchSeeds(int filterIndex, VectorMask searchResultMask, in MotelySearchContextParams searchParams)
        {
            FilterSeedBatch* filterBatch = &_filterSeedBatches[filterIndex];

            Debug.Assert(searchResultMask.IsPartiallyTrue(), "Mask should be checked for partial truth before calling enqueue seeds (for performance).");

            for (int lane = 0; lane < Vector512<double>.Count; lane++)
            {
                if (searchResultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    int seedBatchIndex = filterBatch->SeedCount;

                    if (seedBatchIndex == 0)
                    {
                        filterBatch->SeedLength = searchParams.SeedLength;

                        // This will track how long this seed has been waiting for, and if it is waiting for
                        //  too long we'll search it even if the batch is not full
                        filterBatch->WaitStartMS = Search._elapsedTime.ElapsedMilliseconds;
                    }
                    else
                    {
                        // Each batch can only contain seeds of the same length, we should check if this seed can go into the batch
                        if (filterBatch->SeedLength != searchParams.SeedLength)
                        {
                            // This seed is a different length to the ones already in the batch :c
                            // Let's flush the batch and start again.
                            SearchFilterBatch(filterIndex, filterBatch);

                            Debug.Assert(filterBatch->SeedCount == 0, "Searching the batch should have reset it.");
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
                            ((double*)&filterBatch->SeedCharacters)[i * Vector512<double>.Count + seedBatchIndex] =
                                ((double*)searchParams.SeedLastCharacters)[i * Vector512<double>.Count + lane];
                        }

                        for (; i < searchParams.SeedLength; i++)
                        {
                            ((double*)&filterBatch->SeedCharacters)[i * Vector512<double>.Count + seedBatchIndex] =
                               searchParams.SeedFirstCharacters[i - searchParams.SeedLastCharactersLength];
                        }
                    }

                    // Store the cached hashes
                    for (int i = 0; i < Search._pseudoHashKeyLengthCount; i++)
                    {
                        int partialHashLength = Search._pseudoHashKeyLengths[i];
                        
                        ((double*)filterBatch->SeedHashes)[i * Vector512<double>.Count + seedBatchIndex] =
                            ((double*)searchParams.SeedHashCache->Cache[partialHashLength])[i * Vector512<double>.Count + lane];
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
        private void SearchFilterBatch(int filterIndex, FilterSeedBatch* filterBatch)
        {
            Debug.Assert(filterBatch->SeedCount != 0);

            // Clear ALL characters in unused lanes to prevent garbage data
            for (int i = filterBatch->SeedCount; i < Vector512<double>.Count; i++)
            {
                for (int j = 0; j < filterBatch->SeedLength; j++)
                {
                    ((double*)&filterBatch->SeedCharacters)[j * Vector512<double>.Count + i] = 0;
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

            MotelyVectorSearchContext searchContext = new(in Search._searchParameters, in searchParams);

            DebugLogger.Log($"[BATCH] About to call additional filter {filterIndex}");
            VectorMask searchResultMask = Search._additionalFilters[filterIndex].Filter(ref searchContext);
            DebugLogger.Log($"[BATCH] Additional filter {filterIndex} returned mask: {searchResultMask.Value:X}");

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
                    // Otherwise, we batch the seeds up for the next filter :3
                    BatchSeeds(nextFilterIndex, searchResultMask, in searchParams);
                }
            }

            // Reset the batch
            filterBatch->SeedCount = 0;
            filterBatch->SeedHashCache.Reset();
        }

        public void Dispose()
        {
            Thread.Join();

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

        public MotelyProviderSearchThread(MotelySearch<TBaseFilter> search, MotelySearchSettings<TBaseFilter> settings, int index) : base(search, index)
        {

            if (settings.SeedProvider == null)
                throw new ArgumentException("Cannot create a provider search without a seed provider.");

            SeedProvider = settings.SeedProvider;

            MaxBatch = (SeedProvider.SeedCount + (long)(Motely.MaxVectorWidth - 1)) / (long)Motely.MaxVectorWidth;
            SeedsPerBatch = (long)Motely.MaxVectorWidth;

            _hashes = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * search._pseudoHashKeyLengthCount);

            _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
            *_hashCache = new PartialSeedHashCache(search, _hashes);

            _seedCharacterMatrix = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Motely.MaxSeedLength);
        }

        protected override void SearchBatch(long batchIdx)
        {
            // Calculate how many seeds remain for this batch
            long seedsProcessedSoFar = batchIdx * Motely.MaxVectorWidth;
            long seedsRemainingInProvider = Math.Max(0, SeedProvider.SeedCount - seedsProcessedSoFar);
            
            // If we have fewer than 8 seeds remaining, search them individually
            if (seedsRemainingInProvider < Motely.MaxVectorWidth)
            {
                for (int i = 0; i < seedsRemainingInProvider; i++)
                {
                    SearchSingleSeed(SeedProvider.NextSeed());
                }
                return;
            }

            // The length of all the seeds
            int* seedLengths = stackalloc int[Motely.MaxVectorWidth];

            // Are all the seeds the same length?
            bool homogeneousSeedLength = true;

            int actualSeedCount = 0;
            for (int seedIdx = 0; seedIdx < Motely.MaxVectorWidth; seedIdx++)
            {
                ReadOnlySpan<char> seed = SeedProvider.NextSeed();

                // If we get an empty seed, we've run out of seeds to process
                if (seed.IsEmpty || seed.Length == 0)
                {
                    // If we have no seeds at all, mark as completed and return
                    if (seedIdx == 0)
                    {
                        Search._status = MotelySearchStatus.Completed;
                        return;
                    }
                    // Otherwise, process the seeds we have so far
                    break;
                }

                seedLengths[seedIdx] = seed.Length;

                if (seedLengths[0] != seed.Length)
                    homogeneousSeedLength = false;

                for (int i = 0; i < seed.Length; i++)
                {
                    ((double*)_seedCharacterMatrix)[i * Motely.MaxVectorWidth + seedIdx] = seed[i];
                }
                actualSeedCount++;
            }


            if (homogeneousSeedLength)
            {
                // If all the seeds are the same length, we can be fast and vectorize!
                int seedLength = seedLengths[0];

                // Calculate the partial psuedohash cache
                for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < Search._pseudoHashKeyLengthCount; pseudohashKeyIdx++)
                {
                    int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                    Vector512<double> numVector = Vector512<double>.One;

                    for (int i = seedLength - 1; i >= 0; i--)
                    {
                        numVector = Vector512.Divide(Vector512.Create(1.1239285023), numVector);

                        numVector = Vector512.Multiply(numVector, _seedCharacterMatrix[i]);

                        numVector = Vector512.Multiply(numVector, Math.PI);
                        numVector = Vector512.Add(numVector, Vector512.Create((i + pseudohashKeyLength + 1) * Math.PI));

                        Vector512<double> intPart = Vector512.Floor(numVector);
                        numVector = Vector512.Subtract(numVector, intPart);
                    }

                    _hashes[pseudohashKeyIdx] = numVector;
                }

                SearchSeeds(new MotelySearchContextParams(
                    _hashCache,
                    seedLength,
                    0, null,
                    _seedCharacterMatrix
                ));
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
                        seed[j] = (char)((double*)_seedCharacterMatrix)[j * Motely.MaxVectorWidth + i];
                    }

                    SearchSingleSeed(seed[..seedLength]);
                }

            }
        }

        private void SearchSingleSeed(ReadOnlySpan<char> seed)
        {
            // Skip empty seeds (indicates we've run out of seeds in the list)
            if (seed.IsEmpty || seed.Length == 0)
                return;
                
            char* seedLastCharacters = stackalloc char[Motely.MaxSeedLength - 1];

            // Calculate the partial psuedohash cache
            for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < Search._pseudoHashKeyLengthCount; pseudohashKeyIdx++)
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                double num = 1;

                for (int i = seed.Length - 1; i >= 0; i--)
                {
                    num = (1.1239285023 / num * seed[i] * Math.PI + (i + pseudohashKeyLength + 1) * Math.PI) % 1;
                }

                _hashes[pseudohashKeyIdx] = Vector512.Create(num);
            }

            for (int i = 0; i < seed.Length - 1; i++)
            {
                seedLastCharacters[i] = seed[i + 1];
            }

            Vector512<double> firstCharacterVector = Vector512.CreateScalar((double)seed[0]);

            SearchSeeds(new MotelySearchContextParams(
                _hashCache,
                seed.Length,
                seed.Length - 1,
                seedLastCharacters,
                &firstCharacterVector
            ));
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
        private static readonly Vector512<double>[] SeedDigitVectors = new Vector512<double>[(Motely.SeedDigits.Length + Motely.MaxVectorWidth - 1) / Motely.MaxVectorWidth];

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

        public MotelySequentialSearchThread(MotelySearch<TBaseFilter> search, MotelySearchSettings<TBaseFilter> settings, int index) : base(search, index)
        {
            _digits = (char*)Marshal.AllocHGlobal(sizeof(char) * Motely.MaxSeedLength);

            _batchCharCount = settings.SequentialBatchCharacterCount;
            SeedsPerBatch = (long)Math.Pow(Motely.SeedDigits.Length, _batchCharCount);

            _nonBatchCharCount = Motely.MaxSeedLength - _batchCharCount;
            MaxBatch = (long)Math.Pow(Motely.SeedDigits.Length, _nonBatchCharCount);

            // Safety check for pseudoHashKeyLengthCount to prevent null pointer issues
            if (Search._pseudoHashKeyLengthCount <= 0)
            {
                throw new InvalidOperationException($"Invalid pseudoHashKeyLengthCount: {Search._pseudoHashKeyLengthCount}. Search may not be properly initialized.");
            }

            _hashes = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Search._pseudoHashKeyLengthCount * (_batchCharCount + 1));

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

            Vector512<double>* hashes = &_hashes[_batchCharCount * Search._pseudoHashKeyLengthCount];

            // Calculate hash for the first digits at all the required pseudohash lengths
            for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < Search._pseudoHashKeyLengthCount; pseudohashKeyIdx++)
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                double num = 1;

                for (int i = Motely.MaxSeedLength - 1; i > _batchCharCount - 1; i--)
                {
                    num = (1.1239285023 / num * _digits[i] * Math.PI + (i + pseudohashKeyLength + 1) * Math.PI) % 1;
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

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void SearchVector(int i, Vector512<double> seedDigitVector, Vector512<double>* nums, int numsLaneIndex)
        {
            // Check for cancellation/disposal periodically to make large batches responsive
            if (Search._status == MotelySearchStatus.Disposed || Search._status == MotelySearchStatus.Paused)
            {
                return;
            }

            Vector512<double>* hashes = &_hashes[i * Search._pseudoHashKeyLengthCount];

            for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < Search._pseudoHashKeyLengthCount; pseudohashKeyIdx++)
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];
                Vector512<double> calcVector = Vector512.Create(1.1239285023 / ((double*)&nums[pseudohashKeyIdx])[numsLaneIndex]);

                calcVector = Vector512.Multiply(calcVector, seedDigitVector);

                calcVector = Vector512.Multiply(calcVector, Math.PI);
                calcVector = Vector512.Add(calcVector, Vector512.Create((i + pseudohashKeyLength + 1) * Math.PI));

                Vector512<double> intPart = Vector512.Floor(calcVector);
                calcVector = Vector512.Subtract(calcVector, intPart);

                hashes[pseudohashKeyIdx] = calcVector;
            }

            if (i == 0)
            {
                SearchSeeds(new MotelySearchContextParams(
                    _hashCache, Motely.MaxSeedLength, Motely.MaxSeedLength - 1, &_digits[1], &seedDigitVector
                ));
            }
            else
            {
                for (int lane = 0; lane < Motely.MaxVectorWidth; lane++)
                {
                    if (seedDigitVector[lane] == 0) break;

                    _digits[i] = (char)seedDigitVector[lane];

                    for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
                    {
                        SearchVector(i - 1, SeedDigitVectors[vectorIndex], hashes, lane);
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