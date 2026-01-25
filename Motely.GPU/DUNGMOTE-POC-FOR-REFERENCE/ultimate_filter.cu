/**
 * @file ultimate_filter.cu
 * @brief Find Perkeo from Soul card + score by best joker grouping
 *
 * This tool:
 * 1. Checks if Perkeo appears from a Soul card in Arcana/Spectral packs
 * 2. Scores seeds by best grouping of desired jokers (sudden death forgiveness)
 * 3. Only outputs seeds with Perkeo that meet minimum score
 *
 * Compiled with `--fmad=false` for Lua-precision compatibility.
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"
#include <chrono>

#include "balatro_args.cuh"
#include "balatro_enums.cuh"
#include "balatro_rng.cuh"
#include "balatro_batch.cuh"
#include "balatro_batch_kernel.cuh"
#include "balatro_progress.cuh"
#include "balatro_results.cuh"
#include "balatro_streams.cuh"
#include "balatro_joker_names.cuh"

#define MAX_RESULTS 1000000
#define MAX_RESULT_LEN 16  // 8 chars + padding

struct UFResult {
    char seed_str[9];
    int sum_score;
    int max_score;
    int perkeo_ante;
    int showman_flag; // 1 if found, 0 otherwise
};

// ============================================================================
// Configuration
// ============================================================================

struct UltimateFilterConfig {
    int start_ante;
    int end_ante;
    int joker_rolls;
    int wanted_joker_id0;
    int wanted_joker_id1;
    int wanted_joker_id2;
    int wanted_joker_id3;
    int num_wanted_jokers;
    int min_score;
};

GPU_DEVICE __forceinline__ bool add_result_uf(
    UFResult* results, int* result_count, int max_results,
    const char* seed_str, int sum_score, int max_score, int perkeo_ante, int showman_flag
) {
    int idx = atomicAdd(result_count, 1);
    if (idx >= max_results) return false;
    for (int i = 0; i < 8; i++) results[idx].seed_str[i] = seed_str[i];
    results[idx].seed_str[8] = '\0';
    results[idx].sum_score = sum_score;
    results[idx].max_score = max_score;
    results[idx].perkeo_ante = perkeo_ante;
    results[idx].showman_flag = showman_flag;
    return true;
}

// ============================================================================
// Soul Card Check
// ============================================================================

/**
 * @brief Best grouping with sudden death (from negative_tag_skipper)
 */
GPU_DEVICE int best_group_sudden_death(
    const char* seed_str, double seed_hash, int ante, int joker_rolls,
    int wanted_joker_id0, int wanted_joker_id1, int wanted_joker_id2, int wanted_joker_id3,
    int num_wanted_jokers
) {
    int max_group = 0;
    int cur = 0;
    bool sudden_death = false;

    JokerStream js = create_shop_joker_stream(seed_str, 8, seed_hash, ante, STAKE_WHITE);

    for (int i = 0; i < joker_rolls; i++) {
        Item joker = get_next_joker(&js);
        int joker_index = joker.type_value & JOKER_INDEX_MASK;
        int joker_rarity = get_joker_rarity(joker.type_value);
        int joker_full_id = ((int)joker_rarity | joker_index);

        bool is_wanted =
            (joker_full_id == wanted_joker_id0) ||
            (num_wanted_jokers > 1 && joker_full_id == wanted_joker_id1) ||
            (num_wanted_jokers > 2 && joker_full_id == wanted_joker_id2) ||
            (num_wanted_jokers > 3 && joker_full_id == wanted_joker_id3);

        if (is_wanted) {
            cur++;
        } else {
            if (sudden_death) {
                if (cur > max_group) max_group = cur;
                cur = 0;
                sudden_death = false;
            } else {
                sudden_death = true;
            }
        }
    }

    if (cur > max_group) max_group = cur;
    return max_group;
}

/**
 * @brief Best grouping with sudden death, with an initial burn of joker rolls
 */
GPU_DEVICE int best_group_sudden_death_burn(
    const char* seed_str, double seed_hash, int ante, int joker_rolls, int burn_rolls,
    int wanted_joker_id0, int wanted_joker_id1, int wanted_joker_id2, int wanted_joker_id3,
    int num_wanted_jokers
) {
    int max_group = 0;
    int cur = 0;
    bool sudden_death = false;

    JokerStream js = create_shop_joker_stream(seed_str, 8, seed_hash, ante, STAKE_WHITE);

    int burn = burn_rolls;
    if (burn > joker_rolls) burn = joker_rolls;
    for (int i = 0; i < burn; i++) {
        get_next_joker(&js);
    }

    for (int i = burn; i < joker_rolls; i++) {
        Item joker = get_next_joker(&js);
        int joker_index = joker.type_value & JOKER_INDEX_MASK;
        int joker_rarity = get_joker_rarity(joker.type_value);
        int joker_full_id = ((int)joker_rarity | joker_index);

        bool is_wanted =
            (joker_full_id == wanted_joker_id0) ||
            (num_wanted_jokers > 1 && joker_full_id == wanted_joker_id1) ||
            (num_wanted_jokers > 2 && joker_full_id == wanted_joker_id2) ||
            (num_wanted_jokers > 3 && joker_full_id == wanted_joker_id3);

        if (is_wanted) {
            cur++;
        } else {
            if (sudden_death) {
                if (cur > max_group) max_group = cur;
                cur = 0;
                sudden_death = false;
            } else {
                sudden_death = true;
            }
        }
    }

    if (cur > max_group) max_group = cur;
    return max_group;
}

/**
 * @brief Check Showman appears in early rolls before scoring Negative: first 2 shop rolls of ante 1 and 2
 */
GPU_DEVICE bool has_showman_prescore(
    const char* seed_str, double seed_hash, int start_ante, int end_ante, int joker_rolls
) {
    const int SHOWMAN_ID = ((int)RARITY_UNCOMMON | (J_SHOWMAN & JOKER_INDEX_MASK));
    int max_ante_check = end_ante;
    if (max_ante_check > 2) max_ante_check = 2;  // only before ante 3
    int min_ante_check = start_ante;
    if (min_ante_check < 1) min_ante_check = 1;
    for (int ante = min_ante_check; ante <= max_ante_check; ante++) {
        JokerStream js = create_shop_joker_stream(seed_str, 8, seed_hash, ante, STAKE_WHITE);
        int limit = (joker_rolls < 2) ? joker_rolls : 2;
        for (int i = 0; i < limit; i++) {
            Item joker = get_next_joker(&js);
            int joker_index = joker.type_value & JOKER_INDEX_MASK;
            int joker_rarity = get_joker_rarity(joker.type_value);
            int joker_full_id = ((int)joker_rarity | joker_index);
            if (joker_full_id == SHOWMAN_ID) return true;
        }
    }
    return false;
}

/**
 * @brief Check first 3 Arcana slots (ante 1-4) for Soul->Perkeo using soul_Tarot keys,
 *        consuming the Arcana pack tarot stream per slot.
 */
GPU_DEVICE int check_perkeo_from_soul_packs(
    const char* seed, int seed_len, double seed_hash,
    const UltimateFilterConfig* config
) {
    int ante_start = config->start_ante;
    int ante_end = config->end_ante;

    for (int ante = ante_start; ante <= ante_end; ante++) {
        // Arcana pack tarot stream for this ante
        TarotStream tarot_stream = create_arcana_pack_tarot_stream(seed, seed_len, seed_hash, ante);

        // Soul chance key: "soul_Tarot" + ante
        char key_buf[16];
        int key_len = 0;
        key_buf[key_len++] = 's';
        key_buf[key_len++] = 'o';
        key_buf[key_len++] = 'u';
        key_buf[key_len++] = 'l';
        key_buf[key_len++] = '_';
        key_buf[key_len++] = 'T';
        key_buf[key_len++] = 'a';
        key_buf[key_len++] = 'r';
        key_buf[key_len++] = 'o';
        key_buf[key_len++] = 't';
        key_buf[key_len++] = '0' + ante;
        key_buf[key_len] = '\0';

        PrngStream soul_stream = create_prng_stream(key_buf, key_len, seed, seed_len, seed_hash);
        for (int slot = 0; slot < 3; slot++) {
            // advance soul stream to this slot
            for (int s = 0; s < slot; s++) get_next_random(&soul_stream);

            // consume the tarot card for this slot (not used further, but keeps stream in sync)
            get_next_tarot(&tarot_stream);

            double roll = get_next_random(&soul_stream);  // soul roll for this slot
            if (roll > 0.997) {  // Soul
                SoulJokerStream joker_stream = create_soul_joker_stream(seed, seed_len, seed_hash, ante);
                for (int s = 0; s < slot; s++) get_next_soul_joker(&joker_stream);
                Item legendary = get_next_soul_joker(&joker_stream);
                int leg_index = get_joker_index(legendary.type_value);
                if (leg_index == (int)J_PERKEO) return ante;
            }
        }
    }
    return 0;
}

// ============================================================================
// CUDA Kernel
// ============================================================================

GPU_KERNEL void ultimate_filter_kernel(
    uint64_t batch_index,
    int batch_chars,
    const UltimateFilterConfig* config,
    UFResult* d_results,
    int* d_result_count,
    int* d_progress_counter,
    int* d_cutoff,
    int max_results
) {
    // Shared memory cutoff per block
    __shared__ int block_cutoff;
    if (threadIdx.x == 0) {
        block_cutoff = atomicAdd(d_cutoff, 0);
        if (block_cutoff < config->min_score) block_cutoff = config->min_score;
    }
    __syncthreads();
    
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);

    // Local cutoff - refresh periodically
    int local_cutoff = block_cutoff;
    int cutoff_refresh_counter = 0;
    const int CUTOFF_REFRESH_INTERVAL = 32;

    // Get cached suffix hash (shared across all threads in block)
    double cached_hash = get_cached_suffix_hash(batch_index, batch_chars);

    char seed_str[9];
    for (uint64_t local_idx = tid; local_idx < seeds_per_batch; local_idx += stride) {
        double seed_hash = process_seed_in_batch(batch_index, local_idx, batch_chars, cached_hash, seed_str);

        // Perkeo ante (0 if none)
        int perkeo_ante = check_perkeo_from_soul_packs(seed_str, 8, seed_hash, config);

        // Showman flag: first 2 shop rolls of antes 1-2 (if in range)
        int showman_flag = has_showman_prescore(seed_str, seed_hash, config->start_ante, config->end_ante, config->joker_rolls) ? 1 : 0;

        // Score: best-group per Negative ante with effective rolls = joker_rolls * ante, burn 2 then score
        int total_sum = 0;
        int max_group = 0;
        for (int ante = config->start_ante; ante <= config->end_ante; ante++) {
            Tag ante_tag = get_tag_for_ante(seed_str, 8, seed_hash, ante, "", 0);
            if (ante_tag != TAG_NEGATIVE) continue;
            int effective_rolls = config->joker_rolls * ante;
            if (effective_rolls < 0) effective_rolls = 0;
            int score = best_group_sudden_death_burn(
                seed_str, seed_hash, ante, effective_rolls, 2,
                config->wanted_joker_id0, config->wanted_joker_id1,
                config->wanted_joker_id2, config->wanted_joker_id3,
                config->num_wanted_jokers
            );
            total_sum += score;
            if (score > max_group) max_group = score;
        }

        // Early exit if can't beat cutoff (based on sum)
        if (total_sum < local_cutoff) continue;

        if (total_sum >= config->min_score) {
            add_result_uf(d_results, d_result_count, max_results, seed_str, total_sum, max_group, perkeo_ante, showman_flag);

            // Update cutoff on better sum
            if (total_sum > local_cutoff) {
                int old_cutoff = atomicAdd(d_cutoff, 0);
                while (total_sum > old_cutoff) {
                    int swapped = atomicCAS((int*)d_cutoff, old_cutoff, total_sum);
                    if (swapped == old_cutoff) {
                        __threadfence();
                        local_cutoff = total_sum;
                        break;
                    }
                    old_cutoff = swapped;
                }
            }
        }

        // Refresh local cutoff periodically
        cutoff_refresh_counter++;
        if (cutoff_refresh_counter >= CUTOFF_REFRESH_INTERVAL) {
            if (threadIdx.x == 0) {
                block_cutoff = atomicAdd(d_cutoff, 0);
            }
            __syncthreads();
            local_cutoff = block_cutoff;
            cutoff_refresh_counter = 0;
        }

        // Progress update
        if ((local_idx % 10000 == 0) && tid == 0) {
            atomicAdd(d_progress_counter, 10000);
        }
    }
}

// ============================================================================
// Main
// ============================================================================

int main(int argc, char** argv) {
    // Parse arguments
    int batch_chars = 4;
    int64_t start_batch = 0;
    int64_t end_batch = -1;
    int start_ante = 1;
    int end_ante = 8;
    int joker_rolls = 7;
    int min_score = 3;
    const char* jokers_str = NULL;

    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--batch-chars") == 0 && i + 1 < argc) {
            batch_chars = atoi(argv[++i]);
        } else if (strcmp(argv[i], "--start-batch") == 0 && i + 1 < argc) {
            start_batch = strtoll(argv[++i], NULL, 10);
        } else if (strcmp(argv[i], "--end-batch") == 0 && i + 1 < argc) {
            end_batch = strtoll(argv[++i], NULL, 10);
        } else if (strcmp(argv[i], "--start-ante") == 0 && i + 1 < argc) {
            start_ante = atoi(argv[++i]);
        } else if (strcmp(argv[i], "--end-ante") == 0 && i + 1 < argc) {
            end_ante = atoi(argv[++i]);
        } else if (strcmp(argv[i], "--jokers") == 0 && i + 1 < argc) {
            jokers_str = argv[++i];
        } else if (strcmp(argv[i], "--joker-rolls") == 0 && i + 1 < argc) {
            joker_rolls = atoi(argv[++i]);
        } else if (strcmp(argv[i], "--min-score") == 0 && i + 1 < argc) {
            min_score = atoi(argv[++i]);
        } else if (strcmp(argv[i], "--help") == 0) {
            printf("Usage: %s [options]\n", argv[0]);
            printf("Options:\n");
            printf("  --batch-chars N    Number of batch characters (default: 4)\n");
            printf("  --start-batch N    Start batch index (default: 0)\n");
            printf("  --end-batch N      End batch index (default: -1 = all)\n");
            printf("  --start-ante N   First ante to check (default: 1)\n");
            printf("  --end-ante N     Last ante to check (default: 8)\n");
            printf("  --jokers LIST     Comma-separated joker names (required)\n");
            printf("  --joker-rolls N   Number of joker rolls per ante (default: 7)\n");
            printf("  --min-score N     Minimum score to output (default: 1)\n");
            return 0;
        }
    }

    // Parse jokers
    if (!jokers_str) {
        fprintf(stderr, "$Error: --jokers required\n");
        return 1;
    }

    int wanted_ids[4] = {-1, -1, -1, -1};
    int num_wanted = 0;
    char buf[256];
    strncpy(buf, jokers_str, sizeof(buf) - 1);
    buf[sizeof(buf) - 1] = '\0';
    char* tok = strtok(buf, ",");
    while (tok && num_wanted < 4) {
        // Trim whitespace
        while (*tok == ' ') tok++;
        char* end = tok + strlen(tok) - 1;
        while (end > tok && *end == ' ') *end-- = '\0';
        
        int jid = joker_name_to_id(tok);
        if (jid >= 0) {
            wanted_ids[num_wanted++] = jid;
        } else {
            fprintf(stderr, "$Warning: Unknown joker: %s\n", tok);
        }
        tok = strtok(NULL, ",");
    }

    if (num_wanted == 0) {
        fprintf(stderr, "$Error: No valid jokers specified\n");
        return 1;
    }

    // Calculate total batches if needed
    uint64_t total_batches = calculate_total_batches(batch_chars);
    uint64_t start_batch_u = (uint64_t)start_batch;
    uint64_t end_batch_u = (end_batch >= 0) ? (uint64_t)end_batch : (total_batches - 1);
    if (end_batch_u >= total_batches) end_batch_u = total_batches - 1;
    if (end_batch_u < start_batch_u) {
        fprintf(stderr, "$Error: end_batch < start_batch\n");
        return 1;
    }

    // Setup config
    UltimateFilterConfig config;
    config.start_ante = start_ante;
    config.end_ante = end_ante;
    config.joker_rolls = joker_rolls;
    config.wanted_joker_id0 = wanted_ids[0];
    config.wanted_joker_id1 = wanted_ids[1];
    config.wanted_joker_id2 = wanted_ids[2];
    config.wanted_joker_id3 = wanted_ids[3];
    config.num_wanted_jokers = num_wanted;
    config.min_score = min_score;

    // GPU setup
    int device;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    fprintf(stderr, "$GPU: %s (SM %d.%d)\n", prop.name, prop.major, prop.minor);

    // Allocate device memory
    UltimateFilterConfig* d_config;
    GPU_MALLOC((void**)&d_config, sizeof(UltimateFilterConfig));
    GPU_MEMCPY(d_config, &config, sizeof(UltimateFilterConfig), GPU_MEMCPY_HOST_TO_DEVICE);

    UFResult* d_results = nullptr;
    int* d_result_buffer_count = nullptr;
    int* d_progress_counter;
    int* d_cutoff;
    int max_results = MAX_RESULTS_BUFFER_SIZE;
    GPU_MALLOC((void**)&d_results, sizeof(UFResult) * max_results);
    GPU_MALLOC((void**)&d_result_buffer_count, sizeof(int));
    GPU_MALLOC((void**)&d_progress_counter, sizeof(int));
    GPU_MALLOC((void**)&d_cutoff, sizeof(int));
    int zero = 0;
    GPU_MEMCPY(d_cutoff, &min_score, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    GPU_MEMCPY(d_progress_counter, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);

    // Progress tracking
    ProgressTracker progress;
    uint64_t total_seeds = (end_batch_u - start_batch_u + 1) * calculate_seeds_per_batch(batch_chars);
    progress_init(&progress, total_seeds, total_batches, batch_chars);

    fprintf(stderr, "$Searching for Perkeo from Soul card + scoring jokers...\n");
    fprintf(stderr, "$Antes: %d-%d\n", start_ante, end_ante);
    fprintf(stderr, "$Joker rolls: %d\n", joker_rolls);
    fprintf(stderr, "$Min score: %d\n", min_score);
    fprintf(stderr, "$Batches: %llu to %llu (total: %llu)\n", 
            (unsigned long long)start_batch_u, (unsigned long long)end_batch_u, (unsigned long long)total_batches);
    fprintf(stderr, "$\n");

    auto start_time = std::chrono::high_resolution_clock::now();

    // Process batches
    uint64_t batches_processed = 0;
    uint64_t total_seeds_processed = 0;

    for (uint64_t batch = start_batch_u; batch <= end_batch_u; batch++) {
        // Reset counters
        GPU_MEMCPY(d_result_buffer_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
        GPU_MEMCPY(d_progress_counter, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);

        // Launch kernel
        int num_blocks = prop.multiProcessorCount * 32;
        int block_size = 256;
        ultimate_filter_kernel<<<num_blocks, block_size>>>(
            batch, batch_chars, d_config,
            d_results, d_result_buffer_count, d_progress_counter, d_cutoff, max_results
        );
        
        GPUError err = GPU_GET_LAST_ERROR();
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "$GPU launch error at batch %llu: %d\n", (unsigned long long)batch, (int)err);
            break;
        }
        
        err = GPU_DEVICE_SYNCHRONIZE();
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "$GPU sync error at batch %llu: %d\n", (unsigned long long)batch, (int)err);
            break;
        }

        // Get results
        int result_count = 0;
        err = GPU_MEMCPY(&result_count, d_result_buffer_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "$GPU memcpy error at batch %llu: %d\n", (unsigned long long)batch, (int)err);
            break;
        }

        // Print results IMMEDIATELY (deduplicate within batch)
        if (result_count > 0) {
            UFResult* h_results = (UFResult*)malloc(sizeof(UFResult) * result_count);
            GPU_MEMCPY(h_results, d_results, sizeof(UFResult) * result_count, GPU_MEMCPY_DEVICE_TO_HOST);
 
            // Deduplicate within batch and print immediately
            for (int i = 0; i < result_count; i++) {
                bool is_duplicate = false;
                for (int j = 0; j < i; j++) {
                    if (strncmp(h_results[i].seed_str, h_results[j].seed_str, 8) == 0) {
                        // Duplicate in this batch - keep higher score
                        if (h_results[i].sum_score > h_results[j].sum_score) {
                            h_results[j].sum_score = h_results[i].sum_score;
                        }
                        is_duplicate = true;
                        break;
                    }
                }
                if (!is_duplicate) {
                    // Print immediately!
                    char out_buf[128];
                    sprintf(out_buf, "|%s,%d\n", h_results[i].seed_str, h_results[i].sum_score);
                    printf("%s", out_buf);
                    fflush(stdout);
                }
            }
 
            free(h_results);
        }

        // Progress update
        batches_processed++;
        uint64_t seeds_in_batch = calculate_seeds_per_batch(batch_chars);
        total_seeds_processed += seeds_in_batch;

        progress_update(&progress, seeds_in_batch, result_count, batch);
    }

    auto end_time = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time);

    fprintf(stderr, "$\n");
    fprintf(stderr, "$Results:\n");
    fprintf(stderr, "$Batches processed: %llu\n", (unsigned long long)batches_processed);
    fprintf(stderr, "$Seeds processed: %llu\n", (unsigned long long)total_seeds_processed);
    fprintf(stderr, "$Time: %lld ms\n", (long long)duration.count());
    if (duration.count() > 0) {
        fprintf(stderr, "$Rate: %.2f M seeds/sec\n", 
                (double)total_seeds_processed / (duration.count() / 1000.0) / 1000000.0);
    }

    // Cleanup
    GPU_FREE(d_config);
    if (d_results) GPU_FREE(d_results);
    if (d_result_buffer_count) GPU_FREE(d_result_buffer_count);
    if (d_progress_counter) GPU_FREE(d_progress_counter);
    if (d_cutoff) GPU_FREE(d_cutoff);

    return 0;
}
