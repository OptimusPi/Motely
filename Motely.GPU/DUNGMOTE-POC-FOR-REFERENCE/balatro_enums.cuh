/**
 * @file balatro_enums.cuh
 * @brief Complete Balatro game enums for CUDA seed searching
 *
 * This file contains ALL the game enums needed for seed searching,
 * matching Balatro's game data structure.
 */

#ifndef BALATRO_ENUMS_CUH
#define BALATRO_ENUMS_CUH

// ============================================================================
// Joker Counts (for pool size calculations)
// ============================================================================
#define NUM_COMMON_JOKERS   61
#define NUM_UNCOMMON_JOKERS 64
#define NUM_RARE_JOKERS     20
#define NUM_LEGENDARY_JOKERS 5

// ============================================================================
// Joker Rarity (top 2 bits encode rarity)
// ============================================================================
#define JOKER_RARITY_OFFSET 6
#define JOKER_RARITY_MASK   (0b11 << JOKER_RARITY_OFFSET)
#define JOKER_INDEX_MASK    ((1 << JOKER_RARITY_OFFSET) - 1)

enum JokerRarity : int {
    RARITY_COMMON    = 0b00 << JOKER_RARITY_OFFSET,
    RARITY_UNCOMMON  = 0b01 << JOKER_RARITY_OFFSET,
    RARITY_RARE      = 0b10 << JOKER_RARITY_OFFSET,
    RARITY_LEGENDARY = 0b11 << JOKER_RARITY_OFFSET
};

// ============================================================================
// Common Jokers (61 total)
// ============================================================================
enum JokerCommon : int {
    J_JOKER = 0,
    J_GREEDY_JOKER = 1,
    J_LUSTY_JOKER = 2,
    J_WRATHFUL_JOKER = 3,
    J_GLUTTONOUS_JOKER = 4,
    J_JOLLY_JOKER = 5,
    J_ZANY_JOKER = 6,
    J_MAD_JOKER = 7,
    J_CRAZY_JOKER = 8,
    J_DROLL_JOKER = 9,
    J_SLY_JOKER = 10,
    J_WILY_JOKER = 11,
    J_CLEVER_JOKER = 12,
    J_DEVIOUS_JOKER = 13,
    J_CRAFTY_JOKER = 14,
    J_HALF_JOKER = 15,
    J_CREDIT_CARD = 16,
    J_BANNER = 17,
    J_MYSTIC_SUMMIT = 18,
    J_EIGHT_BALL = 19,
    J_MISPRINT = 20,
    J_RAISED_FIST = 21,
    J_CHAOS_THE_CLOWN = 22,
    J_SCARY_FACE = 23,
    J_ABSTRACT_JOKER = 24,
    J_DELAYED_GRATIFICATION = 25,
    J_GROS_MICHEL = 26,
    J_EVEN_STEVEN = 27,
    J_ODD_TODD = 28,
    J_SCHOLAR = 29,
    J_BUSINESS_CARD = 30,
    J_SUPERNOVA = 31,
    J_RIDE_THE_BUS = 32,
    J_EGG = 33,
    J_RUNNER = 34,
    J_ICE_CREAM = 35,
    J_SPLASH = 36,
    J_BLUE_JOKER = 37,
    J_FACELESS_JOKER = 38,
    J_GREEN_JOKER = 39,
    J_SUPERPOSITION = 40,
    J_TODO_LIST = 41,
    J_CAVENDISH = 42,
    J_RED_CARD = 43,
    J_SQUARE_JOKER = 44,
    J_RIFF_RAFF = 45,
    J_PHOTOGRAPH = 46,
    J_RESERVED_PARKING = 47,
    J_MAIL_IN_REBATE = 48,
    J_HALLUCINATION = 49,
    J_FORTUNE_TELLER = 50,
    J_JUGGLER = 51,
    J_DRUNKARD = 52,
    J_GOLDEN_JOKER = 53,
    J_POPCORN = 54,
    J_WALKIE_TALKIE = 55,
    J_SMILEY_FACE = 56,
    J_GOLDEN_TICKET = 57,
    J_SWASHBUCKLER = 58,
    J_HANGING_CHAD = 59,
    J_SHOOT_THE_MOON = 60
};

// ============================================================================
// Uncommon Jokers (64 total)
// ============================================================================
enum JokerUncommon : int {
    J_JOKER_STENCIL = 0,
    J_FOUR_FINGERS = 1,
    J_MIME = 2,
    J_CEREMONIAL_DAGGER = 3,
    J_MARBLE_JOKER = 4,
    J_LOYALTY_CARD = 5,
    J_DUSK = 6,
    J_FIBONACCI = 7,
    J_STEEL_JOKER = 8,
    J_HACK = 9,
    J_PAREIDOLIA = 10,
    J_SPACE_JOKER = 11,
    J_BURGLAR = 12,
    J_BLACKBOARD = 13,
    J_SIXTH_SENSE = 14,
    J_CONSTELLATION = 15,
    J_HIKER = 16,
    J_CARD_SHARP = 17,
    J_MADNESS = 18,
    J_SEANCE = 19,
    J_VAMPIRE = 20,
    J_SHORTCUT = 21,
    J_HOLOGRAM = 22,
    J_CLOUD_9 = 23,
    J_ROCKET = 24,
    J_MIDAS_MASK = 25,
    J_LUCHADOR = 26,
    J_GIFT_CARD = 27,
    J_TURTLE_BEAN = 28,
    J_EROSION = 29,
    J_TO_THE_MOON = 30,
    J_STONE_JOKER = 31,
    J_LUCKY_CAT = 32,
    J_BULL = 33,
    J_DIET_COLA = 34,
    J_TRADING_CARD = 35,
    J_FLASH_CARD = 36,
    J_SPARE_TROUSERS = 37,
    J_RAMEN = 38,
    J_SELTZER = 39,
    J_CASTLE = 40,
    J_MR_BONES = 41,
    J_ACROBAT = 42,
    J_SOCK_AND_BUSKIN = 43,
    J_TROUBADOUR = 44,
    J_CERTIFICATE = 45,
    J_SMEARED_JOKER = 46,
    J_THROWBACK = 47,
    J_ROUGH_GEM = 48,
    J_BLOODSTONE = 49,
    J_ARROWHEAD = 50,
    J_ONYX_AGATE = 51,
    J_GLASS_JOKER = 52,
    J_SHOWMAN = 53,
    J_FLOWER_POT = 54,
    J_MERRY_ANDY = 55,
    J_OOPS_ALL_6S = 56,
    J_THE_IDOL = 57,
    J_SEEING_DOUBLE = 58,
    J_MATADOR = 59,
    J_SATELLITE = 60,
    J_CARTOMANCER = 61,
    J_ASTRONOMER = 62,
    J_BOOTSTRAPS = 63
};

// ============================================================================
// Rare Jokers (20 total)
// ============================================================================
enum JokerRare : int {
    J_DNA = 0,
    J_VAGABOND = 1,
    J_BARON = 2,
    J_OBELISK = 3,
    J_BASEBALL_CARD = 4,
    J_ANCIENT_JOKER = 5,
    J_CAMPFIRE = 6,
    J_BLUEPRINT = 7,
    J_WEE_JOKER = 8,
    J_HIT_THE_ROAD = 9,
    J_THE_DUO = 10,
    J_THE_TRIO = 11,
    J_THE_FAMILY = 12,
    J_THE_ORDER = 13,
    J_THE_TRIBE = 14,
    J_STUNTMAN = 15,
    J_INVISIBLE_JOKER = 16,
    J_BRAINSTORM = 17,
    J_DRIVERS_LICENSE = 18,
    J_BURNT_JOKER = 19
};

// ============================================================================
// Legendary Jokers (5 total)
// ============================================================================
enum JokerLegendary : int {
    J_CANIO = 0,
    J_TRIBOULET = 1,
    J_YORICK = 2,
    J_CHICOT = 3,
    J_PERKEO = 4
};

// ============================================================================
// Tarot Cards (22 total)
// ============================================================================
#define NUM_TAROTS 22

enum TarotCard : int {
    T_THE_FOOL = 0,
    T_THE_MAGICIAN = 1,
    T_THE_HIGH_PRIESTESS = 2,
    T_THE_EMPRESS = 3,
    T_THE_EMPEROR = 4,
    T_THE_HIEROPHANT = 5,
    T_THE_LOVERS = 6,
    T_THE_CHARIOT = 7,
    T_JUSTICE = 8,
    T_THE_HERMIT = 9,
    T_THE_WHEEL_OF_FORTUNE = 10,
    T_STRENGTH = 11,
    T_THE_HANGED_MAN = 12,
    T_DEATH = 13,
    T_TEMPERANCE = 14,
    T_THE_DEVIL = 15,
    T_THE_TOWER = 16,
    T_THE_STAR = 17,
    T_THE_MOON = 18,
    T_THE_SUN = 19,
    T_JUDGEMENT = 20,
    T_THE_WORLD = 21
};

// ============================================================================
// Planet Cards (12 total)
// ============================================================================
#define NUM_PLANETS 12

enum PlanetCard : int {
    P_PLUTO = 0,
    P_MERCURY = 1,
    P_URANUS = 2,
    P_VENUS = 3,
    P_SATURN = 4,
    P_JUPITER = 5,
    P_EARTH = 6,
    P_MARS = 7,
    P_NEPTUNE = 8,
    P_PLANET_X = 9,
    P_CERES = 10,
    P_ERIS = 11
};

// ============================================================================
// Spectral Cards (18 total)
// ============================================================================
#define NUM_SPECTRALS 18

enum SpectralCard : int {
    S_FAMILIAR = 0,
    S_GRIM = 1,
    S_INCANTATION = 2,
    S_TALISMAN = 3,
    S_AURA = 4,
    S_WRAITH = 5,
    S_SIGIL = 6,
    S_OUIJA = 7,
    S_ECTOPLASM = 8,
    S_IMMOLATE = 9,
    S_ANKH = 10,
    S_DEJA_VU = 11,
    S_HEX = 12,
    S_TRANCE = 13,
    S_MEDIUM = 14,
    S_CRYPTID = 15,
    S_THE_SOUL = 16,
    S_BLACK_HOLE = 17
};

// ============================================================================
// Vouchers (32 total)
// ============================================================================
#define NUM_VOUCHERS 32

enum Voucher : int {
    V_OVERSTOCK = 0,
    V_OVERSTOCK_PLUS = 1,
    V_CLEARANCE_SALE = 2,
    V_LIQUIDATION = 3,
    V_HONE = 4,
    V_GLOW_UP = 5,
    V_REROLL_SURPLUS = 6,
    V_REROLL_GLUT = 7,
    V_CRYSTAL_BALL = 8,
    V_OMEN_GLOBE = 9,
    V_TELESCOPE = 10,
    V_OBSERVATORY = 11,
    V_GRABBER = 12,
    V_NACHO_TONG = 13,
    V_WASTEFUL = 14,
    V_RECYCLOMANCY = 15,
    V_TAROT_MERCHANT = 16,
    V_TAROT_TYCOON = 17,
    V_PLANET_MERCHANT = 18,
    V_PLANET_TYCOON = 19,
    V_SEED_MONEY = 20,
    V_MONEY_TREE = 21,
    V_BLANK = 22,
    V_ANTIMATTER = 23,
    V_MAGIC_TRICK = 24,
    V_ILLUSION = 25,
    V_HIEROGLYPH = 26,
    V_PETROGLYPH = 27,
    V_DIRECTORS_CUT = 28,
    V_RETCON = 29,
    V_PAINT_BRUSH = 30,
    V_PALETTE = 31
};

// ============================================================================
// Boss Blinds (30 total)
// ============================================================================
#define NUM_BOSSES 30

enum BossBlind : int {
    B_THE_HOOK = 0,
    B_THE_OX = 1,
    B_THE_HOUSE = 2,
    B_THE_WALL = 3,
    B_THE_WHEEL = 4,
    B_THE_ARM = 5,
    B_THE_CLUB = 6,
    B_THE_FISH = 7,
    B_THE_PSYCHIC = 8,
    B_THE_GOAD = 9,
    B_THE_WATER = 10,
    B_THE_WINDOW = 11,
    B_THE_MANACLE = 12,
    B_THE_EYE = 13,
    B_THE_MOUTH = 14,
    B_THE_PLANT = 15,
    B_THE_SERPENT = 16,
    B_THE_PILLAR = 17,
    B_THE_NEEDLE = 18,
    B_THE_HEAD = 19,
    B_THE_TOOTH = 20,
    B_THE_FLINT = 21,
    B_THE_MARK = 22,
    B_AMBER_ACORN = 23,
    B_VERDANT_LEAF = 24,
    B_VIOLET_VESSEL = 25,
    B_CRIMSON_HEART = 26,
    B_CERULEAN_BELL = 27,
    B_THE_CLOCK = 28,
    B_THE_HEART = 29  // Finisher blind
};

// ============================================================================
// Tags (23 total)
// ============================================================================
#define NUM_TAGS 23

enum Tag : int {
    TAG_UNCOMMON = 0,
    TAG_RARE = 1,
    TAG_NEGATIVE = 2,
    TAG_FOIL = 3,
    TAG_HOLOGRAPHIC = 4,
    TAG_POLYCHROME = 5,
    TAG_INVESTMENT = 6,
    TAG_VOUCHER = 7,
    TAG_BOSS = 8,
    TAG_STANDARD = 9,
    TAG_CHARM = 10,
    TAG_METEOR = 11,
    TAG_BUFFOON = 12,
    TAG_HANDY = 13,
    TAG_GARBAGE = 14,
    TAG_ETHEREAL = 15,
    TAG_COUPON = 16,
    TAG_DOUBLE = 17,
    TAG_JUGGLE = 18,
    TAG_D6 = 19,
    TAG_TOP_UP = 20,
    TAG_SPEED = 21,
    TAG_ORBITAL = 22
};

// ============================================================================
// Edition Types
// ============================================================================
enum Edition : int {
    EDITION_NONE = 0,
    EDITION_FOIL = 1,
    EDITION_HOLO = 2,
    EDITION_POLYCHROME = 3,
    EDITION_NEGATIVE = 4
};

// ============================================================================
// Card Enhancements
// ============================================================================
enum Enhancement : int {
    ENH_NONE = 0,
    ENH_BONUS = 1,
    ENH_MULT = 2,
    ENH_WILD = 3,
    ENH_GLASS = 4,
    ENH_STEEL = 5,
    ENH_STONE = 6,
    ENH_GOLD = 7,
    ENH_LUCKY = 8
};

// ============================================================================
// Card Seals
// ============================================================================
enum Seal : int {
    SEAL_NONE = 0,
    SEAL_GOLD = 1,
    SEAL_RED = 2,
    SEAL_BLUE = 3,
    SEAL_PURPLE = 4
};

// ============================================================================
// Stake Types
// ============================================================================
enum Stake : int {
    STAKE_WHITE = 0,
    STAKE_RED = 1,
    STAKE_GREEN = 2,
    STAKE_BLACK = 3,
    STAKE_BLUE = 4,
    STAKE_PURPLE = 5,
    STAKE_ORANGE = 6,
    STAKE_GOLD = 7
};

// ============================================================================
// Deck Types
// ============================================================================
enum Deck : int {
    DECK_RED = 0,
    DECK_BLUE = 1,
    DECK_YELLOW = 2,
    DECK_GREEN = 3,
    DECK_BLACK = 4,
    DECK_MAGIC = 5,
    DECK_NEBULA = 6,
    DECK_GHOST = 7,
    DECK_ABANDONED = 8,
    DECK_CHECKERED = 9,
    DECK_ZODIAC = 10,
    DECK_PAINTED = 11,
    DECK_ANAGLYPH = 12,
    DECK_PLASMA = 13,
    DECK_ERRATIC = 14
};

// ============================================================================
// Playing Card Ranks (for Erratic deck)
// ============================================================================
enum Rank : int {
    RANK_2 = 0,
    RANK_3 = 1,
    RANK_4 = 2,
    RANK_5 = 3,
    RANK_6 = 4,
    RANK_7 = 5,
    RANK_8 = 6,
    RANK_9 = 7,
    RANK_10 = 8,
    RANK_J = 9,
    RANK_Q = 10,
    RANK_K = 11,
    RANK_A = 12
};

// ============================================================================
// Playing Card Suits (for Erratic deck)
// ============================================================================
enum Suit : int {
    SUIT_CLUBS = 0,
    SUIT_DIAMONDS = 1,
    SUIT_HEARTS = 2,
    SUIT_SPADES = 3
};

// ============================================================================
// Booster Pack Types
// ============================================================================
enum BoosterPackType : int {
    PACK_ARCANA = 0,     // Tarot cards
    PACK_CELESTIAL = 1,  // Planet cards
    PACK_SPECTRAL = 2,   // Spectral cards
    PACK_STANDARD = 3,   // Playing cards
    PACK_BUFFOON = 4     // Jokers
};

// ============================================================================
// Helper: Get joker pool size by rarity
// ============================================================================
__device__ __forceinline__ int get_joker_pool_size(JokerRarity rarity) {
    switch (rarity) {
        case RARITY_COMMON:    return NUM_COMMON_JOKERS;
        case RARITY_UNCOMMON:  return NUM_UNCOMMON_JOKERS;
        case RARITY_RARE:      return NUM_RARE_JOKERS;
        case RARITY_LEGENDARY: return NUM_LEGENDARY_JOKERS;
        default:               return NUM_COMMON_JOKERS;
    }
}

// ============================================================================
// Helper: Make a full joker ID (rarity + index)
// ============================================================================
__device__ __forceinline__ int make_joker_id(JokerRarity rarity, int index) {
    return (int)rarity | (index & JOKER_INDEX_MASK);
}

// ============================================================================
// Helper: Extract rarity from joker ID
// ============================================================================
__device__ __forceinline__ JokerRarity get_joker_rarity(int joker_id) {
    return (JokerRarity)(joker_id & JOKER_RARITY_MASK);
}

// ============================================================================
// Helper: Extract index from joker ID
// ============================================================================
__device__ __forceinline__ int get_joker_index(int joker_id) {
    return joker_id & JOKER_INDEX_MASK;
}

#endif // BALATRO_ENUMS_CUH
