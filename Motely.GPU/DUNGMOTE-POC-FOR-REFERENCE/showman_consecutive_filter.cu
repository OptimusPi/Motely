/**
 * @file showman_consecutive_filter.cu
 * @brief Filter: Showman in slot 0 or 1, then score by consecutive jokers
 * 
 * Requirements:
 * - Ante parameter (1-8)
 * - First OR second shop slot must be Showman (early exit if not)
 * - Score by consecutive jokers (default: InvisibleJoker, configurable)
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
#include "balatro_batch_main.cuh"
#include "balatro_progress.cuh"
#include "balatro_streams.cuh"
#include "balatro_joker_names.cuh"
#include "balatro_filter_main.cuh"

typedef struct {
    int* antes;  // Array of antes to check
    int num_antes;
    int joker_rolls;
    int wanted_joker_id;
    int min_score;
} ShowmanConsecutiveConfig;


// SIMT optimization: __launch_bounds__ for optimal occupancy
// Max threads per block, min blocks per SM (compiler will optimize)
GPU_KERNEL GPU_LAUNCH_BOUNDS(256) void showman_consecutive_kernel(
    uint64_t batch_index,
    int batch_chars,
    ShowmanConsecutiveConfig* config,
    int* result_count,
    int* d_cutoff
) {
    __shared__ int block_cutoff;
    if (threadIdx.x == 0) {
        block_cutoff = atomicAdd(d_cutoff, 0);
        if (block_cutoff < config->min_score) block_cutoff = config->min_score;
    }
    __syncthreads();
    
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    
    int local_cutoff = block_cutoff;
    int cutoff_refresh_counter = 0;
    const int CUTOFF_REFRESH_INTERVAL = 32;
    
    double cached_hash = get_cached_suffix_hash(batch_index, batch_chars);
    char seed_str[9];
    
    // Showman ID (hardcoded)
    int showman_id = ((int)RARITY_UNCOMMON | (J_SHOWMAN & JOKER_INDEX_MASK));
    
    // SIMT: Warp-level variables for reduction
    const int warp_id = threadIdx.x / 32;
    const int lane_id = threadIdx.x % 32;
    __shared__ int warp_result_counts[8];  // Max 8 warps per block (256/32)
    if (lane_id == 0) warp_result_counts[warp_id] = 0;
    __syncwarp();
    
    for (uint64_t local_idx = tid; local_idx < seeds_per_batch; local_idx += stride) {
        if (++cutoff_refresh_counter >= CUTOFF_REFRESH_INTERVAL) {
            cutoff_refresh_counter = 0;
            if (block_cutoff > local_cutoff) local_cutoff = block_cutoff;
            if (threadIdx.x == 0 && (local_idx % (CUTOFF_REFRESH_INTERVAL * blockDim.x)) == 0) {
                __threadfence();
                int global_cutoff = atomicAdd(d_cutoff, 0);
                if (global_cutoff > block_cutoff) block_cutoff = global_cutoff;
            }
            __syncthreads();
        }
        
        double seed_hash = process_seed_in_batch(batch_index, local_idx, batch_chars, cached_hash, seed_str);
        
        // Check each ante
        int best_score = 0;
        int best_ante = 0;
        
        for (int a_idx = 0; a_idx < config->num_antes; a_idx++) {
            int ante = config->antes[a_idx];
            
            // Create streams for shop slot types and jokers
            ShopItemTypeStream type_stream = create_shop_item_type_stream(
                seed_str, 8, seed_hash, ante, DECK_RED,
                false, false,  // tarot merchant/tycoon
                false, false,  // planet merchant/tycoon
                false          // magic trick
            );
            JokerStream js = create_shop_joker_stream(seed_str, 8, seed_hash, ante, STAKE_WHITE);
            
            // Check first TWO shop slots - must find Showman in first OR second joker slot
            int slot0_id = -1, slot1_id = -1;
            bool slot0_is_joker = false, slot1_is_joker = false;
            bool found_showman = false;
            
            // Check slot 0
            ShopSlotType slot0_type = get_next_shop_slot_type(&type_stream);
            if (slot0_type == SLOT_JOKER) {
                Item joker0 = get_next_joker(&js);
                slot0_id = ((int)get_joker_rarity(joker0.type_value) | (joker0.type_value & JOKER_INDEX_MASK));
                slot0_is_joker = true;
                if (slot0_id == showman_id) found_showman = true;
            }
            
            // Check slot 1
            ShopSlotType slot1_type = get_next_shop_slot_type(&type_stream);
            if (slot1_type == SLOT_JOKER) {
                Item joker1 = get_next_joker(&js);
                slot1_id = ((int)get_joker_rarity(joker1.type_value) | (joker1.type_value & JOKER_INDEX_MASK));
                slot1_is_joker = true;
                if (slot1_id == showman_id) found_showman = true;
            }
            
            // SIMT optimization: Use predicate instead of continue to reduce divergence
            // All threads continue loop, but skip scoring if no Showman
            bool should_score = found_showman;
            
            // Base score: 1 if either of first two joker slots is target joker
            int score = 0;
            if (should_score) {
                if (slot0_is_joker && slot0_id == config->wanted_joker_id) score = 1;
                else if (slot1_is_joker && slot1_id == config->wanted_joker_id) score = 1;
                
                // Count consecutive target jokers AFTER first two slots (skip non-joker slots)
                for (int i = 2; i < config->joker_rolls; i++) {
                    ShopSlotType slot_type = get_next_shop_slot_type(&type_stream);
                    if (slot_type != SLOT_JOKER) {
                        break;  // Stop at first non-joker slot
                    }
                    
                    Item joker = get_next_joker(&js);
                    int joker_index = joker.type_value & JOKER_INDEX_MASK;
                    int joker_rarity = get_joker_rarity(joker.type_value);
                    int joker_full_id = ((int)joker_rarity | joker_index);
                    
                    if (joker_full_id == config->wanted_joker_id) {
                        score++;
                    } else {
                        break;  // Stop at first non-matching joker
                    }
                }
                
                // Track best score and ante
                if (score > best_score) {
                    best_score = score;
                    best_ante = ante;
                }
            }
        }
        
        // SIMT: Use predicate instead of continue to reduce divergence
        bool should_output = (best_score >= local_cutoff) && (best_score >= config->min_score);
        
        // SIMT: Warp-level reduction using __ballot_sync
        unsigned int output_mask = __ballot_sync(0xFFFFFFFF, should_output);
        int warp_outputs = __popc(output_mask);
        
        // Print directly (cutoff makes this rare) - only threads that match
        if (should_output) {
            seed_str[8] = '\0';
            printf("%s,%d,%d\n", seed_str, best_score, best_ante);
        }
        
        // SIMT: Only first thread in warp does atomic add (reduces contention)
        if (lane_id == 0 && warp_outputs > 0) {
            atomicAdd(result_count, warp_outputs);
            warp_result_counts[warp_id] += warp_outputs;
        }
        
        // SIMT: Update cutoff only if we have a valid score
        if (should_output && best_score > local_cutoff) {
            local_cutoff = best_score;
            int old_block = block_cutoff;
            while (best_score > old_block) {
                int swapped = atomicCAS(&block_cutoff, old_block, best_score);
                if (swapped == old_block) break;
                old_block = swapped;
            }
            int old_cutoff = atomicAdd(d_cutoff, 0);
            while (best_score > old_cutoff) {
                int swapped = atomicCAS((int*)d_cutoff, old_cutoff, best_score);
                if (swapped == old_cutoff) {
                    __threadfence();
                    break;
                }
                old_cutoff = swapped;
            }
        }
    }
}

static void usage(const char* exe) {
    printf("Usage: %s --start-batch N [--end-batch M] [options]\n", exe);
    printf("\n");
    printf("Filter: First 2 jokers must include Showman or target joker, then score consecutive target jokers\n");
    printf("\n");
    printf("Options:\n");
    printf("  --start-batch N     Start batch (default: 0)\n");
    printf("  --end-batch M       End batch (-1 = all)\n");
    printf("  --batch-chars N     Batch size (default: 4)\n");
    printf("  --antes N,M,...    Antes to check (1-8, comma-separated, default: 2-8)\n");
    printf("  --ante N           Single ante to check (1-8, for backwards compatibility)\n");
    printf("  --joker NAME        Joker to score (default: InvisibleJoker)\n");
    printf("  --joker-rolls N     Number of shop rolls to check (default: 4, max: 4)\n");
    printf("  --min-score N       Minimum score to output (default: 1)\n");
    printf("  --block-size N      Threads per block (default: 256)\n");
    printf("  --blocks-per-sm N   Blocks per SM (default: 32)\n");
    printf("\n");
    printf("Output: SEED,SCORE,ANTE\n");
    printf("  SCORE: Best consecutive group of specified joker\n");
    printf("  ANTE: Ante number where best score was found\n");
}

int main(int argc, char** argv) {
    if (argc < 2) {
        usage(argv[0]);
        return 1;
    }
    
    // Parse batch range using shared utility
    int batch_chars;
    uint64_t start_batch, end_batch, total_batches;
    parse_batch_range(argc, argv, &batch_chars, &start_batch, &end_batch, &total_batches);
    
    // Parse antes (support both --antes and --ante for backwards compatibility)
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
    // Backwards compatibility: --ante (single)
    if (num_antes == 0) {
        const char* ante_arg = get_flag_value(argc, argv, "--ante");
        if (ante_arg) {
            int a = atoi(ante_arg);
            if (a >= 1 && a <= 8) {
                antes[num_antes++] = a;
            }
        }
    }
    // Default: antes 2-8
    if (num_antes == 0) {
        for (int i = 2; i <= 8; i++) {
            antes[num_antes++] = i;
        }
    }
    
    int wanted_joker_id = ((int)RARITY_RARE | (J_INVISIBLE_JOKER & JOKER_INDEX_MASK));  // Default: InvisibleJoker
    const char* joker_arg = get_flag_value(argc, argv, "--joker");
    if (joker_arg) {
        int jid = joker_name_to_id(joker_arg);
        if (jid < 0) {
            fprintf(stderr, "Error: Unknown joker: %s\n", joker_arg);
            return 1;
        }
        wanted_joker_id = jid;
    }
    
    int joker_rolls = 4;  // Default 4 (incredibly rare, max needed is 4)
    const char* rolls_arg = get_flag_value(argc, argv, "--joker-rolls");
    if (rolls_arg) joker_rolls = atoi(rolls_arg);
    if (joker_rolls < 1) joker_rolls = 4;
    if (joker_rolls > 4) joker_rolls = 4;  // Cap at 4
    
    int min_score = 1;
    const char* min_score_arg = get_flag_value(argc, argv, "--min-score");
    if (min_score_arg) min_score = atoi(min_score_arg);
    if (min_score < 1) min_score = 1;
    
    // Initialize GPU config using shared utility
    GPUConfig gpu_config;
    init_gpu_config(&gpu_config, argc, argv);
    
    // SIMT: Ensure block size is multiple of 32 (warp size) for optimal SIMT execution
    if (gpu_config.block_size % 32 != 0) {
        gpu_config.block_size = ((gpu_config.block_size + 31) / 32) * 32;  // Round up to nearest multiple of 32
        fprintf(stderr, "Warning: block_size adjusted to %d (must be multiple of 32 for SIMT)\n", gpu_config.block_size);
        gpu_config.num_blocks = gpu_config.prop.multiProcessorCount * gpu_config.blocks_per_sm;
    }
    
    // Allocate device memory for antes array
    int* d_antes = nullptr;
    GPUError err = GPU_MALLOC((void**)&d_antes, sizeof(int) * num_antes);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MALLOC(d_antes) failed: %d\n", (int)err);
        return 1;
    }
    err = GPU_MEMCPY(d_antes, antes, sizeof(int) * num_antes, GPU_MEMCPY_HOST_TO_DEVICE);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "Error: GPU_MEMCPY(d_antes) failed: %d\n", (int)err);
        return 1;
    }
    
    ShowmanConsecutiveConfig config;
    config.antes = d_antes;
    config.num_antes = num_antes;
    config.joker_rolls = joker_rolls;
    config.wanted_joker_id = wanted_joker_id;
    config.min_score = min_score;
    
    ShowmanConsecutiveConfig* d_config;
    int* d_result_count;
    int* d_cutoff;
    
    GPU_MALLOC((void**)&d_config, sizeof(ShowmanConsecutiveConfig));
    GPU_MALLOC((void**)&d_result_count, sizeof(int));
    GPU_MALLOC((void**)&d_cutoff, sizeof(int));
    
    GPU_MEMCPY(d_config, &config, sizeof(ShowmanConsecutiveConfig), GPU_MEMCPY_HOST_TO_DEVICE);
    GPU_MEMCPY(d_cutoff, &min_score, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    
    int zero = 0;
    GPU_MEMCPY(d_result_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    
    uint64_t num_batches = end_batch - start_batch + 1;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    uint64_t total_seeds = num_batches * seeds_per_batch;
    
    // Print info using shared utility
    print_gpu_info(&gpu_config, start_batch, end_batch, total_batches, total_seeds);
    fprintf(stderr, "Antes: ");
    for (int i = 0; i < num_antes; i++) {
        fprintf(stderr, "%d", antes[i]);
        if (i < num_antes - 1) fprintf(stderr, ",");
    }
    fprintf(stderr, " | Joker rolls: %d (max 4) | Min score: %d\n", joker_rolls, min_score);
    fprintf(stderr, "Filter: First 2 jokers must include Showman or %s, then score consecutive %s\n\n", joker_arg ? joker_arg : "InvisibleJoker", joker_arg ? joker_arg : "InvisibleJoker");
    
    ProgressTracker progress;
    progress_init(&progress, total_seeds, num_batches, batch_chars);
    
    // Process batches using standard chunking pattern
    uint64_t batches_per_chunk = calculate_batches_per_chunk(batch_chars);
    
    for (uint64_t chunk_start = start_batch; 
         chunk_start <= end_batch && chunk_start < total_batches; 
         chunk_start += batches_per_chunk) {
        
        uint64_t chunk_end = chunk_start + batches_per_chunk - 1;
        if (chunk_end > end_batch) chunk_end = end_batch;
        if (chunk_end >= total_batches) chunk_end = total_batches - 1;
        
        // Launch all batches in chunk (async)
        for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
            showman_consecutive_kernel<<<gpu_config.num_blocks, gpu_config.block_size>>>(
                batch, batch_chars, d_config,
                d_result_count, d_cutoff
            );
            cudaError_t err = cudaGetLastError();
            if (err != cudaSuccess) continue;
        }
        
        // Sync once per chunk
        GPUError err = GPU_DEVICE_SYNCHRONIZE();
        if (err != cudaSuccess) {
            fprintf(stderr, "\nGPU sync error: %s\n", GPU_GET_ERROR_STRING(err));
            break;
        }
        
        // Collect results
        for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
            int total_count = 0;
            err = GPU_MEMCPY(&total_count, d_result_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
            if (err == cudaSuccess) {
                progress_update(&progress, seeds_per_batch, total_count, batch);
            }
        }
    }
    
    progress_print_final(&progress);
    
    GPU_FREE(d_config);
    GPU_FREE(d_antes);
    GPU_FREE(d_result_count);
    GPU_FREE(d_cutoff);
    
    return 0;
}
