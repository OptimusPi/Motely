using Motely.Analysis;

namespace Motely;

public sealed class MotelyWasmSearchContext : IMotelyWasmSearchContext
{
    private const int MaxChunkCount = 64_000;

    private readonly MotelySeedRouterDesc _router;
    private readonly MotelySingleSearchContext _ctx;

    public MotelyWasmSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _router = new MotelySeedRouterDesc(seed, deck, stake);
        _ctx = _router.Instance();
    }

    // ── Boss ──────────────────────────────────────────────────────────

    public MotelySingleBossStream CreateBossStream()
    {
        return _ctx.CreateBossStream();
    }

    public MotelyBossStreamResult GetNextBossForAnte(
        MotelySingleBossStream stream,
        int ante,
        MotelyJsRunState runState
    )
    {
        var nextStream = stream;
        var state = runState.ToRunState();
        var boss = _ctx.GetBossForAnte(ref nextStream, ante, ref state);
        return new(boss, nextStream, new(state.VoucherBitfield, state.BossBitfield));
    }

    public MotelyBossChunkResult GetNextBossForAnteChunk(
        MotelySingleBossStream stream,
        int startAnte,
        int count,
        MotelyJsRunState runState
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var nextStream = stream;
        var state = runState.ToRunState();
        var bosses = new MotelyBossBlind[count];
        for (int i = 0; i < count; i++)
        {
            int ante = startAnte + i;
            bosses[i] = _ctx.GetBossForAnte(ref nextStream, ante, ref state);
        }
        return new(bosses, nextStream, new(state.VoucherBitfield, state.BossBitfield));
    }

    // ── Vouchers ──────────────────────────────────────────────────────

    public MotelyVoucherStateResult GetAnteFirstVoucher(int ante, MotelyJsRunState runState)
    {
        var state = runState.ToRunState();
        var voucher = _ctx.GetAnteFirstVoucher(ante, state);
        state.ActivateVoucher(voucher);
        return new(voucher, new(state.VoucherBitfield, state.BossBitfield));
    }

    public MotelyVoucherChunkResult GetAnteFirstVoucherChunk(
        int startAnte,
        int count,
        MotelyJsRunState runState
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var state = runState.ToRunState();
        var vouchers = new MotelyVoucher[count];
        for (int i = 0; i < count; i++)
        {
            int ante = startAnte + i;
            var voucher = _ctx.GetAnteFirstVoucher(ante, state);
            state.ActivateVoucher(voucher);
            vouchers[i] = voucher;
        }
        return new(vouchers, new(state.VoucherBitfield, state.BossBitfield));
    }

    // ── Tags ──────────────────────────────────────────────────────────

    public MotelySingleTagStream CreateTagStream(int ante)
    {
        return _ctx.CreateTagStream(ante);
    }

    public MotelyTagStreamResult GetNextTag(MotelySingleTagStream stream)
    {
        var nextStream = stream;
        var tag = _ctx.GetNextTag(ref nextStream);
        return new(tag, nextStream);
    }

    public MotelyTagChunkResult GetNextTagChunk(MotelySingleTagStream stream, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var nextStream = stream;
        var tags = new MotelyTag[count];
        for (int i = 0; i < count; i++)
            tags[i] = _ctx.GetNextTag(ref nextStream);
        return new(tags, nextStream);
    }

    // ── Booster Packs ─────────────────────────────────────────────────

    public MotelySingleBoosterPackStream CreateBoosterPackStream(int ante)
    {
        return _ctx.CreateBoosterPackStream(ante);
    }

    public MotelyBoosterPackStreamResult GetNextBoosterPack(MotelySingleBoosterPackStream stream)
    {
        var nextStream = stream;
        var pack = _ctx.GetNextBoosterPack(ref nextStream);
        return new(pack, nextStream);
    }

    public MotelyBoosterPackChunkResult GetNextBoosterPackChunk(MotelySingleBoosterPackStream stream, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var nextStream = stream;
        var packs = new MotelyBoosterPack[count];
        for (int i = 0; i < count; i++)
            packs[i] = _ctx.GetNextBoosterPack(ref nextStream);
        return new(packs, nextStream);
    }

    // ── Shop Items ────────────────────────────────────────────────────

    public MotelySingleShopItemStream CreateShopItemStream(
        int ante,
        MotelyJsRunState runState,
        MotelyShopStreamFlags flags,
        MotelyJokerStreamFlags jokerFlags
    )
    {
        var state = runState.ToRunState();
        return _ctx.CreateShopItemStream(ante, state, flags, jokerFlags);
    }

    public MotelyShopItemStreamResult GetNextShopItem(MotelySingleShopItemStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextShopItem(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyShopItemChunkResult GetNextShopItemChunk(MotelySingleShopItemStream stream, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextShopItem(ref nextStream).Value;
        return new(items, nextStream);
    }

    // ── Misprint ──────────────────────────────────────────────────────

    public MotelySinglePrngStream CreateMisprintPrngStream()
    {
        return _ctx.CreateMisprintPrngStream();
    }

    public MotelyIntPrngStreamResult GetNextMisprintMult(MotelySinglePrngStream stream)
    {
        var nextStream = stream;
        var value = _ctx.GetNextMisprintMult(ref nextStream);
        return new(value, nextStream);
    }

    public MotelyIntPrngChunkResult GetNextMisprintMultChunk(MotelySinglePrngStream stream, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var nextStream = stream;
        var values = new int[count];
        for (int i = 0; i < count; i++)
            values[i] = _ctx.GetNextMisprintMult(ref nextStream);
        return new(values, nextStream);
    }

    // ── Lucky Money ───────────────────────────────────────────────────

    public MotelySinglePrngStream CreateLuckyCardMoneyStream()
    {
        return _ctx.CreateLuckyCardMoneyStream();
    }

    public MotelyBoolPrngStreamResult GetNextLuckyMoney(MotelySinglePrngStream stream, double baseLuck)
    {
        var nextStream = stream;
        var value = _ctx.GetNextLuckyMoney(ref nextStream, baseLuck);
        return new(value, nextStream);
    }

    public MotelyBoolPrngChunkResult GetNextLuckyMoneyChunk(MotelySinglePrngStream stream, int count, double baseLuck)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var nextStream = stream;
        var values = new bool[count];
        for (int i = 0; i < count; i++)
            values[i] = _ctx.GetNextLuckyMoney(ref nextStream, baseLuck);
        return new(values, nextStream);
    }

    // ── Lucky Mult ────────────────────────────────────────────────────

    public MotelySinglePrngStream CreateLuckyCardMultStream()
    {
        return _ctx.CreateLuckyCardMultStream();
    }

    public MotelyBoolPrngStreamResult GetNextLuckyMult(MotelySinglePrngStream stream, double baseLuck)
    {
        var nextStream = stream;
        var value = _ctx.GetNextLuckyMult(ref nextStream, baseLuck);
        return new(value, nextStream);
    }

    public MotelyBoolPrngChunkResult GetNextLuckyMultChunk(MotelySinglePrngStream stream, int count, double baseLuck)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var nextStream = stream;
        var values = new bool[count];
        for (int i = 0; i < count; i++)
            values[i] = _ctx.GetNextLuckyMult(ref nextStream, baseLuck);
        return new(values, nextStream);
    }

    // ── Erratic Deck ──────────────────────────────────────────────────

    public MotelySinglePrngStream CreateErraticDeckPrngStream()
    {
        return _ctx.CreateErraticDeckPrngStream();
    }

    public MotelyItemPrngStreamResult GetNextErraticDeckCard(MotelySinglePrngStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextErraticDeckCard(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyItemChunkResult GetNextErraticDeckCardChunk(MotelySinglePrngStream stream, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);

        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextErraticDeckCard(ref nextStream).Value;
        return new(items, nextStream);
    }

    // ── Dispose ───────────────────────────────────────────────────────

    public void Dispose()
    {
        _router.Dispose();
    }
}
