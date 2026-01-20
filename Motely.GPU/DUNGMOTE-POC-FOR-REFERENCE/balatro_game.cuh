/**
 * @file balatro_game.cuh
 * @brief CUDA implementation of Balatro game logic for seed searching
 *
 * This file implements the game simulation logic needed to evaluate
 * whether a seed meets filter criteria. Key functions include:
 *
 * - Joker rarity selection
 * - Voucher selection
 * - Pack generation
 * - Boss blind selection
 * - Tag selection
 * - Edition polling
 *
 * Reference: Balatro's common_events.lua and game.lua
 */

#ifndef BALATRO_GAME_CUH
#define BALATRO_GAME_CUH

#include "balatro_rng.cuh"  // Now uses verified v2 implementation

// ============================================================================
// Game Constants
// ============================================================================

// Joker rarities
enum JokerRarity {
    RARITY_COMMON = 1,
    RARITY_UNCOMMON = 2,
    RARITY_RARE = 3,
    RARITY_LEGENDARY = 4
};

// Edition types
enum Edition {
    EDITION_NONE = 0,
    EDITION_FOIL = 1,
    EDITION_HOLO = 2,
    EDITION_POLYCHROME = 3,
    EDITION_NEGATIVE = 4
};

// Stakes
enum Stake {
    STAKE_WHITE = 0,
    STAKE_RED = 1,
    STAKE_GREEN = 2,
    STAKE_BLACK = 3,
    STAKE_BLUE = 4,
    STAKE_PURPLE = 5,
    STAKE_ORANGE = 6,
    STAKE_GOLD = 7
};

// Decks
enum Deck {
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

// Number of items in each pool (approximate, for demonstration)
#define NUM_COMMON_JOKERS 70
#define NUM_UNCOMMON_JOKERS 35
#define NUM_RARE_JOKERS 20
#define NUM_LEGENDARY_JOKERS 5  // Perkeo, Triboulet, etc.
#define NUM_VOUCHERS 32
#define NUM_TAGS 23
#define NUM_BOSSES 30
#define NUM_TAROTS 22
#define NUM_PLANETS 12
#define NUM_SPECTRALS 18

// ============================================================================
// Game State for Seed Evaluation
// ============================================================================

/**
 * @brief Minimal game state needed for seed searching
 *
 * This is much smaller than the full game state - we only track
 * what's needed to evaluate filter conditions.
 */
struct GameState {
    char seed[9];           // 8-char seed + null
    double hashed_seed;     // Cached pseudohash(seed)
    int ante;               // Current ante (1-8)
    Stake stake;
    Deck deck;

    // Tracking what's been "used" (for Showman interactions, etc.)
    // For basic searching, we can ignore this complexity
};

/**
 * @brief Initialize game state for a seed
 */
__device__ void init_game_state(GameState* state, const char* seed, Stake stake, Deck deck) {
    for (int i = 0; i < 8; i++) {
        state->seed[i] = seed[i];
    }
    state->seed[8] = '\0';
    state->hashed_seed = pseudohash8(seed);
    state->ante = 1;
    state->stake = stake;
    state->deck = deck;
}

// ============================================================================
// Joker Generation
// ============================================================================

/**
 * @brief Determine joker rarity based on RNG roll
 *
 * From common_events.lua get_current_pool():
 *   local rarity = pseudorandom('rarity'..G.GAME.round_resets.ante..(_append or ''))
 *   rarity = (rarity > 0.95 and 3) or (rarity > 0.7 and 2) or 1
 *
 * Legendary is only rolled separately via soul cards.
 */
__device__ JokerRarity get_joker_rarity(GameState* state, const char* append, int append_len) {
    // Build key: "rarity" + ante + append
    char key[32];
    int key_len = 0;

    // "rarity"
    const char* rarity_str = "rarity";
    for (int i = 0; rarity_str[i]; i++) {
        key[key_len++] = rarity_str[i];
    }

    // Ante number (1-8)
    key[key_len++] = '0' + state->ante;

    // Append
    for (int i = 0; i < append_len; i++) {
        key[key_len++] = append[i];
    }
    key[key_len] = '\0';

    double roll = pseudorandom(key, key_len, state->seed, 8, state->hashed_seed);

    if (roll > 0.95) return RARITY_RARE;
    if (roll > 0.70) return RARITY_UNCOMMON;
    return RARITY_COMMON;
}

/**
 * @brief Get which joker from a rarity pool
 *
 * From common_events.lua:
 *   _pool_key = 'Joker'..rarity..append
 *   local center = pseudorandom_element(_pool, pseudoseed(_pool_key))
 */
__device__ int get_joker_from_pool(GameState* state, JokerRarity rarity, const char* append, int append_len) {
    // Build key: "Joker" + rarity + append + ante
    char key[32];
    int key_len = 0;

    const char* joker_str = "Joker";
    for (int i = 0; joker_str[i]; i++) {
        key[key_len++] = joker_str[i];
    }

    key[key_len++] = '0' + (int)rarity;

    for (int i = 0; i < append_len; i++) {
        key[key_len++] = append[i];
    }

    // Ante (for non-legendary)
    if (rarity != RARITY_LEGENDARY) {
        key[key_len++] = '0' + state->ante;
    }

    key[key_len] = '\0';

    int pool_size;
    switch (rarity) {
        case RARITY_COMMON:    pool_size = NUM_COMMON_JOKERS; break;
        case RARITY_UNCOMMON:  pool_size = NUM_UNCOMMON_JOKERS; break;
        case RARITY_RARE:      pool_size = NUM_RARE_JOKERS; break;
        case RARITY_LEGENDARY: pool_size = NUM_LEGENDARY_JOKERS; break;
        default:               pool_size = NUM_COMMON_JOKERS; break;
    }

    return (int)pseudorandom_range(key, key_len, state->seed, 8, state->hashed_seed, 1, pool_size);
}

// ============================================================================
// Edition Polling
// ============================================================================

/**
 * @brief Poll for edition on a card
 *
 * From common_events.lua poll_edition():
 *   local edition_poll = pseudorandom(pseudoseed(_key or 'edition_generic'))
 *   Negative: > 1 - 0.003*_mod
 *   Polychrome: > 1 - 0.006*edition_rate*_mod
 *   Holo: > 1 - 0.02*edition_rate*_mod
 *   Foil: > 1 - 0.04*edition_rate*_mod
 */
__device__ Edition poll_edition(GameState* state, const char* key, int key_len, double modifier, bool guaranteed) {
    double roll = pseudorandom(key, key_len, state->seed, 8, state->hashed_seed);

    if (guaranteed) {
        // Guaranteed edition (from certain packs)
        if (roll > 1.0 - 0.003 * 25) return EDITION_NEGATIVE;
        if (roll > 1.0 - 0.006 * 25) return EDITION_POLYCHROME;
        if (roll > 1.0 - 0.02 * 25)  return EDITION_HOLO;
        if (roll > 1.0 - 0.04 * 25)  return EDITION_FOIL;
    } else {
        // Standard rates (edition_rate is typically 1.0)
        double edition_rate = 1.0;
        if (roll > 1.0 - 0.003 * modifier) return EDITION_NEGATIVE;
        if (roll > 1.0 - 0.006 * edition_rate * modifier) return EDITION_POLYCHROME;
        if (roll > 1.0 - 0.02 * edition_rate * modifier) return EDITION_HOLO;
        if (roll > 1.0 - 0.04 * edition_rate * modifier) return EDITION_FOIL;
    }

    return EDITION_NONE;
}

// ============================================================================
// Voucher Generation
// ============================================================================

/**
 * @brief Get voucher for an ante
 *
 * Vouchers use pool key "Voucher" + ante
 */
__device__ int get_voucher(GameState* state) {
    char key[16];
    int key_len = 0;

    const char* voucher_str = "Voucher";
    for (int i = 0; voucher_str[i]; i++) {
        key[key_len++] = voucher_str[i];
    }
    key[key_len++] = '0' + state->ante;
    key[key_len] = '\0';

    return (int)pseudorandom_range(key, key_len, state->seed, 8, state->hashed_seed, 1, NUM_VOUCHERS);
}

// ============================================================================
// Boss Blind Selection
// ============================================================================

/**
 * @brief Get boss blind for an ante
 */
__device__ int get_boss_blind(GameState* state) {
    char key[16];
    int key_len = 0;

    const char* boss_str = "Boss";
    for (int i = 0; boss_str[i]; i++) {
        key[key_len++] = boss_str[i];
    }
    key[key_len++] = '0' + state->ante;
    key[key_len] = '\0';

    return (int)pseudorandom_range(key, key_len, state->seed, 8, state->hashed_seed, 1, NUM_BOSSES);
}

// ============================================================================
// Tag Selection
// ============================================================================

/**
 * @brief Get tag for skipping a blind
 */
__device__ int get_tag(GameState* state, const char* append, int append_len) {
    char key[16];
    int key_len = 0;

    const char* tag_str = "Tag";
    for (int i = 0; tag_str[i]; i++) {
        key[key_len++] = tag_str[i];
    }
    for (int i = 0; i < append_len; i++) {
        key[key_len++] = append[i];
    }
    key[key_len++] = '0' + state->ante;
    key[key_len] = '\0';

    return (int)pseudorandom_range(key, key_len, state->seed, 8, state->hashed_seed, 1, NUM_TAGS);
}

// ============================================================================
// Soul Card Check (Legendary Joker Spawn)
// ============================================================================

/**
 * @brief Check if a soul/black hole card spawns in a pack
 *
 * From create_card():
 *   if pseudorandom('soul_'.._type..G.GAME.round_resets.ante) > 0.997 then
 *     forced_key = 'c_soul'
 *   end
 */
__device__ bool check_soul_spawn(GameState* state, const char* type_str, int type_len) {
    char key[32];
    int key_len = 0;

    const char* soul_str = "soul_";
    for (int i = 0; soul_str[i]; i++) {
        key[key_len++] = soul_str[i];
    }
    for (int i = 0; i < type_len; i++) {
        key[key_len++] = type_str[i];
    }
    key[key_len++] = '0' + state->ante;
    key[key_len] = '\0';

    double roll = pseudorandom(key, key_len, state->seed, 8, state->hashed_seed);
    return roll > 0.997;
}

// ============================================================================
// First Shop Pack (Special Case)
// ============================================================================

/**
 * @brief Check first shop pack
 *
 * From get_pack():
 *   if not G.GAME.first_shop_buffoon then
 *     G.GAME.first_shop_buffoon = true
 *     return G.P_CENTERS['p_buffoon_normal_'..(math.random(1, 2))]
 *   end
 *
 * The first shop ALWAYS has a Buffoon pack (Joker pack).
 * The variant (1 or 2) is determined by math.random() using
 * whatever seed state exists at that moment.
 */
__device__ int get_first_buffoon_variant(GameState* state) {
    // This uses the raw math.random, not pseudorandom
    // It's seeded by the current pseudorandom state
    // For simplicity in searching, we can approximate this
    return 1;  // Could be 1 or 2
}

#endif // BALATRO_GAME_CUH
