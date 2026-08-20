namespace Motely.SeedProviders;

public interface IMotelySeedProvider
{
    public long SeedCount { get; }
    public string NextSeed();

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

    public string NextSeed()
    {
        if (Interlocked.Increment(ref _seedsGenerated) > SeedCount)
            return string.Empty;

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

    public string NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_palindromeEnumerator.MoveNext())
            {
                return _palindromeEnumerator.Current;
            }
            return string.Empty;
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
/// Generates psychosis seeds lazily via <see cref="JamlAesthetics.EnumerateSeeds"/> (ABAxBxxx pattern, ~1 billion seeds).
/// </summary>
public sealed class MotelyPsychosisSeedProvider : IMotelySeedProvider
{
    public long SeedCount { get; } = JamlAesthetics.GetSeedCount(JamlAesthetic.Psychosis);

    private readonly IEnumerator<string> _psychosisEnumerator;
    private readonly object _enumeratorLock = new();

    public MotelyPsychosisSeedProvider()
    {
        _psychosisEnumerator = JamlAesthetics.EnumerateSeeds(JamlAesthetic.Psychosis).GetEnumerator();
    }

    public string NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_psychosisEnumerator.MoveNext())
            {
                return _psychosisEnumerator.Current;
            }
            return string.Empty;
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

    public MotelyAestheticSeedProvider(JamlAesthetic aesthetic, char[]? paddingAlphabet = null)
    {
        SeedCount = JamlAesthetics.GetSeedCount(aesthetic, paddingAlphabet);
        _enumerator = JamlAesthetics.EnumerateSeeds(aesthetic, paddingAlphabet).GetEnumerator();
    }

    public string NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_enumerator.MoveNext())
                return _enumerator.Current;
            return string.Empty;
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
        _enumerator = MotelyGlobals
            .GeneratePaddedSeedsForKeywords(keywords, paddingChars)
            .GetEnumerator();
    }

    public string NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_enumerator.MoveNext())
            {
                return _enumerator.Current;
            }
            return string.Empty;
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

    // IEnumerator<T> is not thread-safe; lock is intentional.
    private readonly object _enumeratorLock = new();

    public long SeedCount { get; private set; } = -1;

    public MotelySeedListProvider(IEnumerable<string> seeds, long seedCount = -1)
    {
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

    public string NextSeed()
    {
        lock (_enumeratorLock)
        {
            _seedIndex++;
            if (_seedEnumerator.MoveNext())
            {
                _currentSeed = _seedEnumerator.Current;
                return _currentSeed;
            }
            return string.Empty;
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
                seeds[i] = _currentSeed;
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
/// Drains <paramref name="first"/> to exhaustion, then falls through to <paramref name="second"/>.
/// Used to always run a JAML file's saved <c>seeds:</c> list ahead of whatever seed source the
/// search was otherwise configured with.
/// </summary>
public sealed class MotelyChainedSeedProvider(IMotelySeedProvider first, IMotelySeedProvider second)
    : IMotelySeedProvider
{
    private bool _firstExhausted;

    public long SeedCount { get; } =
        first.SeedCount >= 0 && second.SeedCount >= 0
            ? first.SeedCount + second.SeedCount
            : -1;

    public string NextSeed()
    {
        if (!_firstExhausted)
        {
            string seed = first.NextSeed();
            if (seed.Length != 0)
                return seed;
            _firstExhausted = true;
        }
        return second.NextSeed();
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return 0;

        int count = 0;
        if (!_firstExhausted)
        {
            count = first.NextSeeds(seeds);
            if (count < seeds.Length)
                _firstExhausted = true;
        }

        if (count < seeds.Length)
        {
            var remaining = new string[seeds.Length - count];
            int gotFromSecond = second.NextSeeds(remaining);
            Array.Copy(remaining, 0, seeds, count, gotFromSecond);
            count += gotFromSecond;
        }

        return count;
    }
}

/// <summary>
/// Optional <see cref="IMotelySeedProvider"/> for <see cref="IAsyncEnumerable{T}"/> sources.
/// Pass to <see cref="MotelySearchSettings{TBaseFilter}.WithProviderSearch"/>; do not use unless you
/// truly need async streaming — prefer <see cref="MotelySeedListProvider"/> / <see cref="MotelySearchSettings{TBaseFilter}.WithSeedGenerator"/>.
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

    public string NextSeed()
    {
        lock (_enumeratorLock)
        {
            if (_disposed)
                return string.Empty;

            var enumerator = EnsureEnumerator();
            if (!MoveNextSync(enumerator))
                return string.Empty;

            _currentSeed = enumerator.Current;
            return _currentSeed;
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
