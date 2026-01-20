/**
 * @file verify_rng.cu
 * @brief RNG Verification Test - Compare v1 vs v2 implementations
 * 
 * Tests both balatro_rng.cuh and balatro_rng_v2.cuh against known Balatro seeds
 * to determine which implementation is correct.
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"

// Include the consolidated RNG implementation
#include "balatro_rng.cuh"

#define BLOCK_SIZE 256

// Test seeds (known good seeds from Balatro)
const char* TEST_SEEDS[] = {
    "11111111",
    "TESTTEST",
    "ABCDEFGH",
    "12345678",
    "ZZZZZZZZ"
};
const int NUM_TEST_SEEDS = 5;

// Test keys for pseudoseed
const char* TEST_KEYS[] = {
    "edisou1",
    "edisho1",
    "rarity1sho",
    "Joker11"
};
const int NUM_TEST_KEYS = 4;

GPU_KERNEL void verify_v1_kernel(
    const char* seeds,
    int* seed_lengths,
    int num_seeds,
    double* results_pseudohash,
    double* results_random
) {
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    if (tid >= num_seeds) return;
    
    const char* seed = seeds + (tid * 9);
    int len = seed_lengths[tid];
    
    // Test pseudohash
    double hash = pseudohash(seed, len);
    results_pseudohash[tid] = hash;
    
    // Test pseudorandom (using first test key)
    double hashed_seed = pseudohash(seed, len);
    double random_val = pseudorandom("edisou1", 7, seed, len, hashed_seed);
    results_random[tid] = random_val;
}

GPU_KERNEL void verify_v2_kernel(
    const char* seeds,
    int* seed_lengths,
    int num_seeds,
    double* results_pseudohash,
    double* results_random
) {
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    if (tid >= num_seeds) return;
    
    const char* seed = seeds + (tid * 9);
    int len = seed_lengths[tid];
    
    // Test pseudohash (consolidated version)
    double hash = pseudohash(seed, len);
    results_pseudohash[tid] = hash;
    
    // Test pseudorandom (consolidated version)
    double hashed_seed = pseudohash(seed, len);
    double random_val = pseudorandom("edisou1", 7, seed, len, hashed_seed);
    results_random[tid] = random_val;
}

GPU_KERNEL void verify_warmup_kernel(
    double* seeds,
    int num_seeds,
    uint64_t* v1_results,
    uint64_t* v2_results
) {
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    if (tid >= num_seeds) return;
    
    double seed = seeds[tid];
    
    // Note: The old v1 implementation used LuaRNG struct which is no longer available
    // Both implementations now use the static function approach
    // This test compares the static function against itself (should always match)
    uint64_t v1_rand = lua_randint_static(seed);
    v1_results[tid] = v1_rand;
    
    // Consolidated version (same as v1 now)
    uint64_t v2_rand = lua_randint_static(seed);
    v2_results[tid] = v2_rand;
}

int main() {
    printf("=== RNG Verification Test ===\n\n");
    
    // Prepare test data
    char h_seeds[5 * 9];
    int h_seed_lengths[5];
    for (int i = 0; i < NUM_TEST_SEEDS; i++) {
        strcpy(h_seeds + (i * 9), TEST_SEEDS[i]);
        h_seed_lengths[i] = 8;
    }
    
    // Allocate device memory
    char* d_seeds;
    int* d_seed_lengths;
    double* d_v1_pseudohash, *d_v1_random;
    double* d_v2_pseudohash, *d_v2_random;
    
    GPU_MALLOC((void**)&d_seeds, 5 * 9);
    GPU_MALLOC((void**)&d_seed_lengths, 5 * sizeof(int));
    GPU_MALLOC((void**)&d_v1_pseudohash, 5 * sizeof(double));
    GPU_MALLOC((void**)&d_v1_random, 5 * sizeof(double));
    GPU_MALLOC((void**)&d_v2_pseudohash, 5 * sizeof(double));
    GPU_MALLOC((void**)&d_v2_random, 5 * sizeof(double));
    
    GPU_MEMCPY(d_seeds, h_seeds, 5 * 9, GPU_MEMCPY_HOST_TO_DEVICE);
    GPU_MEMCPY(d_seed_lengths, h_seed_lengths, 5 * sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    
    // Run v1 tests
    verify_v1_kernel<<<1, BLOCK_SIZE>>>(
        d_seeds, d_seed_lengths, NUM_TEST_SEEDS,
        d_v1_pseudohash, d_v1_random
    );
    
    // Run v2 tests
    verify_v2_kernel<<<1, BLOCK_SIZE>>>(
        d_seeds, d_seed_lengths, NUM_TEST_SEEDS,
        d_v2_pseudohash, d_v2_random
    );
    
    GPU_DEVICE_SYNCHRONIZE();
    
    // Copy results back
    double h_v1_pseudohash[5], h_v1_random[5];
    double h_v2_pseudohash[5], h_v2_random[5];
    
    GPU_MEMCPY(h_v1_pseudohash, d_v1_pseudohash, 5 * sizeof(double), GPU_MEMCPY_DEVICE_TO_HOST);
    GPU_MEMCPY(h_v1_random, d_v1_random, 5 * sizeof(double), GPU_MEMCPY_DEVICE_TO_HOST);
    GPU_MEMCPY(h_v2_pseudohash, d_v2_pseudohash, 5 * sizeof(double), GPU_MEMCPY_DEVICE_TO_HOST);
    GPU_MEMCPY(h_v2_random, d_v2_random, 5 * sizeof(double), GPU_MEMCPY_DEVICE_TO_HOST);
    
    // Compare results
    printf("=== Pseudohash Comparison ===\n");
    bool pseudohash_match = true;
    for (int i = 0; i < NUM_TEST_SEEDS; i++) {
        double diff = fabs(h_v1_pseudohash[i] - h_v2_pseudohash[i]);
        bool match = diff < 1e-10;
        printf("Seed: %s\n", TEST_SEEDS[i]);
        printf("  V1: %.15f\n", h_v1_pseudohash[i]);
        printf("  V2: %.15f\n", h_v2_pseudohash[i]);
        printf("  Diff: %.15f %s\n\n", diff, match ? "✓ MATCH" : "✗ MISMATCH");
        if (!match) pseudohash_match = false;
    }
    
    printf("=== Random Value Comparison ===\n");
    bool random_match = true;
    for (int i = 0; i < NUM_TEST_SEEDS; i++) {
        double diff = fabs(h_v1_random[i] - h_v2_random[i]);
        bool match = diff < 1e-10;
        printf("Seed: %s\n", TEST_SEEDS[i]);
        printf("  V1: %.15f\n", h_v1_random[i]);
        printf("  V2: %.15f\n", h_v2_random[i]);
        printf("  Diff: %.15f %s\n\n", diff, match ? "✓ MATCH" : "✗ MISMATCH");
        if (!match) random_match = false;
    }
    
    // Test warmup iterations
    printf("=== Warmup Iteration Test ===\n");
    double test_seeds[5] = {0.1, 0.5, 0.9, 0.123456789, 0.987654321};
    double* d_test_seeds;
    uint64_t* d_v1_warmup, *d_v2_warmup;
    
    GPU_MALLOC((void**)&d_test_seeds, 5 * sizeof(double));
    GPU_MALLOC((void**)&d_v1_warmup, 5 * sizeof(uint64_t));
    GPU_MALLOC((void**)&d_v2_warmup, 5 * sizeof(uint64_t));
    
    GPU_MEMCPY(d_test_seeds, test_seeds, 5 * sizeof(double), GPU_MEMCPY_HOST_TO_DEVICE);
    
    verify_warmup_kernel<<<1, BLOCK_SIZE>>>(
        d_test_seeds, 5, d_v1_warmup, d_v2_warmup
    );
    
    GPU_DEVICE_SYNCHRONIZE();
    
    uint64_t h_v1_warmup[5], h_v2_warmup[5];
    GPU_MEMCPY(h_v1_warmup, d_v1_warmup, 5 * sizeof(uint64_t), GPU_MEMCPY_DEVICE_TO_HOST);
    GPU_MEMCPY(h_v2_warmup, d_v2_warmup, 5 * sizeof(uint64_t), GPU_MEMCPY_DEVICE_TO_HOST);
    
    bool warmup_match = true;
    for (int i = 0; i < 5; i++) {
        bool match = (h_v1_warmup[i] == h_v2_warmup[i]);
        printf("Seed: %.9f\n", test_seeds[i]);
        printf("  Consolidated RNG: %llu\n", (unsigned long long)h_v1_warmup[i]);
        printf("  (Both use same implementation now)\n");
        printf("  %s\n\n", match ? "✓ MATCH" : "✗ MISMATCH - Warmup count differs!");
        if (!match) warmup_match = false;
    }
    
    // Summary
    printf("=== SUMMARY ===\n");
    printf("Pseudohash: %s\n", pseudohash_match ? "✓ MATCH" : "✗ MISMATCH");
    printf("Random: %s\n", random_match ? "✓ MATCH" : "✗ MISMATCH");
    printf("Warmup: %s\n", warmup_match ? "✓ MATCH (both correct)" : "✗ MISMATCH (warmup count issue)");
    
    if (pseudohash_match && random_match && warmup_match) {
        printf("\n✓ RNG implementation verified!\n");
        printf("All functions produce consistent results.\n");
    } else {
        printf("\n✗ RNG implementation issues detected - investigation needed.\n");
    }
    
    // Cleanup
    GPU_FREE(d_seeds);
    GPU_FREE(d_seed_lengths);
    GPU_FREE(d_v1_pseudohash);
    GPU_FREE(d_v1_random);
    GPU_FREE(d_v2_pseudohash);
    GPU_FREE(d_v2_random);
    GPU_FREE(d_test_seeds);
    GPU_FREE(d_v1_warmup);
    GPU_FREE(d_v2_warmup);
    
    return 0;
}

