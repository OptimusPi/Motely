/**
 * @file negative_rare_prefilter.cu
 * @brief Negative edition rare joker pre-filter
 *
 * This tests the idea of using negative edition rare jokers from shop slots
 * as a fast pre-filter before doing more expensive seed searches.
 *
 * IMPORTANT: Must compile with --fmad=false to match Lua's floating-point precision exactly!
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"
#include <chrono>

#include "balatro_enums.cuh"
#include "balatro_rng.cuh"
#include "balatro_main.cuh"

// Batching configuration (configurable via command-line)

// Pre-filter check: Count negative rare jokers in shop slots for an ante
GPU_DEVICE int check_negative_rare_prefilter(
    const char *seed_str,
    double seed_hash,
    int ante,
    int checks)
{
    // Create edition stream: "edisho" + ante
    char edition_key_buf[16];
    int edition_key_len = 0;
    edition_key_buf[edition_key_len++] = 'e';
    edition_key_buf[edition_key_len++] = 'd';
    edition_key_buf[edition_key_len++] = 'i';
    edition_key_buf[edition_key_len++] = 's';
    edition_key_buf[edition_key_len++] = 'h';
    edition_key_buf[edition_key_len++] = 'o';

    if (ante < 10) {
        edition_key_buf[edition_key_len++] = '0' + ante;
    } else {
        edition_key_buf[edition_key_len++] = '0' + (ante / 10);
        edition_key_buf[edition_key_len++] = '0' + (ante % 10);
    }
    edition_key_buf[edition_key_len] = '\0';

    PrngStream edition_stream = create_prng_stream(edition_key_buf, edition_key_len, seed_str, 8, seed_hash);

    // Create rarity stream: "rarity" + ante + "sho"
    char rarity_key_buf[16];
    int rarity_key_len = 0;
    rarity_key_buf[rarity_key_len++] = 'r';
    rarity_key_buf[rarity_key_len++] = 'a';
    rarity_key_buf[rarity_key_len++] = 'r';
    rarity_key_buf[rarity_key_len++] = 'i';
    rarity_key_buf[rarity_key_len++] = 't';
    rarity_key_buf[rarity_key_len++] = 'y';

    if (ante < 10) {
        rarity_key_buf[rarity_key_len++] = '0' + ante;
    } else {
        rarity_key_buf[rarity_key_len++] = '0' + (ante / 10);
        rarity_key_buf[rarity_key_len++] = '0' + (ante % 10);
    }

    rarity_key_buf[rarity_key_len++] = 's';
    rarity_key_buf[rarity_key_len++] = 'h';
    rarity_key_buf[rarity_key_len++] = 'o';
    rarity_key_buf[rarity_key_len] = '\0';

    PrngStream rarity_stream = create_prng_stream(rarity_key_buf, rarity_key_len, seed_str, 8, seed_hash);

    int hit_count = 0;

    for (int slot = 0; slot < checks; slot++) {
        double edition_roll = get_next_random(&edition_stream);
        if (edition_roll <= 0.997) {
            get_next_random(&rarity_stream); // sync
            continue;
        }

        double rarity_roll = get_next_random(&rarity_stream);
        if (rarity_roll > 0.95) {
            hit_count++;
        }
    }

    return hit_count;
}

// Kernel: Pre-filter search
GPU_KERNEL void rare_negative_prefilter_kernel(
    uint64_t start_idx,
    uint64_t num_seeds,
    int *antes_to_check,
    int num_antes,
    int *result_count)
{
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;

    // Local accumulator for matches (avoid atomicAdd in hot loop!)
    int local_matches = 0;

    for (uint64_t i = tid; i < num_seeds; i += stride) {
        uint64_t seed_idx = start_idx + i;

        char seed_str[9];
        seed_index_to_string(seed_idx, seed_str);
        double seed_hash = pseudohash8(seed_str);

        int hit_count = 0;
        for (int a = 0; a < num_antes; a++) {
            hit_count += check_negative_rare_prefilter(seed_str, seed_hash, antes_to_check[a], 10);
        }
        
        if (hit_count >= 3) {
            local_matches++;
            printf("%s,%d\n", seed_str, hit_count);
        }
    }
    
    // Single atomicAdd at the end (only if we found matches)
    if (local_matches > 0) {
        atomicAdd(result_count, local_matches);
    }
}

int main(int argc, char **argv) {
    if (argc < 2) {
        printf("Usage: %s <seed_count|all> [antes] [start_seed] [output_file] [--block-size N] [--blocks-per-sm N]\n", argv[0]);
        printf("\n");
        printf("GPU Tuning Flags (optional, can appear anywhere):\n");
        printf("  --block-size N    - Threads per block (default: 256, try: 128, 256, 512)\n");
        printf("  --blocks-per-sm N - Blocks per SM (default: 32, try: 8, 16, 32, 64)\n");
        return 1;
    }
    
    // Parse GPU flags
    int block_size, blocks_per_sm;
    parse_gpu_flags(argc, argv, &block_size, &blocks_per_sm);
    
    // Parse common arguments
    uint64_t seed_count;
    int antes[8];
    int num_antes = 0;
    const char* start_seed;
    const char* output_file;
    parse_common_args(argc, argv, &seed_count, antes, &num_antes, &start_seed, &output_file);

    // Print GPU info
    print_gpu_info(block_size, blocks_per_sm);

    uint64_t start_index = seed_string_to_index_host(start_seed);
    int device;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;

    // Setup CUDA memory
    int *d_antes, *d_result_count;
    setup_cuda_memory(antes, num_antes, &d_antes, &d_result_count);

    if (output_file) freopen(output_file, "w", stdout);
    setvbuf(stdout, NULL, _IONBF, 0);

    auto start_time = std::chrono::high_resolution_clock::now();
    rare_negative_prefilter_kernel<<<num_blocks, block_size>>>(
        start_index, seed_count, d_antes, num_antes, d_result_count);
    GPU_DEVICE_SYNCHRONIZE();
    auto end_time = std::chrono::high_resolution_clock::now();
    
    int result_count;
    GPU_MEMCPY(&result_count, d_result_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time);

    print_results(seed_count, result_count, duration);

    if (output_file) fclose(stdout);
    cleanup_cuda_memory(d_antes, d_result_count);
    return 0;
}
