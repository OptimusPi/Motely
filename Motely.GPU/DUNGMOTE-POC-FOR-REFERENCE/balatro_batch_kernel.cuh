/**
 * @file balatro_batch_kernel.cuh
 * @brief Shared batch processing kernel utilities with Motely-style suffix caching
 * 
 * This provides the CORE batch processing loop that all searches should use.
 * No more duplicating the batch iteration, suffix hash computation, or seed iteration!
 */

#ifndef BALATRO_BATCH_KERNEL_CUH
#define BALATRO_BATCH_KERNEL_CUH

#include "balatro_batch.cuh"
#include "balatro_rng.cuh"

/**
 * @brief Compute cached suffix hash for a batch (device function)
 * 
 * This is the CORE function - computes the suffix hash once per batch.
 * All searches should use this instead of duplicating the logic.
 */
GPU_DEVICE __forceinline__ double compute_batch_suffix_hash(
    uint64_t batch_index,
    int batch_chars
) {
    int suffix_chars = 8 - batch_chars;
    char suffix[9];
    const char* chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    uint64_t temp = batch_index;
    
    for (int i = suffix_chars - 1; i >= 0; i--) {
        if (temp > 0) {
            suffix[i] = chars[temp % 35];
            temp /= 35;
        } else {
            suffix[i] = '1';
        }
    }
    suffix[suffix_chars] = '\0';
    
    return pseudohash_prefix(suffix, suffix_chars, 8);
}

/**
 * @brief Get cached suffix hash from shared memory (thread-safe)
 * 
 * Call this at the start of your kernel. Thread 0 computes it, all threads use it.
 */
GPU_DEVICE __forceinline__ double get_cached_suffix_hash(
    uint64_t batch_index,
    int batch_chars
) {
    __shared__ double cached_hash;
    if (threadIdx.x == 0) {
        cached_hash = compute_batch_suffix_hash(batch_index, batch_chars);
    }
    __syncthreads();
    return cached_hash;
}

/**
 * @brief Process a single seed in a batch (core iteration helper)
 * 
 * This is the CORE seed processing function. Use this instead of duplicating:
 * - seed_index calculation
 * - seed string conversion
 * - pseudohash computation with caching
 * 
 * @param batch_index Current batch index
 * @param local_idx Local index within batch (0 to seeds_per_batch-1)
 * @param batch_chars Number of batch characters
 * @param cached_suffix_hash Pre-computed suffix hash (from get_cached_suffix_hash)
 * @param seed_str_out Output buffer for seed string (9 bytes)
 * @return Computed seed hash (pseudohash)
 */
GPU_DEVICE __forceinline__ double process_seed_in_batch(
    uint64_t batch_index,
    uint64_t local_idx,
    int batch_chars,
    double cached_suffix_hash,
    char* seed_str_out
) {
    uint64_t seed_idx = local_index_to_seed_index(batch_index, local_idx, batch_chars);
    seed_index_to_string(seed_idx, seed_str_out);
    
    int suffix_chars = 8 - batch_chars;
    return pseudohash8_with_batch_prefix(cached_suffix_hash, suffix_chars, seed_str_out);
}

/**
 * @brief Standard batch kernel loop structure
 * 
 * Use this pattern in your kernels instead of duplicating the loop:
 * 
 * GPU_KERNEL void my_kernel(uint64_t batch_index, int batch_chars, ...) {
 *     uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
 *     uint64_t stride = gridDim.x * blockDim.x;
 *     uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
 *     
 *     // Get cached suffix hash (shared across all threads in block)
 *     double cached_hash = get_cached_suffix_hash(batch_index, batch_chars);
 *     
 *     char seed_str[9];
 *     for (uint64_t local_idx = tid; local_idx < seeds_per_batch; local_idx += stride) {
 *         double seed_hash = process_seed_in_batch(batch_index, local_idx, batch_chars, cached_hash, seed_str);
 *         
 *         // YOUR SEARCH LOGIC HERE
 *         // ... check jokers, tags, etc. using seed_str and seed_hash
 *     }
 * }
 */
// (This is a documentation comment - the actual implementation is in your kernel)

#endif // BALATRO_BATCH_KERNEL_CUH
