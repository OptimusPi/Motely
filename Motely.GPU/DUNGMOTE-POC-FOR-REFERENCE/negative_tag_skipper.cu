/**
 * @file negative_tag_skipper.cu
 * @brief Negative Tag (small blind) + consecutive joker grouping search (with “sudden death” forgiveness)
 *
 * This is the Motely-style “batch searcher”:
 * - Search space is partitioned into batches of size 35^batch_chars
 * - Batch index encodes the rightmost (8 - batch_chars) characters (the suffix)
 * - Seeds are enumerated as: seed_index = batch_index + local_index * 35^(8 - batch_chars)
 *
 * Filter:
 * - Small blind tag for the specified ante is TAG_NEGATIVE
 * - Then scan the shop joker stream for `joker_rolls` pulls and compute the best group length:
 *   - Count a run of wanted jokers
 *   - Allow exactly 1 non-wanted “offender”, then enter sudden-death (next offender ends group)
 *
 * Output:
 * - CSV: SEED,HIT_COUNT
 *
 * Notes:
 * - Compiled with `--fmad=false` for Lua-precision compatibility (required for accurate RNG).
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

// DuckDB seed source output (optional - link libduckdb to enable)
#ifdef DUCKDB_AVAILABLE
#include "duckdb_seed_source.h"
#endif

// ============================================================================
// Minimal JAML loader (v1)
// ============================================================================

// YAML parser removed - use command-line arguments instead
// All configuration is now done via --ante, --antes, --min, --joker, --jokers, --joker-rolls, etc.

// Simple trim function for parsing comma-separated lists
static GPU_HOST __forceinline__ void trim_whitespace(char* s) {
    if (!s) return;
    // left
    size_t i = 0;
    while (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n') i++;
    if (i) memmove(s, s + i, strlen(s + i) + 1);
    // right
    size_t n = strlen(s);
    while (n && (s[n - 1] == ' ' || s[n - 1] == '\t' || s[n - 1] == '\r' || s[n - 1] == '\n')) {
        s[n - 1] = '\0';
        n--;
    }
}

GPU_DEVICE __forceinline__ int best_group_sudden_death(
    const char* seed_str,
    double seed_hash,
    int ante,
    int joker_rolls,
    int wanted_joker_id0,
    int wanted_joker_id1,
    int wanted_joker_id2,
    int wanted_joker_id3,
    int num_wanted_jokers,
    bool require_showman
) {
    // Showman is uncommon joker #53
    const int SHOWMAN_ID = ((int)RARITY_UNCOMMON | (J_SHOWMAN & JOKER_INDEX_MASK));
    
    int max_group = 0;
    int cur = 0;
    bool sudden_death = false;

    JokerStream js = create_shop_joker_stream(seed_str, 8, seed_hash, ante, STAKE_WHITE);

    // Check that FIRST joker is Showman (only if required)
    Item first_joker = get_next_joker(&js);
    int first_joker_index = first_joker.type_value & JOKER_INDEX_MASK;
    int first_joker_rarity = get_joker_rarity(first_joker.type_value);
    int first_joker_full_id = ((int)first_joker_rarity | first_joker_index);
    
    if (require_showman && first_joker_full_id != SHOWMAN_ID) {
        return 0;  // First joker is not Showman - fail
    }

    // First joker is Showman (if required), now check if it's also wanted (OopsAll6s)
    bool first_is_wanted =
        (first_joker_full_id == wanted_joker_id0) ||
        (num_wanted_jokers > 1 && first_joker_full_id == wanted_joker_id1) ||
        (num_wanted_jokers > 2 && first_joker_full_id == wanted_joker_id2) ||
        (num_wanted_jokers > 3 && first_joker_full_id == wanted_joker_id3);
    
    if (first_is_wanted) {
        cur++;
    } else {
        sudden_death = true;  // First joker is not wanted (or not Showman if required), enter sudden death
    }

    // Continue scanning remaining jokers
    for (int i = 1; i < joker_rolls; i++) {
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

GPU_KERNEL void negative_tag_skipper_kernel(
    uint64_t batch_index,
    int batch_chars,
    int* antes,
    int num_antes,
    int joker_rolls,
    int wanted_joker_id0,
    int wanted_joker_id1,
    int wanted_joker_id2,
    int wanted_joker_id3,
    int num_wanted_jokers,
    int min_hits,
    bool require_showman_check,
    int* d_cutoff,  // Global dynamic cutoff (atomic)
    PrefilterResult* results,
    int* result_buffer_count,
    int max_results,
    int* result_count
) {
    // Shared memory cutoff per block (reduces atomic contention)
    __shared__ int block_cutoff;
    if (threadIdx.x == 0) {
        block_cutoff = atomicAdd(d_cutoff, 0);  // Thread 0 reads global cutoff once
        if (block_cutoff < min_hits) block_cutoff = min_hits;
    }
    __syncthreads();
    
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;

    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);

    // Local cutoff - start with block cutoff, refresh periodically
    int local_cutoff = block_cutoff;
    int cutoff_refresh_counter = 0;
    const int CUTOFF_REFRESH_INTERVAL = 32;  // Refresh every 32 seeds (warp-aligned)

    int local_matches = 0;
    char seed_str[9];

    // Get cached suffix hash (shared across all threads in block) - CORE FUNCTION
    double cached_suffix_hash = get_cached_suffix_hash(batch_index, batch_chars);

    // Iterate left-to-right within batch (Motely pattern) - CORE LOOP
    for (uint64_t local_idx = tid; local_idx < seeds_per_batch; local_idx += stride) {
        // Periodically refresh local cutoff from shared block cutoff (no atomic needed!)
        if (++cutoff_refresh_counter >= CUTOFF_REFRESH_INTERVAL) {
            cutoff_refresh_counter = 0;
            if (block_cutoff > local_cutoff) {
                local_cutoff = block_cutoff;
            }
            // Thread 0 refreshes block cutoff from global (infrequently)
            if (threadIdx.x == 0 && (local_idx % (CUTOFF_REFRESH_INTERVAL * blockDim.x)) == 0) {
                __threadfence();
                int global_cutoff = atomicAdd(d_cutoff, 0);
                if (global_cutoff > block_cutoff) {
                    block_cutoff = global_cutoff;
                }
            }
            __syncthreads();
        }
        
        // Process seed with cached hash - CORE FUNCTION
        double seed_hash = process_seed_in_batch(batch_index, local_idx, batch_chars, cached_suffix_hash, seed_str);

        // Check each ante for negative tag and joker requirement
        // Sum hits across ALL antes (min_hits is total across all antes)
        // Only FIRST ante requires negative tag; subsequent antes check jokers regardless
        // Showman check only applies to the FIRST ante with negative tag
        int total_hits = 0;
        bool showman_checked = false;
        bool first_ante_has_negative = false;
        
        for (int a = 0; a < num_antes; a++) {
            int ante = antes[a];
            
            // Tag stream key: "Tag" + ante (e.g. "Tag2" for ante 2, "Tag6" for ante 6)
            char tag_key[8];
            tag_key[0] = 'T'; tag_key[1] = 'a'; tag_key[2] = 'g';
            tag_key[3] = (char)('0' + ante); // ante 1..8
            tag_key[4] = '\0';

            Tag small_tag = (Tag)pseudorandom_range(tag_key, 4, seed_str, 8, seed_hash, 0, NUM_TAGS);
            bool has_negative = (small_tag == TAG_NEGATIVE);
            
            // First ante MUST have negative tag
            if (a == 0 && !has_negative) {
                break;  // Fail if first ante doesn't have negative tag
            }
            
            // Only count hits from antes that HAVE negative tags
            if (!has_negative) {
                continue;  // Skip this ante if no negative tag
            }
            
            // Showman check only for first ante with negative tag (if enabled)
            bool require_showman = require_showman_check && !showman_checked && has_negative;
            if (has_negative) showman_checked = true;
            if (a == 0) first_ante_has_negative = true;
            
            int hit = best_group_sudden_death(
                seed_str, seed_hash, ante, joker_rolls,
                wanted_joker_id0, wanted_joker_id1, wanted_joker_id2, wanted_joker_id3,
                num_wanted_jokers,
                require_showman
            );

            total_hits += hit;
        }
        
        // Must have negative tag on first ante
        if (!first_ante_has_negative) {
            continue;  // Skip this seed
        }

        // Early exit if can't beat local cutoff
        if (total_hits < local_cutoff) {
            continue;
        }

        // Check if total hits across all antes meets the threshold
        if (total_hits >= min_hits) {
            local_matches++;
            add_result(results, result_buffer_count, max_results, seed_str, total_hits);
        }
        
        // ALWAYS update cutoff if we found a better result
        if (total_hits > local_cutoff) {
            local_cutoff = total_hits;
            // Update block cutoff (shared memory, fast - CAS loop)
            int old_block = block_cutoff;
            while (total_hits > old_block) {
                int swapped = atomicCAS(&block_cutoff, old_block, total_hits);
                if (swapped == old_block) break;
                old_block = swapped;
            }
            
            // Update global cutoff (atomic, but less frequent now)
            int old_cutoff = atomicAdd(d_cutoff, 0);
            while (total_hits > old_cutoff) {
                int swapped = atomicCAS((int*)d_cutoff, old_cutoff, total_hits);
                if (swapped == old_cutoff) {
                    __threadfence();  // Memory barrier
                    break;
                }
                old_cutoff = swapped;
            }
        }
    }

    if (local_matches > 0) atomicAdd(result_count, local_matches);
}

static void usage(const char* exe) {
    printf("Usage: %s --start-batch N [--end-batch M] [--batch-chars N] --joker NAME [--min-hits N] [--ante N] [--joker-rolls N] [--require-showman] [--output-db PATH] [--block-size N] [--blocks-per-sm N]\n", exe);
    printf("   or: %s [--block-size N] [--blocks-per-sm N]\n", exe);
    printf("\n");
    printf("Options:\n");
    printf("  --require-showman   Require first joker to be Showman (default: disabled)\n");
    printf("  --output-db PATH    Write seeds to DuckDB seed source file (for Motely --seedsource)\n");
    printf("\n");
    printf("Example:\n");
    printf("  %s --start-batch 0 --end-batch 200000 --batch-chars 4 --joker OopsAll6s --min-hits 4 --ante 6 --joker-rolls 100\n", exe);
    printf("  %s --start-batch 0 --end-batch 200000 --batch-chars 4 --joker OopsAll6s --min-hits 4 --antes 2,3,4 --require-showman\n", exe);
    printf("  %s --ante 8 --min 4 --joker Brainstorm --joker-rolls 120 --output-db SeedSources/gpu_candidates.db\n", exe);
    printf("\n");
    printf("DuckDB Seed Source:\n");
    printf("  Use --output-db to create a seed source file that Motely can use:\n");
    printf("    %s --ante 8 --joker Brainstorm --output-db candidates.db\n", exe);
    printf("    dotnet run --project Motely.CLI -- --jaml Filter --seedsource candidates.db\n");
}

int main(int argc, char** argv) {
    if (argc < 2 || get_flag_value(argc, argv, "--help") || get_flag_value(argc, argv, "-h")) {
        usage(argv[0]);
        return (argc < 2) ? 1 : 0;
    }

    // GPU tuning
    int block_size = 256, blocks_per_sm = 32;
    parse_gpu_flags(argc, argv, &block_size, &blocks_per_sm);
    if (block_size <= 0) {
        fprintf(stderr, "Error: --block-size must be > 0\n");
        return 1;
    }
    if (block_size > 1024) {
        fprintf(stderr, "Warning: --block-size %d is > 1024 (invalid). Clamping to 1024.\n", block_size);
        block_size = 1024;
    }

    // Batch flags
    int batch_chars = 4;
    int64_t start_batch_i64 = 0, end_batch_i64 = -1;
    parse_batch_flags(argc, argv, &batch_chars, &start_batch_i64, &end_batch_i64);
    if (start_batch_i64 < 0) start_batch_i64 = 0;
    uint64_t start_batch = (uint64_t)start_batch_i64;

    uint64_t total_batches = calculate_total_batches(batch_chars);
    uint64_t end_batch = (end_batch_i64 >= 0) ? (uint64_t)end_batch_i64 : (total_batches - 1);
    if (end_batch >= total_batches) end_batch = total_batches - 1;
    if (end_batch < start_batch) {
        fprintf(stderr, "Error: end_batch < start_batch\n");
        return 1;
    }

    // Filter args - parse antes (single or multiple)
    int antes[8] = {6};  // Default to ante 6
    int num_antes = 1;
    
    const char* antes_arg = get_flag_value(argc, argv, "--antes");
    const char* ante_arg = get_flag_value(argc, argv, "--ante");
    
    if (antes_arg) {
        // Parse comma-separated list
        char buf[256];
        strncpy(buf, antes_arg, sizeof(buf) - 1);
        buf[sizeof(buf) - 1] = '\0';
        num_antes = 0;
        char* tok = strtok(buf, ",");
        while (tok && num_antes < 8) {
            trim_whitespace(tok);
            int a = atoi(tok);
            if (a >= 1 && a <= 8) {
                antes[num_antes++] = a;
            }
            tok = strtok(nullptr, ",");
        }
        if (num_antes == 0) {
            fprintf(stderr, "Error: --antes must contain at least one valid ante (1..8)\n");
            return 1;
        }
    } else if (ante_arg) {
        int a = atoi(ante_arg);
        if (a >= 1 && a <= 8) {
            antes[0] = a;
            num_antes = 1;
        } else {
            fprintf(stderr, "Error: --ante must be 1..8\n");
            return 1;
        }
    }

    int min_hits = 4;
    const char* min_hits_arg = get_flag_value(argc, argv, "--min-hits");
    if (min_hits_arg) min_hits = atoi(min_hits_arg);
    if (min_hits < 1) min_hits = 1;

    int joker_rolls = 8;
    const char* jr_arg = get_flag_value(argc, argv, "--joker-rolls");
    if (jr_arg) joker_rolls = atoi(jr_arg);
    if (joker_rolls < 1) joker_rolls = 1;

    // Joker selection: --joker NAME or --jokers A,B,C
    int wanted_ids[4] = {-1, -1, -1, -1};
    int num_wanted = 0;

    const char* joker_arg = get_flag_value(argc, argv, "--joker");
    const char* jokers_arg = get_flag_value(argc, argv, "--jokers");
    if (joker_arg) {
        int jid = joker_name_to_id(joker_arg);
        if (jid < 0) {
            fprintf(stderr, "Error: Unknown joker name: %s\n", joker_arg);
            return 1;
        }
        wanted_ids[num_wanted++] = jid;
    } else if (jokers_arg) {
        char buf[256];
        strncpy(buf, jokers_arg, sizeof(buf) - 1);
        buf[sizeof(buf) - 1] = '\0';
        char* tok = strtok(buf, ",");
        while (tok && num_wanted < 4) {
            trim_whitespace(tok);
            int jid = joker_name_to_id(tok);
            if (jid >= 0) wanted_ids[num_wanted++] = jid;
            tok = strtok(nullptr, ",");
        }
    } else {
        fprintf(stderr, "Error: Must specify --joker NAME or --jokers LIST\n");
        usage(argv[0]);
        return 1;
    }

    if (num_wanted <= 0) {
        fprintf(stderr, "Error: No valid jokers specified\n");
        return 1;
    }

    int wanted0 = wanted_ids[0];
    int wanted1 = wanted_ids[1];
    int wanted2 = wanted_ids[2];
    int wanted3 = wanted_ids[3];
    
    // Showman check flag (default: disabled)
    bool require_showman = false;
    const char* showman_arg = get_flag_value(argc, argv, "--require-showman");
    if (showman_arg) {
        require_showman = true;
    }

    // GPU setup
    int device;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;

    PrefilterResult* d_results = nullptr;
    int* d_result_buffer_count = nullptr;
    int* d_result_count = nullptr;
    int max_results = MAX_RESULTS_BUFFER_SIZE;
    GPUError err = allocate_result_buffer(&d_results, &d_result_buffer_count, max_results);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: allocate_result_buffer failed: %d\n", (int)err);
        return 1;
    }
    GPU_MALLOC((void**)&d_result_count, sizeof(int));
    int zero = 0;
    GPU_MEMCPY(d_result_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    
    // Allocate device memory for dynamic cutoff
    int* d_cutoff = nullptr;
    err = GPU_MALLOC((void**)&d_cutoff, sizeof(int));
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MALLOC(d_cutoff) failed: %d\n", (int)err);
        return 1;
    }
    GPU_MEMCPY(d_cutoff, &min_hits, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    
    // Allocate device memory for antes array
    int* d_antes = nullptr;
    err = GPU_MALLOC((void**)&d_antes, sizeof(int) * num_antes);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MALLOC(d_antes) failed: %d\n", (int)err);
        return 1;
    }
    err = GPU_MEMCPY(d_antes, antes, sizeof(int) * num_antes, GPU_MEMCPY_HOST_TO_DEVICE);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MEMCPY(d_antes) failed: %d\n", (int)err);
        return 1;
    }

    uint64_t num_batches = end_batch - start_batch + 1;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    uint64_t total_seeds = num_batches * seeds_per_batch;

    fprintf(stderr, "$GPU: %s\n", prop.name);
    fprintf(stderr, "$Config: %d blocks x %d threads\n", num_blocks, block_size);
    fprintf(stderr, "$Batch: chars=%d, start=%llu, end=%llu (batches=%llu, seeds=%llu)\n",
        batch_chars,
        (unsigned long long)start_batch,
        (unsigned long long)end_batch,
        (unsigned long long)num_batches,
        (unsigned long long)total_seeds);
    fprintf(stderr, "$Filter: antes=");
    for (int i = 0; i < num_antes; i++) {
        fprintf(stderr, "%d", antes[i]);
        if (i < num_antes - 1) fprintf(stderr, ",");
    }
    fprintf(stderr, " small_tag=NEGATIVE, jokers=%d (IDs:", num_wanted);
    for (int i = 0; i < num_wanted; i++) {
        fprintf(stderr, " %d", wanted_ids[i]);
    }
    fprintf(stderr, "), min_hits=%d, rolls=%d", min_hits, joker_rolls);
    if (require_showman) {
        fprintf(stderr, ", require_showman=YES");
    }
    fprintf(stderr, "\n\n");

    // DuckDB seed source output (optional)
    const char* output_db_path = get_flag_value(argc, argv, "--output-db");
#ifdef DUCKDB_AVAILABLE
    DuckDBSeedWriter db_writer = {0};
    bool use_db = (output_db_path != nullptr);
    if (use_db) {
        if (!duckdb_seed_writer_init(&db_writer, output_db_path)) {
            fprintf(stderr, "❌ Error: Failed to initialize DuckDB writer\n");
            return 1;
        }
        fprintf(stderr, "💾 Writing seed source to: %s\n", output_db_path);
    }
#else
    bool use_db = false;
    if (output_db_path) {
        fprintf(stderr, "⚠️  Warning: --output-db specified but DuckDB not linked. Install DuckDB C library and define DUCKDB_AVAILABLE.\n");
        fprintf(stderr, "   Seeds will be written to stdout instead.\n");
    }
#endif

    ProgressTracker progress;
    progress_init(&progress, total_seeds, num_batches, batch_chars);
    
    auto t0 = std::chrono::high_resolution_clock::now();

    // Process one batch at a time - the batch IS the chunk in Motely's system
    for (uint64_t batch = start_batch; batch <= end_batch; batch++) {
        // reset buffer counter
        err = GPU_MEMCPY(d_result_buffer_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "\nGPU error: GPU_MEMCPY(reset result buffer) failed: %d\n", (int)err);
            break;
        }

        negative_tag_skipper_kernel<<<num_blocks, block_size>>>(
            batch, batch_chars,
            d_antes, num_antes, joker_rolls,
            wanted0, wanted1, wanted2, wanted3, num_wanted,
            min_hits,
            require_showman,
            d_cutoff,
            d_results, d_result_buffer_count, max_results,
            d_result_count
        );
        err = GPU_GET_LAST_ERROR();
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "\nGPU launch error at batch %llu: %d\n",
                (unsigned long long)batch, (int)err);
            break;
        }
        err = GPU_DEVICE_SYNCHRONIZE();
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "\nGPU sync error at batch %llu: %d\n",
                (unsigned long long)batch, (int)err);
            break;
        }

        int buf_count = 0;
        err = GPU_MEMCPY(&buf_count, d_result_buffer_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "\nGPU error: GPU_MEMCPY(result count) failed: %d\n", (int)err);
            break;
        }

        if (buf_count > 0) {
            // Avoid gluing to progress line
            fprintf(stderr, "\n");
            fflush(stderr);

            PrefilterResult* h = (PrefilterResult*)malloc(sizeof(PrefilterResult) * buf_count);
            GPU_MEMCPY(h, d_results, sizeof(PrefilterResult) * buf_count, GPU_MEMCPY_DEVICE_TO_HOST);
            for (int i = 0; i < buf_count; i++) {
#ifdef DUCKDB_AVAILABLE
                if (use_db) {
                    // Write to DuckDB seed source
                    duckdb_seed_writer_add(&db_writer, h[i].seed_str);
                } else {
                    // Fallback to stdout CSV
                    printf("|%s,%d\n", h[i].seed_str, h[i].hit_count);
                }
#else
                // Always stdout if DuckDB not available
                printf("|%s,%d\n", h[i].seed_str, h[i].hit_count);  // | prefix for CSV results
#endif
            }
            if (!use_db) {
                fflush(stdout);  // Flush immediately so results appear
            }
            free(h);
        }

        progress_update(&progress, seeds_per_batch, (uint64_t)buf_count, batch);
    }

    auto t1 = std::chrono::high_resolution_clock::now();
    (void)t0; (void)t1;
    progress_print_final(&progress);

#ifdef DUCKDB_AVAILABLE
    if (use_db) {
        int64_t total_seeds = duckdb_seed_writer_close(&db_writer);
        fprintf(stderr, "\n💾 Wrote %lld seeds to seed source: %s\n", (long long)total_seeds, output_db_path);
        fprintf(stderr, "   Use with Motely: --seedsource %s\n", output_db_path);
    }
#endif

    free_result_buffer(d_results, d_result_buffer_count);
    GPU_FREE(d_result_count);
    if (d_antes) GPU_FREE(d_antes);
    if (d_cutoff) GPU_FREE(d_cutoff);
    return 0;
}


