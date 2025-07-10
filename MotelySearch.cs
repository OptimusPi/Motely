using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;


namespace Motely;

public ref struct MotelyFilterCreationContext
{
    private readonly HashSet<int> _cachedPseudohashKeyLengths;
    public readonly IReadOnlyCollection<int> CachedPseudohashKeyLengths => _cachedPseudohashKeyLengths;

    public MotelyFilterCreationContext()
    {
        _cachedPseudohashKeyLengths = [0];
    }

    public readonly void RemoveCachedPseudoHash(int keyLength)
    {
        _cachedPseudohashKeyLengths.Remove(keyLength);
    }

    public readonly void RemoveCachedPseudoHash(string key)
    {
        RemoveCachedPseudoHash(key.Length);
    }

    public readonly void CachePseudoHash(int keyLength)
    {
        _cachedPseudohashKeyLengths.Add(keyLength);
    }

    public readonly void CachePseudoHash(string key)
    {
        CachePseudoHash(key.Length);
    }

    private readonly void CacheResampleStream(string key)
    {
        CachePseudoHash(key);
        CachePseudoHash(key + MotelyPrngKeys.Resample + "X");
        // We don't cache resamples > 8 because they'd use an extra digit
    }

    public readonly void CacheBoosterPackStream(int ante) => CachePseudoHash(MotelyPrngKeys.ShopPack + ante);

    public readonly void CacheTagStream(int ante) => CachePseudoHash(MotelyPrngKeys.Tags + ante);

    public readonly void CacheVoucherStream(int ante) => CacheResampleStream(MotelyPrngKeys.Voucher + ante);

    public readonly void CacheTarotStream(int ante)
    {
        CacheResampleStream(MotelyPrngKeys.Tarot + MotelyPrngKeys.ArcanaPack + ante);
        CachePseudoHash(MotelyPrngKeys.TarotSoul + MotelyPrngKeys.Tarot + ante);
    }

}

public interface IMotelySeedFilterDesc<TFilter> where TFilter : struct, IMotelySeedFilter
{
    public TFilter CreateFilter(ref MotelyFilterCreationContext ctx);
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

    public long SeedCount => Seeds.LongLength;

    internal long _currentSeed = -1;
    
    // For thread coordination
    private readonly object _lockObject = new object();
    private bool _hasBeenRead = false;
    
    public void Reset()
    {
        lock (_lockObject)
        {
            _currentSeed = -1;
            _hasBeenRead = false;
            DebugLogger.LogFormat("[DEBUG] MotelySeedListProvider reset to initial state");
        }
    }
    
    public ReadOnlySpan<char> NextSeed() 
    {
        lock (_lockObject)
        {
            // Mark that this provider has been read
            _hasBeenRead = true;
            
            // Get the next seed index
            long current = ++_currentSeed;
            
            // If we've gone past the end of the array, return an empty string to signal end
            if (current >= Seeds.LongLength)
            {
                return "";
            }
            
            string seed = Seeds[current];
            DebugLogger.LogFormat("[DEBUG] MotelySeedListProvider providing seed {0}/{1}: {2}", current + 1, Seeds.LongLength, seed);
            return seed;
        }
    }
    
    public bool HasBeenFullyRead()
    {
        lock (_lockObject)
        {
            return _hasBeenRead && _currentSeed >= Seeds.LongLength - 1;
        }
    }
}

public sealed class MotelySearchSettings<TFilter>(IMotelySeedFilterDesc<TFilter> filterDesc)
    where TFilter : struct, IMotelySeedFilter
{
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
    public int StartBatchIndex { get; set; } = -1;


    public IMotelySeedFilterDesc<TFilter> FilterDesc { get; set; } = filterDesc;

    public MotelySearchMode Mode;

    /// <summary>
    /// The object which provides seeds to search. Should only be non-null if
    /// `Mode` is set to `Provider`.
    /// </summary>
    public IMotelySeedProvider? SeedProvider;

    /// <summary>
    /// The number of seed characters each batch contains.
    ///  
    /// For example, with a value of 3 one batch would go through 35^3 seeds.
    /// Only meaningful when `Mode` is set to `Sequential`.
    /// </summary>
    public int SequentialBatchCharacterCount { get; set; } = 3;

    public bool Quiet { get; private set; } = false;
    public MotelySearchSettings<TFilter> WithQuiet(bool quiet)
    {
        Quiet = quiet;
        return this;
    }

    public MotelySearchSettings<TFilter> WithThreadCount(int threadCount)
    {
        ThreadCount = threadCount;
        return this;
    }

    public MotelySearchSettings<TFilter> WithStartBatchIndex(int startBatchIndex)
    {
        StartBatchIndex = startBatchIndex;
        return this;
    }

    public MotelySearchSettings<TFilter> WithBatchCharacterCount(int batchCharacterCount)
    {
        SequentialBatchCharacterCount = batchCharacterCount;
        return this;
    }

    public MotelySearchSettings<TFilter> WithListSearch(IEnumerable<string> seeds) {
    var seedList = seeds.ToList();
    int seedCount = seedList.Count;
    
    // Optimize thread count if seed count is small
    if (seedCount < Vector512<double>.Count) {
        // For very small seed lists, just use that many threads
        ThreadCount = Math.Max(1, seedCount);
        DebugLogger.LogFormat("[OPTIMIZE] Reducing thread count to {0} for {1} seeds", ThreadCount, seedCount);
    }
    
    return WithProviderSearch(new MotelySeedListProvider(seedList));
}

    public MotelySearchSettings<TFilter> WithProviderSearch(IMotelySeedProvider provider)
    {
        SeedProvider = provider;
        Mode = MotelySearchMode.Provider;
        return this;
    }

    public MotelySearchSettings<TFilter> WithSequentialSearch()
    {
        SeedProvider = null;
        Mode = MotelySearchMode.Sequential;
        return this;
    }

    public IMotelySearch Start()
    {
        MotelySearch<TFilter> search = new(this);

        search.Start();

        return search;
    }
}

public interface IMotelySearch : IDisposable
{
    public MotelySearchStatus Status { get; }
    public long BatchIndex { get; }
    public long CompletedBatchCount { get; }

    public void Start();
    public void Pause();
}

public enum MotelySearchStatus
{
    Paused,
    Running,
    Completed,
    Disposed
}

public unsafe sealed class MotelySearch<TFilter> : IMotelySearch
    where TFilter : struct, IMotelySeedFilter
{
    private readonly MotelySearchThread[] _threads;
    private readonly Barrier _pauseBarrier;
    private readonly Barrier _unpauseBarrier;
    private volatile MotelySearchStatus _status;
    public MotelySearchStatus Status => _status;

    private readonly TFilter _filter;
    private readonly int _pseudoHashKeyLengthCount;
    private readonly int* _pseudoHashKeyLengths;
    private readonly int* _pseudoHashReverseMap;

    private readonly int _startBatchIndex;
    private long _batchIndex;
    public long BatchIndex => _batchIndex;
    private long _completedBatchCount;
    public long CompletedBatchCount => _completedBatchCount;

    private double _lastReportMS;

    private readonly Stopwatch _elapsedTime = new();

    private readonly bool _quiet;

    public MotelySearch(MotelySearchSettings<TFilter> settings)
    {
        MotelyFilterCreationContext filterCreationContext = new();
        _filter = settings.FilterDesc.CreateFilter(ref filterCreationContext);
        _quiet = settings.Quiet;

        _startBatchIndex = settings.StartBatchIndex;
        _batchIndex = _startBatchIndex;
        _completedBatchCount = _startBatchIndex;

        int[] pseudohashKeyLengths = filterCreationContext.CachedPseudohashKeyLengths.ToArray();
        _pseudoHashKeyLengthCount = pseudohashKeyLengths.Length;
        _pseudoHashKeyLengths = (int*)Marshal.AllocHGlobal(sizeof(int) * _pseudoHashKeyLengthCount);

        for (int i = 0; i < _pseudoHashKeyLengthCount; i++)
        {
            _pseudoHashKeyLengths[i] = pseudohashKeyLengths[i];
        }

        _pseudoHashReverseMap = (int*)Marshal.AllocHGlobal(sizeof(int) * Motely.MaxCachedPseudoHashKeyLength);

        for (int i = 0; i < Motely.MaxCachedPseudoHashKeyLength; i++)
            _pseudoHashReverseMap[i] = -1;

        for (int i = 0; i < _pseudoHashKeyLengthCount; i++)
        {
            _pseudoHashReverseMap[_pseudoHashKeyLengths[i]] = i;
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
        //if (seed.ToString().Contains("PI"))
            //OuijaStyleConsole.WriteLine($"{seed}");
    }

    private void PrintReport()
    {
        double elapsedMS = _elapsedTime.ElapsedMilliseconds;

        if (elapsedMS - _lastReportMS < 900) return;

        _lastReportMS = elapsedMS;

        long thisCompletedCount = _completedBatchCount - _startBatchIndex;

        double totalPortionFinished = _completedBatchCount / (double)_threads[0].MaxBatch;
        double thisPortionFinished = thisCompletedCount / (double)_threads[0].MaxBatch;

        // Prevent division by zero or negative/NaN
        if (thisPortionFinished <= 0 || double.IsNaN(thisPortionFinished) || double.IsInfinity(thisPortionFinished))
            thisPortionFinished = 1e-9;

        double totalTimeEstimate = elapsedMS / thisPortionFinished;
        double timeLeft = Math.Max(0, totalTimeEstimate - elapsedMS);

        // 30 days in ms
        double thirtyDaysMs = 30L * 24 * 60 * 60 * 1000;

        string timeLeftFormatted;
        if (thisCompletedCount < 10 || elapsedMS < 2000) {
            timeLeftFormatted = "--:--:--";
        } else if (timeLeft > thirtyDaysMs || double.IsInfinity(timeLeft) || double.IsNaN(timeLeft)) {
            timeLeftFormatted = "A long time!";
        } else {
            TimeSpan timeLeftSpan = TimeSpan.FromMilliseconds(timeLeft);
            if (timeLeftSpan.TotalDays >= 1)
                timeLeftFormatted = $"{(int)timeLeftSpan.TotalDays}d {timeLeftSpan:hh\\:mm\\:ss}";
            else
                timeLeftFormatted = $"{timeLeftSpan:hh\\:mm\\:ss}";
        }

        double seedsPerMS = thisCompletedCount * ((double)_threads[0].SeedsPerBatch / Math.Max(1, elapsedMS));

        if (!_quiet)
        {
            OuijaStyleConsole.SetBottomLine($"{Math.Round(totalPortionFinished * 100, 2):F2}% ~{timeLeftFormatted} remaining ({Math.Round(seedsPerMS)} seeds/ms)");
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
        Marshal.FreeHGlobal((nint)_pseudoHashReverseMap);

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
        public readonly MotelySearch<TFilter> Search;
        public readonly int ThreadIndex;
        public readonly Thread Thread;

        public long MaxBatch { get; internal set; }
        public long SeedsPerBatch { get; internal set; }

        public MotelySearchThread(MotelySearch<TFilter> search, int threadIndex)
        {

            Search = search;
            ThreadIndex = threadIndex;

            Thread = new(ThreadMain)
            {
                Name = $"Motely Search Thread {ThreadIndex}"
            };

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
                        Debug.Assert(Search._batchIndex >= MaxBatch);
                        return;

                    case MotelySearchStatus.Disposed:
                        return;
                }

                long batchIdx = Interlocked.Increment(ref Search._batchIndex);

                if (batchIdx > MaxBatch)
                {
                    Search._batchIndex = MaxBatch;
                    Search._status = MotelySearchStatus.Completed;
                    return;
                }


                SearchBatch(batchIdx);

                Interlocked.Increment(ref Search._completedBatchCount);

            }

        }

        protected abstract void SearchBatch(long batchIdx);

        public void Dispose()
        {
            Thread.Join();
        }
    }

    private sealed unsafe class MotelyProviderSearchThread : MotelySearchThread
    {
        public readonly IMotelySeedProvider SeedProvider;

        private readonly Vector512<double>* _hashCache;
        private readonly Vector512<double>* _seedCharacterMatrix;

        public MotelyProviderSearchThread(MotelySearch<TFilter> search, MotelySearchSettings<TFilter> settings, int index) : base(search, index)
        {

        if (settings.SeedProvider == null)
        throw new ArgumentException("Cannot create a provider search without a seed provider.");

        SeedProvider = settings.SeedProvider;

        // If using a list provider, reset its counter before starting
        if (ThreadIndex == 0 && SeedProvider is MotelySeedListProvider listProvider)
        {
            DebugLogger.LogFormat("[DEBUG] Thread {0} initializing list provider, resetting counter", ThreadIndex);
            listProvider.Reset();
        }

        // Calculate maximum batch count, ensuring at least 1 batch even for empty lists
        MaxBatch = Math.Max(1, (SeedProvider.SeedCount + Vector512<double>.Count - 1) / Vector512<double>.Count);
        SeedsPerBatch = Vector512<double>.Count;
        
        // For list providers with fewer seeds than Vector512<double>.Count, adjust MaxBatch
        if (SeedProvider is MotelySeedListProvider && SeedProvider.SeedCount < Vector512<double>.Count)
        {
            MaxBatch = 1; // Only process one batch for small lists
        }
        
        DebugLogger.LogFormat("[DEBUG] Thread {0} initialized: Vector512<double>.Count={1}, MaxBatch={2}, SeedsPerBatch={3}, SeedCount={4}", ThreadIndex, Vector512<double>.Count, MaxBatch, SeedsPerBatch, SeedProvider.SeedCount);

            _hashCache = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Search._pseudoHashKeyLengthCount);
            _seedCharacterMatrix = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Motely.MaxSeedLength);
        }

        protected override void SearchBatch(long batchIdx)
        {
            // Special case for single-seed list provider - process directly and avoid vector overhead
            if (SeedProvider is MotelySeedListProvider listProvider && listProvider.SeedCount == 1 && ThreadIndex == 0)
            {
                // Get the single seed
                ReadOnlySpan<char> seed = listProvider.NextSeed();
                DebugLogger.LogFormat("[DEBUG] Thread {0} processing single seed: {1}", ThreadIndex, seed.ToString());
                if (seed.Length > 0 && seed.Length <= Motely.MaxSeedLength)
                {
                    DebugLogger.LogFormat("[OPTIMIZE] Thread {0} directly processing single seed: {1}", ThreadIndex, seed.ToString());
                    SearchSingleSeed(seed);
                }
                else
                {
                    DebugLogger.LogFormat("[DEBUG] Thread {0} skipping single seed - invalid length: {1}", ThreadIndex, seed.Length);
                }
                return;
            }

            // Check if we should skip this batch
            if (SeedProvider is MotelySeedListProvider lpp && lpp.HasBeenFullyRead())
            {
                DebugLogger.LogFormat("[DEBUG] Thread {0} skipping batch {1} - list provider has been fully read", ThreadIndex, batchIdx);
                return;
            }
            
            // If this is the last batch and we don't have enough seeds to fill a vector
            if (batchIdx == MaxBatch && SeedProvider.SeedCount != MaxBatch * Vector512<double>.Count)
            {
                // Calculate remaining seeds in the last batch
                long remainingSeeds = SeedProvider.SeedCount - (MaxBatch - 1) * Vector512<double>.Count;
                
                // If we have no remaining seeds, skip this batch
                if (remainingSeeds <= 0)
                {
                    return;
                }
                
                // Process the remaining seeds individually
                for (int i = 0; i < remainingSeeds; i++)
                {
                    ReadOnlySpan<char> seed = SeedProvider.NextSeed();
                    if (seed.Length > 0 && seed.Length <= Motely.MaxSeedLength)
                    {
                        SearchSingleSeed(seed);
                    }
                }
                return;
            }

            // The length of all the seeds
            int* seedLengths = stackalloc int[Vector512<double>.Count];

            DebugLogger.LogFormat("[DEBUG] Thread {0}: Processing batch {1} for {2}", ThreadIndex, batchIdx, SeedProvider.GetType().Name);

            // Are all the seeds the same length?
            bool homogeneousSeedLength = true;
            int validSeedCount = 0;
            int firstValidSeedLength = 0;

            for (int seedIdx = 0; seedIdx < Vector512<double>.Count; seedIdx++)
            {
                ReadOnlySpan<char> seed = SeedProvider.NextSeed();
                
                // If we've reached the end of the seed list or the seed is too long
                if (seed.Length == 0 || seed.Length > Motely.MaxSeedLength)
                {
                    // Mark this position as invalid
                    seedLengths[seedIdx] = 0;
                    continue;
                }
                
                // Track valid seeds
                validSeedCount++;
                seedLengths[seedIdx] = seed.Length;
                
                DebugLogger.LogFormat("[DEBUG] Loading valid seed {0}: {1}", validSeedCount, seed.ToString());
                
                // Save the first valid seed length for comparison
                if (validSeedCount == 1)
                {
                    firstValidSeedLength = seed.Length;
                }
                // Only compare with other valid seeds
                else if (seed.Length != firstValidSeedLength)
                {
                    homogeneousSeedLength = false;
                }
                
                // Store the seed characters in the matrix
                for (int i = 0; i < seed.Length; i++)
                {
                    ((double*)_seedCharacterMatrix)[i * Vector512<double>.Count + seedIdx] = seed[i];
                }
            }

            // If we have no valid seeds, consider batch as done
            if (validSeedCount == 0)
            {
                DebugLogger.LogFormat("[DEBUG] Thread {0}: No valid seeds found in batch {1}", ThreadIndex, batchIdx);
                return;
            }

            // If all valid seeds are the same length, we can vectorize
            if (homogeneousSeedLength && validSeedCount > 0)
            {
                DebugLogger.LogFormat("[DEBUG] All seeds in batch {0} are of the same length: {1}", batchIdx, firstValidSeedLength);
                // If all the seeds are the same length, we can be fast and vectorize!
                int seedLength = firstValidSeedLength;

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

                    _hashCache[pseudohashKeyIdx] = numVector;
                }

                MotelySearchContextParams searchParams = new(
                    new(_hashCache, Search._pseudoHashReverseMap),
                    seedLength,
                    0, null,
                    _seedCharacterMatrix
                );

                MotelyVectorSearchContext searchContext = new(ref searchParams);

                VectorMask mask = Search._filter.Filter(ref searchContext);

                if (mask.Value != 0)
                {
                    Span<char> seed = stackalloc char[Motely.MaxSeedLength];

                    for (int i = 0; i < Vector512<double>.Count; i++)
                    {
                        if (mask[i])
                        {
                            for (int j = 0; j < seedLength; j++)
                            {
                                seed[j] = (char)((double*)_seedCharacterMatrix)[j * Vector512<double>.Count + i];
                            }

                            Search.ReportSeed(seed[..seedLength]);
                        }
                    }
                }
            }
            else
            {
                // Otherwise, we need to search all the seeds individually
                DebugLogger.LogFormat("[DEBUG] Thread {0}: Seeds in batch {1} have different lengths, searching individually", ThreadIndex, batchIdx);
                Span<char> seed = stackalloc char[Motely.MaxSeedLength];

                for (int i = 0; i < Vector512<double>.Count; i++)
                {
                    int seedLength = seedLengths[i];
                    
                    // Skip invalid/empty seeds
                    if (seedLength == 0)
                        continue;

                    for (int j = 0; j < seedLength; j++)
                    {
                        seed[j] = (char)((double*)_seedCharacterMatrix)[j * Vector512<double>.Count + i];
                    }

                    DebugLogger.LogFormat("[DEBUG] Thread {0}: Searching seed {1}/{2}: {3}", ThreadIndex, i + 1, validSeedCount, seed[..seedLength].ToString());
                    SearchSingleSeed(seed[..seedLength]);
                }

            }
        }

        private void SearchSingleSeed(ReadOnlySpan<char> seed)
        {
            DebugLogger.LogFormat("[DEBUG] Entering SearchSingleSeed for: {0}", seed.ToString());
            Console.Out.Flush();
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

                // Store in the first position of each Vector512 in the hash cache
                _hashCache[pseudohashKeyIdx] = Vector512.Create(num);
            }

            for (int i = 0; i < seed.Length - 1; i++)
            {
                seedLastCharacters[i] = seed[i + 1];
            }

            Vector512<double> firstCharacterVector = Vector512.CreateScalar((double)seed[0]);

            MotelySearchContextParams searchParams = new(
                new(_hashCache, Search._pseudoHashReverseMap),
                seed.Length,
                seed.Length - 1,
                seedLastCharacters,
                &firstCharacterVector
            );

            MotelyVectorSearchContext searchContext = new(ref searchParams);

            // --- DEBUG: Print seed before filtering ---
            DebugLogger.LogFormat("[DEBUG] Checking seed: {0}", seed.ToString());
            Console.Out.Flush();

            VectorMask mask = Search._filter.Filter(ref searchContext);

            if (mask[0] && seed.Length > 0)
            {
                DebugLogger.LogFormat("[DEBUG] MATCH: {0}", seed.ToString());
            }
            else if (seed.Length > 0)
            {
                DebugLogger.LogFormat("[DEBUG] FILTERED OUT: {0}", seed.ToString());
                DebugLogger.LogFormat("[DEBUG] Seed {0} filtered out by needs.", seed.ToString());
            }
            DebugLogger.LogFormat("[DEBUG] Exiting SearchSingleSeed for: {0}", seed.ToString());
            Console.Out.Flush();
        }

        public new void Dispose()
        {
            base.Dispose();
            Marshal.FreeHGlobal((nint)_hashCache);
            Marshal.FreeHGlobal((nint)_seedCharacterMatrix);
        }
    }

    private sealed unsafe class MotelySequentialSearchThread : MotelySearchThread
    {
        // A cache of vectors containing all the seed's digits.
        private static readonly Vector512<double>[] SeedDigitVectors = new Vector512<double>[(Motely.SeedDigits.Length + Vector512<double>.Count - 1) / Vector512<double>.Count];

        static MotelySequentialSearchThread()
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

        public long LastCompletedBatch;

        public MotelySequentialSearchThread(MotelySearch<TFilter> search, MotelySearchSettings<TFilter> settings, int index) : base(search, index)
        {
            _digits = (char*)Marshal.AllocHGlobal(sizeof(char) * Motely.MaxSeedLength);

            _batchCharCount = settings.SequentialBatchCharacterCount;
            SeedsPerBatch = (int)Math.Pow(Motely.SeedDigits.Length, _batchCharCount);

            _nonBatchCharCount = Motely.MaxSeedLength - _batchCharCount;
            MaxBatch = (int)Math.Pow(Motely.SeedDigits.Length, _nonBatchCharCount);
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

            Vector512<double>* hashes = stackalloc Vector512<double>[Search._pseudoHashKeyLengthCount];

            // Calculate hash for the first digits at all the required pseudohash lengths
            for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < Search._pseudoHashKeyLengthCount; pseudohashKeyIdx++)
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                double num = 1;

                for (int i = Motely.MaxSeedLength - 1; i > _batchCharCount - 1; i--)
                {
                    num = (1.1239285023 / num * _digits[i] * Math.PI + (i + pseudohashKeyLength + 1) * Math.PI) % 1;
                }

                hashes[pseudohashKeyIdx] = Vector512.Create(num);
            }


            // Start searching
            for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
            {
                SearchVector(_batchCharCount - 1, SeedDigitVectors[vectorIndex], hashes, 0);
            }

            LastCompletedBatch = batchIdx;
        }

#if !DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void SearchVector(int i, Vector512<double> seedDigitVector, Vector512<double>* nums, int numsLaneIndex)
        {
            Vector512<double>* hashes = stackalloc Vector512<double>[Search._pseudoHashKeyLengthCount];

            for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < Search._pseudoHashKeyLengthCount; pseudohashKeyIdx++)
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                Vector512<double> calcVector = Vector512.Divide(Vector512.Create(1.1239285023), ((double*)&nums[pseudohashKeyIdx])[numsLaneIndex]);

                calcVector = Vector512.Multiply(calcVector, seedDigitVector);

                calcVector = Vector512.Multiply(calcVector, Math.PI);
                calcVector = Vector512.Add(calcVector, Vector512.Create((i + pseudohashKeyLength + 1) * Math.PI));

                Vector512<double> intPart = Vector512.Floor(calcVector);
                calcVector = Vector512.Subtract(calcVector, intPart);

                hashes[pseudohashKeyIdx] = calcVector;
            }

            if (i == 0)
            {
                MotelySearchContextParams searchContextParams = new(
                    new(hashes, Search._pseudoHashReverseMap),
                    Motely.MaxSeedLength, Motely.MaxSeedLength - 1, &_digits[1], &seedDigitVector
                );

                MotelyVectorSearchContext searchContext = new(ref searchContextParams);
                uint successMask = Search._filter.Filter(ref searchContext).Value;
                //TODO return the scores as a mask...?


                if (successMask != 0)
                {
                    Span<char> seed = stackalloc char[Motely.MaxSeedLength];

                    for (int lane = 0; lane < Vector512<double>.Count; lane++)
                    {
                        if ((successMask & 1) != 0)
                        {
                            _digits[0] = (char)seedDigitVector[lane];

                            for (int digit = 0; digit < Motely.MaxSeedLength; digit++)
                            {

                                if (_digits[digit] != '\0')
                                    seed[digit] = _digits[digit];
                            }
                            Search.ReportSeed(seed);
                        }

                        successMask >>= 1;
                    }
                }
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
            Search.PrintReport();
        }

        public new void Dispose()
        {
            base.Dispose();
            Marshal.FreeHGlobal((nint)_digits);
        }
    }
}