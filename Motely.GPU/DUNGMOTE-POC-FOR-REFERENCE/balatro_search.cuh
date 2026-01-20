/**
 * @file balatro_search.cuh
 * @brief CUDA kernel for parallel seed searching
 *
 * This is the top-level search interface. It includes:
 * - Main search kernel (processes millions of seeds in parallel)
 * - Result collection and output
 * - Filter configuration helpers
 *
 * Usage:
 * 1. Create a FilterConfig with your search criteria
 * 2. Call launch_search() with seed range
 * 3. Process results (matching seeds with scores)
 */

#ifndef BALATRO_SEARCH_CUH
#define BALATRO_SEARCH_CUH

#include "balatro_evaluator.cuh"

// ============================================================================
// Search Result Structure
// ============================================================================

/**
 * @brief Compact result for a matching seed
 */
struct SeedResult {
    uint64_t seed_index;  // Seed as 64-bit index
    int score;            // Total should score
};

// ============================================================================
// Main Search Kernel
// ============================================================================

/**
 * @brief CUDA kernel to search seeds in parallel
 *
 * Each thread evaluates one seed against all filters.
 * Matching seeds are atomically added to the results buffer.
 *
 * @param start_seed     First seed index to check
 * @param num_seeds      Number of seeds to check
 * @param config         Filter configuration (in constant memory)
 * @param results        Output buffer for matching seeds
 * @param result_count   Atomic counter for results
 * @param max_results    Maximum results to store
 */
GPU_KERNEL void search_seeds_kernel(
    uint64_t start_seed,
    uint64_t num_seeds,
    const FilterConfig* config,
    SeedResult* results,
    uint32_t* result_count,
    uint32_t max_results
) {
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    if (tid >= num_seeds) return;

    uint64_t seed_index = start_seed + tid;

    // Convert index to seed string
    char seed[9];
    seed_to_string_v2(seed_index, seed);

    // Evaluate seed
    FilterResult result = evaluate_seed(seed, 8, config);

    // If passes must conditions, add to results
    if (result.passes_must) {
        uint32_t idx = atomicAdd(result_count, 1);
        if (idx < max_results) {
            results[idx].seed_index = seed_index;
            results[idx].score = result.total_score;
        }
    }
}

// ============================================================================
// Filter Builder Helpers
// ============================================================================

/**
 * @brief Helper to add a joker filter
 */
GPU_HOST void add_joker_filter(
    FilterConfig* config,
    int joker_id,           // -1 for any, or specific joker ID
    JokerRarity rarity,     // -1 for any
    Edition edition,        // EDITION_NONE for any
    uint32_t sources,       // SOURCE_ANY, or specific bits
    uint32_t antes,         // Bitflags for antes (1 << ante)
    int score,              // Score if matched (for should)
    bool is_must            // true = must, false = should
) {
    if (config->num_joker_filters >= MAX_JOKER_FILTERS) return;

    JokerFilter* f = &config->joker_filters[config->num_joker_filters++];
    f->wanted_joker = joker_id;
    f->wanted_rarity = rarity;
    f->wanted_edition = edition;
    f->allowed_sources = sources;
    f->allowed_antes = antes;
    f->score = score;
    f->is_must = is_must;
}

/**
 * @brief Helper to add a specific joker by enum
 */
__host__ void add_joker_filter_common(FilterConfig* config, JokerCommon joker, uint32_t antes, bool is_must, int score = 0) {
    add_joker_filter(config, ((int)RARITY_COMMON) | joker, (JokerRarity)-1, EDITION_NONE, SOURCE_ANY, antes, score, is_must);
}

__host__ void add_joker_filter_uncommon(FilterConfig* config, JokerUncommon joker, uint32_t antes, bool is_must, int score = 0) {
    add_joker_filter(config, ((int)RARITY_UNCOMMON) | joker, (JokerRarity)-1, EDITION_NONE, SOURCE_ANY, antes, score, is_must);
}

__host__ void add_joker_filter_rare(FilterConfig* config, JokerRare joker, uint32_t antes, bool is_must, int score = 0) {
    add_joker_filter(config, ((int)RARITY_RARE) | joker, (JokerRarity)-1, EDITION_NONE, SOURCE_ANY, antes, score, is_must);
}

__host__ void add_joker_filter_legendary(FilterConfig* config, JokerLegendary joker, uint32_t antes, bool is_must, int score = 0) {
    add_joker_filter(config, ((int)RARITY_LEGENDARY) | joker, (JokerRarity)-1, EDITION_NONE, SOURCE_ANY, antes, score, is_must);
}

/**
 * @brief Helper to add tarot filter
 */
__host__ void add_tarot_filter(
    FilterConfig* config,
    TarotCard tarot,
    uint32_t sources,
    uint32_t antes,
    int score,
    bool is_must
) {
    if (config->num_tarot_filters >= MAX_TAROT_FILTERS) return;

    TarotFilter* f = &config->tarot_filters[config->num_tarot_filters++];
    f->wanted_tarot = tarot;
    f->allowed_sources = sources;
    f->allowed_antes = antes;
    f->score = score;
    f->is_must = is_must;
}

/**
 * @brief Helper to add voucher filter
 */
__host__ void add_voucher_filter(
    FilterConfig* config,
    Voucher voucher,
    uint32_t antes,
    int score,
    bool is_must
) {
    if (config->num_voucher_filters >= MAX_VOUCHER_FILTERS) return;

    VoucherFilter* f = &config->voucher_filters[config->num_voucher_filters++];
    f->wanted_voucher = voucher;
    f->allowed_antes = antes;
    f->score = score;
    f->is_must = is_must;
}

/**
 * @brief Helper to add erratic rank filter
 */
__host__ void add_erratic_rank_filter(
    FilterConfig* config,
    Rank rank,
    int min_count,
    int score,
    bool is_must
) {
    if (config->num_erratic_rank_filters >= MAX_ERRATIC_RANK_FILTERS) return;

    ErraticRankFilter* f = &config->erratic_rank_filters[config->num_erratic_rank_filters++];
    f->rank = rank;
    f->min_count = min_count;
    f->score = score;
    f->is_must = is_must;
}

/**
 * @brief Helper to add erratic suit filter
 */
__host__ void add_erratic_suit_filter(
    FilterConfig* config,
    Suit suit,
    int min_count,
    int score,
    bool is_must
) {
    if (config->num_erratic_suit_filters >= MAX_ERRATIC_SUIT_FILTERS) return;

    ErraticSuitFilter* f = &config->erratic_suit_filters[config->num_erratic_suit_filters++];
    f->suit = suit;
    f->min_count = min_count;
    f->score = score;
    f->is_must = is_must;
}

/**
 * @brief Helper to add soul joker filter
 */
__host__ void add_soul_joker_filter(
    FilterConfig* config,
    JokerLegendary legendary,  // -1 for any legendary
    uint32_t sources,
    uint32_t antes,
    int score,
    bool is_must
) {
    if (config->num_soul_joker_filters >= MAX_SOUL_JOKER_FILTERS) return;

    SoulJokerFilter* f = &config->soul_joker_filters[config->num_soul_joker_filters++];
    f->wanted_legendary = legendary;
    f->allowed_sources = sources;
    f->allowed_antes = antes;
    f->score = score;
    f->is_must = is_must;
}

// ============================================================================
// Example Filter Configurations
// ============================================================================

/**
 * @brief Create filter for "Daily Wee Erratic" search
 *
 * From dailywee.jaml:
 * - Erratic deck with 10+ Twos
 * - Wee Joker in ante 1
 * - Hanging Chad in antes 1-2
 * - Hack in antes 1-2
 */
__host__ FilterConfig create_daily_wee_erratic_filter() {
    FilterConfig config;

    config.deck = DECK_ERRATIC;
    config.stake = STAKE_WHITE;
    config.max_ante = 2;

    // Must: 10+ Twos in starting deck
    add_erratic_rank_filter(&config, RANK_2, 10, 0, true);

    // Must: Wee Joker in ante 1
    add_joker_filter_rare(&config, J_WEE_JOKER, (1 << 1), true);

    // Must: Hanging Chad in ante 1 or 2
    add_joker_filter_common(&config, J_HANGING_CHAD, (1 << 1) | (1 << 2), true);

    // Must: Hack in ante 1 or 2
    add_joker_filter_uncommon(&config, J_HACK, (1 << 1) | (1 << 2), true);

    // Should: Showman in ante 2 (score 10)
    add_joker_filter_uncommon(&config, J_SHOWMAN, (1 << 2), false, 10);

    return config;
}

/**
 * @brief Create filter for "Perkeo Judgement" search
 *
 * From PerkeoJudgement.jaml:
 * - Judgement tarot in ante 1 shop
 * - Perkeo (Soul) in ante 1 pack
 */
GPU_HOST FilterConfig create_perkeo_judgement_filter() {
    FilterConfig config;

    config.deck = DECK_CHECKERED;
    config.stake = STAKE_WHITE;
    config.max_ante = 1;

    // Must: Judgement tarot in ante 1 shop slots
    add_tarot_filter(&config, T_JUDGEMENT, SOURCE_ANY_SHOP, (1 << 1), 0, true);

    // Must: Perkeo from Soul card in ante 1 pack
    add_soul_joker_filter(&config, J_PERKEO, SOURCE_ANY_PACK, (1 << 1), 0, true);

    // Should: Any negative joker (score 10)
    add_joker_filter(&config, -1, (JokerRarity)-1, EDITION_NEGATIVE, SOURCE_ANY, 0xFF, 10, false);

    return config;
}

// ============================================================================
// Ante Mask Helpers
// ============================================================================

#define ANTE_1 (1 << 1)
#define ANTE_2 (1 << 2)
#define ANTE_3 (1 << 3)
#define ANTE_4 (1 << 4)
#define ANTE_5 (1 << 5)
#define ANTE_6 (1 << 6)
#define ANTE_7 (1 << 7)
#define ANTE_8 (1 << 8)
#define ANTE_1_2 (ANTE_1 | ANTE_2)
#define ANTE_1_3 (ANTE_1 | ANTE_2 | ANTE_3)
#define ANTE_ALL (0xFF << 1)

#endif // BALATRO_SEARCH_CUH
