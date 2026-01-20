/**
 * @file balatro_batch.cuh
 * @brief Batch processing utilities for Motely-compatible seed searching
 *
 * Motely convention used here:
 * - `batch_chars` means the number of LEFTMOST characters that vary within a batch.
 * - The batch index encodes the RIGHTMOST (8 - batch_chars) characters (the suffix).
 * - Global seed index is base-35 for 8-char seeds.
 *
 * So:
 *   seeds_per_batch = 35^batch_chars
 *   total_batches   = 35^(8 - batch_chars)
 *   seed_index      = batch_index + local_index * 35^(8 - batch_chars)
 */

#ifndef BALATRO_BATCH_CUH
#define BALATRO_BATCH_CUH

#include <stdint.h>
#include "balatro_rng.cuh"

__host__ __device__ __forceinline__ uint64_t calculate_seeds_per_batch(int batch_chars) {
    uint64_t result = 1;
    for (int i = 0; i < batch_chars; i++) result *= 35ULL;
    return result;
}

__host__ __device__ __forceinline__ uint64_t calculate_total_batches(int batch_chars) {
    uint64_t result = 1;
    int non_batch_chars = 8 - batch_chars;
    for (int i = 0; i < non_batch_chars; i++) result *= 35ULL;
    return result;
}

__host__ __device__ __forceinline__ uint64_t batch_index_to_start_seed_index(uint64_t batch_index, int /*batch_chars*/) {
    // Batch index encodes rightmost chars, and the varying leftmost chars are 0 at batch start.
    return batch_index;
}

__host__ __device__ __forceinline__ uint64_t seed_index_to_batch_index(uint64_t seed_index, int batch_chars) {
    int suffix_chars = 8 - batch_chars;
    uint64_t suffix_mask = 1;
    for (int i = 0; i < suffix_chars; i++) suffix_mask *= 35ULL;
    return seed_index % suffix_mask;
}

__host__ __forceinline__ void batch_index_to_suffix_host(uint64_t batch_index, int batch_chars, char* out) {
    const char* chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    int suffix_chars = 8 - batch_chars;
    uint64_t temp = batch_index;
    for (int i = suffix_chars - 1; i >= 0; i--) {
        if (temp > 0) {
            out[i] = chars[temp % 35];
            temp /= 35;
        } else {
            out[i] = '1';
        }
    }
    out[suffix_chars] = '\0';
}

__host__ __device__ __forceinline__ uint64_t local_index_to_seed_index(uint64_t batch_index, uint64_t local_index, int batch_chars) {
    int suffix_chars = 8 - batch_chars;
    uint64_t suffix_multiplier = 1;
    for (int i = 0; i < suffix_chars; i++) suffix_multiplier *= 35ULL;
    return batch_index + local_index * suffix_multiplier;
}

__host__ __forceinline__ uint64_t seed_string_to_batch_index_host(const char* seed, int batch_chars) {
    const char* chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    int suffix_chars = 8 - batch_chars;
    uint64_t batch_index = 0;

    for (int i = 8 - suffix_chars; i < 8 && seed[i]; i++) {
        char c = seed[i];
        int digit = -1;
        for (int j = 0; j < 35; j++) {
            if (chars[j] == c) { digit = j; break; }
        }
        if (digit < 0) digit = 0;
        batch_index = batch_index * 35 + (uint64_t)digit;
    }
    return batch_index;
}

#endif // BALATRO_BATCH_CUH


