namespace Motely;

public static class MotelyPrngKeys
{


    // === Canonical PRNG stream keys (matching Immolate/OpenCL cache.cl) ===
    // From type_str(int x) in cache.cl
    public const string JokerCommon = "Joker1";              // R_Joker_Common
    public const string JokerUncommon = "Joker2";            // R_Joker_Uncommon
    public const string JokerRare = "Joker3";                // R_Joker_Rare
    public const string JokerLegendary = "Joker4";           // R_Joker_Legendary
    public const string JokerRarity = "rarity";              // R_Joker_Rarity
    public const string JokerEdition = "edi";                // R_Joker_Edition
    public const string Misprint = "misprint";               // R_Misprint
    public const string StandardHasEnhancement = "stdset";   // R_Standard_Has_Enhancement
    public const string Enhancement = "Enhanced";            // R_Enhancement
    public const string Card = "front";                      // R_Card
    public const string StandardEdition = "standard_edition"; // R_Standard_Edition
    public const string StandardHasSeal = "stdseal";         // R_Standard_Has_Seal
    public const string StandardSeal = "stdsealtype";        // R_Standard_Seal
    public const string ShopPack = "shop_pack";              // R_Shop_Pack
    public const string Tarot = "Tarot";                     // R_Tarot
    public const string Spectral = "Spectral";               // R_Spectral
    public const string Tags = "Tag";                        // R_Tags
    public const string ShuffleNewRound = "nr";              // R_Shuffle_New_Round
    public const string CardType = "cdt";                    // R_Card_Type
    public const string Planet = "Planet";                   // R_Planet
    public const string LuckyMult = "lucky_mult";            // R_Lucky_Mult
    public const string LuckyMoney = "lucky_money";          // R_Lucky_Money
    public const string Sigil = "sigil";                     // R_Sigil
    public const string Ouija = "ouija";                     // R_Ouija
    public const string WheelOfFortune = "wheel_of_fortune"; // R_Wheel_of_Fortune
    public const string GrosMichel = "gros_michel";          // R_Gros_Michel
    public const string Cavendish = "cavendish";             // R_Cavendish
    public const string Voucher = "Voucher";                 // R_Voucher
    public const string VoucherTag = "Voucher_fromtag";      // R_Voucher_Tag
    public const string OrbitalTag = "orbital";              // R_Orbital_Tag
    public const string Soul = "soul_";                      // R_Soul
    public const string Erratic = "erratic";                 // R_Erratic
    public const string Eternal = "stake_shop_joker_eternal"; // R_Eternal
    public const string Perishable = "ssjp";                 // R_Perishable
    public const string EternalPerishable = "etperpoll";     // R_Eternal_Perishable
    public const string Rental = "ssjr";                     // R_Rental
    public const string RentalPack = "packssjr";             // R_Rental_Pack
    public const string EternalPerishablePack = "packetper"; // R_Eternal_Perishable_Pack
    public const string Boss = "boss";                       // R_Boss

    // From source_str(int x) in cache.cl (for source node types)
    public const string ShopSource = "sho";                  // S_Shop
    public const string EmperorSource = "emp";               // S_Emperor
    public const string HighPriestessSource = "pri";         // S_High_Priestess
    public const string JudgementSource = "jud";             // S_Judgement
    public const string WraithSource = "wra";                // S_Wraith
    public const string ArcanaSource = "ar1";                // S_Arcana
    public const string CelestialSource = "pl1";             // S_Celestial
    public const string SpectralSource = "spe";              // S_Spectral
    public const string StandardSource = "sta";              // S_Standard
    public const string BuffoonSource = "buf";               // S_Buffoon
    public const string VagabondSource = "vag";              // S_Vagabond
    public const string SuperpositionSource = "sup";         // S_Superposition
    public const string EightBallSource = "8ba";             // S_8_Ball
    public const string SeanceSource = "sea";                // S_Seance
    public const string SixthSenseSource = "sixth";          // S_Sixth_Sense
    public const string TopUpSource = "top";                 // S_Top_Up
    public const string RareTagSource = "rta";               // S_Rare_Tag
    public const string UncommonTagSource = "uta";           // S_Uncommon_Tag
    public const string BlueSealSource = "blusl";            // S_Blue_Seal
    public const string PurpleSealSource = "8ba";            // S_Purple_Seal
    public const string SoulSource = "sou";                  // S_Soul
    public const string RiffRaffSource = "rif";              // S_Riff_Raff
    public const string CartomancerSource = "car";           // S_Cartomancer

    // Resample node (see resample_str in cache.cl)
    public static string Resample(int n) => n == 0 ? "" : $"_resample{n+1}";
}