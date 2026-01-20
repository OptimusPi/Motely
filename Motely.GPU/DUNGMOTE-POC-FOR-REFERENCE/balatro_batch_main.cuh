/**
 * @file balatro_batch_main.cuh
 * @brief Shared batch processing utilities for all filters
 * 
 * STANDARDIZED batch processing pattern - use this instead of duplicating code!
 */

#ifndef BALATRO_BATCH_MAIN_CUH
#define BALATRO_BATCH_MAIN_CUH

#include "gpu_common.h"
#include "balatro_batch.cuh"
#include "balatro_progress.cuh"

/**
 * @brief Calculate optimal batches per chunk based on batch_chars
 * 
 * Larger chunks = less sync overhead = smoother GPU utilization
 */
GPU_HOST __forceinline__ uint64_t calculate_batches_per_chunk(int batch_chars) {
    if (batch_chars == 1) return 100ULL;
    else if (batch_chars == 2) return 50ULL;
    else if (batch_chars == 3) return 20ULL;
    else if (batch_chars >= 4) return 10ULL;
    return 10ULL;
}

/**
 * @brief Standard batch processing loop pattern
 * 
 * USE THIS PATTERN in all filters:
 * 
 * uint64_t batches_per_chunk = calculate_batches_per_chunk(batch_chars);
 * 
 * for (uint64_t chunk_start = start_batch; chunk_start <= end_batch; chunk_start += batches_per_chunk) {
 *     uint64_t chunk_end = chunk_start + batches_per_chunk - 1;
 *     if (chunk_end > end_batch) chunk_end = end_batch;
 *     
 *     // Launch all batches in chunk (async)
 *     for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
 *         cudaMemcpyAsync(d_result_buffer_count, &zero, sizeof(int), cudaMemcpyHostToDevice);
 *         your_kernel<<<num_blocks, block_size>>>(batch, batch_chars, ...);
 *     }
 *     
 *     // Sync ONCE per chunk (not per batch) - reduces overhead
 *     cudaDeviceSynchronize();
 *     
 *     // Collect results from all batches in chunk
 *     for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
 *         // ... collect and output results
 *     }
 * }
 */

#endif // BALATRO_BATCH_MAIN_CUH
