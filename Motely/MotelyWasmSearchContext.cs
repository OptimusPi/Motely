using Motely.Analysis;

namespace Motely;

public sealed class MotelyWasmSearchContext : IMotelyWasmSearchContext
{
    private readonly MotelySeedRouterDesc _router;
    private readonly MotelySingleSearchContext _ctx;

    public MotelyWasmSearchContext(string seed, MotelyDeck deck, MotelyStake stake)
    {
        _router = new MotelySeedRouterDesc(seed, deck, stake);
        _ctx = _router.Instance();
    }

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

    public MotelyVoucherStateResult GetAnteFirstVoucher(int ante, MotelyJsRunState runState)
    {
        var state = runState.ToRunState();
        var voucher = _ctx.GetAnteFirstVoucher(ante, state);
        state.ActivateVoucher(voucher);
        return new(voucher, new(state.VoucherBitfield, state.BossBitfield));
    }

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

    public void Dispose()
    {
        _router.Dispose();
    }
}
