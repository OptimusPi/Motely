#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"
#include <chrono>

#include "balatro_enums.cuh"
#include "balatro_filters.cuh"
#include "balatro_rng.cuh"
#include "balatro_args.cuh"

// Batching configuration (configurable via command-line)

// Simple filter data that can be copied to device
struct SimpleSoulFilter {
    uint32_t allowed_antes;
};

// Soul joker check using the filter system
__device__ bool check_soul_joker_filter(const SimpleSoulFilter* filter, const char* seed_str, double seed_hash, int ante) {
    // Check if ante is allowed
    if (!(filter->allowed_antes & (1 << (ante - 1)))) {
        return false;
    }
    
    // Key: "edisou" + ante
    char key_buf[16];
    int key_len = 0;
    key_buf[key_len++] = 'e'; key_buf[key_len++] = 'd'; key_buf[key_len++] = 'i';
    key_buf[key_len++] = 's'; key_buf[key_len++] = 'o'; key_buf[key_len++] = 'u';
    key_buf[key_len++] = '0' + ante;
    key_buf[key_len] = '\0';
    
    // First roll for ante
    double p_seed = compute_pseudoseed(key_buf, key_len, seed_str, 8, seed_hash);
    double edition_roll = lua_random_static(p_seed);
    
    return (edition_roll > 0.997);
}

GPU_KERNEL void soul_joker_filter_kernel(
    uint64_t start_idx, 
    uint64_t num_seeds,
    const SimpleSoulFilter* filter
) {
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;

    for (uint64_t i = tid; i < num_seeds; i += stride) {
        uint64_t seed_idx = start_idx + i;
        
        char seed_str[9];
        seed_index_to_string(seed_idx, seed_str);
        
        double seed_hash = pseudohash8(seed_str);
        
        for (int ante = 1; ante <= 8; ante++) {
            if (check_soul_joker_filter(filter, seed_str, seed_hash, ante)) {
                printf("NEGATIVE SOUL JOKER: %s ante %d\n", seed_str, ante);
            }
        }
    }
}

int main(int argc, char** argv) {
    if (argc < 2) {
        printf("Usage: %s <seed_count> [start_seed] [--block-size N] [--blocks-per-sm N]\n", argv[0]);
        printf("\n");
        printf("GPU Tuning Flags (optional, can appear anywhere):\n");
        printf("  --block-size N    - Threads per block (default: 256, try: 128, 256, 512)\n");
        printf("  --blocks-per-sm N - Blocks per SM (default: 32, try: 8, 16, 32, 64)\n");
        return 1;
    }
    
    // Parse GPU flags
    int block_size, blocks_per_sm;
    parse_gpu_flags(argc, argv, &block_size, &blocks_per_sm);
    
    // Parse positional arguments
    const char* seed_arg = get_positional_arg(argc, argv, 0);
    if (!seed_arg) {
        printf("Error: seed_count is required\n");
        return 1;
    }
    
    uint64_t seed_count = strtoull(seed_arg, NULL, 10);
    const char* start_seed = get_positional_arg(argc, argv, 1);
    if (!start_seed) start_seed = "11111111";
    
    int device; GPU_GET_DEVICE(&device);
    GPUDeviceProp prop; GPU_GET_DEVICE_PROPERTIES(&prop, device);
    printf("GPU: %s (SM %d.%d)\n\n", prop.name, prop.major, prop.minor);
    
    printf("Soul Joker Filter Search\n");
    printf("========================\n");
    printf("Filter: ANY negative legendary soul joker in antes 1-8\n");
    printf("Searching %llu seeds starting at %s...\n", (unsigned long long)seed_count, start_seed);
    
    // Setup filter for negative soul jokers
    SimpleSoulFilter filter;
    filter.allowed_antes = 0xFF;  // All antes 1-8
    
    // Copy filter to device
    SimpleSoulFilter* d_filter;
    GPU_MALLOC((void**)&d_filter, sizeof(SimpleSoulFilter));
    GPU_MEMCPY(d_filter, &filter, sizeof(SimpleSoulFilter), GPU_MEMCPY_HOST_TO_DEVICE);
    
    uint64_t start_index = seed_string_to_index_host(start_seed);
    
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;
    printf("GPU Config: %d blocks × %d threads = %d total threads\n", 
           num_blocks, block_size, num_blocks * block_size);
    
    auto start_time = std::chrono::high_resolution_clock::now();
    
    soul_joker_filter_kernel<<<num_blocks, block_size>>>(
        start_index, seed_count, d_filter
    );
    
    GPU_DEVICE_SYNCHRONIZE();
    
    auto end_time = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time);
    
    printf("\n=== Results ===\n");
    printf("Seeds searched: %llu\n", (unsigned long long)seed_count);
    printf("Time: %lld ms, Rate: %.2f M seeds/sec\n", (long long)duration.count(), 
           (double)seed_count / (duration.count() / 1000.0) / 1000000.0);
    
    GPU_FREE(d_filter);
    
    return 0;
}
