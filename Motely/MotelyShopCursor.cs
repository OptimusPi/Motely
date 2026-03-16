using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Motely.Analysis;

namespace Motely;

/// <summary>
/// Single-seed shop item cursor. Uses the same single-seed context and
/// CreateShopItemStream/GetNextShopItem path as the analyzer; does not touch the SIMD hot path.
/// </summary>
public sealed unsafe class MotelyShopCursor : IDisposable
{
    private readonly string _seed;
    private readonly MotelySearchParameters _searchParameters;
    private readonly int _ante;
    private readonly PartialSeedHashCache* _hashCache;
    private int _nextItemIndex;
    private bool _disposed;

    /// <summary>
    /// Creates a shop cursor for the given seed, deck, stake, and ante.
    /// Seed must be non-empty, length &lt;= MaxSeedLength, and contain no '0'.
    /// </summary>
    public MotelyShopCursor(string seed, MotelyDeck deck, MotelyStake stake, int ante)
    {
        if (string.IsNullOrEmpty(seed))
            throw new ArgumentException("Seed cannot be null or empty.", nameof(seed));
        if (seed.Length > MotelyCore.MaxSeedLength)
            throw new ArgumentException(
                $"Seed length must be <= {MotelyCore.MaxSeedLength}.",
                nameof(seed)
            );
        if (seed.IndexOf('0') >= 0)
            throw new ArgumentException("Seed cannot contain '0'.", nameof(seed));

        _seed = seed;
        _searchParameters = new MotelySearchParameters { Deck = deck, Stake = stake };
        _ante = ante;
        _nextItemIndex = 0;

        var cache = new PartialSeedHashCache(true);
        _hashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(sizeof(PartialSeedHashCache));
        *_hashCache = cache;
    }

    /// <summary>
    /// Returns the next shop item as a typed DTO (Id = item.Type.ToString(), Name = item.ToString()).
    /// </summary>
    public ShopItemDto GetNextShopItem()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int seedLength = _seed.Length;

        char* pFirstChar = stackalloc char[1];
        pFirstChar[0] = _seed[0];

        Span<Vector512<double>> lastChars = stackalloc Vector512<double>[MotelyCore.MaxSeedLength - 1];
        for (int i = 0; i < seedLength - 1; i++)
            lastChars[i] = Vector512.CreateScalar((double)_seed[i + 1]);

        fixed (Vector512<double>* pLastChars = lastChars)
        {
            var contextParams = new MotelySearchContextParams(
                _hashCache,
                seedLength,
                1,
                pFirstChar,
                pLastChars
            );

            var ctx = new MotelySingleSearchContext(
                in _searchParameters,
                in contextParams,
                0
            );

            MotelySingleShopItemStream stream = ctx.CreateShopItemStream(_ante);

            for (int i = 0; i < _nextItemIndex; i++)
                ctx.GetNextShopItem(ref stream);

            MotelyItem item = ctx.GetNextShopItem(ref stream);
            _nextItemIndex++;

            return new ShopItemDto
            {
                Id = item.Type.ToString(),
                Name = item.ToString(),
            };
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _hashCache->Dispose();
        Marshal.FreeHGlobal((nint)_hashCache);
        _disposed = true;
    }

    /// <summary>
    /// Efficient static batch read: creates the shop PRNG stream ONCE, fast-forwards
    /// <paramref name="skip"/> items, then returns the next <paramref name="count"/> items.
    /// O(skip + count) per call — no O(N²) rebuild penalty.
    /// This is the correct hook-in for "infinite shop scroll": the caller tracks their
    /// position (skip) and passes it on subsequent calls.
    /// </summary>
    public static ShopItemDto[] GetRange(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        int ante,
        int skip,
        int count
    )
    {
        if (string.IsNullOrEmpty(seed))
            throw new ArgumentException("Seed cannot be null or empty.", nameof(seed));
        if (seed.Length > MotelyCore.MaxSeedLength)
            throw new ArgumentException(
                $"Seed length must be <= {MotelyCore.MaxSeedLength}.",
                nameof(seed)
            );
        if (seed.IndexOf('0') >= 0)
            throw new ArgumentException("Seed cannot contain '0'.", nameof(seed));
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), "skip must be >= 0.");
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be > 0.");

        int seedLength = seed.Length;
        var searchParameters = new MotelySearchParameters { Deck = deck, Stake = stake };

        char* pFirstChar = stackalloc char[1];
        pFirstChar[0] = seed[0];

        Span<Vector512<double>> lastChars = stackalloc Vector512<double>[
            MotelyCore.MaxSeedLength - 1
        ];
        for (int i = 0; i < seedLength - 1; i++)
            lastChars[i] = Vector512.CreateScalar((double)seed[i + 1]);

        var cache = new PartialSeedHashCache(true);
        var pHashCache = (PartialSeedHashCache*)Marshal.AllocHGlobal(
            sizeof(PartialSeedHashCache)
        );
        *pHashCache = cache;

        try
        {
            fixed (Vector512<double>* pLastChars = lastChars)
            {
                var contextParams = new MotelySearchContextParams(
                    pHashCache,
                    seedLength,
                    1,
                    pFirstChar,
                    pLastChars
                );

                var ctx = new MotelySingleSearchContext(
                    in searchParameters,
                    in contextParams,
                    0
                );

                MotelySingleShopItemStream stream = ctx.CreateShopItemStream(ante);

                // Fast-forward to the requested position — O(skip)
                for (int i = 0; i < skip; i++)
                    ctx.GetNextShopItem(ref stream);

                // Collect the requested items — O(count)
                var result = new ShopItemDto[count];
                for (int i = 0; i < count; i++)
                {
                    MotelyItem item = ctx.GetNextShopItem(ref stream);
                    result[i] = new ShopItemDto
                    {
                        Id = item.Type.ToString(),
                        Name = item.ToString(),
                    };
                }

                return result;
            }
        }
        finally
        {
            pHashCache->Dispose();
            Marshal.FreeHGlobal((nint)pHashCache);
        }
    }
}
