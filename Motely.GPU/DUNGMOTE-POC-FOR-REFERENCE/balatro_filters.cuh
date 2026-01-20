/**
 * @file balatro_filters.cuh
 * @brief CUDA filter system for seed searching (JAML-compatible)
 *
 * This file implements the filter evaluation system compatible with
 * JAML filter format. A filter consists of:
 *
 * - must: All conditions must be met (AND)
 * - should: Optional conditions with scores (for ranking)
 *
 * Filter types:
 * - Joker: Specific joker, any rarity, with optional edition
 * - Tarot: Specific tarot card in shop or pack
 * - Planet: Specific planet card in shop or pack
 * - Spectral: Specific spectral card in shop or pack
 * - Voucher: Specific voucher in ante
 * - Tag: Specific tag for skipping blinds
 * - Boss: Specific boss blind in ante
 * - SoulJoker: Legendary joker from Soul card
 * - Erratic: Rank/suit counts for Erratic deck
 */

#ifndef BALATRO_FILTERS_CUH
#define BALATRO_FILTERS_CUH

#include "balatro_streams.cuh"

// ============================================================================
// Filter Source Flags
// ============================================================================

/**
 * @brief Bitflags for allowed item sources
 *
 * Used to specify where an item can come from (shop slots, pack slots,
 * tag sources, Judgement tarot, etc.)
 */
#define SOURCE_SHOP_SLOT_0      (1 << 0)
#define SOURCE_SHOP_SLOT_1      (1 << 1)
#define SOURCE_SHOP_SLOT_2      (1 << 2)
#define SOURCE_SHOP_SLOT_3      (1 << 3)  // With Overstock voucher
#define SOURCE_PACK_SLOT_0      (1 << 4)
#define SOURCE_PACK_SLOT_1      (1 << 5)
#define SOURCE_PACK_SLOT_2      (1 << 6)
#define SOURCE_PACK_SLOT_3      (1 << 7)  // Mega packs
#define SOURCE_PACK_SLOT_4      (1 << 8)  // Mega packs
#define SOURCE_RARE_TAG         (1 << 9)  // From Rare Tag
#define SOURCE_UNCOMMON_TAG     (1 << 10) // From Uncommon Tag
#define SOURCE_NEGATIVE_TAG     (1 << 11) // From Negative Tag
#define SOURCE_JUDGEMENT_0      (1 << 12) // First Judgement use
#define SOURCE_JUDGEMENT_1      (1 << 13) // Second Judgement use
#define SOURCE_JUDGEMENT_2      (1 << 14) // Third Judgement use

// Common source groups
#define SOURCE_ANY_SHOP         (SOURCE_SHOP_SLOT_0 | SOURCE_SHOP_SLOT_1 | SOURCE_SHOP_SLOT_2 | SOURCE_SHOP_SLOT_3)
#define SOURCE_ANY_PACK         (SOURCE_PACK_SLOT_0 | SOURCE_PACK_SLOT_1 | SOURCE_PACK_SLOT_2 | SOURCE_PACK_SLOT_3 | SOURCE_PACK_SLOT_4)
#define SOURCE_ANY              (0xFFFFFFFF)

// ============================================================================
// Joker Filter
// ============================================================================

/**
 * @brief Filter for matching jokers
 */
struct JokerFilter {
    int wanted_joker;           // Specific joker ID, or -1 for any
    JokerRarity wanted_rarity;  // Required rarity, or -1 for any
    Edition wanted_edition;     // Required edition, or EDITION_NONE for any
    uint32_t allowed_sources;   // Bitflags for allowed sources
    uint32_t allowed_antes;     // Bitflags for antes (1 << ante)
    int score;                  // Score if matched (for should)
    bool is_must;               // true = must, false = should

    __device__ JokerFilter() : wanted_joker(-1), wanted_rarity((JokerRarity)-1),
                               wanted_edition(EDITION_NONE), allowed_sources(SOURCE_ANY),
                               allowed_antes(0xFF), score(0), is_must(true) {}
};

/**
 * @brief Check if a joker matches this filter
 */
__device__ bool joker_matches(const JokerFilter* filter, const Item* joker) {
    // Check joker type if specific
    if (filter->wanted_joker >= 0 && joker->type_value != filter->wanted_joker) {
        return false;
    }

    // Check rarity if specific
    if ((int)filter->wanted_rarity >= 0) {
        JokerRarity actual_rarity = get_joker_rarity(joker->type_value);
        if (actual_rarity != filter->wanted_rarity) {
            return false;
        }
    }

    // Check edition if required
    if (filter->wanted_edition != EDITION_NONE &&
        joker->edition != filter->wanted_edition) {
        return false;
    }

    return true;
}

// ============================================================================
// Tarot Filter
// ============================================================================

struct TarotFilter {
    TarotCard wanted_tarot;     // Specific tarot, or -1 for any
    uint32_t allowed_sources;   // Shop slots, Arcana pack slots
    uint32_t allowed_antes;
    int score;
    bool is_must;

    __device__ TarotFilter() : wanted_tarot((TarotCard)-1), allowed_sources(SOURCE_ANY),
                               allowed_antes(0xFF), score(0), is_must(true) {}
};

// ============================================================================
// Planet Filter
// ============================================================================

struct PlanetFilter {
    PlanetCard wanted_planet;
    uint32_t allowed_sources;
    uint32_t allowed_antes;
    int score;
    bool is_must;

    __device__ PlanetFilter() : wanted_planet((PlanetCard)-1), allowed_sources(SOURCE_ANY),
                                allowed_antes(0xFF), score(0), is_must(true) {}
};

// ============================================================================
// Spectral Filter
// ============================================================================

struct SpectralFilter {
    SpectralCard wanted_spectral;
    uint32_t allowed_sources;
    uint32_t allowed_antes;
    int score;
    bool is_must;

    __device__ SpectralFilter() : wanted_spectral((SpectralCard)-1), allowed_sources(SOURCE_ANY),
                                  allowed_antes(0xFF), score(0), is_must(true) {}
};

// ============================================================================
// Voucher Filter
// ============================================================================

struct VoucherFilter {
    Voucher wanted_voucher;
    uint32_t allowed_antes;
    int score;
    bool is_must;

    __device__ VoucherFilter() : wanted_voucher((Voucher)-1), allowed_antes(0xFF),
                                 score(0), is_must(true) {}
};

// ============================================================================
// Tag Filter
// ============================================================================

struct TagFilter {
    Tag wanted_tag;
    uint32_t allowed_antes;
    int score;
    bool is_must;

    __device__ TagFilter() : wanted_tag((Tag)-1), allowed_antes(0xFF),
                             score(0), is_must(true) {}
};

// ============================================================================
// Boss Filter
// ============================================================================

struct BossFilter {
    BossBlind wanted_boss;
    uint32_t allowed_antes;
    int score;
    bool is_must;

    __device__ BossFilter() : wanted_boss((BossBlind)-1), allowed_antes(0xFF),
                              score(0), is_must(true) {}
};

// ============================================================================
// Soul Joker Filter (Legendary from Soul card)
// ============================================================================

struct SoulJokerFilter {
    JokerLegendary wanted_legendary;  // Perkeo, Canio, etc.
    uint32_t allowed_sources;          // Pack slots where Soul can appear
    uint32_t allowed_antes;
    int score;
    bool is_must;

    __device__ SoulJokerFilter() : wanted_legendary((JokerLegendary)-1),
                                   allowed_sources(SOURCE_ANY_PACK),
                                   allowed_antes(0xFF), score(0), is_must(true) {}
};

// ============================================================================
// Erratic Filter (Deck starting state)
// ============================================================================

struct ErraticRankFilter {
    Rank rank;          // Which rank to count
    int min_count;      // Minimum cards of this rank
    int score;
    bool is_must;

    __device__ ErraticRankFilter() : rank(RANK_2), min_count(1),
                                     score(0), is_must(true) {}
};

struct ErraticSuitFilter {
    Suit suit;          // Which suit to count
    int min_count;      // Minimum cards of this suit
    int score;
    bool is_must;

    __device__ ErraticSuitFilter() : suit(SUIT_CLUBS), min_count(1),
                                     score(0), is_must(true) {}
};

// ============================================================================
// Playing Card Filter (for Standard packs, shop with Magic Trick, etc.)
// ============================================================================

struct PlayingCardFilter {
    Rank wanted_rank;           // -1 for any
    Suit wanted_suit;           // -1 for any
    Enhancement wanted_enhancement;
    Seal wanted_seal;
    Edition wanted_edition;
    uint32_t allowed_sources;
    uint32_t allowed_antes;
    int score;
    bool is_must;

    __device__ PlayingCardFilter() : wanted_rank((Rank)-1), wanted_suit((Suit)-1),
                                     wanted_enhancement(ENH_NONE), wanted_seal(SEAL_NONE),
                                     wanted_edition(EDITION_NONE),
                                     allowed_sources(SOURCE_ANY), allowed_antes(0xFF),
                                     score(0), is_must(true) {}
};

// ============================================================================
// Unified Filter Config (for seed searching)
// ============================================================================

/**
 * @brief Maximum filters per type
 *
 * These limits keep the filter struct from being too large.
 * Most real searches use far fewer filters.
 */
#define MAX_JOKER_FILTERS       8
#define MAX_TAROT_FILTERS       4
#define MAX_PLANET_FILTERS      4
#define MAX_SPECTRAL_FILTERS    4
#define MAX_VOUCHER_FILTERS     4
#define MAX_TAG_FILTERS         4
#define MAX_BOSS_FILTERS        4
#define MAX_SOUL_JOKER_FILTERS  2
#define MAX_ERRATIC_RANK_FILTERS 4
#define MAX_ERRATIC_SUIT_FILTERS 4
#define MAX_PLAYING_CARD_FILTERS 4

/**
 * @brief Complete filter configuration for a search
 */
struct FilterConfig {
    // Search parameters
    Deck deck;
    Stake stake;
    int max_ante;  // How far to simulate (1-8)

    // Joker filters
    JokerFilter joker_filters[MAX_JOKER_FILTERS];
    int num_joker_filters;

    // Tarot filters
    TarotFilter tarot_filters[MAX_TAROT_FILTERS];
    int num_tarot_filters;

    // Planet filters
    PlanetFilter planet_filters[MAX_PLANET_FILTERS];
    int num_planet_filters;

    // Spectral filters
    SpectralFilter spectral_filters[MAX_SPECTRAL_FILTERS];
    int num_spectral_filters;

    // Voucher filters
    VoucherFilter voucher_filters[MAX_VOUCHER_FILTERS];
    int num_voucher_filters;

    // Tag filters
    TagFilter tag_filters[MAX_TAG_FILTERS];
    int num_tag_filters;

    // Boss filters
    BossFilter boss_filters[MAX_BOSS_FILTERS];
    int num_boss_filters;

    // Soul joker filters
    SoulJokerFilter soul_joker_filters[MAX_SOUL_JOKER_FILTERS];
    int num_soul_joker_filters;

    // Erratic filters
    ErraticRankFilter erratic_rank_filters[MAX_ERRATIC_RANK_FILTERS];
    int num_erratic_rank_filters;
    ErraticSuitFilter erratic_suit_filters[MAX_ERRATIC_SUIT_FILTERS];
    int num_erratic_suit_filters;

    // Playing card filters
    PlayingCardFilter playing_card_filters[MAX_PLAYING_CARD_FILTERS];
    int num_playing_card_filters;

    __device__ __host__ FilterConfig() :
        deck(DECK_RED), stake(STAKE_WHITE), max_ante(8),
        num_joker_filters(0), num_tarot_filters(0), num_planet_filters(0),
        num_spectral_filters(0), num_voucher_filters(0), num_tag_filters(0),
        num_boss_filters(0), num_soul_joker_filters(0),
        num_erratic_rank_filters(0), num_erratic_suit_filters(0),
        num_playing_card_filters(0) {}
};

// ============================================================================
// Search Result
// ============================================================================

/**
 * @brief Result of evaluating a seed against filters
 */
struct FilterResult {
    bool passes_must;   // All must conditions satisfied
    int total_score;    // Sum of should scores

    __device__ FilterResult() : passes_must(true), total_score(0) {}
};

// ============================================================================
// Helper: Check if ante is allowed
// ============================================================================

__device__ __forceinline__ bool ante_allowed(uint32_t allowed_antes, int ante) {
    return (allowed_antes & (1 << ante)) != 0;
}

// ============================================================================
// Helper: Check if source is allowed
// ============================================================================

__device__ __forceinline__ bool source_allowed(uint32_t allowed_sources, int source_bit) {
    return (allowed_sources & source_bit) != 0;
}

#endif // BALATRO_FILTERS_CUH
