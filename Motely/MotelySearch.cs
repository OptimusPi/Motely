using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Motely;
public struct MotelySearchResult
{
    public string Seed;
    public bool Success;
    public int TotalScore;
    public int NaturalNegativeJokers;
    public int DesiredNegativeJokers;
    public int[] ScoreWants;
}

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
        CachePseudoHash(key + MotelyPrngKeys.ResampleSingleDigit);
        // We don't cache resamples > 8 because they'd use an extra digit
    }

    public readonly void CacheBoosterPackStream(int ante) => CachePseudoHash(MotelyPrngKeys.ShopPack + ante);

    public readonly void CacheTagStream(int ante) => CachePseudoHash(MotelyPrngKeys.Tags + ante);

    public readonly void CacheVoucherStream(int ante) => CacheResampleStream(MotelyPrngKeys.Voucher + ante);

    public readonly void CacheTarotStream(int ante)
    {
        CacheResampleStream(MotelyPrngKeys.Tarot + MotelyPrngKeys.ArcanaPack + ante);
        CachePseudoHash(MotelyPrngKeys.SoulSource + MotelyPrngKeys.Tarot + ante);
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

public sealed class MotelySearchSettings<TFilter>(IMotelySeedFilterDesc<TFilter> filterDesc)
    where TFilter : struct, IMotelySeedFilter
{
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
    public int StartBatchIndex { get; set; } = 0;

    /// <summary>
    /// The number of seed characters each batch contains.
    ///  
    /// For example, with a value of 3 one batch would go through 35^3 seeds.
    /// </summary>
    public int BatchCharacterCount { get; set; } = 3;
    public int EndBatchIndex { get; set; } = -1;

    public IMotelySeedFilterDesc<TFilter> FilterDesc { get; set; } = filterDesc;

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
        BatchCharacterCount = batchCharacterCount;
        return this;
    }

    public MotelySearchSettings<TFilter> WithEndBatchIndex(int endBatchIndex)
    {
        EndBatchIndex = endBatchIndex;
        return this;
    }

    public IMotelySearch Start()
    {
        MotelySearch<TFilter> search = new(this);

        search.Start();

        return search;
    }
}

#if !DEBUG
[method:MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
internal unsafe struct SeedHashCache(Vector512<double>* seedHashes, int* seedHashesLookup)
{
    // A map of pseudohash key length => cache index
    public readonly int* SeedHashesLookup = seedHashesLookup;

    // A list of all the cached seed hashs
    public readonly Vector512<double>* SeedHashes = seedHashes;

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public readonly bool HasPartialHash(int keyLength)
    {
        return keyLength < Motely.MaxCachedPseudoHashKeyLength && SeedHashesLookup[keyLength] != -1;
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public readonly Vector512<double> GetSeedHashVector()
    {
        Debug.Assert(SeedHashesLookup[0] == 0);
        return SeedHashes[0];
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public readonly double GetSeedHash(int lane)
    {
        return GetSeedHashVector()[lane];
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public readonly Vector512<double> GetPartialHashVector(int keyLength)
    {
        return SeedHashes[SeedHashesLookup[keyLength]];
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public readonly double GetPartialHash(int keyLength, int lane)
    {
        return GetPartialHashVector(keyLength)[lane];
    }
}

public interface IMotelySearch : IDisposable
{
    ConcurrentQueue<MotelySearchResult> Results { get; }
    bool IsCompleted { get; }
    public MotelySearchStatus Status { get; }
    public int BatchIndex { get; }
    public int CompletedBatchCount { get; }
    public int TotalBatchCount { get; }
    public int SeedsPerBatch { get; }

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
    // A cache of vectors containing all the seed's digits.
    private static readonly Vector512<double>[] SeedDigitVectors = new Vector512<double>[(Motely.SeedDigits.Length + Vector512<double>.Count - 1) / Vector512<double>.Count];

    static MotelySearch()
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

    private readonly MotelySearchThread[] _threads;
    private readonly ConcurrentQueue<MotelySearchResult> _results = new();
    public ConcurrentQueue<MotelySearchResult> Results => _results;
    public bool IsCompleted => _completedBatchCount >= _batchCount;
    private readonly Barrier _pauseBarrier;
    private readonly Barrier _unpauseBarrier;

    private volatile MotelySearchStatus _status;
    public MotelySearchStatus Status => _status;

    private readonly int _batchCharCount;
    private readonly int _nonBatchCharCount;
    private readonly int _maxBatch;
    private readonly int _seedsPerBatch;

    private readonly TFilter _filter;
    private readonly int _pseudoHashKeyLengthCount;
    private readonly int* _pseudoHashKeyLengths;
    private readonly int* _pseudoHashReverseMap;

    private readonly int _startBatchIndex;
    private int _batchIndex;
    private readonly int _batchCount;
    public int BatchIndex => _batchIndex;
    private int _completedBatchCount;
    private static long _lastStatusUpdateTicks;
    public int CompletedBatchCount => _completedBatchCount;
    public int TotalBatchCount => _batchCount;
    public int SeedsPerBatch => _seedsPerBatch;
    private readonly Stopwatch _elapsedTime = new();

    public MotelySearch(MotelySearchSettings<TFilter> settings)
    {
        MotelyFilterCreationContext filterCreationContext = new();
        _filter = settings.FilterDesc.CreateFilter(ref filterCreationContext);

        _startBatchIndex = settings.StartBatchIndex;
        _batchIndex = _startBatchIndex;
        _completedBatchCount = _startBatchIndex;

        _batchCharCount = settings.BatchCharacterCount;
        _seedsPerBatch = (int)Math.Pow(Motely.SeedDigits.Length, _batchCharCount);
        _nonBatchCharCount = Motely.MaxSeedLength - _batchCharCount;
        if (settings.EndBatchIndex != -1)
        {
            _batchCount = settings.EndBatchIndex - _startBatchIndex + 1;
        }
        else
        {
            _batchCount = (int)Math.Pow(Motely.SeedDigits.Length, _nonBatchCharCount);
        }

        _maxBatch = (int)Math.Pow(Motely.SeedDigits.Length, _nonBatchCharCount);

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
            _threads[i] = new(this, i);
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

    public void Dispose()
    {
        Pause();

        // Atomically replace paused state with Disposed state

        MotelySearchStatus oldStatus = Interlocked.Exchange(ref _status, MotelySearchStatus.Disposed);

        if (oldStatus == MotelySearchStatus.Paused)
        {
            _unpauseBarrier.SignalAndWait();
        }





        foreach (MotelySearchThread thread in _threads)
        {
            thread.Thread.Join();
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

    private sealed unsafe class MotelySearchThread : IDisposable
    {
        public readonly int ThreadIndex;
        public readonly Thread Thread;
        public readonly MotelySearch<TFilter> Search;

        private readonly char* _digits;

        public int LastCompletedBatch;

        public MotelySearchThread(MotelySearch<TFilter> search, int index)
        {
            ThreadIndex = index;
            Search = search;

            _digits = (char*)Marshal.AllocHGlobal(sizeof(char) * Motely.MaxSeedLength);

            Thread = new(ThreadMain)
            {
                Name = $"Motely Search Thread {index}"
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
                        Debug.Assert(Search._batchIndex >= Search._maxBatch);
                        return;

                    case MotelySearchStatus.Disposed:
                        return;
                }

                int batchIdx = Interlocked.Increment(ref Search._batchIndex);

                if (batchIdx >= Search._maxBatch)
                {
                    Search._batchIndex = Search._maxBatch;
                    Search._status = MotelySearchStatus.Completed;
                    return;
                }

                if (batchIdx == -1)
                {
                    // TODO Search all lengths less than 1
                }
                else
                {
                    SearchBatch(batchIdx);
                    LastCompletedBatch = batchIdx;
                }

                int totalCompletedCount = Interlocked.Increment(ref Search._completedBatchCount);
                int thisCompletedCount = totalCompletedCount - Search._startBatchIndex;

                double totalPortionFinished = totalCompletedCount / (double)Search._batchCount;
                double thisPortionFinished = thisCompletedCount / (double)Search._batchCount;
                double elapsedMS = Search._elapsedTime.ElapsedMilliseconds;
                double totalTimeEstimate = elapsedMS / thisPortionFinished;
                double timeLeft = totalTimeEstimate - elapsedMS;

                TimeSpan timeLeftSpan = TimeSpan.FromMilliseconds(timeLeft);

                double seedsPerMS = (thisCompletedCount * (double)Search._seedsPerBatch) / elapsedMS;

                string timeLeftFormatted;

                if (timeLeftSpan.Days == 0) timeLeftFormatted = $"{timeLeftSpan:hh\\:mm\\:ss}";
                else timeLeftFormatted = $"{timeLeftSpan:d\\:hh\\:mm\\:ss}";

                long lastTicks = Interlocked.Read(ref _lastStatusUpdateTicks);
                long currentTicks = Environment.TickCount64;
                if (currentTicks - lastTicks > 500)
                {
                    if (Interlocked.CompareExchange(ref _lastStatusUpdateTicks, currentTicks, lastTicks) == lastTicks)
                    {
                        // Clamp totalPortionFinished to [0,1] to prevent >100% progress
                        double clampedPortion = Math.Min(Math.Max(totalPortionFinished, 0.0), 1.0);
                        FancyConsole.SetBottomLine($"{Math.Round(clampedPortion * 100, 2):F2}% ~{timeLeftFormatted} remaining ({Math.Round(seedsPerMS)} seeds/ms)");
                    }
                }
            }
        }

        private void SearchBatch(int batchIdx)
        {

            // Figure out which digits this search is doing
            for (int i = Search._nonBatchCharCount - 1; i >= 0; i--)
            {
                int charIndex = batchIdx % Motely.SeedDigits.Length;
                _digits[Motely.MaxSeedLength - i - 1] = Motely.SeedDigits[charIndex];
                batchIdx /= Motely.SeedDigits.Length;
            }

            Vector512<double>* hashes = stackalloc Vector512<double>[Search._pseudoHashKeyLengthCount];

            // Calculate hash for the first digits at all the required pseudohash lengths
            for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < Search._pseudoHashKeyLengthCount; pseudohashKeyIdx++)
            {
                int pseudohashKeyLength = Search._pseudoHashKeyLengths[pseudohashKeyIdx];

                double num = 1;

                for (int i = Motely.MaxSeedLength - 1; i > Search._batchCharCount - 1; i--)
                {
                    num = (1.1239285023 / num * _digits[i] * Math.PI + (i + pseudohashKeyLength + 1) * Math.PI) % 1;
                }

                hashes[pseudohashKeyIdx] = Vector512.Create(num);
            }


            // Start searching
            for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
            {
                SearchVector(Search._batchCharCount - 1, SeedDigitVectors[vectorIndex], hashes, 0);
            }
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
                    Motely.MaxSeedLength, &_digits[1], seedDigitVector
                );

                MotelyVectorSearchContext searchContext = new(ref searchContextParams);
                uint successMask = Search._filter.Filter(ref searchContext).Value;

                if (successMask != 0)
                {
                    for (int lane = 0; lane < Vector512<double>.Count; lane++)
                    {
                        if ((successMask & 1) != 0)
                        {
                            _digits[0] = (char)seedDigitVector[lane];

                            string seed = "";

                            for (int digit = 0; digit < Motely.MaxSeedLength; digit++)
                            {
                                if (_digits[digit] != '\0')
                                    seed += _digits[digit];
                            }

                            // Get actual scoring data from OuijaJsonFilter if available
                            int totalScore = 1; // Default fallback
                            int[] scoreWants = new int[1] { 1 }; // Default fallback
                            
                            // Try to get actual scoring data from OuijaJsonFilter
                            if (Search._filter is OuijaJsonFilterDesc.OuijaJsonFilter)
                            {
                                var threadResults = OuijaJsonFilterDesc.OuijaJsonFilter.GetThreadLocalResults();
                                if (threadResults.TryGetValue(seed, out var ouijaResult))
                                {
                                    totalScore = ouijaResult.TotalScore;
                                    scoreWants = ouijaResult.ScoreWants.Where(s => s > 0).Select(s => (int)s).ToArray();
                                    if (scoreWants.Length == 0) scoreWants = new int[1] { 1 }; // Ensure at least one element
                                }
                            }

                            Search._results.Enqueue(new MotelySearchResult 
                            { 
                                Seed = seed, 
                                Success = true,
                                TotalScore = totalScore,
                                ScoreWants = scoreWants
                            });
                        }

                        successMask >>= 1;
                    }
                }

                // Environment.Exit(0);
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

        public void Dispose()
        {
            Thread.Join();
            Marshal.FreeHGlobal((nint)_digits);
        }
    }
}