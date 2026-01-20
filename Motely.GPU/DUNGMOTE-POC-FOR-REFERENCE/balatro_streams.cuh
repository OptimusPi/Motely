/**
 * @file balatro_streams.cuh
 * @brief CUDA item stream generation for seed searching
 *
 * This file implements the "stream" pattern where each
 * item type (joker, tarot, planet, etc.) has its own PRNG stream
 * that can be advanced independently.
 *
 * Key streams:
 * - Shop Item Type Stream: Determines if slot is joker/tarot/planet/etc
 * - Joker Stream: Generates jokers with rarity, edition, stickers
 * - Tarot Stream: Generates tarot cards
 * - Planet Stream: Generates planet cards
 * - Spectral Stream: Generates spectral cards
 * - Voucher Stream: Generates vouchers
 * - Tag Stream: Generates tags
 * - Boss Stream: Generates boss blinds
 */

#ifndef BALATRO_STREAMS_CUH
#define BALATRO_STREAMS_CUH

#include "balatro_rng.cuh"
#include "balatro_enums.cuh"

// ============================================================================
// PRNG Key Strings (from Balatro's RNG key system)
// ============================================================================

// Shop keys
#define KEY_SHOP_ITEM_TYPE    "cdt"    // Shop slot type determination
#define KEY_SHOP_ITEM_SOURCE  "sho"    // Shop joker source

// Joker keys
#define KEY_JOKER_RARITY      "rarity" // Joker rarity roll
#define KEY_JOKER_EDITION     "edi"    // Joker edition roll
#define KEY_JOKER_COMMON      "Joker1" // Common joker pool
#define KEY_JOKER_UNCOMMON    "Joker2" // Uncommon joker pool
#define KEY_JOKER_RARE        "Joker3" // Rare joker pool
#define KEY_JOKER_LEGENDARY   "Joker4" // Legendary joker pool (Soul)

// Pack keys
#define KEY_BUFFOON_PACK_SOURCE "buf"  // Buffoon pack joker source
#define KEY_ARCANA_PACK_SOURCE  "ar1"  // Arcana pack tarot source
#define KEY_CELESTIAL_PACK_SOURCE "pl1" // Celestial pack planet source
#define KEY_SPECTRAL_PACK_SOURCE "spe" // Spectral pack source
#define KEY_STANDARD_PACK_SOURCE "sta" // Standard pack source

// Consumable keys
#define KEY_TAROT             "Tarot"
#define KEY_PLANET            "Planet"
#define KEY_SPECTRAL          "Spectral"

// Voucher/Tag/Boss keys
#define KEY_VOUCHER           "Voucher"
#define KEY_TAG               "Tag"
#define KEY_BOSS              "boss"

// Soul/Legendary keys
#define KEY_SOUL_PREFIX       "soul_"  // Soul card check
#define KEY_SOUL_JOKER_SOURCE "sou"    // Soul joker source

// Sticker keys (for high stakes)
#define KEY_SHOP_ETERNAL_PERISHABLE "etperpoll"
#define KEY_SHOP_RENTAL             "ssjr"
#define KEY_PACK_ETERNAL_PERISHABLE "packetper"
#define KEY_PACK_RENTAL             "packssjr"

// ============================================================================
// Item with Full Metadata
// ============================================================================

/**
 * @brief Complete item representation with all metadata
 *
 * Packed format for efficient GPU memory:
 * - Type/Value: 16 bits (item type + specific value)
 * - Edition: 4 bits
 * - Enhancement: 4 bits
 * - Seal: 4 bits
 * - Stickers: 4 bits (eternal, perishable, rental)
 */
struct Item {
    int type_value;     // Full item type with value
    Edition edition;
    Enhancement enhancement;
    Seal seal;
    bool eternal;
    bool perishable;
    bool rental;

    __device__ Item() : type_value(0), edition(EDITION_NONE),
                        enhancement(ENH_NONE), seal(SEAL_NONE),
                        eternal(false), perishable(false), rental(false) {}
};

// ============================================================================
// Joker Stream
// ============================================================================

/**
 * @brief Joker generation stream with all sub-streams
 */
struct JokerStream {
    PrngStream rarity_stream;
    PrngStream edition_stream;
    PrngStream common_stream;
    PrngStream uncommon_stream;
    PrngStream rare_stream;
    PrngStream eternal_perishable_stream;
    PrngStream rental_stream;

    bool has_edition;
    bool has_stickers;
    Stake stake;
};

/**
 * @brief Create a shop joker stream for given ante
 */
__device__ JokerStream create_shop_joker_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante, Stake stake
) {
    JokerStream stream;
    stream.stake = stake;
    stream.has_edition = true;
    stream.has_stickers = (stake >= STAKE_BLACK);

    // Build keys with ante
    char key_buf[32];
    int key_len;

    // Rarity stream: "rarity" + ante + "sho"
    key_len = 0;
    for (int i = 0; KEY_JOKER_RARITY[i]; i++) key_buf[key_len++] = KEY_JOKER_RARITY[i];
    key_buf[key_len++] = '0' + ante;
    for (int i = 0; KEY_SHOP_ITEM_SOURCE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_SOURCE[i];
    key_buf[key_len] = '\0';
    stream.rarity_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Edition stream: "edi" + "sho" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_EDITION[i]; i++) key_buf[key_len++] = KEY_JOKER_EDITION[i];
    for (int i = 0; KEY_SHOP_ITEM_SOURCE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.edition_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Common joker stream: "Joker1" + "sho" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_COMMON[i]; i++) key_buf[key_len++] = KEY_JOKER_COMMON[i];
    for (int i = 0; KEY_SHOP_ITEM_SOURCE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.common_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Uncommon joker stream: "Joker2" + "sho" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_UNCOMMON[i]; i++) key_buf[key_len++] = KEY_JOKER_UNCOMMON[i];
    for (int i = 0; KEY_SHOP_ITEM_SOURCE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.uncommon_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Rare joker stream: "Joker3" + "sho" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_RARE[i]; i++) key_buf[key_len++] = KEY_JOKER_RARE[i];
    for (int i = 0; KEY_SHOP_ITEM_SOURCE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.rare_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Sticker streams (only for Black+ stakes)
    if (stake >= STAKE_BLACK) {
        // Eternal/Perishable stream: "etperpoll" + ante
        key_len = 0;
        for (int i = 0; KEY_SHOP_ETERNAL_PERISHABLE[i]; i++) key_buf[key_len++] = KEY_SHOP_ETERNAL_PERISHABLE[i];
        key_buf[key_len++] = '0' + ante;
        key_buf[key_len] = '\0';
        stream.eternal_perishable_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

        if (stake >= STAKE_GOLD) {
            // Rental stream: "ssjr" + ante
            key_len = 0;
            for (int i = 0; KEY_SHOP_RENTAL[i]; i++) key_buf[key_len++] = KEY_SHOP_RENTAL[i];
            key_buf[key_len++] = '0' + ante;
            key_buf[key_len] = '\0';
            stream.rental_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
        }
    }

    return stream;
}

/**
 * @brief Create a buffoon pack joker stream for given ante
 */
__device__ JokerStream create_buffoon_pack_joker_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante, Stake stake
) {
    JokerStream stream;
    stream.stake = stake;
    stream.has_edition = true;
    stream.has_stickers = (stake >= STAKE_BLACK);

    char key_buf[32];
    int key_len;

    // Rarity stream: "rarity" + ante + "buf"
    key_len = 0;
    for (int i = 0; KEY_JOKER_RARITY[i]; i++) key_buf[key_len++] = KEY_JOKER_RARITY[i];
    key_buf[key_len++] = '0' + ante;
    for (int i = 0; KEY_BUFFOON_PACK_SOURCE[i]; i++) key_buf[key_len++] = KEY_BUFFOON_PACK_SOURCE[i];
    key_buf[key_len] = '\0';
    stream.rarity_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Edition stream: "edi" + "buf" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_EDITION[i]; i++) key_buf[key_len++] = KEY_JOKER_EDITION[i];
    for (int i = 0; KEY_BUFFOON_PACK_SOURCE[i]; i++) key_buf[key_len++] = KEY_BUFFOON_PACK_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.edition_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Common joker stream: "Joker1" + "buf" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_COMMON[i]; i++) key_buf[key_len++] = KEY_JOKER_COMMON[i];
    for (int i = 0; KEY_BUFFOON_PACK_SOURCE[i]; i++) key_buf[key_len++] = KEY_BUFFOON_PACK_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.common_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Uncommon joker stream: "Joker2" + "buf" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_UNCOMMON[i]; i++) key_buf[key_len++] = KEY_JOKER_UNCOMMON[i];
    for (int i = 0; KEY_BUFFOON_PACK_SOURCE[i]; i++) key_buf[key_len++] = KEY_BUFFOON_PACK_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.uncommon_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Rare joker stream: "Joker3" + "buf" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_RARE[i]; i++) key_buf[key_len++] = KEY_JOKER_RARE[i];
    for (int i = 0; KEY_BUFFOON_PACK_SOURCE[i]; i++) key_buf[key_len++] = KEY_BUFFOON_PACK_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.rare_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Sticker streams
    if (stake >= STAKE_BLACK) {
        key_len = 0;
        for (int i = 0; KEY_PACK_ETERNAL_PERISHABLE[i]; i++) key_buf[key_len++] = KEY_PACK_ETERNAL_PERISHABLE[i];
        key_buf[key_len++] = '0' + ante;
        key_buf[key_len] = '\0';
        stream.eternal_perishable_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

        if (stake >= STAKE_GOLD) {
            key_len = 0;
            for (int i = 0; KEY_PACK_RENTAL[i]; i++) key_buf[key_len++] = KEY_PACK_RENTAL[i];
            key_buf[key_len++] = '0' + ante;
            key_buf[key_len] = '\0';
            stream.rental_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
        }
    }

    return stream;
}

/**
 * @brief Get next joker from stream
 *
 * Returns full joker ID (rarity | index) with edition and stickers
 */
__device__ Item get_next_joker(JokerStream* stream) {
    Item joker;

    // Roll rarity (0.95 = rare, 0.70 = uncommon, else common)
    double rarity_roll = get_next_random(&stream->rarity_stream);
    JokerRarity rarity;
    int joker_index;

    if (rarity_roll > 0.95) {
        rarity = RARITY_RARE;
        joker_index = get_next_random_int(&stream->rare_stream, 0, NUM_RARE_JOKERS);
    } else if (rarity_roll > 0.70) {
        rarity = RARITY_UNCOMMON;
        joker_index = get_next_random_int(&stream->uncommon_stream, 0, NUM_UNCOMMON_JOKERS);
    } else {
        rarity = RARITY_COMMON;
        joker_index = get_next_random_int(&stream->common_stream, 0, NUM_COMMON_JOKERS);
    }

    joker.type_value = make_joker_id(rarity, joker_index);

    // Roll edition if stream provides it
    if (stream->has_edition) {
        double edition_roll = get_next_random(&stream->edition_stream);

        // Edition thresholds: Negative > Poly > Holo > Foil
        if (edition_roll > 0.997) {
            joker.edition = EDITION_NEGATIVE;
        } else if (edition_roll > 1.0 - 0.006) {
            joker.edition = EDITION_POLYCHROME;
        } else if (edition_roll > 1.0 - 0.02) {
            joker.edition = EDITION_HOLO;
        } else if (edition_roll > 1.0 - 0.04) {
            joker.edition = EDITION_FOIL;
        }
    }

    // Roll stickers for Black+ stakes
    if (stream->has_stickers) {
        double sticker_roll = get_next_random(&stream->eternal_perishable_stream);

        // Check if joker can be eternal (self-destruct jokers can't)
        bool can_be_eternal = true;
        int idx = get_joker_index(joker.type_value);
        JokerRarity r = get_joker_rarity(joker.type_value);

        // Common jokers that can't be eternal
        if (r == RARITY_COMMON) {
            if (idx == J_GROS_MICHEL || idx == J_ICE_CREAM ||
                idx == J_CAVENDISH || idx == J_POPCORN) {
                can_be_eternal = false;
            }
        }
        // Uncommon jokers that can't be eternal
        else if (r == RARITY_UNCOMMON) {
            if (idx == J_LUCHADOR || idx == J_TURTLE_BEAN ||
                idx == J_DIET_COLA || idx == J_RAMEN ||
                idx == J_SELTZER || idx == J_MR_BONES) {
                can_be_eternal = false;
            }
        }
        // Rare jokers that can't be eternal
        else if (r == RARITY_RARE) {
            if (idx == J_INVISIBLE_JOKER) {
                can_be_eternal = false;
            }
        }

        if (sticker_roll > 0.7 && can_be_eternal) {
            joker.eternal = true;
        } else if (stream->stake >= STAKE_ORANGE && sticker_roll > 0.4) {
            joker.perishable = true;
        }

        // Rental for Gold+ stakes
        if (stream->stake >= STAKE_GOLD) {
            double rental_roll = get_next_random(&stream->rental_stream);
            if (rental_roll > 0.7) {
                joker.rental = true;
            }
        }
    }

    return joker;
}

// ============================================================================
// Soul Joker Stream (Legendary)
// ============================================================================

/**
 * @brief Soul joker stream for legendary jokers from Soul card
 */
struct SoulJokerStream {
    PrngStream joker_stream;
    PrngStream edition_stream;
    bool has_edition;
};

/**
 * @brief Create a soul joker stream
 */
__device__ SoulJokerStream create_soul_joker_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    SoulJokerStream stream;
    stream.has_edition = true;

    char key_buf[32];
    int key_len;

    // Legendary joker stream: "Joker4" (no ante for legendary!)
    key_len = 0;
    for (int i = 0; KEY_JOKER_LEGENDARY[i]; i++) key_buf[key_len++] = KEY_JOKER_LEGENDARY[i];
    key_buf[key_len] = '\0';
    stream.joker_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Edition stream: "edi" + "sou" + ante
    key_len = 0;
    for (int i = 0; KEY_JOKER_EDITION[i]; i++) key_buf[key_len++] = KEY_JOKER_EDITION[i];
    for (int i = 0; KEY_SOUL_JOKER_SOURCE[i]; i++) key_buf[key_len++] = KEY_SOUL_JOKER_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    stream.edition_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    return stream;
}

/**
 * @brief Get next legendary joker from soul stream
 */
__device__ Item get_next_soul_joker(SoulJokerStream* stream) {
    Item joker;

    int joker_index = get_next_random_int(&stream->joker_stream, 0, NUM_LEGENDARY_JOKERS);
    joker.type_value = make_joker_id(RARITY_LEGENDARY, joker_index);

    if (stream->has_edition) {
        double edition_roll = get_next_random(&stream->edition_stream);
        if (edition_roll > 0.997) {
            joker.edition = EDITION_NEGATIVE;
        } else if (edition_roll > 1.0 - 0.006) {
            joker.edition = EDITION_POLYCHROME;
        } else if (edition_roll > 1.0 - 0.02) {
            joker.edition = EDITION_HOLO;
        } else if (edition_roll > 1.0 - 0.04) {
            joker.edition = EDITION_FOIL;
        }
    }

    return joker;
}

// ============================================================================
// Tarot Stream
// ============================================================================

struct TarotStream {
    PrngStream tarot_stream;
};

__device__ TarotStream create_shop_tarot_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    TarotStream stream;

    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_TAROT[i]; i++) key_buf[key_len++] = KEY_TAROT[i];
    for (int i = 0; KEY_SHOP_ITEM_SOURCE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    stream.tarot_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
    return stream;
}

__device__ TarotStream create_arcana_pack_tarot_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    TarotStream stream;

    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_TAROT[i]; i++) key_buf[key_len++] = KEY_TAROT[i];
    for (int i = 0; KEY_ARCANA_PACK_SOURCE[i]; i++) key_buf[key_len++] = KEY_ARCANA_PACK_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    stream.tarot_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
    return stream;
}

__device__ TarotCard get_next_tarot(TarotStream* stream) {
    return (TarotCard)get_next_random_int(&stream->tarot_stream, 0, NUM_TAROTS);
}

// ============================================================================
// Planet Stream
// ============================================================================

struct PlanetStream {
    PrngStream planet_stream;
};

__device__ PlanetStream create_shop_planet_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    PlanetStream stream;

    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_PLANET[i]; i++) key_buf[key_len++] = KEY_PLANET[i];
    for (int i = 0; KEY_SHOP_ITEM_SOURCE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    stream.planet_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
    return stream;
}

__device__ PlanetStream create_celestial_pack_planet_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    PlanetStream stream;

    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_PLANET[i]; i++) key_buf[key_len++] = KEY_PLANET[i];
    for (int i = 0; KEY_CELESTIAL_PACK_SOURCE[i]; i++) key_buf[key_len++] = KEY_CELESTIAL_PACK_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    stream.planet_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
    return stream;
}

__device__ PlanetCard get_next_planet(PlanetStream* stream) {
    return (PlanetCard)get_next_random_int(&stream->planet_stream, 0, NUM_PLANETS);
}

// ============================================================================
// Spectral Stream
// ============================================================================

struct SpectralStream {
    PrngStream spectral_stream;
};

__device__ SpectralStream create_shop_spectral_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    SpectralStream stream;

    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_SPECTRAL[i]; i++) key_buf[key_len++] = KEY_SPECTRAL[i];
    for (int i = 0; KEY_SHOP_ITEM_SOURCE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    stream.spectral_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
    return stream;
}

__device__ SpectralStream create_spectral_pack_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    SpectralStream stream;

    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_SPECTRAL[i]; i++) key_buf[key_len++] = KEY_SPECTRAL[i];
    for (int i = 0; KEY_SPECTRAL_PACK_SOURCE[i]; i++) key_buf[key_len++] = KEY_SPECTRAL_PACK_SOURCE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    stream.spectral_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
    return stream;
}

__device__ SpectralCard get_next_spectral(SpectralStream* stream) {
    return (SpectralCard)get_next_random_int(&stream->spectral_stream, 0, NUM_SPECTRALS);
}

// ============================================================================
// Voucher Stream
// ============================================================================

__device__ Voucher get_voucher_for_ante(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_VOUCHER[i]; i++) key_buf[key_len++] = KEY_VOUCHER[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    int voucher = (int)pseudorandom_range(key_buf, key_len, seed, seed_len, seed_hash, 0, NUM_VOUCHERS);
    return (Voucher)voucher;
}

// ============================================================================
// Tag Stream
// ============================================================================

__device__ Tag get_tag_for_ante(
    const char* seed, int seed_len, double seed_hash,
    int ante, const char* suffix, int suffix_len
) {
    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_TAG[i]; i++) key_buf[key_len++] = KEY_TAG[i];
    for (int i = 0; i < suffix_len; i++) key_buf[key_len++] = suffix[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    int tag = (int)pseudorandom_range(key_buf, key_len, seed, seed_len, seed_hash, 0, NUM_TAGS);
    return (Tag)tag;
}

// ============================================================================
// Boss Blind Stream
// ============================================================================

__device__ BossBlind get_boss_for_ante(
    const char* seed, int seed_len, double seed_hash,
    int ante
) {
    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_BOSS[i]; i++) key_buf[key_len++] = KEY_BOSS[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    // Note: Boss pool is smaller for early antes
    int boss = (int)pseudorandom_range(key_buf, key_len, seed, seed_len, seed_hash, 0, NUM_BOSSES);
    return (BossBlind)boss;
}

// ============================================================================
// Shop Item Type Stream
// ============================================================================

/**
 * @brief Determine what type of item appears in a shop slot
 *
 * Shop rates (base):
 * - Joker: 20
 * - Tarot: 4
 * - Planet: 4
 * - Playing Card: 0 (4 with Magic Trick voucher)
 * - Spectral: 0 (2 on Ghost Deck)
 */
enum ShopSlotType {
    SLOT_JOKER = 0,
    SLOT_TAROT = 1,
    SLOT_PLANET = 2,
    SLOT_PLAYING_CARD = 3,
    SLOT_SPECTRAL = 4
};

struct ShopItemTypeStream {
    PrngStream type_stream;
    double joker_rate;
    double tarot_rate;
    double planet_rate;
    double playing_card_rate;
    double spectral_rate;
    double total_rate;
};

__device__ ShopItemTypeStream create_shop_item_type_stream(
    const char* seed, int seed_len, double seed_hash,
    int ante, Deck deck,
    bool has_tarot_merchant, bool has_tarot_tycoon,
    bool has_planet_merchant, bool has_planet_tycoon,
    bool has_magic_trick
) {
    ShopItemTypeStream stream;

    char key_buf[32];
    int key_len = 0;
    for (int i = 0; KEY_SHOP_ITEM_TYPE[i]; i++) key_buf[key_len++] = KEY_SHOP_ITEM_TYPE[i];
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    stream.type_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);

    // Base rates
    stream.joker_rate = 20.0;

    // Tarot rate
    if (has_tarot_tycoon) stream.tarot_rate = 32.0;
    else if (has_tarot_merchant) stream.tarot_rate = 9.6;
    else stream.tarot_rate = 4.0;

    // Planet rate
    if (has_planet_tycoon) stream.planet_rate = 32.0;
    else if (has_planet_merchant) stream.planet_rate = 9.6;
    else stream.planet_rate = 4.0;

    // Playing card rate
    stream.playing_card_rate = has_magic_trick ? 4.0 : 0.0;

    // Spectral rate
    stream.spectral_rate = (deck == DECK_GHOST) ? 2.0 : 0.0;

    stream.total_rate = stream.joker_rate + stream.tarot_rate +
                        stream.planet_rate + stream.playing_card_rate +
                        stream.spectral_rate;

    return stream;
}

__device__ ShopSlotType get_next_shop_slot_type(ShopItemTypeStream* stream) {
    double roll = get_next_random(&stream->type_stream) * stream->total_rate;

    if (roll < stream->joker_rate) {
        return SLOT_JOKER;
    }
    roll -= stream->joker_rate;

    if (roll < stream->tarot_rate) {
        return SLOT_TAROT;
    }
    roll -= stream->tarot_rate;

    if (roll < stream->planet_rate) {
        return SLOT_PLANET;
    }
    roll -= stream->planet_rate;

    if (roll < stream->playing_card_rate) {
        return SLOT_PLAYING_CARD;
    }

    return SLOT_SPECTRAL;
}

#endif // BALATRO_STREAMS_CUH
