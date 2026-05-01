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

    // ── Joker Streams ──────────────────────────────────────────────────

    public MotelySingleJokerStream CreateShopJokerStream(int ante, MotelyJokerStreamFlags flags)
    {
        return _ctx.CreateShopJokerStream(ante, flags);
    }

    public MotelyJokerStreamResult GetNextShopJoker(MotelySingleJokerStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyJokerChunkResult GetNextShopJokerChunk(MotelySingleJokerStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerStream CreateBuffoonPackJokerStream(int ante, MotelyJokerStreamFlags flags)
    {
        return _ctx.CreateBuffoonPackJokerStream(ante, flags);
    }

    public MotelyJokerStreamResult GetNextBuffoonPackJoker(MotelySingleJokerStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyJokerChunkResult GetNextBuffoonPackJokerChunk(MotelySingleJokerStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelyItemSetResult GetNextBuffoonPackContents(MotelySingleJokerStream stream, MotelyBoosterPackSize size)
    {
        var nextStream = stream;
        var items = _ctx.GetNextBuffoonPackContents(ref nextStream, size);
        return new(ToItemValues(items), nextStream);
    }

    public MotelyItemSetResult GetNextBuffoonPackContentsSized(MotelySingleJokerStream stream, int size)
    {
        var nextStream = stream;
        var items = _ctx.GetNextBuffoonPackContents(ref nextStream, size);
        return new(ToItemValues(items), nextStream);
    }

    public MotelySingleJokerStream CreateJudgementJokerStream(int ante, MotelyJokerStreamFlags flags)
    {
        return _ctx.CreateJudgementJokerStream(ante, flags);
    }

    public MotelyJokerStreamResult GetNextJudgementJoker(MotelySingleJokerStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyJokerChunkResult GetNextJudgementJokerChunk(MotelySingleJokerStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerStream CreateWraithJokerStream(int ante, MotelyJokerStreamFlags flags)
    {
        return _ctx.CreateWraithJokerStream(ante, flags);
    }

    public MotelyJokerStreamResult GetNextWraithJoker(MotelySingleJokerStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyJokerChunkResult GetNextWraithJokerChunk(MotelySingleJokerStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerFixedRarityStream CreateLegendaryJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags)
    {
        return _ctx.CreateLegendaryJokerStream(ante, flags);
    }

    public MotelyFixedJokerStreamResult GetNextLegendaryJoker(MotelySingleJokerFixedRarityStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyFixedJokerChunkResult GetNextLegendaryJokerChunk(MotelySingleJokerFixedRarityStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerFixedRarityStream CreateRareTagJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags)
    {
        return _ctx.CreateRareTagJokerStream(ante, flags);
    }

    public MotelyFixedJokerStreamResult GetNextRareTagJoker(MotelySingleJokerFixedRarityStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyFixedJokerChunkResult GetNextRareTagJokerChunk(MotelySingleJokerFixedRarityStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerFixedRarityStream CreateUncommonTagJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags)
    {
        return _ctx.CreateUncommonTagJokerStream(ante, flags);
    }

    public MotelyFixedJokerStreamResult GetNextUncommonTagJoker(MotelySingleJokerFixedRarityStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyFixedJokerChunkResult GetNextUncommonTagJokerChunk(MotelySingleJokerFixedRarityStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerFixedRarityStream CreateRiffRaffJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags)
    {
        return _ctx.CreateRiffRaffJokerStream(ante, flags);
    }

    public MotelyFixedJokerStreamResult GetNextRiffRaffJoker(MotelySingleJokerFixedRarityStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyFixedJokerChunkResult GetNextRiffRaffJokerChunk(MotelySingleJokerFixedRarityStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerFixedRarityStream CreateCommonShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags)
    {
        return _ctx.CreateCommonShopJokerStream(ante, flags);
    }

    public MotelyFixedJokerStreamResult GetNextCommonShopJoker(MotelySingleJokerFixedRarityStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyFixedJokerChunkResult GetNextCommonShopJokerChunk(MotelySingleJokerFixedRarityStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerFixedRarityStream CreateUncommonShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags)
    {
        return _ctx.CreateUncommonShopJokerStream(ante, flags);
    }

    public MotelyFixedJokerStreamResult GetNextUncommonShopJoker(MotelySingleJokerFixedRarityStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyFixedJokerChunkResult GetNextUncommonShopJokerChunk(MotelySingleJokerFixedRarityStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleJokerFixedRarityStream CreateRareShopJokerStream(int ante, MotelyJokerFixedRarityStreamFlags flags)
    {
        return _ctx.CreateRareShopJokerStream(ante, flags);
    }

    public MotelyFixedJokerStreamResult GetNextRareShopJoker(MotelySingleJokerFixedRarityStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextJoker(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyFixedJokerChunkResult GetNextRareShopJokerChunk(MotelySingleJokerFixedRarityStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextJoker(ref nextStream).Value;
        return new(items, nextStream);
    }

    // ── Tarot ──────────────────────────────────────────────────────────

    public MotelySingleTarotStream CreateArcanaPackTarotStream(int ante, bool soulOnly)
    {
        return _ctx.CreateArcanaPackTarotStream(ante, soulOnly);
    }

    public MotelyBoolTarotStreamResult GetNextArcanaPackHasTheSoul(MotelySingleTarotStream stream, MotelyBoosterPackSize size)
    {
        var nextStream = stream;
        var value = _ctx.GetNextArcanaPackHasTheSoul(ref nextStream, size);
        return new(value, nextStream);
    }

    public MotelyTarotChunkResult GetNextArcanaPackContents(MotelySingleTarotStream stream, MotelyBoosterPackSize size)
    {
        var nextStream = stream;
        var items = _ctx.GetNextArcanaPackContents(ref nextStream, size);
        return new(ToItemValues(items), nextStream);
    }

    public MotelyTarotStreamResult GetNextTarot(MotelySingleTarotStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextTarot(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyTarotChunkResult GetNextTarotChunk(MotelySingleTarotStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextTarot(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleTarotStream CreateShopTarotStream(int ante)
    {
        return _ctx.CreateShopTarotStream(ante);
    }

    public MotelyTarotStreamResult GetNextShopTarot(MotelySingleTarotStream stream) => GetNextTarot(stream);

    public MotelyTarotChunkResult GetNextShopTarotChunk(MotelySingleTarotStream stream, int count) =>
        GetNextTarotChunk(stream, count);

    public MotelySingleTarotStream CreateEmperorTarotStream(int ante)
    {
        return _ctx.CreateEmperorTarotStream(ante);
    }

    public MotelyTarotPairResult GetNextEmperorTarots(MotelySingleTarotStream stream)
    {
        var nextStream = stream;
        var (first, second) = _ctx.GetNextEmperorTarots(ref nextStream);
        return new(first, second, nextStream);
    }

    public MotelySingleTarotStream CreatePurpleSealTarotStream(int ante)
    {
        return _ctx.CreatePurpleSealTarotStream(ante);
    }

    public MotelyTarotStreamResult GetNextPurpleSealTarot(MotelySingleTarotStream stream) => GetNextTarot(stream);

    public MotelyTarotChunkResult GetNextPurpleSealTarotChunk(MotelySingleTarotStream stream, int count) =>
        GetNextTarotChunk(stream, count);

    // ── Planets ────────────────────────────────────────────────────────

    public MotelySinglePlanetStream CreateCelestialPackPlanetStream(int ante)
    {
        return _ctx.CreateCelestialPackPlanetStream(ante);
    }

    public MotelyPlanetChunkResult GetNextCelestialPackContents(MotelySinglePlanetStream stream, MotelyBoosterPackSize size)
    {
        var nextStream = stream;
        var items = _ctx.GetNextCelestialPackContents(ref nextStream, size);
        return new(ToItemValues(items), nextStream);
    }

    public MotelyPlanetStreamResult GetNextPlanet(MotelySinglePlanetStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextPlanet(ref nextStream);
        return new(item, nextStream);
    }

    public MotelyPlanetChunkResult GetNextPlanetChunk(MotelySinglePlanetStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextPlanet(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySinglePlanetStream CreateShopPlanetStream(int ante)
    {
        return _ctx.CreateShopPlanetStream(ante);
    }

    public MotelyPlanetStreamResult GetNextShopPlanet(MotelySinglePlanetStream stream) => GetNextPlanet(stream);

    public MotelyPlanetChunkResult GetNextShopPlanetChunk(MotelySinglePlanetStream stream, int count) =>
        GetNextPlanetChunk(stream, count);

    // ── Spectral ───────────────────────────────────────────────────────

    public MotelySingleSpectralStream CreateSpectralPackSpectralStream(int ante, bool soulOnly)
    {
        return _ctx.CreateSpectralPackSpectralStream(ante, soulOnly);
    }

    public MotelyBoolSpectralStreamResult GetNextSpectralPackHasTheSoul(MotelySingleSpectralStream stream, MotelyBoosterPackSize size)
    {
        var nextStream = stream;
        var value = _ctx.GetNextSpectralPackHasTheSoul(ref nextStream, size);
        return new(value, nextStream);
    }

    public MotelySpectralChunkResult GetNextSpectralPackContents(MotelySingleSpectralStream stream, MotelyBoosterPackSize size)
    {
        var nextStream = stream;
        var items = _ctx.GetNextSpectralPackContents(ref nextStream, size);
        return new(ToItemValues(items), nextStream);
    }

    public MotelySpectralStreamResult GetNextSpectral(MotelySingleSpectralStream stream)
    {
        var nextStream = stream;
        var item = _ctx.GetNextSpectral(ref nextStream);
        return new(item, nextStream);
    }

    public MotelySpectralChunkResult GetNextSpectralChunk(MotelySingleSpectralStream stream, int count)
    {
        ValidateChunkCount(count);
        var nextStream = stream;
        var items = new int[count];
        for (int i = 0; i < count; i++)
            items[i] = (int)_ctx.GetNextSpectral(ref nextStream).Value;
        return new(items, nextStream);
    }

    public MotelySingleSpectralStream CreateShopSpectralStream(int ante)
    {
        return _ctx.CreateShopSpectralStream(ante);
    }

    public MotelySpectralStreamResult GetNextShopSpectral(MotelySingleSpectralStream stream) => GetNextSpectral(stream);

    public MotelySpectralChunkResult GetNextShopSpectralChunk(MotelySingleSpectralStream stream, int count) =>
        GetNextSpectralChunk(stream, count);

    public MotelySingleSpectralStream CreateSixthSenseSpectralStream(int ante)
    {
        return _ctx.CreateSixthSenseSpectralStream(ante);
    }

    public MotelySpectralStreamResult GetNextSixthSenseSpectral(MotelySingleSpectralStream stream) => GetNextSpectral(stream);

    public MotelySpectralChunkResult GetNextSixthSenseSpectralChunk(MotelySingleSpectralStream stream, int count) =>
        GetNextSpectralChunk(stream, count);

    public MotelySingleSpectralStream CreateSeanceSpectralStream(int ante)
    {
        return _ctx.CreateSeanceSpectralStream(ante);
    }

    public MotelySpectralStreamResult GetNextSeanceSpectral(MotelySingleSpectralStream stream) => GetNextSpectral(stream);

    public MotelySpectralChunkResult GetNextSeanceSpectralChunk(MotelySingleSpectralStream stream, int count) =>
        GetNextSpectralChunk(stream, count);

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

    // ── Dispose / Cancel ─────────────────────────────────────────────

    public void Cancel() => Dispose();

    public void Dispose()
    {
        _router.Dispose();
    }

    private static void ValidateChunkCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxChunkCount);
    }

    private static int[] ToItemValues(MotelySingleItemSet itemSet)
    {
        var values = new int[itemSet.Length];
        for (int i = 0; i < itemSet.Length; i++)
            values[i] = (int)itemSet[i].Value;
        return values;
    }
}
