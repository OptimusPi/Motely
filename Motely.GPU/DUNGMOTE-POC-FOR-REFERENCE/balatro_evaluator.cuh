/**
 * @file balatro_evaluator.cuh
 * @brief CUDA seed evaluation engine for filter matching
 *
 * This is the heart of the seed searcher - it takes a seed and a filter
 * configuration, simulates the game state, and checks if all must conditions
 * are met while tallying should scores.
 *
 * The evaluator is designed for maximum GPU efficiency:
 * - Early exit when must conditions fail
 * - Minimal memory allocation (stack-based)
 * - Stream-based iteration (only compute what's needed)
 */

#ifndef BALATRO_EVALUATOR_CUH
#define BALATRO_EVALUATOR_CUH

#include "balatro_filters.cuh"
#include "balatro_rng.cuh"

// ============================================================================
// Shop Slot Count by Vouchers
// ============================================================================

#define BASE_SHOP_SLOTS      2
#define OVERSTOCK_SHOP_SLOTS 3
#define OVERSTOCK_PLUS_SHOP_SLOTS 4

// ============================================================================
// Pack Size Constants
// ============================================================================

#define BUFFOON_PACK_SIZE_NORMAL 2
#define BUFFOON_PACK_SIZE_JUMBO  3
#define BUFFOON_PACK_SIZE_MEGA   4

#define ARCANA_PACK_SIZE_NORMAL  3
#define ARCANA_PACK_SIZE_JUMBO   4
#define ARCANA_PACK_SIZE_MEGA    5

// ============================================================================
// Erratic Deck Evaluation
// ============================================================================

/**
 * @brief Evaluate erratic deck starting state
 *
 * For Erratic deck, we need to check rank/suit counts in the
 * randomly generated starting deck.
 */
__device__ bool evaluate_erratic_filters(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int* out_score
) {
    if (config->deck != DECK_ERRATIC) {
        return true;  // Not erratic, skip
    }

    if (config->num_erratic_rank_filters == 0 &&
        config->num_erratic_suit_filters == 0) {
        return true;  // No erratic filters
    }

    // Count ranks and suits in starting deck
    int rank_counts[13] = {0};
    int suit_counts[4] = {0};

    // Generate 52 cards for erratic deck
    for (int card = 0; card < 52; card++) {
        // Build key: "front" + card_index
        char key_buf[16];
        int key_len = 0;

        key_buf[key_len++] = 'f';
        key_buf[key_len++] = 'r';
        key_buf[key_len++] = 'o';
        key_buf[key_len++] = 'n';
        key_buf[key_len++] = 't';

        // Card index as string
        if (card >= 10) {
            key_buf[key_len++] = '0' + (card / 10);
        }
        key_buf[key_len++] = '0' + (card % 10);
        key_buf[key_len] = '\0';

        // Get random rank (0-12) and suit (0-3)
        // Erratic generates each independently
        int rank = pseudorandom_range_v2(key_buf, key_len, seed, seed_len, seed_hash, 0, 13);

        // Suit key: "front" + card_index + "suit" (different stream)
        key_buf[key_len++] = 's';
        key_buf[key_len] = '\0';
        int suit = pseudorandom_range_v2(key_buf, key_len, seed, seed_len, seed_hash, 0, 4);

        rank_counts[rank]++;
        suit_counts[suit]++;
    }

    // Check rank filters
    for (int i = 0; i < config->num_erratic_rank_filters; i++) {
        const ErraticRankFilter* filter = &config->erratic_rank_filters[i];
        int count = rank_counts[(int)filter->rank];

        if (filter->is_must) {
            if (count < filter->min_count) {
                return false;  // Must condition failed
            }
        } else {
            if (count >= filter->min_count) {
                *out_score += filter->score;
            }
        }
    }

    // Check suit filters
    for (int i = 0; i < config->num_erratic_suit_filters; i++) {
        const ErraticSuitFilter* filter = &config->erratic_suit_filters[i];
        int count = suit_counts[(int)filter->suit];

        if (filter->is_must) {
            if (count < filter->min_count) {
                return false;
            }
        } else {
            if (count >= filter->min_count) {
                *out_score += filter->score;
            }
        }
    }

    return true;
}

// ============================================================================
// Joker Filter Evaluation
// ============================================================================

/**
 * @brief Check joker filters against shop jokers for an ante
 */
__device__ bool evaluate_shop_joker_filters_for_ante(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int ante, int num_shop_slots,
    bool* filter_satisfied,  // Track which filters are satisfied
    int* out_score
) {
    // Create streams for this ante
    JokerStream joker_stream = create_shop_joker_stream(
        seed, seed_len, seed_hash, ante, config->stake
    );

    ShopItemTypeStream type_stream = create_shop_item_type_stream(
        seed, seed_len, seed_hash, ante, config->deck,
        false, false,  // tarot merchant/tycoon
        false, false,  // planet merchant/tycoon
        false          // magic trick
    );

    // Check each shop slot
    for (int slot = 0; slot < num_shop_slots; slot++) {
        ShopSlotType slot_type = get_next_shop_slot_type(&type_stream);

        if (slot_type == SLOT_JOKER) {
            Item joker = get_next_joker(&joker_stream);

            // Check against all joker filters
            for (int f = 0; f < config->num_joker_filters; f++) {
                const JokerFilter* filter = &config->joker_filters[f];

                // Already satisfied?
                if (filter_satisfied[f]) continue;

                // Ante allowed?
                if (!ante_allowed(filter->allowed_antes, ante)) continue;

                // Source allowed?
                int source_bit = (1 << slot);  // SHOP_SLOT_0..3
                if (!source_allowed(filter->allowed_sources, source_bit)) continue;

                // Joker matches?
                if (joker_matches(filter, &joker)) {
                    filter_satisfied[f] = true;
                    if (!filter->is_must) {
                        *out_score += filter->score;
                    }
                }
            }
        }
    }

    return true;  // Continue evaluation
}

/**
 * @brief Check joker filters against buffoon pack for an ante
 */
__device__ bool evaluate_buffoon_pack_joker_filters_for_ante(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int ante, int pack_size,
    bool* filter_satisfied,
    int* out_score
) {
    JokerStream joker_stream = create_buffoon_pack_joker_stream(
        seed, seed_len, seed_hash, ante, config->stake
    );

    for (int slot = 0; slot < pack_size; slot++) {
        Item joker = get_next_joker(&joker_stream);

        for (int f = 0; f < config->num_joker_filters; f++) {
            const JokerFilter* filter = &config->joker_filters[f];

            if (filter_satisfied[f]) continue;
            if (!ante_allowed(filter->allowed_antes, ante)) continue;

            int source_bit = (1 << (4 + slot));  // PACK_SLOT_0..4
            if (!source_allowed(filter->allowed_sources, source_bit)) continue;

            if (joker_matches(filter, &joker)) {
                filter_satisfied[f] = true;
                if (!filter->is_must) {
                    *out_score += filter->score;
                }
            }
        }
    }

    return true;
}

// ============================================================================
// Tarot Filter Evaluation
// ============================================================================

__device__ bool evaluate_shop_tarot_filters_for_ante(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int ante,
    bool* filter_satisfied,
    int* out_score
) {
    TarotStream tarot_stream = create_shop_tarot_stream(
        seed, seed_len, seed_hash, ante
    );

    // Shop tarots appear when slot type is TAROT
    // For simplicity, generate first few tarots and check
    for (int slot = 0; slot < 4; slot++) {
        TarotCard tarot = get_next_tarot(&tarot_stream);

        for (int f = 0; f < config->num_tarot_filters; f++) {
            const TarotFilter* filter = &config->tarot_filters[f];

            if (filter_satisfied[f]) continue;
            if (!ante_allowed(filter->allowed_antes, ante)) continue;

            int source_bit = (1 << slot);  // SHOP_SLOT_0..3
            if (!source_allowed(filter->allowed_sources, source_bit)) continue;

            if (filter->wanted_tarot == (TarotCard)-1 ||
                filter->wanted_tarot == tarot) {
                filter_satisfied[f] = true;
                if (!filter->is_must) {
                    *out_score += filter->score;
                }
            }
        }
    }

    return true;
}

__device__ bool evaluate_arcana_pack_tarot_filters_for_ante(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int ante, int pack_size,
    bool* filter_satisfied,
    int* out_score
) {
    TarotStream tarot_stream = create_arcana_pack_tarot_stream(
        seed, seed_len, seed_hash, ante
    );

    for (int slot = 0; slot < pack_size; slot++) {
        TarotCard tarot = get_next_tarot(&tarot_stream);

        for (int f = 0; f < config->num_tarot_filters; f++) {
            const TarotFilter* filter = &config->tarot_filters[f];

            if (filter_satisfied[f]) continue;
            if (!ante_allowed(filter->allowed_antes, ante)) continue;

            int source_bit = (1 << (4 + slot));  // PACK_SLOT_0..4
            if (!source_allowed(filter->allowed_sources, source_bit)) continue;

            if (filter->wanted_tarot == (TarotCard)-1 ||
                filter->wanted_tarot == tarot) {
                filter_satisfied[f] = true;
                if (!filter->is_must) {
                    *out_score += filter->score;
                }
            }
        }
    }

    return true;
}

// ============================================================================
// Voucher Filter Evaluation
// ============================================================================

__device__ bool evaluate_voucher_filters_for_ante(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int ante,
    bool* filter_satisfied,
    int* out_score
) {
    Voucher voucher = get_voucher_for_ante(seed, seed_len, seed_hash, ante);

    for (int f = 0; f < config->num_voucher_filters; f++) {
        const VoucherFilter* filter = &config->voucher_filters[f];

        if (filter_satisfied[f]) continue;
        if (!ante_allowed(filter->allowed_antes, ante)) continue;

        if (filter->wanted_voucher == (Voucher)-1 ||
            filter->wanted_voucher == voucher) {
            filter_satisfied[f] = true;
            if (!filter->is_must) {
                *out_score += filter->score;
            }
        }
    }

    return true;
}

// ============================================================================
// Boss Filter Evaluation
// ============================================================================

__device__ bool evaluate_boss_filters_for_ante(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int ante,
    bool* filter_satisfied,
    int* out_score
) {
    BossBlind boss = get_boss_for_ante(seed, seed_len, seed_hash, ante);

    for (int f = 0; f < config->num_boss_filters; f++) {
        const BossFilter* filter = &config->boss_filters[f];

        if (filter_satisfied[f]) continue;
        if (!ante_allowed(filter->allowed_antes, ante)) continue;

        if (filter->wanted_boss == (BossBlind)-1 ||
            filter->wanted_boss == boss) {
            filter_satisfied[f] = true;
            if (!filter->is_must) {
                *out_score += filter->score;
            }
        }
    }

    return true;
}

// ============================================================================
// Tag Filter Evaluation
// ============================================================================

__device__ bool evaluate_tag_filters_for_ante(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int ante,
    bool* filter_satisfied,
    int* out_score
) {
    // Tags for small blind and big blind skips
    Tag small_blind_tag = get_tag_for_ante(seed, seed_len, seed_hash, ante, "1", 1);
    Tag big_blind_tag = get_tag_for_ante(seed, seed_len, seed_hash, ante, "2", 1);

    for (int f = 0; f < config->num_tag_filters; f++) {
        const TagFilter* filter = &config->tag_filters[f];

        if (filter_satisfied[f]) continue;
        if (!ante_allowed(filter->allowed_antes, ante)) continue;

        if (filter->wanted_tag == (Tag)-1 ||
            filter->wanted_tag == small_blind_tag ||
            filter->wanted_tag == big_blind_tag) {
            filter_satisfied[f] = true;
            if (!filter->is_must) {
                *out_score += filter->score;
            }
        }
    }

    return true;
}

// ============================================================================
// Soul Joker Filter Evaluation
// ============================================================================

/**
 * @brief Check for Soul card spawning in packs
 *
 * Soul cards can appear in Spectral packs with 0.3% chance.
 * They provide a legendary joker.
 */
__device__ bool check_soul_spawn(
    const char* seed, int seed_len, double seed_hash,
    int ante, const char* type_str, int type_len
) {
    char key_buf[32];
    int key_len = 0;

    // "soul_" + type + ante
    key_buf[key_len++] = 's';
    key_buf[key_len++] = 'o';
    key_buf[key_len++] = 'u';
    key_buf[key_len++] = 'l';
    key_buf[key_len++] = '_';

    for (int i = 0; i < type_len; i++) {
        key_buf[key_len++] = type_str[i];
    }
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';

    double roll = pseudorandom(key_buf, key_len, seed, seed_len, seed_hash);
    return roll > 0.997;  // 0.3% chance
}

__device__ bool evaluate_soul_joker_filters_for_ante(
    const char* seed, int seed_len, double seed_hash,
    const FilterConfig* config,
    int ante,
    bool* filter_satisfied,
    int* out_score
) {
    if (config->num_soul_joker_filters == 0) return true;

    // Check each pack slot for Soul spawn
    for (int slot = 0; slot < 5; slot++) {
        // Check if Soul spawns in this slot
        char slot_str[2] = {'0' + slot, '\0'};
        bool soul_spawns = check_soul_spawn(seed, seed_len, seed_hash, ante, slot_str, 1);

        if (soul_spawns) {
            // Get the legendary joker from Soul
            SoulJokerStream soul_stream = create_soul_joker_stream(
                seed, seed_len, seed_hash, ante
            );

            // Advance stream to this slot
            for (int s = 0; s < slot; s++) {
                get_next_soul_joker(&soul_stream);
            }

            Item legendary = get_next_soul_joker(&soul_stream);
            int leg_index = get_joker_index(legendary.type_value);

            for (int f = 0; f < config->num_soul_joker_filters; f++) {
                const SoulJokerFilter* filter = &config->soul_joker_filters[f];

                if (filter_satisfied[f]) continue;
                if (!ante_allowed(filter->allowed_antes, ante)) continue;

                int source_bit = (1 << (4 + slot));  // PACK_SLOT_0..4
                if (!source_allowed(filter->allowed_sources, source_bit)) continue;

                if (filter->wanted_legendary == (JokerLegendary)-1 ||
                    (int)filter->wanted_legendary == leg_index) {
                    filter_satisfied[f] = true;
                    if (!filter->is_must) {
                        *out_score += filter->score;
                    }
                }
            }
        }
    }

    return true;
}

// ============================================================================
// Main Seed Evaluation
// ============================================================================

/**
 * @brief Evaluate a seed against all filters
 *
 * Returns true if all must conditions are met.
 * Fills in the total score from should conditions.
 */
__device__ FilterResult evaluate_seed(
    const char* seed, int seed_len,
    const FilterConfig* config
) {
    FilterResult result;

    // Pre-compute seed hash
    double seed_hash = pseudohash(seed, seed_len);

    // Track which filters are satisfied
    bool joker_satisfied[MAX_JOKER_FILTERS] = {false};
    bool tarot_satisfied[MAX_TAROT_FILTERS] = {false};
    bool planet_satisfied[MAX_PLANET_FILTERS] = {false};
    bool spectral_satisfied[MAX_SPECTRAL_FILTERS] = {false};
    bool voucher_satisfied[MAX_VOUCHER_FILTERS] = {false};
    bool tag_satisfied[MAX_TAG_FILTERS] = {false};
    bool boss_satisfied[MAX_BOSS_FILTERS] = {false};
    bool soul_joker_satisfied[MAX_SOUL_JOKER_FILTERS] = {false};

    // === ERRATIC DECK FILTERS (checked first, before ante loop) ===
    if (!evaluate_erratic_filters(seed, seed_len, seed_hash, config, &result.total_score)) {
        result.passes_must = false;
        return result;
    }

    // === PER-ANTE EVALUATION ===
    for (int ante = 1; ante <= config->max_ante; ante++) {
        int num_shop_slots = BASE_SHOP_SLOTS;  // TODO: Track vouchers

        // Joker filters - shop
        evaluate_shop_joker_filters_for_ante(
            seed, seed_len, seed_hash, config,
            ante, num_shop_slots,
            joker_satisfied, &result.total_score
        );

        // Joker filters - buffoon pack
        evaluate_buffoon_pack_joker_filters_for_ante(
            seed, seed_len, seed_hash, config,
            ante, BUFFOON_PACK_SIZE_NORMAL,
            joker_satisfied, &result.total_score
        );

        // Tarot filters - shop
        evaluate_shop_tarot_filters_for_ante(
            seed, seed_len, seed_hash, config,
            ante, tarot_satisfied, &result.total_score
        );

        // Tarot filters - arcana pack
        evaluate_arcana_pack_tarot_filters_for_ante(
            seed, seed_len, seed_hash, config,
            ante, ARCANA_PACK_SIZE_NORMAL,
            tarot_satisfied, &result.total_score
        );

        // Voucher filters
        evaluate_voucher_filters_for_ante(
            seed, seed_len, seed_hash, config,
            ante, voucher_satisfied, &result.total_score
        );

        // Boss filters
        evaluate_boss_filters_for_ante(
            seed, seed_len, seed_hash, config,
            ante, boss_satisfied, &result.total_score
        );

        // Tag filters
        evaluate_tag_filters_for_ante(
            seed, seed_len, seed_hash, config,
            ante, tag_satisfied, &result.total_score
        );

        // Soul joker filters
        evaluate_soul_joker_filters_for_ante(
            seed, seed_len, seed_hash, config,
            ante, soul_joker_satisfied, &result.total_score
        );
    }

    // === CHECK MUST CONDITIONS ===

    // Joker must conditions
    for (int f = 0; f < config->num_joker_filters; f++) {
        if (config->joker_filters[f].is_must && !joker_satisfied[f]) {
            result.passes_must = false;
            return result;
        }
    }

    // Tarot must conditions
    for (int f = 0; f < config->num_tarot_filters; f++) {
        if (config->tarot_filters[f].is_must && !tarot_satisfied[f]) {
            result.passes_must = false;
            return result;
        }
    }

    // Planet must conditions
    for (int f = 0; f < config->num_planet_filters; f++) {
        if (config->planet_filters[f].is_must && !planet_satisfied[f]) {
            result.passes_must = false;
            return result;
        }
    }

    // Spectral must conditions
    for (int f = 0; f < config->num_spectral_filters; f++) {
        if (config->spectral_filters[f].is_must && !spectral_satisfied[f]) {
            result.passes_must = false;
            return result;
        }
    }

    // Voucher must conditions
    for (int f = 0; f < config->num_voucher_filters; f++) {
        if (config->voucher_filters[f].is_must && !voucher_satisfied[f]) {
            result.passes_must = false;
            return result;
        }
    }

    // Tag must conditions
    for (int f = 0; f < config->num_tag_filters; f++) {
        if (config->tag_filters[f].is_must && !tag_satisfied[f]) {
            result.passes_must = false;
            return result;
        }
    }

    // Boss must conditions
    for (int f = 0; f < config->num_boss_filters; f++) {
        if (config->boss_filters[f].is_must && !boss_satisfied[f]) {
            result.passes_must = false;
            return result;
        }
    }

    // Soul joker must conditions
    for (int f = 0; f < config->num_soul_joker_filters; f++) {
        if (config->soul_joker_filters[f].is_must && !soul_joker_satisfied[f]) {
            result.passes_must = false;
            return result;
        }
    }

    return result;
}

#endif // BALATRO_EVALUATOR_CUH
