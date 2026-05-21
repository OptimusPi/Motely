using Motely.Analysis;

namespace Motely;

/// <summary>
/// Walks a single <see cref="MotelySingleSearchContext"/> PRNG stream for one seed.
/// Holds stream cursors and a <see cref="MotelySeedRouterDesc"/> for context lifetime.
/// </summary>
public sealed class MotelyStreamCursor : IMotelyStreamCursor
{
    private readonly MotelySeedRouterDesc _router;
    private readonly MotelyStreamKind _kind;
    private readonly int _ante;
    private readonly MotelyDeck _deck;
    private int _voucherBitfield;
    private int _bossBitfield;

    private MotelySingleShopItemStream _shop;
    private MotelySingleJokerStream _joker;
    private MotelySingleTarotStream _tarot;
    private MotelySinglePlanetStream _planet;
    private MotelySingleSpectralStream _spectral;
    private MotelySingleJokerFixedRarityStream _legendaryJoker;
    private MotelySingleJokerFixedRarityStream _rareTagJoker;
    private MotelySingleTagStream _tag;
    private MotelySingleVoucherStream _voucher;

    public static MotelyStreamCursor Create(
        string seed,
        MotelyDeck deck,
        MotelyStake stake,
        int ante,
        MotelyStreamKind kind
    )
    {
        var router = new MotelySeedRouterDesc(seed, deck, stake);
        var cursor = new MotelyStreamCursor(router, deck, ante, kind);
        cursor.InitializeStreams();
        return cursor;
    }

    private MotelyStreamCursor(
        MotelySeedRouterDesc router,
        MotelyDeck deck,
        int ante,
        MotelyStreamKind kind
    )
    {
        _router = router;
        _deck = deck;
        _ante = ante;
        _kind = kind;
    }

    private void InitializeStreams()
    {
        var ctx = _router.Instance();
        switch (_kind)
        {
            case MotelyStreamKind.Shop:
                _shop = ctx.CreateShopItemStream(_ante, _deck.GetDefaultRunState());
                break;
            case MotelyStreamKind.Joker:
                _joker = ctx.CreateShopJokerStream(_ante);
                break;
            case MotelyStreamKind.Tarot:
                _tarot = ctx.CreateShopTarotStream(_ante);
                break;
            case MotelyStreamKind.Planet:
                _planet = ctx.CreateShopPlanetStream(_ante);
                break;
            case MotelyStreamKind.Spectral:
                _spectral = ctx.CreateShopSpectralStream(_ante);
                break;
            case MotelyStreamKind.LegendaryJoker:
                _legendaryJoker = ctx.CreateLegendaryJokerStream(_ante);
                break;
            case MotelyStreamKind.RareTagJoker:
                _rareTagJoker = ctx.CreateRareTagJokerStream(_ante);
                break;
            case MotelyStreamKind.Tag:
                _tag = ctx.CreateTagStream(_ante);
                break;
            case MotelyStreamKind.Voucher:
                _voucher = ctx.CreateVoucherStream(_ante);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(_kind), _kind, null);
        }
    }

    public int GetNext()
    {
        var ctx = _router.Instance();
        return _kind switch
        {
            MotelyStreamKind.Shop => ctx.GetNextShopItem(ref _shop).Value,
            MotelyStreamKind.Joker => ctx.GetNextJoker(ref _joker).Value,
            MotelyStreamKind.Tarot => ctx.GetNextTarot(ref _tarot).Value,
            MotelyStreamKind.Planet => ctx.GetNextPlanet(ref _planet).Value,
            MotelyStreamKind.Spectral => ctx.GetNextSpectral(ref _spectral).Value,
            MotelyStreamKind.LegendaryJoker => ctx.GetNextJoker(ref _legendaryJoker).Value,
            MotelyStreamKind.RareTagJoker => ctx.GetNextJoker(ref _rareTagJoker).Value,
            MotelyStreamKind.Tag => (int)ctx.GetNextTag(ref _tag),
            MotelyStreamKind.Voucher => GetNextVoucher(ctx),
            _ => throw new ArgumentOutOfRangeException(nameof(_kind), _kind, null),
        };
    }

    private int GetNextVoucher(MotelySingleSearchContext ctx)
    {
        MotelyRunState runState = new()
        {
            VoucherBitfield = _voucherBitfield,
            BossBitfield = _bossBitfield,
        };
        MotelyVoucher voucher = ctx.GetNextVoucher(ref _voucher, in runState);
        _voucherBitfield = runState.VoucherBitfield;
        _bossBitfield = runState.BossBitfield;
        return (int)voucher;
    }

    public int[] GetNextChunk(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var chunk = new int[count];
        for (int i = 0; i < count; i++)
            chunk[i] = GetNext();
        return chunk;
    }

    public void Dispose() => _router.Dispose();
}
