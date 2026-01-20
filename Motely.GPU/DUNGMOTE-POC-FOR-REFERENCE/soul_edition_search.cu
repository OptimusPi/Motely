#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"
#include <chrono>

#include "balatro_enums.cuh"
#include "balatro_rng.cuh"
#include "balatro_args.cuh"

#define MAX_ANTES 8
#define MAX_ROLLS_PER_ANTE 10

// Kernel parameters
struct KernelParams {
    int antes[MAX_ANTES];
    int antes_count;
    int rolls_per_ante[MAX_ANTES];
    int match_threshold;
};

__global__ void soul_edition_search_kernel(
    uint64_t start_idx,
    uint64_t num_seeds,
    KernelParams params,
    int* match_count
) {
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;
    
    int local_count = 0;

    for (uint64_t i = tid; i < num_seeds; i += stride) {
        uint64_t seed_idx = start_idx + i;
        
        char seed_str[9];
        seed_index_to_string(seed_idx, seed_str);
        
        double seed_hash = pseudohash8_v2(seed_str);
        int total_antes_matched = 0;
        
        for (int a = 0; a < params.antes_count; a++) {
            int ante = params.antes[a];
            int max_rolls = params.rolls_per_ante[a];
            
            // Key: "edisou" + ante
            char key_buf[16];
            int key_len = 0;
            key_buf[key_len++] = 'e'; key_buf[key_len++] = 'd'; key_buf[key_len++] = 'i';
            key_buf[key_len++] = 's'; key_buf[key_len++] = 'o'; key_buf[key_len++] = 'u';
            
            if (ante < 10) {
                key_buf[key_len++] = '0' + ante;
            } else {
                key_buf[key_len++] = '0' + (ante / 10);
                key_buf[key_len++] = '0' + (ante % 10);
            }
            key_buf[key_len] = '\0';
            
            // Balatro's pseudoseed for first roll sets the math.randomseed state.
            // The first math.random() call then returns the first value from that state.
            double p_seed = compute_pseudoseed(key_buf, key_len, seed_str, 8, seed_hash);
            
            bool found_in_ante = false;
            
            // First roll
            double roll = lua_random_static(p_seed);
            if (roll > 0.997) {
                found_in_ante = true;
            } else {
                // Subsequent rolls (if needed)
                double current_pseed = p_seed;
                for (int r = 2; r <= max_rolls; r++) {
                    current_pseed = iterate_prng_state(current_pseed);
                    roll = lua_random_static(current_pseed);
                    if (roll > 0.997) {
                        found_in_ante = true;
                        break;
                    }
                }
            }
            
            if (found_in_ante) {
                total_antes_matched++;
            }
        }
        
        if (total_antes_matched >= params.match_threshold) {
            local_count++;
            printf("%s\n", seed_str);
        }
    }
    
    if (local_count > 0) {
        atomicAdd(match_count, local_count);
    }
}

int main(int argc, char** argv) {
    if (argc < 5) {
        printf("Usage: %s <antes> <rolls_per_ante> <match_threshold> <seed_count> [start_seed] [--block-size N] [--blocks-per-sm N]\n", argv[0]);
        printf("\n");
        printf("Arguments:\n");
        printf("  antes           - Comma-separated list of antes to check (1-8), e.g. \"1,2,3\"\n");
        printf("  rolls_per_ante  - Comma-separated max Soul uses to check per ante, e.g. \"1,1,1\"\n");
        printf("  match_threshold - Minimum number of antes that must have a negative roll\n");
        printf("  seed_count      - Number of seeds to search\n");
        printf("  start_seed      - Starting seed (optional, default: 11111111)\n");
        printf("\n");
        printf("GPU Tuning Flags (optional, can appear anywhere):\n");
        printf("  --block-size N    - Threads per block (default: 256, try: 128, 256, 512)\n");
        printf("  --blocks-per-sm N - Blocks per SM (default: 32, try: 8, 16, 32, 64)\n");
        printf("\n");
        printf("Note: This searcher only checks if a Soul card WOULD give a Negative Joker.\n");
        printf("It does NOT check if the Soul card actually spawns.\n");
        return 1;
    }
    
    // Parse GPU flags
    int block_size, blocks_per_sm;
    parse_gpu_flags(argc, argv, &block_size, &blocks_per_sm);
    
    // Parse positional arguments
    const char* antes_arg = get_positional_arg(argc, argv, 0);
    const char* rolls_arg = get_positional_arg(argc, argv, 1);
    const char* threshold_arg = get_positional_arg(argc, argv, 2);
    const char* seed_arg = get_positional_arg(argc, argv, 3);
    
    if (!antes_arg || !rolls_arg || !threshold_arg || !seed_arg) {
        printf("Error: antes, rolls_per_ante, match_threshold, and seed_count are required\n");
        return 1;
    }
    
    KernelParams params;
    params.antes_count = parse_int_list(antes_arg, params.antes, MAX_ANTES);
    int rolls_count = parse_int_list(rolls_arg, params.rolls_per_ante, MAX_ANTES);
    params.match_threshold = atoi(threshold_arg);
    uint64_t seed_count = strtoull(seed_arg, NULL, 10);
    const char* start_seed = get_positional_arg(argc, argv, 4);
    if (!start_seed) start_seed = "11111111";
    
    // Validate
    if (params.antes_count == 0 || params.antes_count != rolls_count) {
        printf("Error: antes and rolls_per_ante must have the same number of elements\n");
        return 1;
    }
    
    if (params.match_threshold < 1 || params.match_threshold > params.antes_count) {
        printf("Error: match_threshold must be between 1 and %d\n", params.antes_count);
        return 1;
    }
    
    // GPU info
    int device; GPU_GET_DEVICE(&device);
    GPUDeviceProp prop; GPU_GET_DEVICE_PROPERTIES(&prop, device);
    printf("GPU: %s (SM %d.%d)\n\n", prop.name, prop.major, prop.minor);
    
    printf("Negative Soul Joker Search\n");
    printf("==========================\n");
    printf("Antes: ");
    for (int i = 0; i < params.antes_count; i++) {
        printf("%d", params.antes[i]);
        if (i < params.antes_count - 1) printf(",");
    }
    printf("\n");
    printf("Max Soul uses per ante: ");
    for (int i = 0; i < rolls_count; i++) {
        printf("%d", params.rolls_per_ante[i]);
        if (i < rolls_count - 1) printf(",");
    }
    printf("\n");
    printf("Match threshold: %d\n", params.match_threshold);
    printf("Searching %llu seeds starting at %s...\n", (unsigned long long)seed_count, start_seed);
    printf("\n");
    
    uint64_t start_index = seed_string_to_index_host(start_seed);
    
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;
    printf("GPU Config: %d blocks × %d threads = %d total threads\n", 
           num_blocks, block_size, num_blocks * block_size);
    
    // Allocate device memory for counter
    int* d_match_count;
    GPU_MALLOC((void**)&d_match_count, sizeof(int));
    int zero = 0;
    GPU_MEMCPY(d_match_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
    
    auto start_time = std::chrono::high_resolution_clock::now();
    
    soul_edition_search_kernel<<<num_blocks, block_size>>>(
        start_index, seed_count, params, d_match_count
    );
    
    GPU_DEVICE_SYNCHRONIZE();
    
    auto end_time = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time);
    
    // Copy results back
    int match_count;
    GPU_MEMCPY(&match_count, d_match_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
    
    GPU_FREE(d_match_count);
    
    printf("\n=== Done ===\n");
    printf("Seeds searched: %llu\n", (unsigned long long)seed_count);
    printf("Matches found: %d\n", match_count);
    printf("Time: %lld ms, Rate: %.2f seeds/sec\n", (long long)duration.count(),
           (double)seed_count / (duration.count() / 1000.0));
    
    return 0;
}
