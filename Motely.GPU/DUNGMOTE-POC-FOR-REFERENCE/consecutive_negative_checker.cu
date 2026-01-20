/**
 * @file consecutive_negative_checker.cu
 * @brief Batch search for consecutive negative jokers with auto cutoff
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"
#include "balatro_rng.cuh"
#include "balatro_streams.cuh"
#include "balatro_enums.cuh"
#include "balatro_batch.cuh"
#include "balatro_batch_kernel.cuh"
#include "balatro_batch_main.cuh"
#include "balatro_progress.cuh"
#include "balatro_args.cuh"

typedef struct {
    int ante;
    int max_slots;
    int min_score;
} ConsecutiveConfig;

GPU_KERNEL void consecutive_negative_kernel(
    uint64_t batch_index,
    int batch_chars,
    ConsecutiveConfig* config,
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
        
        JokerStream js = create_shop_joker_stream(seed_str, 8, seed_hash, config->ante, STAKE_WHITE);
        
        int count = 0;
        for (int slot = 0; slot < config->max_slots; slot++) {
            Item joker = get_next_joker(&js);
            if (joker.edition == EDITION_NEGATIVE) {
                count++;
            } else {
                break;
            }
        }
        
        if (count < local_cutoff) continue;
        if (count < config->min_score) continue;
        
        // Print directly - cutoff makes this rare (once per million seeds)
        seed_str[8] = '\0';
        printf("%s,%d\n", seed_str, count);
        atomicAdd(result_count, 1);
        
        if (count > local_cutoff) {
            local_cutoff = count;
            int old_block = block_cutoff;
            while (count > old_block) {
                int swapped = atomicCAS(&block_cutoff, old_block, count);
                if (swapped == old_block) break;
                old_block = swapped;
            }
            int old_cutoff = atomicAdd(d_cutoff, 0);
            while (count > old_cutoff) {
                int swapped = atomicCAS((int*)d_cutoff, old_cutoff, count);
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
    printf("  --start-batch N     Start batch (default: 0)\n");
    printf("  --end-batch M       End batch (-1 = all)\n");
    printf("  --batch-chars N     Batch size (default: 4)\n");
    printf("  --ante N            Ante to check (default: 2)\n");
    printf("  --max-slots N       Max slots to check (default: 8)\n");
    printf("  --min-score N       Minimum score to output (default: 1)\n");
    printf("  --block-size N      Threads per block (default: 256)\n");
    printf("  --blocks-per-sm N   Blocks per SM (default: 32)\n");
    printf("\nOutput: SEED,SCORE\n");
}

int main(int argc, char** argv) {
    if (argc < 2) {
        usage(argv[0]);
        return 1;
    }
    
    int batch_chars = 4;
    int64_t start_batch_i64 = 0;
    int64_t end_batch_i64 = -1;
    parse_batch_flags(argc, argv, &batch_chars, &start_batch_i64, &end_batch_i64);
    
    if (start_batch_i64 < 0) start_batch_i64 = 0;
    uint64_t start_batch = (uint64_t)start_batch_i64;
    
    uint64_t total_batches = calculate_total_batches(batch_chars);
    uint64_t end_batch = (end_batch_i64 >= 0) ? (uint64_t)end_batch_i64 : (total_batches - 1);
    if (end_batch >= total_batches) end_batch = total_batches - 1;
    
    int ante = 2;
    const char* ante_arg = get_flag_value(argc, argv, "--ante");
    if (ante_arg) ante = atoi(ante_arg);
    if (ante < 1 || ante > 8) ante = 2;
    
    int max_slots = 8;
    const char* slots_arg = get_flag_value(argc, argv, "--max-slots");
    if (slots_arg) max_slots = atoi(slots_arg);
    if (max_slots < 1) max_slots = 8;
    
    int min_score = 1;
    const char* min_score_arg = get_flag_value(argc, argv, "--min-score");
    if (min_score_arg) min_score = atoi(min_score_arg);
    if (min_score < 1) min_score = 1;
    
    int block_size, blocks_per_sm;
    parse_gpu_flags(argc, argv, &block_size, &blocks_per_sm);
    
    int device;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;
    
    ConsecutiveConfig config;
    config.ante = ante;
    config.max_slots = max_slots;
    config.min_score = min_score;
    
    ConsecutiveConfig* d_config;
    int* d_result_count;
    int* d_cutoff;
    
    GPU_MALLOC((void**)&d_config, sizeof(ConsecutiveConfig));
    GPU_MALLOC((void**)&d_result_count, sizeof(int));
    GPU_MALLOC((void**)&d_cutoff, sizeof(int));
    
    GPU_MEMCPY(d_config, &config, sizeof(ConsecutiveConfig), GPU_MEMCPY_HOST_TO_DEVICE);
    GPU_MEMCPY(d_cutoff, &min_score, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    
    int zero = 0;
    GPU_MEMCPY(d_result_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    
    uint64_t num_batches = end_batch - start_batch + 1;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    uint64_t total_seeds = num_batches * seeds_per_batch;
    
    fprintf(stderr, "GPU: %s\n", prop.name);
    fprintf(stderr, "Config: %d blocks x %d threads\n", num_blocks, block_size);
    fprintf(stderr, "Batches: %llu to %llu (total: %llu, seeds: %llu)\n",
        (unsigned long long)start_batch, (unsigned long long)end_batch,
        (unsigned long long)num_batches, (unsigned long long)total_seeds);
    fprintf(stderr, "Ante: %d, Max slots: %d, Min score: %d\n\n", ante, max_slots, min_score);
    
    ProgressTracker progress;
    progress_init(&progress, total_seeds, num_batches, batch_chars);
    
    auto t0 = std::chrono::high_resolution_clock::now();
    
    // STANDARD BATCH PROCESSING PATTERN (from balatro_batch_main.cuh)
    uint64_t batches_per_chunk = calculate_batches_per_chunk(batch_chars);
    
    for (uint64_t chunk_start = start_batch; 
         chunk_start <= end_batch && chunk_start < total_batches; 
         chunk_start += batches_per_chunk) {
        
        uint64_t chunk_end = chunk_start + batches_per_chunk - 1;
        if (chunk_end > end_batch) chunk_end = end_batch;
        if (chunk_end >= total_batches) chunk_end = total_batches - 1;
        
        // Launch all batches in chunk (async, no sync yet)
        for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
            consecutive_negative_kernel<<<num_blocks, block_size>>>(
                batch, batch_chars, d_config,
                d_result_count, d_cutoff
            );
            GPUError err = GPU_GET_LAST_ERROR();
            if (err != GPU_SUCCESS) continue;
        }
        
        // Sync ONCE per chunk (not per batch) - reduces overhead, smoother GPU
        GPUError err = GPU_DEVICE_SYNCHRONIZE();
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "\nGPU sync error: %d\n", (int)err);
            break;
        }
        
        // Update progress for all batches in chunk
        for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
            int total_count = 0;
            err = GPU_MEMCPY(&total_count, d_result_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
            if (err == GPU_SUCCESS) {
                progress_update(&progress, seeds_per_batch, total_count, batch);
            }
        }
    }
    
    progress_print_final(&progress);
    
    GPU_FREE(d_config);
    GPU_FREE(d_result_count);
    GPU_FREE(d_cutoff);
    
    return 0;
}
