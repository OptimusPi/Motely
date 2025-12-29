// Browser-specific implementation of MotelySearch
// Uses Task-based async loop instead of Threads to avoid blocking the UI thread on WASM

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
    public string Seed { get; }
}

public interface IMotelySeedScoreProvider
{
    public VectorMask Score(
        ref MotelyVectorSearchContext searchContext,
        MotelySeedScoreTally[] buffer,
        VectorMask baseFilterMask = default,
        int scoreThreshold = 0
    );
}

public interface IMotelySeedFilter
{
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

public sealed class MotelySeedListProvider : IMotelySeedProvider
{
    public readonly string[] Seeds;

    public int SeedCount => Seeds.Length;

    private long _currentSeed = -1;

    public MotelySeedListProvider(IEnumerable<string> seeds, bool alreadySorted = false)
    {
        Seeds = alreadySorted ? [.. seeds] : [.. seeds.OrderBy(seed => seed.Length)];
    }

    public ReadOnlySpan<char> NextSeed()
    {
        long index = Interlocked.Increment(ref _currentSeed);
        if (index >= Seeds.Length)
            return ReadOnlySpan<char>.Empty;
        return Seeds[index];
    }
}

public struct MotelySearchParameters
{
    public MotelyStake Stake;
    public MotelyDeck Deck;
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
    Disposed,
}

public sealed class MotelySearchSettings<TBaseFilter>
    where TBaseFilter : struct, IMotelySeedFilter
{
    public MotelySearchSettings(IMotelySeedFilterDesc<TBaseFilter> baseFilterDesc)
    {
        BaseFilterDesc = baseFilterDesc;
    }

    public int ThreadCount { get; set; } = 1;
    public long StartBatchIndex { get; set; } = 0;
    public long EndBatchIndex { get; set; } = long.MaxValue;

    public IMotelySeedFilterDesc<TBaseFilter> BaseFilterDesc { get; set; }

    public IList<IMotelySeedFilterDesc>? AdditionalFilters { get; set; } = null;

    public IMotelySeedScoreDesc? SeedScoreDesc { get; set; } = null;

    public MotelySearchMode Mode { get; set; }

    public IMotelySeedProvider? SeedProvider { get; set; }

    public int SequentialBatchCharacterCount { get; set; } = 3;

    public MotelyDeck Deck { get; set; } = MotelyDeck.Red;
    public MotelyStake Stake { get; set; } = MotelyStake.White;

    public bool CsvOutput { get; set; } = false;
    public bool QuietMode { get; set; } = false;

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

    public MotelySearchSettings<TBaseFilter> WithListSearch(IEnumerable<string> seeds, bool alreadySorted = false)
    {
        return WithProviderSearch(new MotelySeedListProvider(seeds, alreadySorted));
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

    public MotelySearchSettings<TBaseFilter> WithSeedScoreProvider(
        IMotelySeedScoreDesc seedScoreDesc
    )
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

    public MotelySearchSettings<TBaseFilter> WithProgressCallback(
        Action<long, long, long, double> callback
    )
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

    public IMotelySearch Start()
    {
        // Return the browser-specific implementation
        var search = new MotelySearch<TBaseFilter>(this);
        search.Start();
        return search;
    }
}

// Browser-specific implementation using Task-based async loop
public sealed class MotelySearch<TBaseFilter> : IInternalMotelySearch
    where TBaseFilter : struct, IMotelySeedFilter
{
    private readonly MotelySearchParameters _searchParameters;
    private readonly MotelySearchSettings<TBaseFilter> _settings;
    private volatile MotelySearchStatus _status;
    public MotelySearchStatus Status => _status;

    private readonly TBaseFilter _baseFilter;
    private readonly IMotelySeedFilter[] _additionalFilters;
    private readonly IMotelySeedScoreProvider? _scoreProvider;

    private readonly int _pseudoHashKeyLengthCount;
    int IInternalMotelySearch.PseudoHashKeyLengthCount => _pseudoHashKeyLengthCount;
    private readonly unsafe int* _pseudoHashKeyLengths;
    unsafe int* IInternalMotelySearch.PseudoHashKeyLengths => _pseudoHashKeyLengths;

    private readonly long _startBatchIndex;
    private readonly long _endBatchIndex;
    private long _batchIndex;
    private long _matchingSeeds;
    private long _completedBatchCount;

    public long BatchIndex => _batchIndex;
    public long CompletedBatchCount => _completedBatchCount;
    
    // In sequential mode: 35^batchSize
    // In provider mode: 8 (Vector Width)
    public long TotalSeedsSearched => CompletedBatchCount * _seedsPerBatch;
    public long MatchingSeeds => _matchingSeeds;
    public long FilteredSeeds => Filters.MotelyJsonSeedScoreDesc.FilteredSeedCount;

    public TimeSpan ElapsedTime => _elapsedTime.Elapsed;

    private double _lastReportMS;
    private readonly double reportInterval = 2000;

    private readonly Action<long, long, long, double>? _progressCallback;
    private readonly Stopwatch _elapsedTime = new();
    
    private Task? _searchTask;
    private CancellationTokenSource _cts = new();

    // Fields for Search Execution
    private readonly long _maxBatch;
    private readonly long _seedsPerBatch;
    
    // Result Buffer
    private readonly MotelySeedScoreTally[] _resultBuffer = new MotelySeedScoreTally[8];

    // Filter Batches
    [InlineArray(Motely.MaxSeedLength)]
    private struct FilterSeedBatchCharacters
    {
        public Vector512<double> Character;
    }

    private unsafe struct FilterSeedBatch
    {
        public FilterSeedBatchCharacters SeedCharacters;
        public Vector512<double>* SeedHashes;
        public PartialSeedHashCache SeedHashCache;
        public int SeedLength;
        public int SeedCount;
        public long WaitStartMS;
    }

    private readonly unsafe FilterSeedBatch* _filterSeedBatches;

    // Sequential Mode Fields
    private static readonly Vector512<double>[] SeedDigitVectors = new Vector512<double>[
        (Motely.SeedDigits.Length + Motely.MaxVectorWidth - 1) / Motely.MaxVectorWidth
    ];

    static MotelySearch()
    {
        Span<double> vector = stackalloc double[Motely.MaxVectorWidth];
        for (int i = 0; i < SeedDigitVectors.Length; i++)
        {
            for (int j = 0; j < Motely.MaxVectorWidth; j++)
            {
                int index = i * Motely.MaxVectorWidth + j;
                if (index >= Motely.SeedDigits.Length)
                    vector[j] = 0;
                else
                    vector[j] = Motely.SeedDigits[index];
            }
            SeedDigitVectors[i] = Vector512.Create<double>(vector);
        }
    }

    private readonly int _batchCharCount;
    private readonly int _nonBatchCharCount;
    private readonly unsafe char* _digits;
    private readonly unsafe Vector512<double>* _hashes;
    private readonly unsafe PartialSeedHashCache* _hashCache;

    // Provider Mode Fields
    private readonly IMotelySeedProvider? _seedProvider;
    private readonly unsafe Vector512<double>* _seedCharacterMatrix;

    public unsafe MotelySearch(MotelySearchSettings<TBaseFilter> settings)
    {
        _settings = settings;
        _searchParameters = new() { Deck = settings.Deck, Stake = settings.Stake };
        _progressCallback = settings.ProgressCallback;
        
        // Initialize result buffer
        for (int i = 0; i < _resultBuffer.Length; i++)
            _resultBuffer[i] = new MotelySeedScoreTally("", 0);

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
                _additionalFilters[i] = settings.AdditionalFilters[i].CreateFilter(ref filterCreationContext);
            }
        }

        if (settings.SeedScoreDesc != null)
        {
            _scoreProvider = settings.SeedScoreDesc.CreateScoreProvider(ref filterCreationContext);
        }

        _startBatchIndex = settings.StartBatchIndex;
        _endBatchIndex = settings.EndBatchIndex;
        _batchIndex = _startBatchIndex - 1;
        _completedBatchCount = 0;

        int[] pseudohashKeyLengths = [.. filterCreationContext.CachedPseudohashKeyLengths];
        _pseudoHashKeyLengthCount = pseudohashKeyLengths.Length;
        _pseudoHashKeyLengths = (int*)Marshal.AllocHGlobal(sizeof(int) * _pseudoHashKeyLengthCount);
        for (int i = 0; i < _pseudoHashKeyLengthCount; i++)
            _pseudoHashKeyLengths[i] = pseudohashKeyLengths[i];

        _status = MotelySearchStatus.Paused;

        // Initialize Filter Batches
        if (_additionalFilters.Length != 0)
        {
            _filterSeedBatches = (FilterSeedBatch*)Marshal.AllocHGlobal(sizeof(FilterSeedBatch) * _additionalFilters.Length);
            for (int i = 0; i < _additionalFilters.Length; i++)
            {
                FilterSeedBatch* batch = &_filterSeedBatches[i];
                *batch = new()
                {
                    SeedHashes = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Motely.MaxCachedPseudoHashKeyLength),
                };
                batch->SeedHashCache = new(this, batch->SeedHashes);
            }
        }

        // Initialize Mode-Specific Data
        if (settings.Mode == MotelySearchMode.Sequential)
        {
            _digits = (char*)Marshal.AllocHGlobal(sizeof(char) * Motely.MaxSeedLength);
            _batchCharCount = settings.SequentialBatchCharacterCount;
            _seedsPerBatch = (long)Math.Pow(Motely.SeedDigits.Length, _batchCharCount);
            _nonBatchCharCount = Motely.MaxSeedLength - _batchCharCount;
            _maxBatch = (long)Math.Pow(Motely.SeedDigits.Length, _nonBatchCharCount);

            if (_pseudoHashKeyLengthCount > 0)
            {
                _hashes = (Vector512<double>*)Marshal.AllocHGlobal(
                    sizeof(Vector512<double>) * _pseudoHashKeyLengthCount * (_batchCharCount + 1)
                );
                _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
                *_hashCache = new PartialSeedHashCache(this, &_hashes[0]);
            }
        }
        else // Provider
        {
            if (settings.SeedProvider == null) throw new ArgumentException("SeedProvider required");
            _seedProvider = settings.SeedProvider;
            _maxBatch = (_seedProvider.SeedCount + (long)(Motely.MaxVectorWidth - 1)) / (long)Motely.MaxVectorWidth;
            _seedsPerBatch = (long)Motely.MaxVectorWidth;

            if (_pseudoHashKeyLengthCount > 0)
            {
                _hashes = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * _pseudoHashKeyLengthCount);
                _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
                *_hashCache = new PartialSeedHashCache(this, _hashes);
            }
            _seedCharacterMatrix = (Vector512<double>*)Marshal.AllocHGlobal(sizeof(Vector512<double>) * Motely.MaxSeedLength);
        }
    }

    public void Start()
    {
        if (_status == MotelySearchStatus.Disposed) throw new ObjectDisposedException(nameof(MotelySearch<TBaseFilter>));
        if (_status == MotelySearchStatus.Running) return;

        _status = MotelySearchStatus.Running;
        _elapsedTime.Start();
        
        // Start the background task
        _cts = new CancellationTokenSource();
        _searchTask = Task.Run(() => SearchLoopAsync(_cts.Token));
    }

    public void Pause()
    {
        if (_status == MotelySearchStatus.Disposed) throw new ObjectDisposedException(nameof(MotelySearch<TBaseFilter>));
        if (_status != MotelySearchStatus.Running) return;

        _status = MotelySearchStatus.Paused;
        _elapsedTime.Stop();
        _cts.Cancel();
    }

    public void AwaitCompletion()
    {
        // This is a blocking call in Desktop, but in Browser/Task world we can't easily block.
        // We will just wait for the task if it exists.
        _searchTask?.Wait();
    }

    private async Task SearchLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _status == MotelySearchStatus.Running)
            {
                long batchIdx = Interlocked.Increment(ref _batchIndex);

                if (batchIdx >= _endBatchIndex || batchIdx >= _maxBatch)
                {
                    _status = MotelySearchStatus.Completed;
                    // Finish processing any pending filter batches
                    unsafe { ProcessPendingFilterBatches(); }
                    PrintReport(force: true);
                    return;
                }

                if (_settings.Mode == MotelySearchMode.Sequential)
                {
                    unsafe { SearchSequentialBatch(batchIdx); }
                }
                else
                {
                    unsafe { SearchProviderBatch(batchIdx); }
                }

                _completedBatchCount++;

                // Report progress
                PrintReport();

                // Ensure any partially filled filter batches advance to the next filter
                unsafe { ProcessPendingFilterBatches(); }

                // Time-Slicing: Yield to UI thread every ~16ms (60fps)
                // We check elapsed time of the stopwatch to see if we should yield
                // Note: We don't reset stopwatch, just check if enough time passed since last yield?
                // Actually, Task.Run runs on Thread Pool. On WASM (single threaded), it shares the main thread.
                // We need to yield frequently.
                // Just yielding every batch might be enough if batch is small.
                // But if batch is fast, we waste time yielding.
                // Let's use a counter or time check.
                // Since this is the outer loop, let's just yield every time.
                // Await Task.Delay(1) queues continuation.
                // Optimization: Yield only if > 16ms passed since last yield.
                // But _elapsedTime counts TOTAL search time.
                // We'll trust the OS scheduler for now, but explicit yield helps WASM.
                
                await Task.Delay(1, token);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected on Pause/Stop
            unsafe { ProcessPendingFilterBatches(); }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Search error: {ex}");
            _status = MotelySearchStatus.Disposed;
        }
    }

    private unsafe void SearchSequentialBatch(long batchIdx)
    {
        // Ported from MotelySequentialSearchThread.SearchBatch
        for (int i = _nonBatchCharCount - 1; i >= 0; i--)
        {
            int charIndex = (int)(batchIdx % Motely.SeedDigits.Length);
            _digits[Motely.MaxSeedLength - i - 1] = Motely.SeedDigits[charIndex];
            batchIdx /= Motely.SeedDigits.Length;
        }

        Vector512<double>* hashes = &_hashes[_batchCharCount * _pseudoHashKeyLengthCount];

        for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < _pseudoHashKeyLengthCount; pseudohashKeyIdx++)
        {
            int pseudohashKeyLength = _pseudoHashKeyLengths[pseudohashKeyIdx];
            double num = 1;

            for (int i = Motely.MaxSeedLength - 1; i > _batchCharCount - 1; i--)
            {
                num = (1.1239285023 / num * _digits[i] * Math.PI + (i + pseudohashKeyLength + 1) * Math.PI) % 1;
            }
            *(double*)&hashes[pseudohashKeyIdx] = num;
        }

        for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
        {
            SearchSequentialVector(_batchCharCount - 1, SeedDigitVectors[vectorIndex], hashes, 0);
        }
    }

    private unsafe void SearchSequentialVector(int i, Vector512<double> seedDigitVector, Vector512<double>* nums, int numsLaneIndex)
    {
        if (_status != MotelySearchStatus.Running) return;

        Vector512<double>* hashes = &_hashes[i * _pseudoHashKeyLengthCount];

        for (int pseudohashKeyIdx = 0; pseudohashKeyIdx < _pseudoHashKeyLengthCount; pseudohashKeyIdx++)
        {
            int pseudohashKeyLength = _pseudoHashKeyLengths[pseudohashKeyIdx];
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
            MotelySearchContextParams contextParams = new(
                _hashCache,
                Motely.MaxSeedLength,
                Motely.MaxSeedLength - 1,
                &_digits[1],
                &seedDigitVector
            );
            SearchSeeds(in contextParams);
        }
        else
        {
            for (int lane = 0; lane < Motely.MaxVectorWidth; lane++)
            {
                if (seedDigitVector[lane] == 0) break;
                _digits[i] = (char)seedDigitVector[lane];
                for (int vectorIndex = 0; vectorIndex < SeedDigitVectors.Length; vectorIndex++)
                {
                    SearchSequentialVector(i - 1, SeedDigitVectors[vectorIndex], hashes, lane);
                }
            }
        }
    }

    private unsafe void SearchProviderBatch(long batchIdx)
    {
        if (_seedProvider == null) return;

        long seedsProcessedSoFar = batchIdx * Motely.MaxVectorWidth;
        long seedsRemaining = Math.Max(0, _seedProvider.SeedCount - seedsProcessedSoFar);

        if (seedsRemaining < Motely.MaxVectorWidth)
        {
            for (int i = 0; i < seedsRemaining; i++)
            {
                SearchSingleSeed(_seedProvider.NextSeed());
            }
            return;
        }

        int* seedLengths = stackalloc int[Motely.MaxVectorWidth];
        bool homogeneous = true;
        int actualCount = 0;

        for (int seedIdx = 0; seedIdx < Motely.MaxVectorWidth; seedIdx++)
        {
            ReadOnlySpan<char> seed = _seedProvider.NextSeed();
            if (seed.IsEmpty) break;

            seedLengths[seedIdx] = seed.Length;
            if (seedLengths[0] != seed.Length) homogeneous = false;

            for (int i = 0; i < seed.Length; i++)
            {
                ((double*)_seedCharacterMatrix)[i * Motely.MaxVectorWidth + seedIdx] = seed[i];
            }
            actualCount++;
        }

        if (homogeneous)
        {
            int seedLength = seedLengths[0];
            for (int k = 0; k < _pseudoHashKeyLengthCount; k++)
            {
                int pLen = _pseudoHashKeyLengths[k];
                Vector512<double> numVector = Vector512<double>.One;
                for (int i = seedLength - 1; i >= 0; i--)
                {
                    numVector = Vector512.Divide(Vector512.Create(1.1239285023), numVector);
                    numVector = Vector512.Multiply(numVector, _seedCharacterMatrix[i]);
                    numVector = Vector512.Multiply(numVector, Math.PI);
                    numVector = Vector512.Add(numVector, Vector512.Create((i + pLen + 1) * Math.PI));
                    Vector512<double> intPart = Vector512.Floor(numVector);
                    numVector = Vector512.Subtract(numVector, intPart);
                }
                _hashes[k] = numVector;
            }

            MotelySearchContextParams ctx = new(_hashCache, seedLength, 0, null, _seedCharacterMatrix);
            SearchSeeds(in ctx);
        }
        else
        {
            Span<char> seed = stackalloc char[Motely.MaxSeedLength];
            for (int i = 0; i < actualCount; i++)
            {
                int len = seedLengths[i];
                for (int j = 0; j < len; j++)
                    seed[j] = (char)((double*)_seedCharacterMatrix)[j * Motely.MaxVectorWidth + i];
                SearchSingleSeed(seed[..len]);
            }
        }
    }

    private unsafe void SearchSingleSeed(ReadOnlySpan<char> seed)
    {
        if (seed.IsEmpty) return;
        char* seedLastChars = stackalloc char[Motely.MaxSeedLength - 1];
        
        for (int k = 0; k < _pseudoHashKeyLengthCount; k++)
        {
            int pLen = _pseudoHashKeyLengths[k];
            double num = 1;
            for (int i = seed.Length - 1; i >= 0; i--)
            {
                num = (1.1239285023 / num * seed[i] * Math.PI + (i + pLen + 1) * Math.PI) % 1;
            }
            _hashes[k] = Vector512.Create(num);
        }

        for (int i = 0; i < seed.Length - 1; i++) seedLastChars[i] = seed[i + 1];
        Vector512<double> firstCharVec = Vector512.CreateScalar((double)seed[0]);

        MotelySearchContextParams ctx = new(_hashCache, seed.Length, seed.Length - 1, seedLastChars, &firstCharVec);
        SearchSeeds(in ctx);
    }

    private unsafe void SearchSeeds(in MotelySearchContextParams paramsIn)
    {
        MotelyVectorSearchContext ctx = new(in _searchParameters, in paramsIn);
        VectorMask mask = _baseFilter.Filter(ref ctx);

        if (mask.IsPartiallyTrue())
        {
            if (_additionalFilters.Length == 0)
            {
                ReportSeeds(mask, in paramsIn);
            }
            else
            {
                BatchSeeds(0, mask, in paramsIn);
            }
        }
        
        paramsIn.SeedHashCache->Reset();
    }

    private unsafe void ReportSeeds(VectorMask mask, in MotelySearchContextParams paramsIn)
    {
        // Simple reporting for now - matching Desktop behavior
        if (_scoreProvider != null)
        {
             MotelyVectorSearchContext ctx = new(in _searchParameters, in paramsIn);
             VectorMask scoredMask = _scoreProvider.Score(ref ctx, _resultBuffer, mask, 0);
             if (scoredMask.IsPartiallyTrue())
             {
                 for (int i = 0; i < Motely.MaxVectorWidth; i++)
                 {
                     if (scoredMask[i] && paramsIn.IsLaneValid(i)) _matchingSeeds++;
                 }
             }
        }
        else
        {
            char* seed = stackalloc char[Motely.MaxSeedLength];
            for (int i = 0; i < Motely.MaxVectorWidth; i++)
            {
                if (mask[i] && paramsIn.IsLaneValid(i))
                {
                    _matchingSeeds++;
                    if (!_settings.QuietMode)
                    {
                        int len = paramsIn.GetSeed(i, seed);
                        // In Browser, Console.WriteLine might log to dev tools. 
                        // But usually progress callback is what matters.
                    }
                }
            }
        }
    }

    private unsafe void BatchSeeds(int filterIdx, VectorMask mask, in MotelySearchContextParams paramsIn)
    {
        FilterSeedBatch* batch = &_filterSeedBatches[filterIdx];
        
        for (int lane = 0; lane < Vector512<double>.Count; lane++)
        {
            if (mask[lane] && paramsIn.IsLaneValid(lane))
            {
                int idx = batch->SeedCount;
                if (idx == 0)
                {
                    batch->SeedLength = paramsIn.SeedLength;
                    batch->WaitStartMS = _elapsedTime.ElapsedMilliseconds;
                }
                else if (batch->SeedLength != paramsIn.SeedLength)
                {
                    SearchFilterBatch(filterIdx, batch);
                    idx = 0;
                    batch->SeedLength = paramsIn.SeedLength;
                }

                batch->SeedCount++;
                
                // Copy digits
                for (int i = 0; i < paramsIn.SeedLastCharactersLength; i++)
                    ((double*)&batch->SeedCharacters)[i * Vector512<double>.Count + idx] = 
                        ((double*)paramsIn.SeedLastCharacters)[i * Vector512<double>.Count + lane];
                
                for (int firstCharIndex = 0;
                    firstCharIndex < paramsIn.SeedFirstCharactersLength;
                    firstCharIndex++)
                    ((double*)&batch->SeedCharacters)[
                        (paramsIn.SeedLastCharactersLength + firstCharIndex) * Vector512<double>.Count
                            + idx
                    ] = paramsIn.SeedFirstCharacters[firstCharIndex];

                // Copy hashes
                for (int i = 0; i < _pseudoHashKeyLengthCount; i++)
                {
                    int pLen = _pseudoHashKeyLengths[i];
                    ((double*)batch->SeedHashes)[i * Vector512<double>.Count + idx] = 
                        ((double*)paramsIn.SeedHashCache->Cache[pLen])[i * Vector512<double>.Count + lane];
                }

                if (idx == Vector512<double>.Count - 1)
                {
                    SearchFilterBatch(filterIdx, batch);
                }
            }
        }
    }

    private unsafe void SearchFilterBatch(int filterIdx, FilterSeedBatch* batch)
    {
        if (batch->SeedCount == 0) return;

        // Clear unused lanes
        for (int i = batch->SeedCount; i < Vector512<double>.Count; i++)
            for (int j = 0; j < batch->SeedLength; j++)
                ((double*)&batch->SeedCharacters)[j * Vector512<double>.Count + i] = 0;

        MotelySearchContextParams ctxParams = new(
            &batch->SeedHashCache,
            batch->SeedLength,
            0,
            null,
            (Vector512<double>*)&batch->SeedCharacters,
            isAdditionalFilter: true
        );

        MotelyVectorSearchContext ctx = new(in _searchParameters, in ctxParams);
        VectorMask mask = _additionalFilters[filterIdx].Filter(ref ctx);

        if (mask.IsPartiallyTrue())
        {
            if (filterIdx + 1 == _additionalFilters.Length)
                ReportSeeds(mask, in ctxParams);
            else
                BatchSeeds(filterIdx + 1, mask, in ctxParams);
        }

        batch->SeedCount = 0;
        batch->SeedHashCache.Reset();
    }

    private unsafe void ProcessPendingFilterBatches()
    {
        if (_filterSeedBatches == null) return;
        for (int i = 0; i < _additionalFilters.Length; i++)
        {
            if (_filterSeedBatches[i].SeedCount > 0)
                SearchFilterBatch(i, &_filterSeedBatches[i]);
        }
    }

    private void PrintReport(bool force = false)
    {
        if (_progressCallback == null) return;
        double elapsed = _elapsedTime.ElapsedMilliseconds;
        if (!force && elapsed - _lastReportMS < reportInterval) return;
        
        _lastReportMS = elapsed;
        long seedsSearched = _completedBatchCount * _seedsPerBatch;
        double speed = elapsed > 1 ? seedsSearched / elapsed : 0;
        
        _progressCallback(_completedBatchCount, _maxBatch, seedsSearched, speed);
    }

    public unsafe void Dispose()
    {
        Pause();
        _status = MotelySearchStatus.Disposed;
        
        Marshal.FreeHGlobal((nint)_pseudoHashKeyLengths);
        
        if (_filterSeedBatches != null)
        {
            for (int i = 0; i < _additionalFilters.Length; i++)
            {
                _filterSeedBatches[i].SeedHashCache.Dispose();
                if (_filterSeedBatches[i].SeedHashes != null)
                    Marshal.FreeHGlobal((nint)_filterSeedBatches[i].SeedHashes);
            }
            Marshal.FreeHGlobal((nint)_filterSeedBatches);
        }

        if (_digits != null) Marshal.FreeHGlobal((nint)_digits);
        if (_hashes != null) Marshal.FreeHGlobal((nint)_hashes);
        if (_hashCache != null)
        {
            _hashCache->Dispose();
            Marshal.FreeHGlobal((nint)_hashCache);
        }
        if (_seedCharacterMatrix != null) Marshal.FreeHGlobal((nint)_seedCharacterMatrix);

        GC.SuppressFinalize(this);
    }

    ~MotelySearch()
    {
        Dispose();
    }
}
