using Motely.Analysis;

namespace Motely;

public sealed class MotelySeedExplorer : IMotelySeedExplorer, IDisposable
{
    private readonly MotelySeedRouterDesc _router;
    private MotelySingleSearchContext _ctx;

    // Stream state — one active stream per category
    private MotelySingleShopItemStream _shopStream;
    private bool _hasShopStream;

    private MotelySingleJokerStream _jokerStream;
    private bool _hasJokerStream;

    private MotelySingleJokerFixedRarityStream _fixedJokerStream;
    private bool _hasFixedJokerStream;

    private MotelySingleTagStream _tagStream;
    private bool _hasTagStream;

    private MotelySingleVoucherStream _voucherStream;
    private bool _hasVoucherStream;

    private MotelySingleBossStream _bossStream;
    private bool _hasBossStream;

    private MotelySingleBoosterPackStream _packStream;
    private bool _hasPackStream;

    private MotelySingleTarotStream _tarotStream;
    private bool _hasTarotStream;

    private MotelySinglePlanetStream _planetStream;
    private bool _hasPlanetStream;

    private MotelySingleSpectralStream _spectralStream;
    private bool _hasSpectralStream;

    private MotelySingleStandardCardStream _standardCardStream;
    private bool _hasStandardCardStream;

    private MotelySinglePrngStream _luckyMoneyStream;
    private bool _hasLuckyMoneyStream;

    private MotelySinglePrngStream _luckyMultStream;
    private bool _hasLuckyMultStream;

    private MotelySinglePrngStream _misprintStream;
    private bool _hasMisprintStream;

    private MotelySinglePrngStream _wheelStream;
    private bool _hasWheelStream;

    private MotelySinglePrngStream _erraticStream;
    private bool _hasErraticStream;

    private MotelySinglePrngStream _cavendishStream;
    private bool _hasCavendishStream;

    private MotelySinglePrngStream _grosMichelStream;
    private bool _hasGrosMichelStream;

    // Persistent state for boss/voucher tracking
    private int _voucherBitfield;
    private int _bossBitfield;

    public MotelySeedExplorer(string seed, MotelyDeck deck = MotelyDeck.Red, MotelyStake stake = MotelyStake.White)
    {
        _router = new MotelySeedRouterDesc(seed, deck, stake);
        _ctx = _router.Instance();

        // Pre-apply deck default vouchers to voucher state
        var defaultState = deck.GetDefaultRunState();
        _voucherBitfield = defaultState.VoucherBitfield;
    }

    // === Shop Items ===

    public void CreateShopItemStream(int ante)
    {
        _shopStream = _ctx.CreateShopItemStream(ante);
        _hasShopStream = true;
    }

    public string NextShopItem()
    {
        AssertStream(_hasShopStream, "shop item");
        return FormatUtils.FormatItem(_ctx.GetNextShopItem(ref _shopStream));
    }

    // === Jokers (variable rarity) ===

    public void CreateShopJokerStream(int ante)
    {
        _jokerStream = _ctx.CreateShopJokerStream(ante);
        _hasJokerStream = true;
    }

    public void CreateBuffoonPackJokerStream(int ante)
    {
        _jokerStream = _ctx.CreateBuffoonPackJokerStream(ante);
        _hasJokerStream = true;
    }

    public void CreateJudgementJokerStream(int ante)
    {
        _jokerStream = _ctx.CreateJudgementJokerStream(ante);
        _hasJokerStream = true;
    }

    public void CreateWraithJokerStream(int ante)
    {
        _jokerStream = _ctx.CreateWraithJokerStream(ante);
        _hasJokerStream = true;
    }

    public string NextJoker()
    {
        AssertStream(_hasJokerStream, "joker");
        return FormatUtils.FormatItem(_ctx.GetNextJoker(ref _jokerStream));
    }

    // === Jokers (fixed rarity) ===

    public void CreateSoulJokerStream(int ante)
    {
        _fixedJokerStream = _ctx.CreateSoulJokerStream(ante);
        _hasFixedJokerStream = true;
    }

    public void CreateRareTagJokerStream(int ante)
    {
        _fixedJokerStream = _ctx.CreateRareTagJokerStream(ante);
        _hasFixedJokerStream = true;
    }

    public void CreateUncommonTagJokerStream(int ante)
    {
        _fixedJokerStream = _ctx.CreateUncommonTagJokerStream(ante);
        _hasFixedJokerStream = true;
    }

    public void CreateRiffRaffJokerStream(int ante)
    {
        _fixedJokerStream = _ctx.CreateRiffRaffJokerStream(ante);
        _hasFixedJokerStream = true;
    }

    public void CreateCommonShopJokerStream(int ante)
    {
        _fixedJokerStream = _ctx.CreateCommonShopJokerStream(ante);
        _hasFixedJokerStream = true;
    }

    public void CreateUncommonShopJokerStream(int ante)
    {
        _fixedJokerStream = _ctx.CreateUncommonShopJokerStream(ante);
        _hasFixedJokerStream = true;
    }

    public void CreateRareShopJokerStream(int ante)
    {
        _fixedJokerStream = _ctx.CreateRareShopJokerStream(ante);
        _hasFixedJokerStream = true;
    }

    public string NextFixedRarityJoker()
    {
        AssertStream(_hasFixedJokerStream, "fixed rarity joker");
        return FormatUtils.FormatItem(_ctx.GetNextJoker(ref _fixedJokerStream));
    }

    // === Tags ===

    public void CreateTagStream(int ante)
    {
        _tagStream = _ctx.CreateTagStream(ante);
        _hasTagStream = true;
    }

    public string NextTag()
    {
        AssertStream(_hasTagStream, "tag");
        return FormatUtils.FormatTag(_ctx.GetNextTag(ref _tagStream));
    }

    // === Vouchers ===

    public void CreateVoucherStream(int ante)
    {
        _voucherStream = _ctx.CreateVoucherStream(ante);
        _hasVoucherStream = true;
    }

    public string NextVoucher()
    {
        AssertStream(_hasVoucherStream, "voucher");
        MotelyRunState state = default;
        state.VoucherBitfield = _voucherBitfield;
        var voucher = _ctx.GetNextVoucher(ref _voucherStream, in state);
        return FormatUtils.FormatVoucher(voucher);
    }

    public string GetAnteFirstVoucher(int ante)
    {
        MotelyRunState state = default;
        state.VoucherBitfield = _voucherBitfield;
        var voucher = _ctx.GetAnteFirstVoucher(ante, in state);
        return FormatUtils.FormatVoucher(voucher);
    }

    // === Bosses ===

    public void CreateBossStream()
    {
        _bossStream = _ctx.CreateBossStream();
        _hasBossStream = true;
    }

    public string GetBossForAnte(int ante)
    {
        if (!_hasBossStream)
            CreateBossStream();

        MotelyRunState state = default;
        state.BossBitfield = _bossBitfield;
        var boss = _ctx.GetBossForAnte(ref _bossStream, ante, ref state);
        _bossBitfield = state.BossBitfield;
        return FormatUtils.FormatBoss(boss);
    }

    // === Booster Packs ===

    public void CreateBoosterPackStream(int ante)
    {
        _packStream = _ctx.CreateBoosterPackStream(ante);
        _hasPackStream = true;
    }

    public string NextBoosterPack()
    {
        AssertStream(_hasPackStream, "booster pack");
        return FormatUtils.FormatPackName(_ctx.GetNextBoosterPack(ref _packStream));
    }

    // === Tarots ===

    public void CreateShopTarotStream(int ante)
    {
        _tarotStream = _ctx.CreateShopTarotStream(ante);
        _hasTarotStream = true;
    }

    public void CreateArcanaPackTarotStream(int ante)
    {
        _tarotStream = _ctx.CreateArcanaPackTarotStream(ante);
        _hasTarotStream = true;
    }

    public void CreateEmperorTarotStream(int ante)
    {
        _tarotStream = _ctx.CreateEmperorTarotStream(ante);
        _hasTarotStream = true;
    }

    public void CreatePurpleSealTarotStream(int ante)
    {
        _tarotStream = _ctx.CreatePurpleSealTarotStream(ante);
        _hasTarotStream = true;
    }

    public string NextTarot()
    {
        AssertStream(_hasTarotStream, "tarot");
        return FormatUtils.FormatItem(_ctx.GetNextTarot(ref _tarotStream));
    }

    // === Planets ===

    public void CreateShopPlanetStream(int ante)
    {
        _planetStream = _ctx.CreateShopPlanetStream(ante);
        _hasPlanetStream = true;
    }

    public void CreateCelestialPackPlanetStream(int ante)
    {
        _planetStream = _ctx.CreateCelestialPackPlanetStream(ante);
        _hasPlanetStream = true;
    }

    public string NextPlanet()
    {
        AssertStream(_hasPlanetStream, "planet");
        return FormatUtils.FormatItem(_ctx.GetNextPlanet(ref _planetStream));
    }

    // === Spectrals ===

    public void CreateShopSpectralStream(int ante)
    {
        _spectralStream = _ctx.CreateShopSpectralStream(ante);
        _hasSpectralStream = true;
    }

    public void CreateSpectralPackSpectralStream(int ante)
    {
        _spectralStream = _ctx.CreateSpectralPackSpectralStream(ante);
        _hasSpectralStream = true;
    }

    public void CreateSixthSenseSpectralStream(int ante)
    {
        _spectralStream = _ctx.CreateSixthSenseSpectralStream(ante);
        _hasSpectralStream = true;
    }

    public void CreateSeanceSpectralStream(int ante)
    {
        _spectralStream = _ctx.CreateSeanceSpectralStream(ante);
        _hasSpectralStream = true;
    }

    public string NextSpectral()
    {
        AssertStream(_hasSpectralStream, "spectral");
        return FormatUtils.FormatItem(_ctx.GetNextSpectral(ref _spectralStream));
    }

    // === Standard Cards ===

    public void CreateStandardPackCardStream(int ante)
    {
        _standardCardStream = _ctx.CreateStandardPackCardStream(ante);
        _hasStandardCardStream = true;
    }

    public string NextStandardCard()
    {
        AssertStream(_hasStandardCardStream, "standard card");
        return FormatUtils.FormatItem(_ctx.GetNextStandardCard(ref _standardCardStream));
    }

    // === Lucky Cards ===

    public void CreateLuckyCardMoneyStream()
    {
        _luckyMoneyStream = _ctx.CreateLuckyCardMoneyStream();
        _hasLuckyMoneyStream = true;
    }

    public bool NextLuckyMoney()
    {
        AssertStream(_hasLuckyMoneyStream, "lucky money");
        return _ctx.GetNextLuckyMoney(ref _luckyMoneyStream);
    }

    public void CreateLuckyCardMultStream()
    {
        _luckyMultStream = _ctx.CreateLuckyCardMultStream();
        _hasLuckyMultStream = true;
    }

    public bool NextLuckyMult()
    {
        AssertStream(_hasLuckyMultStream, "lucky mult");
        return _ctx.GetNextLuckyMult(ref _luckyMultStream);
    }

    // === Misprint ===

    public void CreateMisprintStream()
    {
        _misprintStream = _ctx.CreateMisprintPrngStream();
        _hasMisprintStream = true;
    }

    public int NextMisprintMult()
    {
        AssertStream(_hasMisprintStream, "misprint");
        return _ctx.GetNextMisprintMult(ref _misprintStream);
    }

    // === Wheel of Fortune ===

    public void CreateWheelOfFortuneStream()
    {
        _wheelStream = _ctx.CreateWheelOfFortuneStream();
        _hasWheelStream = true;
    }

    public string NextWheelOfFortune()
    {
        AssertStream(_hasWheelStream, "wheel of fortune");
        return _ctx.GetNextWheelOfFortune(ref _wheelStream).ToString();
    }

    // === Erratic Deck ===

    public void CreateErraticDeckStream()
    {
        _erraticStream = _ctx.CreateErraticDeckPrngStream();
        _hasErraticStream = true;
    }

    public string NextErraticDeckCard()
    {
        AssertStream(_hasErraticStream, "erratic deck");
        return FormatUtils.FormatItem(_ctx.GetNextErraticDeckCard(ref _erraticStream));
    }

    // === Banana Jokers ===

    public void CreateCavendishStream()
    {
        _cavendishStream = _ctx.CreateCavendishPrngStream();
        _hasCavendishStream = true;
    }

    public bool NextCavendishExtinct()
    {
        AssertStream(_hasCavendishStream, "cavendish");
        return _ctx.GetNextCavendishExtinct(ref _cavendishStream);
    }

    public void CreateGrosMichelStream()
    {
        _grosMichelStream = _ctx.CreateGrosMichelPrngStream();
        _hasGrosMichelStream = true;
    }

    public bool NextGrosMichelExtinct()
    {
        AssertStream(_hasGrosMichelStream, "gros michel");
        return _ctx.GetNextGrosMichelExtinct(ref _grosMichelStream);
    }

    // === Lifecycle ===

    public void Dispose() => _router.Dispose();

    private static void AssertStream(bool has, string name)
    {
        if (!has)
            throw new InvalidOperationException($"No {name} stream active. Call the corresponding Create* method first.");
    }
}
