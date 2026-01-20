/**
 * @file negative_joker_prefilter.cu
 * @brief Negative edition joker finder (any rarity OR specific joker names)
 *
 * Unified negative joker finder that supports:
 * - Finding negative jokers by rarity (any uncommon, any rare, etc.) - original behavior
 * - Finding negative jokers by specific name (e.g., OopsAll6s, Blueprint) - new
 *
 * Uses Motely-style batch processing with suffix caching for performance.
 *
 * IMPORTANT: Must compile with --fmad=false to match Lua's floating-point precision exactly!
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
#include "balatro_batch_main.cuh"  // Shared batch processing pattern
#include "balatro_progress.cuh"
#include "balatro_results.cuh"
#include "balatro_streams.cuh"
#include "balatro_joker_names.cuh"
#include <ctype.h>

struct NegativeJokerResult {
    char seed_str[9];
    int score;  // Total count of negative edition jokers found
};

struct NegativeJokerConfig {
    int joker_ids[4];      // Specific joker IDs to find (if num_jokers > 0)
    int num_jokers;         // 0 = find any negative joker, >0 = find specific jokers
    int filter_rarity;      // -1 = any rarity, otherwise RARITY_COMMON/UNCOMMON/RARE/LEGENDARY
    int* antes;
    int num_antes;
    int joker_rolls;
    bool require_negative_tag;
    int min_slot;
    int max_slot;
    int min_hits;
};

GPU_KERNEL void negative_joker_prefilter_kernel(
    uint64_t batch_index,
    int batch_chars,
    NegativeJokerConfig* config,
    NegativeJokerResult* results,
    int* result_buffer_count,
    int max_results,
    int* result_count,
    int* d_cutoff  // Global dynamic cutoff (atomic)
) {
    // Shared memory cutoff per block (reduces atomic contention)
    __shared__ int block_cutoff;
    if (threadIdx.x == 0) {
        block_cutoff = atomicAdd(d_cutoff, 0);  // Thread 0 reads global cutoff once
        if (block_cutoff < config->min_hits) block_cutoff = config->min_hits;
    }
    __syncthreads();
    
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;
    
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    
    // Local cutoff - start with block cutoff, refresh periodically
    int local_cutoff = block_cutoff;
    int cutoff_refresh_counter = 0;
    const int CUTOFF_REFRESH_INTERVAL = 32;  // Refresh every 32 seeds (warp-aligned)
    
    // Get cached suffix hash (shared across all threads in block) - CORE FUNCTION
    double cached_suffix_hash = get_cached_suffix_hash(batch_index, batch_chars);
    
    int local_matches = 0;
    char seed_str[9];
    
    // Iterate left-to-right within batch (Motely pattern) - CORE LOOP
    for (uint64_t local_idx = tid; local_idx < seeds_per_batch; local_idx += stride) {
        // Periodically refresh local cutoff from shared block cutoff
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
        
        // Count negative edition jokers across all antes
        int total_count = 0;
        int max_possible = config->num_antes * (config->joker_rolls - config->min_slot);
        
        // Early exit: if even max possible can't beat cutoff, skip
        if (max_possible < local_cutoff) {
            continue;
        }
        
        #pragma unroll
        for (int a = 0; a < 8; a++) {
            if (a >= config->num_antes) break;
            int ante = config->antes[a];
            
            // Check if negative tag is required
            if (config->require_negative_tag) {
                char tag_key[8];
                tag_key[0] = 'T'; tag_key[1] = 'a'; tag_key[2] = 'g';
                tag_key[3] = (char)('0' + ante);
                tag_key[4] = '\0';
                
                Tag small_tag = (Tag)pseudorandom_range(tag_key, 4, seed_str, 8, seed_hash, 0, NUM_TAGS);
                if (small_tag != TAG_NEGATIVE) {
                    continue;  // Skip this ante if negative tag required but not present
                }
            }
            
            // Count negative edition jokers in this ante
            JokerStream js = create_shop_joker_stream(seed_str, 8, seed_hash, ante, STAKE_WHITE);
            
            // Create edition stream for this ante
            char edition_key[16];
            edition_key[0] = 'e'; edition_key[1] = 'd'; edition_key[2] = 'i';
            edition_key[3] = 's'; edition_key[4] = 'h'; edition_key[5] = 'o';
            edition_key[6] = (char)('0' + ante);
            edition_key[7] = '\0';
            PrngStream edition_stream = create_prng_stream(
                edition_key, 7, seed_str, 8, seed_hash
            );
            
            int max_check = (config->max_slot < config->joker_rolls) ? config->max_slot : (config->joker_rolls - 1);
            
            // Early exit during counting: if remaining antes can't help, break
            int remaining_antes = config->num_antes - a - 1;
            int max_remaining = remaining_antes * (max_check - config->min_slot + 1);
            if (total_count + max_remaining < local_cutoff) {
                break;
            }
            
            // Advance streams to min_slot
            for (int skip = 0; skip < config->min_slot; skip++) {
                get_next_joker(&js);
                get_next_random(&edition_stream);
            }
            
            // Process slots in range
            for (int slot = config->min_slot; slot <= max_check; slot++) {
                Item joker = get_next_joker(&js);
                int joker_index = joker.type_value & JOKER_INDEX_MASK;
                int joker_rarity = get_joker_rarity(joker.type_value);
                int joker_full_id = ((int)joker_rarity | joker_index);
                
                // Check edition for this slot
                double edition_roll = get_next_random(&edition_stream);
                bool has_negative = (edition_roll > 0.997);
                
                if (!has_negative) continue;
                
                // Filter by rarity if specified
                if (config->filter_rarity >= 0 && joker_rarity != config->filter_rarity) {
                    continue;
                }
                
                // Filter by specific joker names if specified (optimized with unroll)
                if (config->num_jokers > 0) {
                    bool is_wanted = false;
                    if (config->num_jokers >= 1 && joker_full_id == config->joker_ids[0]) is_wanted = true;
                    else if (config->num_jokers >= 2 && joker_full_id == config->joker_ids[1]) is_wanted = true;
                    else if (config->num_jokers >= 3 && joker_full_id == config->joker_ids[2]) is_wanted = true;
                    else if (config->num_jokers >= 4 && joker_full_id == config->joker_ids[3]) is_wanted = true;
                    if (!is_wanted) continue;
                }
                
                // This joker matches our criteria
                total_count++;
            }
        }
        
        // Early exit if can't beat local cutoff
        if (total_count < local_cutoff) {
            continue;
        }
        
        // Only output if count meets minimum threshold
        if (total_count >= config->min_hits) {
            local_matches++;
            int idx = atomicAdd(result_buffer_count, 1);
            if (idx < max_results) {
                // Copy seed
                for (int i = 0; i < 8; i++) {
                    results[idx].seed_str[i] = seed_str[i];
                }
                results[idx].seed_str[8] = '\0';
                results[idx].score = total_count;
            }
            
            // ALWAYS update cutoff if we found a better result
            if (total_count > local_cutoff) {
                local_cutoff = total_count;
                // Update block cutoff (shared memory, fast - CAS loop)
                int old_block = block_cutoff;
                while (total_count > old_block) {
                    int swapped = atomicCAS(&block_cutoff, old_block, total_count);
                    if (swapped == old_block) break;
                    old_block = swapped;
                }
                
                // Update global cutoff (atomic, but less frequent now)
                int old_cutoff = atomicAdd(d_cutoff, 0);
                while (total_count > old_cutoff) {
                    int swapped = atomicCAS((int*)d_cutoff, old_cutoff, total_count);
                    if (swapped == old_cutoff) {
                        __threadfence();  // Memory barrier
                        break;
                    }
                    old_cutoff = swapped;
                }
            }
        }
    }
    
    if (local_matches > 0) atomicAdd(result_count, local_matches);
}

static void usage(const char* exe) {
    printf("Usage: %s --start-batch N [--end-batch M] [--batch-chars N] [options]\n", exe);
    printf("\n");
    printf("Find seeds with negative edition jokers.\n");
    printf("Can filter by rarity OR by specific joker names.\n");
    printf("\n");
    printf("Options:\n");
    printf("  --start-batch N        Start batch index (default: 0)\n");
    printf("  --end-batch M          End batch index (default: -1 = all)\n");
    printf("  --batch-chars N        Batch size in characters (default: 4)\n");
    printf("  --joker NAME           Find specific joker by name (e.g. OopsAll6s, Blueprint)\n");
    printf("  --jokers LIST          Comma-separated joker names\n");
    printf("  --rarity NAME          Filter by rarity: Common, Uncommon, Rare, Legendary\n");
    printf("  --antes LIST           Comma-separated antes to check (default: 2,3,4,5,6)\n");
    printf("  --joker-rolls N        Number of shop joker pulls to scan (default: 8)\n");
    printf("  --require-negative-tag Require negative tag on small blind\n");
    printf("  --min-slot N           Minimum slot to check (default: 0)\n");
    printf("  --max-slot N           Maximum slot to check (default: joker-rolls-1)\n");
    printf("  --min-hits N           Minimum total count to output (default: 1)\n");
    printf("  --block-size N         Threads per block (default: 256)\n");
    printf("  --blocks-per-sm N      Blocks per SM (default: 32)\n");
    printf("\n");
    printf("Examples:\n");
    printf("  %s --start-batch 0 --end-batch 1000 --batch-chars 4 --joker OopsAll6s --min-hits 3\n", exe);
    printf("  %s --start-batch 0 --end-batch 1000 --batch-chars 4 --rarity Uncommon --min-hits 5\n", exe);
    printf("\n");
    printf("Output: SEED,SCORE\n");
    printf("  - SCORE: Total count of negative edition jokers found across all antes\n");
}

// get_flag_value is already defined in balatro_args.cuh

int main(int argc, char** argv) {
    if (argc < 2) {
        usage(argv[0]);
        return 1;
    }
    
    // Parse batch arguments (use proper signed int64 parsing to handle -1)
    int batch_chars = 4;
    int64_t start_batch_i64 = 0;
    int64_t end_batch_i64 = -1;
    parse_batch_flags(argc, argv, &batch_chars, &start_batch_i64, &end_batch_i64);
    
    if (start_batch_i64 < 0) {
        fprintf(stderr, "Error: --start-batch is required and must be >= 0\n");
        usage(argv[0]);
        return 1;
    }
    uint64_t start_batch = (uint64_t)start_batch_i64;
    
    uint64_t total_batches = calculate_total_batches(batch_chars);
    uint64_t end_batch = (end_batch_i64 >= 0) ? (uint64_t)end_batch_i64 : (total_batches - 1);
    if (end_batch >= total_batches) end_batch = total_batches - 1;
    if (end_batch < start_batch) {
        fprintf(stderr, "Error: end_batch < start_batch\n");
        return 1;
    }
    
    // Parse joker filtering (by name OR by rarity)
    int wanted_joker_ids[4] = {-1, -1, -1, -1};
    int num_wanted = 0;
    int filter_rarity = -1;  // -1 = any rarity
    
    const char* joker_arg = get_flag_value(argc, argv, "--joker");
    const char* jokers_arg = get_flag_value(argc, argv, "--jokers");
    const char* rarity_arg = get_flag_value(argc, argv, "--rarity");
    
    if (joker_arg) {
        int jid = joker_name_to_id(joker_arg);
        if (jid < 0) {
            fprintf(stderr, "Error: Unknown joker name: %s\n", joker_arg);
            return 1;
        }
        wanted_joker_ids[num_wanted++] = jid;
    } else if (jokers_arg) {
        char buf[256];
        strncpy(buf, jokers_arg, sizeof(buf) - 1);
        buf[sizeof(buf) - 1] = '\0';
        char* tok = strtok(buf, ",");
        while (tok && num_wanted < 4) {
            // Trim whitespace
            while (*tok == ' ' || *tok == '\t') tok++;
            char* end = tok + strlen(tok) - 1;
            while (end > tok && (*end == ' ' || *end == '\t')) *end-- = '\0';
            
            int jid = joker_name_to_id(tok);
            if (jid >= 0) {
                wanted_joker_ids[num_wanted++] = jid;
            }
            tok = strtok(nullptr, ",");
        }
    }
    
    if (rarity_arg) {
        // Case-insensitive comparison
        char lower[32];
        int i = 0;
        for (; rarity_arg[i] && i < 31; i++) {
            lower[i] = (char)tolower((unsigned char)rarity_arg[i]);
        }
        lower[i] = '\0';
        
        if (strcmp(lower, "common") == 0) filter_rarity = RARITY_COMMON;
        else if (strcmp(lower, "uncommon") == 0) filter_rarity = RARITY_UNCOMMON;
        else if (strcmp(lower, "rare") == 0) filter_rarity = RARITY_RARE;
        else if (strcmp(lower, "legendary") == 0) filter_rarity = RARITY_LEGENDARY;
        else {
            fprintf(stderr, "Error: Unknown rarity: %s (use: Common, Uncommon, Rare, Legendary)\n", rarity_arg);
            return 1;
        }
    }
    
    if (num_wanted == 0 && filter_rarity < 0) {
        // Default: find any negative joker (backward compatible)
        filter_rarity = -1;  // Any rarity
    }
    
    // Parse antes
    int antes[8];
    int num_antes = 0;
    const char* antes_arg = get_flag_value(argc, argv, "--antes");
    if (antes_arg) {
        char buf[256];
        strncpy(buf, antes_arg, sizeof(buf) - 1);
        buf[sizeof(buf) - 1] = '\0';
        char* tok = strtok(buf, ",");
        while (tok && num_antes < 8) {
            int a = atoi(tok);
            if (a >= 1 && a <= 8) {
                antes[num_antes++] = a;
            }
            tok = strtok(nullptr, ",");
        }
    }
    if (num_antes == 0) {
        // Default: antes 2-6
        for (int i = 2; i <= 6; i++) {
            antes[num_antes++] = i;
        }
    }
    
    // Parse other options
    int joker_rolls = 8;
    const char* rolls_arg = get_flag_value(argc, argv, "--joker-rolls");
    if (rolls_arg) joker_rolls = atoi(rolls_arg);
    if (joker_rolls < 1) joker_rolls = 8;
    
    bool require_negative_tag = (get_flag_value(argc, argv, "--require-negative-tag") != NULL);
    
    int min_slot = 0;
    const char* min_slot_arg = get_flag_value(argc, argv, "--min-slot");
    if (min_slot_arg) min_slot = atoi(min_slot_arg);
    if (min_slot < 0) min_slot = 0;
    
    int max_slot = joker_rolls - 1;
    const char* max_slot_arg = get_flag_value(argc, argv, "--max-slot");
    if (max_slot_arg) max_slot = atoi(max_slot_arg);
    if (max_slot >= joker_rolls) max_slot = joker_rolls - 1;
    if (max_slot < min_slot) max_slot = min_slot;
    
    int min_hits = 1;
    const char* min_hits_arg = get_flag_value(argc, argv, "--min-hits");
    if (min_hits_arg) min_hits = atoi(min_hits_arg);
    if (min_hits < 1) min_hits = 1;
    
    // GPU config
    int block_size = 256;
    const char* block_size_arg = get_flag_value(argc, argv, "--block-size");
    if (block_size_arg) block_size = atoi(block_size_arg);
    if (block_size < 32) block_size = 256;
    
    int blocks_per_sm = 32;
    const char* blocks_per_sm_arg = get_flag_value(argc, argv, "--blocks-per-sm");
    if (blocks_per_sm_arg) blocks_per_sm = atoi(blocks_per_sm_arg);
    if (blocks_per_sm < 1) blocks_per_sm = 32;
    
    int device;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;
    
    // Allocate device memory
    NegativeJokerConfig* d_config = nullptr;
    int* d_antes = nullptr;
    NegativeJokerResult* d_results = nullptr;
    int* d_result_buffer_count = nullptr;
    int* d_result_count = nullptr;
    
    GPU_MALLOC((void**)&d_config, sizeof(NegativeJokerConfig));
    GPU_MALLOC((void**)&d_antes, sizeof(int) * num_antes);
    GPU_MEMCPY(d_antes, antes, sizeof(int) * num_antes, GPU_MEMCPY_HOST_TO_DEVICE);
    
    NegativeJokerConfig config;
    config.num_jokers = num_wanted;
    config.filter_rarity = filter_rarity;
    config.num_antes = num_antes;
    config.joker_rolls = joker_rolls;
    config.require_negative_tag = require_negative_tag;
    config.min_slot = min_slot;
    config.max_slot = max_slot;
    config.min_hits = min_hits;
    config.antes = d_antes;
    for (int i = 0; i < 4; i++) {
        config.joker_ids[i] = (i < num_wanted) ? wanted_joker_ids[i] : -1;
    }
    
    GPUError err = GPU_MEMCPY(d_config, &config, sizeof(NegativeJokerConfig), GPU_MEMCPY_HOST_TO_DEVICE);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MEMCPY(d_config) failed: %d\n", (int)err);
        return 1;
    }
    
    int max_results = MAX_RESULTS_BUFFER_SIZE;
    err = GPU_MALLOC((void**)&d_results, sizeof(NegativeJokerResult) * max_results);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MALLOC(d_results) failed: %d\n", (int)err);
        return 1;
    }
    err = GPU_MALLOC((void**)&d_result_buffer_count, sizeof(int));
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MALLOC(d_result_buffer_count) failed: %d\n", (int)err);
        return 1;
    }
    err = GPU_MALLOC((void**)&d_result_count, sizeof(int));
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MALLOC(d_result_count) failed: %d\n", (int)err);
        return 1;
    }
    
    // Allocate and initialize dynamic cutoff
    int* d_cutoff = nullptr;
    err = GPU_MALLOC((void**)&d_cutoff, sizeof(int));
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MALLOC(d_cutoff) failed: %d\n", (int)err);
        return 1;
    }
    err = GPU_MEMCPY(d_cutoff, &min_hits, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MEMCPY(d_cutoff init) failed: %d\n", (int)err);
        return 1;
    }
    
    int zero = 0;
    err = GPU_MEMCPY(d_result_buffer_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MEMCPY(reset buffer) failed: %d\n", (int)err);
        return 1;
    }
    err = GPU_MEMCPY(d_result_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MEMCPY(reset count) failed: %d\n", (int)err);
        return 1;
    }
    
    uint64_t num_batches = end_batch - start_batch + 1;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    uint64_t total_seeds = num_batches * seeds_per_batch;
    
    fprintf(stderr, "GPU: %s\n", prop.name);
    fprintf(stderr, "Config: %d blocks x %d threads\n", num_blocks, block_size);
    fprintf(stderr, "Batch: chars=%d, start=%llu, end=%llu (batches=%llu, seeds=%llu)\n",
        batch_chars,
        (unsigned long long)start_batch,
        (unsigned long long)end_batch,
        (unsigned long long)num_batches,
        (unsigned long long)total_seeds);
    fprintf(stderr, "Negative Joker Finder:\n");
    if (num_wanted > 0) {
        fprintf(stderr, "  Jokers: ");
        for (int i = 0; i < num_wanted; i++) {
            // Simple name lookup for display
            fprintf(stderr, "ID_%d", wanted_joker_ids[i]);
            if (i < num_wanted - 1) fprintf(stderr, ",");
        }
        fprintf(stderr, "\n");
    }
    if (filter_rarity >= 0) {
        const char* rarity_names[] = {"Common", "Uncommon", "Rare", "Legendary"};
        int rarity_idx = (filter_rarity >> 6) & 0b11;  // Extract rarity bits
        if (rarity_idx < 4) {
            fprintf(stderr, "  Rarity: %s\n", rarity_names[rarity_idx]);
        }
    } else if (num_wanted == 0) {
        fprintf(stderr, "  Filter: Any negative joker\n");
    }
    fprintf(stderr, "  Antes: ");
    for (int i = 0; i < num_antes; i++) {
        fprintf(stderr, "%d", antes[i]);
        if (i < num_antes - 1) fprintf(stderr, ",");
    }
    fprintf(stderr, "\n");
    fprintf(stderr, "  Joker rolls: %d, slots: %d-%d, min-hits: %d\n", joker_rolls, min_slot, max_slot, min_hits);
    if (require_negative_tag) fprintf(stderr, "  Require negative tag: YES\n");
    fprintf(stderr, "\n");
    
    ProgressTracker progress;
    progress_init(&progress, total_seeds, num_batches, batch_chars);
    
    auto t0 = std::chrono::high_resolution_clock::now();
    
    // STANDARD BATCH PROCESSING PATTERN (from balatro_batch_main.cuh)
    // Process batches in chunks to reduce sync overhead = smoother GPU utilization
    uint64_t batches_per_chunk = calculate_batches_per_chunk(batch_chars);
    uint64_t total_matches = 0;
    
    for (uint64_t chunk_start = start_batch; 
         chunk_start <= end_batch && chunk_start < calculate_total_batches(batch_chars); 
         chunk_start += batches_per_chunk) {
        
        uint64_t chunk_end = chunk_start + batches_per_chunk - 1;
        if (chunk_end > end_batch) chunk_end = end_batch;
        if (chunk_end >= calculate_total_batches(batch_chars)) {
            chunk_end = calculate_total_batches(batch_chars) - 1;
        }
        
        // Launch all batches in chunk (async, no sync yet)
        for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
            err = GPU_MEMCPY_ASYNC(d_result_buffer_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
            if (err != GPU_SUCCESS) {
                fprintf(stderr, "\nGPU error: GPU_MEMCPY_ASYNC(reset) failed: %d\n", (int)err);
                continue;
            }
            
            negative_joker_prefilter_kernel<<<num_blocks, block_size>>>(
                batch, batch_chars,
                d_config,
                d_results, d_result_buffer_count, max_results,
                d_result_count,
                d_cutoff
            );
            err = GPU_GET_LAST_ERROR();
            if (err != GPU_SUCCESS) {
                fprintf(stderr, "\nGPU launch error at batch %llu: %d\n",
                    (unsigned long long)batch, (int)err);
                continue;
            }
        }
        
        // Sync ONCE per chunk (not per batch) - reduces overhead, smoother GPU
        err = GPU_DEVICE_SYNCHRONIZE();
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "\nGPU sync error: %d\n", (int)err);
            break;
        }
        
        // Collect results from all batches in chunk
        for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
            int buf_count = 0;
            err = GPU_MEMCPY(&buf_count, d_result_buffer_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
            if (err != GPU_SUCCESS) continue;
            
            if (buf_count > 0) {
                NegativeJokerResult* h = (NegativeJokerResult*)malloc(sizeof(NegativeJokerResult) * buf_count);
                GPU_MEMCPY(h, d_results, sizeof(NegativeJokerResult) * buf_count, GPU_MEMCPY_DEVICE_TO_HOST);
                
                for (int i = 0; i < buf_count; i++) {
                    printf("%s,%d\n", h[i].seed_str, h[i].score);
                }
                
                total_matches += buf_count;
                free(h);
            }
            
            int total_count = 0;
            err = GPU_MEMCPY(&total_count, d_result_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
            if (err == GPU_SUCCESS) {
                progress_update(&progress, seeds_per_batch, total_count, batch);
            }
        }
    }
    
    auto t1 = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(t1 - t0);
    
    int final_count = 0;
    GPU_MEMCPY(&final_count, d_result_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
    
    fprintf(stderr, "\n\n=== Results ===\n");
    fprintf(stderr, "Seeds searched: %llu\n", (unsigned long long)total_seeds);
    fprintf(stderr, "Matches found: %d\n", final_count);
    fprintf(stderr, "Time: %lld ms\n", (long long)duration.count());
    if (duration.count() > 0) {
        fprintf(stderr, "Rate: %.2f M seeds/sec\n", 
                (double)total_seeds / (duration.count() / 1000.0) / 1000000.0);
    }
    
    GPU_FREE(d_config);
    GPU_FREE(d_antes);
    GPU_FREE(d_results);
    GPU_FREE(d_result_buffer_count);
    GPU_FREE(d_result_count);
    GPU_FREE(d_cutoff);
    
    return 0;
}
