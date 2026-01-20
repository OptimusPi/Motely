/**
 * @file balatro_filter_main.cuh
 * @brief Shared main() function utilities for all filters
 * 
 * ELIMINATES CODE DUPLICATION across all filter files!
 * 
 * Common patterns extracted:
 * - Batch flag parsing
 * - GPU setup and configuration
 * - Device memory allocation patterns
 * - Batch processing loops with chunking
 * - Progress tracking
 * - Error handling
 */

#ifndef BALATRO_FILTER_MAIN_CUH
#define BALATRO_FILTER_MAIN_CUH

#include "gpu_common.h"
#include <stdint.h>
#include <chrono>
#include "balatro_args.cuh"
#include "balatro_batch.cuh"
#include "balatro_batch_main.cuh"
#include "balatro_progress.cuh"

/**
 * @brief GPU configuration structure
 */
typedef struct {
    int device;
    GPUDeviceProp prop;
    int num_blocks;
    int block_size;
    int blocks_per_sm;
} GPUConfig;

/**
 * @brief Initialize GPU configuration
 * 
 * @param config Output GPU configuration
 * @param argc Command-line argument count
 * @param argv Command-line arguments
 */
GPU_HOST void init_gpu_config(GPUConfig* config, int argc, char** argv) {
    parse_gpu_flags(argc, argv, &config->block_size, &config->blocks_per_sm);
    
    GPU_GET_DEVICE(&config->device);
    GPU_GET_DEVICE_PROPERTIES(&config->prop, config->device);
    config->num_blocks = config->prop.multiProcessorCount * config->blocks_per_sm;
}

/**
 * @brief Print GPU configuration info
 */
__host__ void print_gpu_info(const GPUConfig* config, uint64_t start_batch, uint64_t end_batch, 
                             uint64_t total_batches, uint64_t total_seeds) {
    fprintf(stderr, "GPU: %s\n", config->prop.name);
    fprintf(stderr, "Config: %d blocks x %d threads\n", config->num_blocks, config->block_size);
    fprintf(stderr, "Batches: %llu to %llu (total: %llu, seeds: %llu)\n",
        (unsigned long long)start_batch, (unsigned long long)end_batch,
        (unsigned long long)total_batches, (unsigned long long)total_seeds);
}

/**
 * @brief Standard batch processing loop with chunking
 * 
 * USE THIS MACRO in your filter's main() function:
 * 
 * PROCESS_BATCHES_CHUNKED(start_batch, end_batch, batch_chars, num_blocks, block_size,
 *     [kernel launch code],
 *     [result collection code],
 *     &progress, seeds_per_batch);
 * 
 * Example:
 * PROCESS_BATCHES_CHUNKED(start_batch, end_batch, batch_chars, config.num_blocks, config.block_size,
 *     {
 *         my_kernel<<<num_blocks, block_size>>>(batch, batch_chars, d_config, ...);
 *     },
 *     {
 *         int count = 0;
 *         cudaMemcpy(&count, d_result_count, sizeof(int), cudaMemcpyDeviceToHost);
 *         progress_update(&progress, seeds_per_batch, count, batch);
 *     },
 *     &progress, seeds_per_batch);
 */
#define PROCESS_BATCHES_CHUNKED(start_batch, end_batch, batch_chars, num_blocks, block_size, \
                                KERNEL_LAUNCH, RESULT_COLLECT, progress_ptr, seeds_per_batch) \
    do { \
        uint64_t batches_per_chunk = calculate_batches_per_chunk(batch_chars); \
        uint64_t total_batches = calculate_total_batches(batch_chars); \
        ProgressTracker* _progress = (progress_ptr); \
        uint64_t _seeds_per_batch = (seeds_per_batch); \
        \
        for (uint64_t chunk_start = (start_batch); \
             chunk_start <= (end_batch) && chunk_start < total_batches; \
             chunk_start += batches_per_chunk) { \
            \
            uint64_t chunk_end = chunk_start + batches_per_chunk - 1; \
            if (chunk_end > (end_batch)) chunk_end = (end_batch); \
            if (chunk_end >= total_batches) chunk_end = total_batches - 1; \
            \
            /* Launch all batches in chunk (async, no sync yet) */ \
            for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) { \
                KERNEL_LAUNCH; \
                cudaError_t _err = cudaGetLastError(); \
                if (_err != cudaSuccess) { \
                    fprintf(stderr, "\nCUDA launch error at batch %llu: %s\n", \
                        (unsigned long long)batch, cudaGetErrorString(_err)); \
                    continue; \
                } \
            } \
            \
            /* Sync ONCE per chunk */ \
            cudaError_t _err = cudaDeviceSynchronize(); \
            if (_err != cudaSuccess) { \
                fprintf(stderr, "\nCUDA sync error: %s\n", cudaGetErrorString(_err)); \
                break; \
            } \
            \
            /* Collect results from all batches in chunk */ \
            for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) { \
                RESULT_COLLECT; \
            } \
        } \
    } while(0)

/**
 * @brief Parse batch flags and calculate batch ranges
 * 
 * @param argc Argument count
 * @param argv Arguments
 * @param batch_chars Output: batch character count
 * @param start_batch Output: starting batch index
 * @param end_batch Output: ending batch index
 * @param total_batches Output: total batches available
 */
__host__ void parse_batch_range(int argc, char** argv, 
                                int* batch_chars,
                                uint64_t* start_batch,
                                uint64_t* end_batch,
                                uint64_t* total_batches) {
    int64_t start_batch_i64 = 0;
    int64_t end_batch_i64 = -1;
    parse_batch_flags(argc, argv, batch_chars, &start_batch_i64, &end_batch_i64);
    
    if (start_batch_i64 < 0) start_batch_i64 = 0;
    *start_batch = (uint64_t)start_batch_i64;
    
    *total_batches = calculate_total_batches(*batch_chars);
    *end_batch = (end_batch_i64 >= 0) ? (uint64_t)end_batch_i64 : (*total_batches - 1);
    if (*end_batch >= *total_batches) *end_batch = *total_batches - 1;
}

#endif // BALATRO_FILTER_MAIN_CUH
