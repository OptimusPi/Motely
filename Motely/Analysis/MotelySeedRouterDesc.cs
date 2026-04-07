using Motely.Filters.Native;

namespace Motely.Analysis;

public sealed class MotelySeedRouterDesc : IMotelySeedRouterDesc, IDisposable
{
    private MotelySearchParameters _searchParams;
    private MotelySearchContextParams _contextParams;
    private int _lane;
    private readonly IMotelySearch? _ownedSearch;

    /// <summary>Direct construction — runs a single-seed search internally, keeps it alive.</summary>
    public MotelySeedRouterDesc(string seed, MotelyDeck deck, MotelyStake stake)
    {
        PassthroughFilterDesc filterDesc = new();
        var settings = new MotelySearchSettings<PassthroughFilterDesc.PassthroughFilter>(filterDesc)
            .WithDeck(deck)
            .WithStake(stake)
            .WithListSearch([seed])
            .WithThreadCount(1)
            .WithSeedRouter(this);
        _ownedSearch = settings.Start();
        _ownedSearch.AwaitCompletion();
    }

    public IMotelySeedRouter CreateSeedRouter(ref MotelyFilterCreationContext ctx)
        => new ContextCapturingRouter(this);

    private readonly struct ContextCapturingRouter(MotelySeedRouterDesc desc) : IMotelySeedRouter
    {
        public void InjectSingleSeedContext(in MotelySingleSearchContext ctx)
        {
            desc._searchParams = ctx.SearchParameters;
            desc._contextParams = ctx.SearchContextParams;
            desc._lane = ctx.VectorLane;
        }
    }

    public MotelySingleSearchContext Instance()
    {
        return new MotelySingleSearchContext(in _searchParams, in _contextParams, _lane);
    }

    public void Dispose() => _ownedSearch?.Dispose();
}

public sealed class MotelySingleSearchContextImpl
{
    private readonly MotelySeedRouterDesc _router;

    // Per-ante stream state
    private readonly Dictionary<int, MotelySingleTagStream> _tagStreams = new();
    private readonly Dictionary<int, MotelySingleBoosterPackStream> _packStreams = new();
    private readonly Dictionary<int, MotelySingleShopItemStream> _shopStreams = new();
    private readonly Dictionary<int, MotelySingleJokerStream> _shopJokerStreams = new();
    private readonly Dictionary<int, MotelySingleTarotStream> _tarotStreams = new();
    private readonly Dictionary<int, MotelySingleSpectralStream> _spectralStreams = new();
    private readonly Dictionary<int, MotelySinglePlanetStream> _planetStreams = new();
    private readonly Dictionary<int, MotelySingleStandardCardStream> _standardCardStreams = new();
    private MotelySingleBossStream? _bossStream;
    private int _bossBitfield;
    private int _lastBossAnte;
    private MotelySinglePrngStream? _misprintStream;
    private MotelySinglePrngStream? _luckyMoneyStream;
    private MotelySinglePrngStream? _luckyMultStream;
    private MotelySinglePrngStream? _erraticStream;

    public MotelySingleSearchContextImpl(MotelySeedRouterDesc router) => _router = router;

    private MotelySingleSearchContext Ctx() => _router.Instance();

    public string GetSeed() => Ctx().GetSeed();
    public double PseudoHash(string key, bool isCached = false) => Ctx().PseudoHash(key, isCached);

    public MotelyVoucher GetAnteFirstVoucher(int ante) => Ctx().GetAnteFirstVoucher(ante);

    public MotelyTag GetNextTag(int ante)
    {
        var ctx = Ctx();
        if (!_tagStreams.TryGetValue(ante, out var stream))
            stream = ctx.CreateTagStream(ante);
        var tag = ctx.GetNextTag(ref stream);
        _tagStreams[ante] = stream;
        return tag;
    }

    public MotelyBossBlind GetBossForAnte(int ante)
    {
        var ctx = Ctx();
        // If jumping ahead, reset and replay from ante 1 to build correct seen-boss state
        if (ante <= _lastBossAnte)
        {
            _bossStream = null;
            _bossBitfield = 0;
            _lastBossAnte = 0;
        }
        _bossStream ??= ctx.CreateBossStream();
        MotelyBossBlind boss = default;
        for (int a = _lastBossAnte + 1; a <= ante; a++)
        {
            var s = _bossStream.Value;
            var state = new MotelyRunState { BossBitfield = _bossBitfield };
            boss = ctx.GetBossForAnte(ref s, a, ref state);
            _bossStream = s;
            _bossBitfield = state.BossBitfield;
        }
        _lastBossAnte = ante;
        return boss;
    }

    public MotelyVoucher GetAnteFirstVoucherWithState(int ante, MotelyJsRunState jsState)
    {
        var state = jsState.ToRunState();
        return Ctx().GetAnteFirstVoucher(ante, in state);
    }

    public MotelyBoosterPack GetNextBoosterPack(int ante)
    {
        var ctx = Ctx();
        if (!_packStreams.TryGetValue(ante, out var stream))
            stream = ctx.CreateBoosterPackStream(ante);
        var pack = ctx.GetNextBoosterPack(ref stream);
        _packStreams[ante] = stream;
        return pack;
    }

    public MotelyItem GetNextShopItem(int ante)
    {
        var ctx = Ctx();
        if (!_shopStreams.TryGetValue(ante, out var stream))
            stream = ctx.CreateShopItemStream(ante);
        var item = ctx.GetNextShopItem(ref stream);
        _shopStreams[ante] = stream;
        return item;
    }

    public MotelyItem GetNextShopJoker(int ante)
    {
        var ctx = Ctx();
        if (!_shopJokerStreams.TryGetValue(ante, out var stream))
            stream = ctx.CreateShopJokerStream(ante);
        var item = ctx.GetNextJoker(ref stream);
        _shopJokerStreams[ante] = stream;
        return item;
    }

    public MotelyItem GetNextTarot(int ante)
    {
        var ctx = Ctx();
        if (!_tarotStreams.TryGetValue(ante, out var stream))
            stream = ctx.CreateShopTarotStream(ante);
        var item = ctx.GetNextTarot(ref stream);
        _tarotStreams[ante] = stream;
        return item;
    }

    public MotelyItem GetNextSpectral(int ante)
    {
        var ctx = Ctx();
        if (!_spectralStreams.TryGetValue(ante, out var stream))
            stream = ctx.CreateShopSpectralStream(ante);
        var item = ctx.GetNextSpectral(ref stream);
        _spectralStreams[ante] = stream;
        return item;
    }

    public MotelyItem GetNextPlanet(int ante)
    {
        var ctx = Ctx();
        if (!_planetStreams.TryGetValue(ante, out var stream))
            stream = ctx.CreateShopPlanetStream(ante);
        var item = ctx.GetNextPlanet(ref stream);
        _planetStreams[ante] = stream;
        return item;
    }

    public MotelyItem GetNextStandardCard(int ante)
    {
        var ctx = Ctx();
        if (!_standardCardStreams.TryGetValue(ante, out var stream))
            stream = ctx.CreateStandardPackCardStream(ante);
        var item = ctx.GetNextStandardCard(ref stream);
        _standardCardStreams[ante] = stream;
        return item;
    }

    public int GetNextMisprintMult()
    {
        var ctx = Ctx();
        _misprintStream ??= ctx.CreateMisprintPrngStream();
        var s = _misprintStream.Value;
        var result = ctx.GetNextMisprintMult(ref s);
        _misprintStream = s;
        return result;
    }

    public bool GetNextLuckyMoney(double baseLuck = 1)
    {
        var ctx = Ctx();
        _luckyMoneyStream ??= ctx.CreateLuckyCardMoneyStream();
        var s = _luckyMoneyStream.Value;
        var result = ctx.GetNextLuckyMoney(ref s, baseLuck);
        _luckyMoneyStream = s;
        return result;
    }

    public bool GetNextLuckyMult(double baseLuck = 1)
    {
        var ctx = Ctx();
        _luckyMultStream ??= ctx.CreateLuckyCardMultStream();
        var s = _luckyMultStream.Value;
        var result = ctx.GetNextLuckyMult(ref s, baseLuck);
        _luckyMultStream = s;
        return result;
    }

    public MotelyItem GetNextErraticDeckCard()
    {
        var ctx = Ctx();
        _erraticStream ??= ctx.CreateErraticDeckPrngStream();
        var s = _erraticStream.Value;
        var item = ctx.GetNextErraticDeckCard(ref s);
        _erraticStream = s;
        return item;
    }
}