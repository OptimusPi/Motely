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
    public long SeedCount { get; }
    public ReadOnlySpan<char> NextSeed();
}

public sealed class MotelyRandomSeedProvider(long count) : IMotelySeedProvider
{
    public long SeedCount { get; } = count;

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

    public long SeedCount => Seeds.Length;

    private long _currentSeed = -1;
    public ReadOnlySpan<char> NextSeed()
    {
        var index = Interlocked.Increment(ref _currentSeed);
        if (index >= Seeds.Length)
            return ReadOnlySpan<char>.Empty;
        return Seeds[index];
    }
}

public sealed class MotelySearchSettings<TBaseFilter>(IMotelySeedFilterDesc<TBaseFilter> baseFilterDesc)
    where TBaseFilter : struct, IMotelySeedFilter
{
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
    public long StartBatchIndex { get; set; } = -1;
    // Exclusive upper bound on batch indices processed. Defaults to unlimited.
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
    
    /// <summary>
    /// Silent mode - skip console output for matching seeds
    /// </summary>
    public bool Silent { get; set; } = false;
    
    /// <summary>
    /// Callback for handling search results with custom formatting
    /// </summary>
    public Action<string, int, int[]>? ResultCallback { get; set; }
    
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

    public MotelySearchSettings<TBaseFilter> WithSilent(bool silent)
    {
        Silent = silent;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithResultCallback(Action<string, int, int[]> callback)
    {
        ResultCallback = callback;
        return this;
    }

    public MotelySearchSettings<TBaseFilter> WithProgressCallback(Action<long, long, long, double> callback)
    {
        ProgressCallback = callback;
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
    public long TotalSeedsSearched { get; }
    public long MatchingSeeds { get; }
    public TimeSpan ElapsedTime { get; }

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
    private readonly Action<long, long, long, double>? _progressCallback;
    private readonly int _batchCharacterCount;
    internal readonly bool _silent;

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
    private readonly long _endBatchIndex;
    // Internal counters stored as long for Interlocked; exposed as long.
    private long _batchIndex;
    public long BatchIndex => _batchIndex;
    private long _completedBatchIndex;
    public long CompletedBatchCount => _completedBatchIndex;
    private long _totalSeedsSearched;
    public long TotalSeedsSearched => _totalSeedsSearched;
    public TimeSpan ElapsedTime => _elapsedTime.Elapsed;
    private long _matchingSeeds;
    public long MatchingSeeds => _matchingSeeds;

    private double _lastReportMS;

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
        _silent = settings.Silent;

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
            filterCreationContext.IsAdditionalFilter = true;

            for (int i = 0; i < _additionalFilters.Length; i++)
            {
                var filterDesc = settings.AdditionalFilters[i];
                if (filterDesc == null)
                {
                    throw new InvalidOperationException($"settings.AdditionalFilters[{i}] is null!");
                }
                var filter = filterDesc.CreateFilter(ref filterCreationContext);
                DebugLogger.Log($"Created additional filter {i}: {filter} (type: {filter?.GetType()})");
                if (filter == null)
                {
                    throw new InvalidOperationException($"CreateFilter returned null for additional filter {i}");
                }
                _additionalFilters[i] = filter;
                
                // Double-check it was stored correctly
                if (_additionalFilters[i] == null)
                {
                    throw new InvalidOperationException($"Failed to store additional filter {i} in array - boxing issue?");
                }
            }
        }

        // Create the score provider if one was specified
        if (settings.SeedScoreDesc != null)
        {
            _scoreProvider = settings.SeedScoreDesc.CreateScoreProvider(ref filterCreationContext);
        }

        _startBatchIndex = settings.StartBatchIndex;
        _endBatchIndex = settings.EndBatchIndex;
        _batchIndex = _startBatchIndex; // first Interlocked.Increment claims start+1
        _completedBatchIndex = (long)_startBatchIndex;

        int[] pseudohashKeyLengths = [.. filterCreationContext.CachedPseudohashKeyLengths];
        _pseudoHashKeyLengthCount = pseudohashKeyLengths.Length;
        
        // Debug logging
        DebugLogger.Log($"[MotelySearch] Cached pseudohash keys: {_pseudoHashKeyLengthCount}");
        for (int i = 0; i < _pseudoHashKeyLengthCount; i++)
        {
            DebugLogger.Log($"  - Key {i}: length {pseudohashKeyLengths[i]}");
        }
        
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

    private void ReportSeed(ReadOnlySpan<char> seed)
    {
        // Simple seed output for normal Motely filters
        // TODO: This should go through the score provider if one exists
        
        // Increment matching seeds counter
        Interlocked.Increment(ref _matchingSeeds);
        
        // Skip console output if in silent mode
        if (_silent)
            return;
        
        // Clear the progress line first
        Console.Write("\r");
        
        // Print the seed
        Console.WriteLine(seed.ToString());
    }
    
    // Helper function to make filter result callback that outputs CSV with scores
    public static Action<string, int, int[]> MakeCsvResultCallback()
    {
        return (seed, totalScore, tallies) =>
        {
            // Fast CSV formatting: Seed,TotalScore,Tally1,Tally2,...
            var sb = new System.Text.StringBuilder(seed.Length + 16 + tallies.Length * 4);
            sb.Append(seed).Append(',').Append(totalScore);
            foreach (var tally in tallies)
            {
                sb.Append(',').Append(tally);
            }
            
            // Clear the progress line first by overwriting with spaces
            Console.Write($"\r{new string(' ', Console.WindowWidth - 1)}\r");
            
            // Now print the seed result on a clean line
            Console.WriteLine(sb.ToString());
        };
    }

    // Store the last progress line so we can restore it after CSV output
    private static string _lastProgressLine = "";
    private static readonly object _progressLock = new object();
    
    public static void RestoreProgressLine()
    {
        lock (_progressLock)
        {
            if (!string.IsNullOrEmpty(_lastProgressLine))
            {
                Console.Write($"\r{_lastProgressLine}");
            }
        }
    }
    
    [MethodImpl(MethodImplOptions.Synchronized)]
    private void PrintReport()
    {
        double elapsedMS = _elapsedTime.ElapsedMilliseconds;
        if (elapsedMS - _lastReportMS < 1000) return;
        _lastReportMS = elapsedMS;
        
        long thisCompletedCount = _completedBatchIndex - _startBatchIndex;

        // Determine effective exclusive end of range (handles configured end batch and thread max)
        // Use actual endBatch parameter instead of total possible batches
        long effectiveMaxExclusive = _endBatchIndex != long.MaxValue ? _endBatchIndex : _threads[0].MaxBatch;
        if (effectiveMaxExclusive <= _startBatchIndex) // fallback guard
            effectiveMaxExclusive = _startBatchIndex + 1;

        long totalSpan = effectiveMaxExclusive - _startBatchIndex; // number of batches in range
        if (totalSpan == 0) totalSpan = 1; // prevent divide by 0

        long clampedCompleted = Math.Min(thisCompletedCount, totalSpan);

        double totalPortionFinished = (double)clampedCompleted / totalSpan;
        double thisPortionFinished = totalPortionFinished; // identical now but kept for clarity if diff logic later
        double totalTimeEstimate = thisPortionFinished <= 0 ? double.PositiveInfinity : elapsedMS / thisPortionFinished;
        double timeLeft = totalTimeEstimate - elapsedMS;

        string timeLeftFormatted;
        bool invalid = double.IsNaN(timeLeft) || double.IsInfinity(timeLeft) || timeLeft < 1;
        // Clamp to max TimeSpan if too large - for very slow searches
        if (invalid)
        {
            timeLeftFormatted = "--:--:--";
        }
        else
        {
            TimeSpan timeLeftSpan = TimeSpan.FromMilliseconds(Math.Min(timeLeft, TimeSpan.MaxValue.TotalMilliseconds));
            if (timeLeftSpan.Days == 0) timeLeftFormatted = $"{timeLeftSpan:hh\\:mm\\:ss}";
            else timeLeftFormatted = $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";
        }

        // Calculate seeds per millisecond using ACTUAL seeds searched
        double seedsPerMS = 0;
        long actualSeedsSearched = _totalSeedsSearched;
        if (elapsedMS > 1 && actualSeedsSearched > 0)
            seedsPerMS = actualSeedsSearched / elapsedMS;

        double pct = Math.Clamp(totalPortionFinished * 100, 0, 100);

        // Calculate rarity with appropriate units if we have results
        string rarityStr = "";
        string rarityEmoji = "🌱";
        string rarityMoniker = "Filtering...";
        var resultsFound = MotelyJsonSeedScoreDesc.ResultsFound;
        
        if (resultsFound > 0)
        {
            long totalSeedsSearched = actualSeedsSearched;
            double rarityPercent = ((double)resultsFound / totalSeedsSearched) * 100.0;

            if (rarityPercent >= 1.0)
            {
                rarityStr = $" | {rarityPercent:F2}%";
                rarityMoniker = "Warming Up";
                rarityEmoji = "♻️";
            }
            else if (rarityPercent >= 0.1)
            {
                double perMille = rarityPercent * 10.0;
                rarityStr = $" | {perMille:F3}‰";
                rarityMoniker = "Filtering";
                rarityEmoji = "🌱";
            }
            else
            {
                double perTenThousand = rarityPercent * 100.0;
                rarityMoniker = perTenThousand switch
                {
                    < 0.000169 => "God Tier",
                    < 0.000269 => "Mythical",
                    < 0.000314 => "Legendary",
                    < 0.00314 => "Rare",
                    < 0.0314 => "Uncommon",
                    _ => "Common"
                };
                rarityEmoji = perTenThousand switch
                {
                    < 0.000169 => "😇",
                    < 0.000269 => "🦄",
                    < 0.000314 => "🏆",
                    < 0.00314 => "🥇",
                    < 0.0314 => "🥈",
                    _ => "🥉"
                };
                rarityStr = $" | {perTenThousand:F5}‱";
            }
        }

        // Simple spinner animation - updates with each progress report
        string[] spinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧"];
        var spinner = spinnerFrames[(int)(elapsedMS / 250) % spinnerFrames.Length];

        // Build the progress line
        string progressLine = $"{spinner} {pct:F2}% | {timeLeftFormatted} remaining | {Math.Round(seedsPerMS)} seeds/ms{rarityStr} | {rarityEmoji} {rarityMoniker}";
        
        // Save and display the progress line
        lock (_progressLock)
        {
            _lastProgressLine = progressLine;
        }
        
        // Use carriage return to overwrite the current line
        Console.Write($"\r{progressLine}");
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

        public const int MAX_SEED_WAIT_MS = 99999;

        public readonly MotelySearch<TBaseFilter> Search;
        public readonly int ThreadIndex;
        public readonly Thread Thread;

        public long MaxBatch { get; internal set; }
        public long SeedsPerBatch { get; internal set; }
        
        // Thread-local counters to avoid atomic operations
        protected long ThreadLocalMatchingSeeds = 0;
        protected long ThreadLocalTotalSeedsSearched = 0;

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


            if (search._additionalFilters.Length != 0)
            {
                _filterSeedBatches = (FilterSeedBatch*)Marshal.AllocHGlobal(sizeof(FilterSeedBatch) * search._additionalFilters.Length);

                for (int i = 0; i < search._additionalFilters.Length; i++)
                {
                    FilterSeedBatch* batch = &_filterSeedBatches[i];

                    *batch = new()
                    {
                        SeedHashes = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Motely.MaxCachedPseudoHashKeyLength),
                    };

                    batch->SeedHashCache = new(search, batch->SeedHashes);
                }
            }
            Thread.Start();
        }

        private void ThreadMain()
        {
            // Stats update timer - only update global stats every 1000ms
            long lastStatsUpdateTime = Environment.TickCount64;
            const long STATS_UPDATE_INTERVAL_MS = 1000;
            
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

                        // We need to search any batches which have yet to be fully searched
                        for (int i = 0; i < Search._additionalFilters.Length; i++)
                        {
                            FilterSeedBatch* batch = &_filterSeedBatches[i];

                            if (batch->SeedCount != 0)
                            {
                                SearchFilterBatch(i, batch);
                            }
                        }

                        // Final consolidation of thread-local counters before thread exits
                        Interlocked.Add(ref Search._totalSeedsSearched, ThreadLocalTotalSeedsSearched);
                        Interlocked.Add(ref Search._matchingSeeds, ThreadLocalMatchingSeeds);
                        
                        // Assert we've reached either MaxBatch or the configured end batch
                        // Note: In Provider mode (list search), early completion due to filter elimination
                        // is valid and doesn't require reaching the full batch count
                        long effectiveEnd = Search._endBatchIndex != 0 ? Search._endBatchIndex : MaxBatch;
                        bool isProviderMode = this is MotelyProviderSearchThread;
                        Debug.Assert(Search._batchIndex >= effectiveEnd || isProviderMode);
                        return;

                    case MotelySearchStatus.Disposed:
                        // Final consolidation if disposing
                        Interlocked.Add(ref Search._totalSeedsSearched, ThreadLocalTotalSeedsSearched);
                        Interlocked.Add(ref Search._matchingSeeds, ThreadLocalMatchingSeeds);
                        return;
                }

                // Get next batch - only ONE Interlocked op here!
                long batchIdx = Interlocked.Increment(ref Search._batchIndex);

                // Check EndBatchIndex (exclusive) BEFORE doing work
                if (batchIdx >= Search._endBatchIndex)
                {
                    // Clamp and finish
                    Interlocked.Exchange(ref Search._batchIndex, Search._endBatchIndex);
                    Search._status = MotelySearchStatus.Completed;
                    continue;
                }

                if (batchIdx > MaxBatch)
                {
                    Interlocked.Exchange(ref Search._batchIndex, MaxBatch);
                    Search._status = MotelySearchStatus.Completed;
                    continue;
                }

                SearchBatch(batchIdx);
                
                long completed = Interlocked.Increment(ref Search._completedBatchIndex);
                
                // Only update stats periodically (every 1000ms) to avoid contention
                long currentTime = Environment.TickCount64;
                if ((currentTime - lastStatsUpdateTime) >= STATS_UPDATE_INTERVAL_MS)
                {
                    Interlocked.Add(ref Search._totalSeedsSearched, ThreadLocalTotalSeedsSearched);
                    Interlocked.Add(ref Search._matchingSeeds, ThreadLocalMatchingSeeds);
                    ThreadLocalTotalSeedsSearched = 0;
                    ThreadLocalMatchingSeeds = 0;
                    lastStatsUpdateTime = currentTime;
                }
                
                
                // Report progress if callback is set
                if (Search._progressCallback != null)
                {
                    var elapsedMs = Search._elapsedTime.Elapsed.TotalMilliseconds;
                    if (elapsedMs % 1000 < 1)
                    {
                        long seedsSearched = Search._totalSeedsSearched;
                        var seedsPerMs = elapsedMs > 0 ? (double)seedsSearched / elapsedMs : 0;
                        // Use actual max batch count if endBatchIndex is unlimited
                        var effectiveEnd = Search._endBatchIndex == long.MaxValue ? MaxBatch : Search._endBatchIndex;
                        var total = effectiveEnd - Search._startBatchIndex;
                        var completedCount = completed - Search._startBatchIndex;

                        Search._progressCallback(completedCount, total, seedsSearched, seedsPerMs);
                    }
                }
                
                

                if (Search._additionalFilters.Length != 0)
                {
                    // Check to see if any batches have been waited for too long to be processed
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
                            }
                        }
                    }
                }

                if ((long)completed >= Search._endBatchIndex)
                {
                    Search._status = MotelySearchStatus.Completed;
                }

                // Print progress even with scoring, but less frequently
                Search.PrintReport();
            }

        }

    protected abstract void SearchBatch(long batchIdx);

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        protected void SearchSeeds(in MotelySearchContextParams searchContextParams)
        {
            // This is the method for searching the base filter, we should not be searching additional filters
            Debug.Assert(!searchContextParams.IsAdditionalFilter);

            // Count how many seeds we're actually searching in this batch (thread-local)
            int seedCount = 0;
            for (int lane = 0; lane < Vector512<double>.Count; lane++)
            {
                if (searchContextParams.IsLaneValid(lane))
                    seedCount++;
            }
            ThreadLocalTotalSeedsSearched += seedCount;

            MotelyVectorSearchContext searchContext = new(in Search._searchParameters, in searchContextParams);

            VectorMask searchResultMask = Search._baseFilter.Filter(ref searchContext);

            if (searchResultMask.IsPartiallyTrue())
            {
                if (Search._additionalFilters.Length == 0)
                {
                    // ReportSeeds handles score provider routing automatically
                    ReportSeeds(searchResultMask, in searchContextParams);
                }
                else
                {
                    // Otherwise, we need to queue up the seeds for the first additional filter.
                    BatchSeeds(0, searchResultMask, in searchContextParams);
                }
            }

            searchContextParams.SeedHashCache->Reset();
        }

        // SMART REPORTING: Routes to score provider if available, falls back to basic seeds
        private void ReportSeeds(VectorMask searchResultMask, in MotelySearchContextParams searchParams)
        {
            Debug.Assert(searchResultMask.IsPartiallyTrue(), "Mask should be checked for partial truth before calling report seeds (for performance).");

            DebugLogger.Log($"🔍 ReportSeeds called with mask {searchResultMask.Value}");
            
            // SMART ROUTING: Check if we should use score provider
            if (Search.TryGetScoreProvider(out var scoreProvider))
            {
                DebugLogger.Log($"🎯 SMART ROUTING: Using score provider for rich results");
                
                // Create search context for scoring
                MotelyVectorSearchContext searchContext = new(in Search._searchParameters, in searchParams);
                
                // Score the seeds and get mask of results - USE ACTUAL CUTOFF!
                var scoredMask = scoreProvider.Score(ref searchContext, _resultBuffer, searchResultMask, scoreThreshold: 0); // Pass real cutoff
                DebugLogger.Log($"🔍 Scoring returned mask {scoredMask.Value}");
                
                // Report scored results from buffer
                ReportScoredResults(scoredMask, in searchParams);
            }
            else
            {
                DebugLogger.Log($"🔍 NO SCORE PROVIDER: Reporting basic seeds");
                // No score provider - report basic seeds
                ReportBasicSeeds(searchResultMask, in searchParams);
            }
        }
        
        private void ReportScoredResults(VectorMask resultMask, in MotelySearchContextParams searchParams)
        {
            char* seed = stackalloc char[Motely.MaxSeedLength];
            
            for (int lane = 0; lane < Vector512<double>.Count; lane++)
            {
                if (resultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    ThreadLocalMatchingSeeds++;
                    
                    if (!Search._silent)
                    {
                        Console.Write("\r");
                        
                        var bufferedResult = _resultBuffer[lane];
                        DebugLogger.Log($"🔍 Lane {lane}: Seed='{bufferedResult.Seed}', Score={bufferedResult.Score}");
                        
                        if (!string.IsNullOrEmpty(bufferedResult.Seed))
                        {
                            var tallies = bufferedResult.TallyColumns != null ? string.Join(",", bufferedResult.TallyColumns) : "";
                            DebugLogger.Log($"🎉 OUTPUT: {bufferedResult.Seed},{bufferedResult.Score},{tallies}");
                            Console.WriteLine($"{bufferedResult.Seed},{bufferedResult.Score},{tallies}");
                        }
                        else
                        {
                            DebugLogger.Log($"⚠️ Empty buffer at lane {lane} - using basic seed");
                            int length = searchParams.GetSeed(lane, seed);
                            Console.WriteLine(new Span<char>(seed, length).ToString());
                        }
                    }
                }
            }
        }
        
        private void ReportBasicSeeds(VectorMask searchResultMask, in MotelySearchContextParams searchParams)
        {
            char* seed = stackalloc char[Motely.MaxSeedLength];

            for (int lane = 0; lane < Vector512<double>.Count; lane++)
            {
                if (searchResultMask[lane] && searchParams.IsLaneValid(lane))
                {
                    ThreadLocalMatchingSeeds++;
                    
                    if (!Search._silent)
                    {
                        Console.Write("\r");
                        int length = searchParams.GetSeed(lane, seed);
                        Console.WriteLine(new Span<char>(seed, length).ToString());
                    }
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
            Debug.Assert(filterBatch->SeedCount != 0, "Batch should have seeds");

            // Zero out unused lanes for partial batches (Tacodiva's approach)
            for (int i = filterBatch->SeedCount; i < Vector512<double>.Count; i++)
            {
                ((double*)&filterBatch->SeedCharacters)[i] = 0;
            }

            // Create search params for the batch (full or partial)
            MotelySearchContextParams searchParams = new(
                &filterBatch->SeedHashCache,
                filterBatch->SeedLength,
                0, null,
                (Vector512<double>*)&filterBatch->SeedCharacters
            );

            MotelyVectorSearchContext searchContext = new(in Search._searchParameters, in searchParams);
            
            // Run the filter on the batch
            // Safety check - this should never happen but let's be defensive
            if (Search._additionalFilters == null || filterIndex >= Search._additionalFilters.Length)
            {
                throw new InvalidOperationException($"SearchFilterBatch called with invalid filterIndex {filterIndex} (additionalFilters length: {Search._additionalFilters?.Length ?? 0})");
            }
            
            var filter = Search._additionalFilters[filterIndex];
            if (filter == null)
            {
                throw new InvalidOperationException($"Additional filter at index {filterIndex} is null!");
            }
            
            VectorMask searchResultMask;
            try
            {
                searchResultMask = filter.Filter(ref searchContext);
            }
            catch (NullReferenceException ex)
            {
                throw new InvalidOperationException($"Filter.Filter() threw NullReferenceException. Filter type: {filter.GetType()}, Index: {filterIndex}", ex);
            }

            if (searchResultMask.IsPartiallyTrue())
            {
                int nextFilterIndex = filterIndex + 1;

                if (nextFilterIndex == Search._additionalFilters.Length)
                {
                    // ReportSeeds handles score provider routing automatically  
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
            if (!Thread.Join(1000))
            {
                // Force interrupt if it doesn't finish
                try
                {
                    Thread.Interrupt();
                    Thread.Join(1000);
                }
                catch { }
            }

            for (int i = 0; i < Search._additionalFilters.Length; i++)
            {
                _filterSeedBatches[i].SeedHashCache.Dispose();
                Marshal.FreeHGlobal((nint)_filterSeedBatches[i].SeedHashes);
            }

            Marshal.FreeHGlobal((nint)_filterSeedBatches);
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

            MaxBatch = (SeedProvider.SeedCount + (long)(Vector512<double>.Count - 1)) / (long)Vector512<double>.Count;
            SeedsPerBatch = (long)Vector512<double>.Count;

            // Ensure we allocate at least 1 element to avoid null pointer issues
            _hashes = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Math.Max(1, search._pseudoHashKeyLengthCount));

            _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
            *_hashCache = new PartialSeedHashCache(search, _hashes);

            _seedCharacterMatrix = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Motely.MaxSeedLength);
        }

        protected override void SearchBatch(long batchIdx)
        {
            // If this is the last batch, check if we have enough seeds to fill a vector.
            if (batchIdx == MaxBatch && SeedProvider.SeedCount != MaxBatch * (long)Vector512<double>.Count)
            {
                // If we don't, search the last seeds individually
                for (long i = 0; i < SeedProvider.SeedCount - (MaxBatch - 1) * (long)Vector512<double>.Count; i++)
                {
                    SearchSingleSeed(SeedProvider.NextSeed());
                }

                return;
            }

            // The length of all the seeds
            int* seedLengths = stackalloc int[Vector512<double>.Count];

            // Are all the seeds the same length?
            bool homogeneousSeedLength = true;

            for (int seedIdx = 0; seedIdx < Vector512<double>.Count; seedIdx++)
            {
                ReadOnlySpan<char> seed = SeedProvider.NextSeed();

                seedLengths[seedIdx] = seed.Length;

                if (seedLengths[0] != seed.Length)
                    homogeneousSeedLength = false;

                for (int i = 0; i < seed.Length; i++)
                {
                    ((double*)_seedCharacterMatrix)[i * Vector512<double>.Count + seedIdx] = seed[i];
                }
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

                for (int i = 0; i < Vector512<double>.Count; i++)
                {
                    int seedLength = seedLengths[i];

                    for (int j = 0; j < seedLength; j++)
                    {
                        seed[j] = (char)((double*)_seedCharacterMatrix)[j * Vector512<double>.Count + i];
                    }

                    SearchSingleSeed(seed[..seedLength]);
                }

            }
        }

        private void SearchSingleSeed(ReadOnlySpan<char> seed)
        {
            if (seed.Length == 0) return;
            char* seedLastCharacters = stackalloc char[Motely.MaxSeedLength];

            // Calculate the partial psuedohash cache
            for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < Search._pseudoHashKeyLengthCount; pseudohashKeyIdx++)
            {
                if (Search._pseudoHashKeyLengths == null)
                {
                    throw new InvalidOperationException($"_pseudoHashKeyLengths is null!");
                }
                if (pseudohashKeyIdx >= Search._pseudoHashKeyLengthCount)
                {
                    throw new InvalidOperationException($"pseudohashKeyIdx {pseudohashKeyIdx} >= _pseudoHashKeyLengthCount {Search._pseudoHashKeyLengthCount}");
                }
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
        private static readonly Vector512<double>[] SeedDigitVectors = new Vector512<double>[(Motely.SeedDigits.Length + Vector512<double>.Count - 1) / Vector512<double>.Count];

        static MotelySequentialSearchThread()
        {
            InitializeSeedDigitVectors();
        }
        
        private static void InitializeSeedDigitVectors()
        {
            Span<double> vector = stackalloc double[Vector512<double>.Count];

            for (int i = 0; i < SeedDigitVectors.Length; i++)
            {
                for (int j = 0; j < Vector512<double>.Count; j++)
                {
                    int index = i * Vector512<double>.Count + j;

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

            // Ensure we allocate at least 1 element to avoid null pointer issues
            int hashCount = Math.Max(1, Search._pseudoHashKeyLengthCount) * (_batchCharCount + 1);
            _hashes = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * hashCount);

            _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
            *_hashCache = new PartialSeedHashCache(search, &_hashes[0]);
        }
    
    protected override void SearchBatch(long batchIdx)
        {
            // Figure out which digits this search is doing
            for (int i = _nonBatchCharCount - 1; i >= 0; i--)
            {
                var charIndex = batchIdx % (long)Motely.SeedDigits.Length;
                _digits[Motely.MaxSeedLength - i - 1] = Motely.SeedDigits[charIndex];
                batchIdx /= (long)Motely.SeedDigits.Length;
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

            // Start searching with bounds safety
            for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
            {
                if (vectorIndex >= 0 && vectorIndex < SeedDigitVectors.Length) // Extra safety check
                {
                    SearchVector(_batchCharCount - 1, SeedDigitVectors[vectorIndex], hashes, 0);
                }
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
                for (int lane = 0; lane < Vector512<double>.Count; lane++)
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