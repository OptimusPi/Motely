namespace Motely;

public interface IMotelySeedExplorer : IDisposable
{
    // === Shop Items (mixed joker/tarot/planet/spectral per rate weights) ===
    void CreateShopItemStream(int ante);
    string NextShopItem();

    // === Joker streams (variable rarity) ===
    void CreateShopJokerStream(int ante);
    void CreateBuffoonPackJokerStream(int ante);
    void CreateJudgementJokerStream(int ante);
    void CreateWraithJokerStream(int ante);
    string NextJoker();

    // === Joker streams (fixed rarity) ===
    void CreateSoulJokerStream(int ante);
    void CreateRareTagJokerStream(int ante);
    void CreateUncommonTagJokerStream(int ante);
    void CreateRiffRaffJokerStream(int ante);
    void CreateCommonShopJokerStream(int ante);
    void CreateUncommonShopJokerStream(int ante);
    void CreateRareShopJokerStream(int ante);
    string NextFixedRarityJoker();

    // === Tags ===
    void CreateTagStream(int ante);
    string NextTag();

    // === Vouchers ===
    void CreateVoucherStream(int ante);
    string NextVoucher();
    string GetAnteFirstVoucher(int ante);

    // === Bosses ===
    void CreateBossStream();
    string GetBossForAnte(int ante);

    // === Booster Packs ===
    void CreateBoosterPackStream(int ante);
    string NextBoosterPack();

    // === Tarots ===
    void CreateShopTarotStream(int ante);
    void CreateArcanaPackTarotStream(int ante);
    void CreateEmperorTarotStream(int ante);
    void CreatePurpleSealTarotStream(int ante);
    string NextTarot();

    // === Planets ===
    void CreateShopPlanetStream(int ante);
    void CreateCelestialPackPlanetStream(int ante);
    string NextPlanet();

    // === Spectrals ===
    void CreateShopSpectralStream(int ante);
    void CreateSpectralPackSpectralStream(int ante);
    void CreateSixthSenseSpectralStream(int ante);
    void CreateSeanceSpectralStream(int ante);
    string NextSpectral();

    // === Standard Cards ===
    void CreateStandardPackCardStream(int ante);
    string NextStandardCard();

    // === Lucky Cards ===
    void CreateLuckyCardMoneyStream();
    bool NextLuckyMoney();
    void CreateLuckyCardMultStream();
    bool NextLuckyMult();

    // === Misprint ===
    void CreateMisprintStream();
    int NextMisprintMult();

    // === Wheel of Fortune ===
    void CreateWheelOfFortuneStream();
    string NextWheelOfFortune();

    // === Erratic Deck ===
    void CreateErraticDeckStream();
    string NextErraticDeckCard();

    // === Banana Jokers ===
    void CreateCavendishStream();
    bool NextCavendishExtinct();
    void CreateGrosMichelStream();
    bool NextGrosMichelExtinct();
}
