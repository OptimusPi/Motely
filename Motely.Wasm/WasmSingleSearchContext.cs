using System;
using Bootsharp;
using Motely;
using Motely.Enums;
using Motely.Analysis;

namespace Motely.Wasm;

public sealed class WasmShopItemStream
{
    internal MotelySingleShopItemStream Inner;
}

public sealed class WasmJokerStream
{
    internal MotelySingleJokerStream Inner;
}

public sealed class WasmJokerFixedRarityStream
{
    internal MotelySingleJokerFixedRarityStream Inner;
}

public sealed class WasmTarotStream
{
    internal MotelySingleTarotStream Inner;
}

public sealed class WasmPlanetStream
{
    internal MotelySinglePlanetStream Inner;
}

public sealed class WasmSpectralStream
{
    internal MotelySingleSpectralStream Inner;
}

public sealed class WasmStandardCardStream
{
    internal MotelySingleStandardCardStream Inner;
}

public sealed class WasmTagStream
{
    internal MotelySingleTagStream Inner;
}

public sealed class WasmVoucherStream
{
    internal MotelySingleVoucherStream Inner;
}

public sealed class WasmBossStream
{
    internal MotelySingleBossStream Inner;
}

public sealed class WasmPrngStream
{
    internal MotelySinglePrngStream Inner;
}

public sealed class WasmSingleSearchContext
{
    private readonly MotelySeedRouterDesc _router;

    public WasmSingleSearchContext(MotelySeedRouterDesc router)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    public string GetSeed()
    {
        var ctx = _router.Instance();
        return ctx.GetSeed();
    }

    // ── Stream Creators ──────────────────────────────────────────────────

    public WasmShopItemStream CreateShopItemStream(
        int ante,
        int flags = 0,
        int jokerFlags = 0,
        bool isCached = false
    )
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateShopItemStream(ante, (MotelyShopStreamFlags)flags, (MotelyJokerStreamFlags)jokerFlags, isCached);
        return new WasmShopItemStream { Inner = inner };
    }

    public WasmShopItemStream CreateShopItemStreamWithState(
        int ante,
        MotelyJsRunState jsRunState,
        int flags = 0,
        int jokerFlags = 0,
        bool isCached = false
    )
    {
        var ctx = _router.Instance();
        var runState = jsRunState.ToRunState();
        var inner = ctx.CreateShopItemStream(ante, runState, (MotelyShopStreamFlags)flags, (MotelyJokerStreamFlags)jokerFlags, isCached);
        return new WasmShopItemStream { Inner = inner };
    }

    public WasmJokerStream CreateShopJokerStream(int ante, int flags = 0, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateShopJokerStream(ante, (MotelyJokerStreamFlags)flags, isCached);
        return new WasmJokerStream { Inner = inner };
    }

    public WasmJokerStream CreateBuffoonPackJokerStream(int ante, int flags = 0, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateBuffoonPackJokerStream(ante, (MotelyJokerStreamFlags)flags, isCached);
        return new WasmJokerStream { Inner = inner };
    }

    public WasmTarotStream CreateArcanaPackTarotStream(int ante, bool soulOnly = false, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateArcanaPackTarotStream(ante, soulOnly, isCached);
        return new WasmTarotStream { Inner = inner };
    }

    public WasmTarotStream CreateShopTarotStream(int ante, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateShopTarotStream(ante, isCached);
        return new WasmTarotStream { Inner = inner };
    }

    public WasmPlanetStream CreateCelestialPackPlanetStream(int ante, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateCelestialPackPlanetStream(ante, isCached);
        return new WasmPlanetStream { Inner = inner };
    }

    public WasmPlanetStream CreateShopPlanetStream(int ante, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateShopPlanetStream(ante, isCached);
        return new WasmPlanetStream { Inner = inner };
    }

    public WasmSpectralStream CreateSpectralPackSpectralStream(int ante, bool soulOnly = false, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateSpectralPackSpectralStream(ante, soulOnly, isCached);
        return new WasmSpectralStream { Inner = inner };
    }

    public WasmSpectralStream CreateShopSpectralStream(int ante, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateShopSpectralStream(ante, isCached);
        return new WasmSpectralStream { Inner = inner };
    }

    public WasmStandardCardStream CreateStandardPackCardStream(int ante, int flags = 0, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateStandardPackCardStream(ante, (MotelyStandardCardStreamFlags)flags, isCached);
        return new WasmStandardCardStream { Inner = inner };
    }

    public WasmTagStream CreateTagStream(int ante, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateTagStream(ante, isCached);
        return new WasmTagStream { Inner = inner };
    }

    public WasmVoucherStream CreateVoucherStream(int ante, bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateVoucherStream(ante, isCached);
        return new WasmVoucherStream { Inner = inner };
    }

    public WasmBossStream CreateBossStream()
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateBossStream();
        return new WasmBossStream { Inner = inner };
    }

    public WasmPrngStream CreateErraticDeckPrngStream(bool isCached = false)
    {
        var ctx = _router.Instance();
        var inner = ctx.CreateErraticDeckPrngStream(isCached);
        return new WasmPrngStream { Inner = inner };
    }

    // ── Get Next Item from Stream ────────────────────────────────────────

    public int GetNextShopItem(WasmShopItemStream stream)
    {
        var ctx = _router.Instance();
        var item = ctx.GetNextShopItem(ref stream.Inner);
        return item.Value;
    }

    public int GetNextJoker(WasmJokerStream stream)
    {
        var ctx = _router.Instance();
        var item = ctx.GetNextJoker(ref stream.Inner);
        return item.Value;
    }

    public int GetNextJokerFixedRarity(WasmJokerFixedRarityStream stream)
    {
        var ctx = _router.Instance();
        var item = ctx.GetNextJoker(ref stream.Inner);
        return item.Value;
    }

    public int GetNextTarot(WasmTarotStream stream)
    {
        var ctx = _router.Instance();
        var item = ctx.GetNextTarot(ref stream.Inner);
        return item.Value;
    }

    public int GetNextPlanet(WasmPlanetStream stream)
    {
        var ctx = _router.Instance();
        var item = ctx.GetNextPlanet(ref stream.Inner);
        return item.Value;
    }

    public int GetNextSpectral(WasmSpectralStream stream)
    {
        var ctx = _router.Instance();
        var item = ctx.GetNextSpectral(ref stream.Inner);
        return item.Value;
    }

    public int GetNextStandardCard(WasmStandardCardStream stream)
    {
        var ctx = _router.Instance();
        var item = ctx.GetNextStandardCard(ref stream.Inner);
        return item.Value;
    }

    public int GetNextTag(WasmTagStream stream)
    {
        var ctx = _router.Instance();
        var tag = ctx.GetNextTag(ref stream.Inner);
        return (int)tag;
    }

    public int GetNextVoucher(WasmVoucherStream stream, MotelyJsRunState jsRunState, out MotelyJsRunState nextJsRunState)
    {
        var ctx = _router.Instance();
        var runState = jsRunState.ToRunState();
        var voucher = ctx.GetNextVoucher(ref stream.Inner, runState);
        nextJsRunState = new MotelyJsRunState(runState.VoucherBitfield, runState.BossBitfield);
        return (int)voucher;
    }

    public int GetAnteFirstVoucher(int ante, MotelyJsRunState jsRunState, out MotelyJsRunState nextJsRunState)
    {
        var ctx = _router.Instance();
        var runState = jsRunState.ToRunState();
        var voucher = ctx.GetAnteFirstVoucher(ante, runState);
        nextJsRunState = new MotelyJsRunState(runState.VoucherBitfield, runState.BossBitfield);
        return (int)voucher;
    }

    public int GetBossForAnte(WasmBossStream stream, int ante, MotelyJsRunState jsRunState, out MotelyJsRunState nextJsRunState)
    {
        var ctx = _router.Instance();
        var runState = jsRunState.ToRunState();
        var boss = ctx.GetBossForAnte(ref stream.Inner, ante, ref runState);
        nextJsRunState = new MotelyJsRunState(runState.VoucherBitfield, runState.BossBitfield);
        return (int)boss;
    }

    public int GetNextErraticDeckCard(WasmPrngStream stream)
    {
        var ctx = _router.Instance();
        var item = ctx.GetNextErraticDeckCard(ref stream.Inner);
        return item.Value;
    }
}
