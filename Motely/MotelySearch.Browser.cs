using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using Motely.Filters;

namespace Motely;

// Browser stub: Interface definitions only (no implementation)
// MotelySearch.cs is excluded from browser builds, but filter classes need these interfaces

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
}

public sealed class MotelySeedListProvider : IMotelySeedProvider
{
    private readonly IEnumerator<string> _seedEnumerator;
    private string? _currentSeed;
    private long _seedIndex = -1;

    public int SeedCount { get; private set; } = -1; // Unknown for enumerables

    public MotelySeedListProvider(IEnumerable<string> seeds, bool alreadySorted = false)
    {
        _seedEnumerator = seeds.GetEnumerator();
    }

    public ReadOnlySpan<char> NextSeed()
    {
        _seedIndex++;
        if (_seedEnumerator.MoveNext())
        {
            _currentSeed = _seedEnumerator.Current;
            return _currentSeed.AsSpan();
        }
        return ReadOnlySpan<char>.Empty;
    }

    public void Dispose()
    {
        _seedEnumerator?.Dispose();
    }
}

public struct MotelySearchParameters
{
    public MotelyStake Stake;
    public MotelyDeck Deck;
}

public enum MotelySearchStatus
{
    Paused,
    Running,
    Completed,
    Disposed,
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
    IMotelySearchSettings WithListSearch(IEnumerable<string> seeds, bool alreadySorted = false);
    IMotelySearchSettings WithRandomSearch(int count);
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
    // Interface implementation
    IMotelySeedFilterDesc IMotelySearchSettings.BaseFilterDescBase => BaseFilterDesc;
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
    public long StartBatchIndex { get; set; } = 0;
    public long EndBatchIndex { get; set; } = long.MaxValue;

    public IMotelySeedFilterDesc<TBaseFilter> BaseFilterDesc { get; set; } = baseFilterDesc;

    public IList<IMotelySeedFilterDesc>? AdditionalFilters { get; set; } = null;

    public IMotelySeedScoreDesc? SeedScoreDesc { get; set; } = null;

    public MotelySearchMode Mode { get; set; }

    public IMotelySeedProvider? SeedProvider { get; set; }

    public int SequentialBatchCharacterCount { get; set; } = 3;

    public MotelyDeck Deck { get; set; } = MotelyDeck.Red;
    public MotelyStake Stake { get; set; } = MotelyStake.White;

    public bool CsvOutput { get; set; } = false;
    public bool QuietMode { get; set; } = false;

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
        bool alreadySorted = false
    )
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

    // Explicit interface implementation for chaining
    IMotelySearchSettings IMotelySearchSettings.WithAdditionalFilter(IMotelySeedFilterDesc filterDesc)
    {
        return WithAdditionalFilter(filterDesc);
    }

    // Explicit interface implementations for chaining
    IMotelySearchSettings IMotelySearchSettings.WithThreadCount(int threadCount) => WithThreadCount(threadCount);
    IMotelySearchSettings IMotelySearchSettings.WithBatchCharacterCount(int count) => WithBatchCharacterCount(count);
    IMotelySearchSettings IMotelySearchSettings.WithStartBatchIndex(long index) => WithStartBatchIndex(index);
    IMotelySearchSettings IMotelySearchSettings.WithEndBatchIndex(long index) => WithEndBatchIndex(index);
    IMotelySearchSettings IMotelySearchSettings.WithSeedScoreProvider(IMotelySeedScoreDesc desc) => WithSeedScoreProvider(desc);
    IMotelySearchSettings IMotelySearchSettings.WithListSearch(IEnumerable<string> seeds, bool alreadySorted) => WithListSearch(seeds, alreadySorted);
    IMotelySearchSettings IMotelySearchSettings.WithRandomSearch(int count) => WithRandomSearch(count);
    IMotelySearchSettings IMotelySearchSettings.WithProviderSearch(IMotelySeedProvider provider) => WithProviderSearch(provider);
    IMotelySearchSettings IMotelySearchSettings.WithSequentialSearch() => WithSequentialSearch();
    IMotelySearchSettings IMotelySearchSettings.WithDeck(MotelyDeck deck) => WithDeck(deck);
    IMotelySearchSettings IMotelySearchSettings.WithStake(MotelyStake stake) => WithStake(stake);
    IMotelySearchSettings IMotelySearchSettings.WithProgressCallback(Action<MotelyProgress> callback) => WithProgressCallback(callback);
    IMotelySearchSettings IMotelySearchSettings.WithCsvOutput(bool csvOutput) => WithCsvOutput(csvOutput);
    IMotelySearchSettings IMotelySearchSettings.WithQuietMode(bool quietMode) => WithQuietMode(quietMode);
    IMotelySearch IMotelySearchSettings.Start(CancellationToken cancellationToken) => Start(cancellationToken);

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
        Action<MotelyProgress> callback
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

    public IMotelySearch Start(CancellationToken cancellationToken = default)
    {
        throw new PlatformNotSupportedException("MotelySearch.Start() is not supported in browser builds. Use JsonSearchExecutor or browser-specific search implementation.");
    }
}
