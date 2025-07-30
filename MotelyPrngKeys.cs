
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Motely;

public static class MotelyPrngKeys
{
    public const string Resample = "_resample";

    public const string Voucher = "Voucher";
    public const string ShopPack = "shop_pack";

    public const string Tarot = "Tarot";
    public const string TarotSoul = "soul_"; // Alias for compatibility
    public const string ArcanaPackItemSource = "ar1";
    public const string ArcanaPack = "ar1"; // Alias for compatibility

    public const string Planet = "Planet";
    public const string PlanetBlackHole = "soul_";
    public const string CelestialPackItemSource = "pl1";
    public const string CelestialPack = "pl1"; // Alias for compatibility

    public const string Spectral = "Spectral";
    public const string SpectralSoulBlackHole = "soul_";
    public const string SpectralSoul = "soul_"; // Alias for compatibility
    public const string SpectralPackItemSource = "spe";
    public const string SpectralPack = "spe"; // Alias for compatibility

    public const string StandardCardBase = "front";
    public const string StandardCardHasEnhancement = "stdset";
    public const string StandardCardEnhancement = "Enhanced";
    public const string StandardCardEdition = "standard_edition";
    public const string StandardCardHasSeal = "stdseal";
    public const string StandardCardSeal = "stdsealtype";
    public const string StandardPackItemSource = "sta";

    public const string BuffoonPackItemSource = "buf";
    public const string BuffoonJokerEternalPerishableSource = "packetper";
    public const string BuffoonJokerRentalSource = "packssjr";

    public const string JokerSoulSource = "sou";
    public const string JokerRarity = "rarity";
    public const string JokerEdition = "edi";
    public const string JokerCommon = "Joker1";
    public const string JokerUncommon = "Joker2";
    public const string JokerRare = "Joker3";
    public const string JokerLegendary = "Joker4";

    public const string Tags = "Tag";

    public const string ShopItemType = "cdt";
    public const string ShopItemSource = "sho";
    public const string Shop = "sho"; // Alias for compatibility
    public const string ShopJokerEternalPerishableSource = "etperpoll";
    public const string ShopJokerRentalSource = "ssjr";

    // Additional source keys from cache.cl
    public const string Emperor = "emp";
    public const string HighPriestess = "pri";
    public const string Judgement = "jud";
    public const string Wraith = "wra";
    public const string Vagabond = "vag";
    public const string Superposition = "sup";
    public const string EightBall = "8ba";
    public const string Seance = "sea";
    public const string SixthSense = "sixth";
    public const string TopUp = "top";
    public const string RareTag = "rta";
    public const string UncommonTag = "uta";
    public const string BlueSeal = "blusl";
    public const string PurpleSeal = "8ba"; // Same as 8ball
    public const string Soul = "sou";
    public const string RiffRaff = "rif";
    public const string Cartomancer = "car";

    // Additional type keys from cache.cl  
    public const string Misprint = "misprint";
    public const string ShuffleNewRound = "nr";
    public const string LuckyMult = "lucky_mult";
    public const string LuckyMoney = "lucky_money";
    public const string Sigil = "sigil";
    public const string Ouija = "ouija";
    public const string WheelOfFortune = "wheel_of_fortune";
    public const string GrosMichel = "gros_michel";
    public const string Cavendish = "cavendish";
    public const string VoucherFromTag = "Voucher_fromtag";
    public const string OrbitalTag = "orbital";
    public const string Erratic = "erratic";
    public const string EternalStake = "stake_shop_joker_eternal";
    public const string Perishable = "ssjp";
    public const string Boss = "boss";

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public static string FixedRarityJoker(MotelyJokerRarity rarity, string source, int ante)
    {
        return rarity switch
        {
            MotelyJokerRarity.Common => JokerCommon + source + ante,
            MotelyJokerRarity.Uncommon => JokerUncommon + source + ante,
            MotelyJokerRarity.Rare => JokerRare + source + ante,
            MotelyJokerRarity.Legendary => JokerLegendary,
            _ => throw new InvalidEnumArgumentException()
        };
    }
}