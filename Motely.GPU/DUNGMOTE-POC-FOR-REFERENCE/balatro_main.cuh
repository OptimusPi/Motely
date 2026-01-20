/**
 * @file balatro_main.cuh
 * @brief Shared main() function utilities for CUDA searchers
 */

#ifndef BALATRO_MAIN_CUH
#define BALATRO_MAIN_CUH

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"
#include <chrono>
#include "balatro_rng.cuh"
#include "balatro_args.cuh"

// Pre-filter main configuration
struct PrefilterConfig {
    uint64_t seed_count;
    int antes[8];
    int num_antes;
    const char* start_seed;
    const char* output_file;
    int block_size;
    int blocks_per_sm;
    int num_blocks;
    uint64_t start_index;
    int* d_antes;
    int* d_result_count;
    char* d_keys;        // Pre-built keys (one per ante)
    int* d_key_lens;     // Key lengths (one per ante)
};

// Host helper to build shop edition key
GPU_HOST int build_shop_edition_key_host(char* key_buf, int ante) {
    key_buf[0] = 'e'; key_buf[1] = 'd'; key_buf[2] = 'i';
    key_buf[3] = 's'; key_buf[4] = 'h'; key_buf[5] = 'o';
    if (ante < 10) {
        key_buf[6] = '0' + ante;
        key_buf[7] = '\0';
        return 7;
    } else {
        key_buf[6] = '0' + (ante / 10);
        key_buf[7] = '0' + (ante % 10);
        key_buf[8] = '\0';
        return 8;
    }
}

/**
 * @brief Common GPU setup - allocates device memory for antes and result counter
 */
GPU_HOST void setup_cuda_memory(
    int* antes, int num_antes,
    int** d_antes, int** d_result_count)
{
    GPU_MALLOC((void**)d_antes, sizeof(int) * num_antes);
    GPU_MALLOC((void**)d_result_count, sizeof(int));
    
    GPU_MEMCPY(*d_antes, antes, sizeof(int) * num_antes, GPU_MEMCPY_HOST_TO_DEVICE);
    int zero = 0;
    GPU_MEMCPY(*d_result_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);
}

/**
 * @brief Common GPU cleanup
 */
GPU_HOST void cleanup_cuda_memory(int* d_antes, int* d_result_count) {
    GPU_FREE(d_antes);
    GPU_FREE(d_result_count);
}

/**
 * @brief Parse common arguments: seed_count, antes, start_seed, output_file
 */
GPU_HOST void parse_common_args(
    int argc, char** argv,
    uint64_t* seed_count,
    int* antes, int* num_antes,
    const char** start_seed,
    const char** output_file)
{
    const char* seed_arg = get_flag_value(argc, argv, "--seed-count");
    if (!seed_arg) {
        printf("Error: --seed-count is required\n");
        exit(1);
    }
    
    if (strcmp(seed_arg, "all") == 0 || strcmp(seed_arg, "ALL") == 0) {
        *seed_count = 1ULL;
        for (int i = 0; i < 8; i++) (*seed_count) *= 35ULL;
    } else {
        *seed_count = strtoull(seed_arg, NULL, 10);
    }

    const char* antes_arg = get_flag_value(argc, argv, "--antes");
    if (antes_arg) {
        *num_antes = parse_int_list(antes_arg, antes, 8);
    }
    if (*num_antes == 0) {
        for (int i = 0; i < 8; i++) antes[i] = i + 1;
        *num_antes = 8;
    }

    const char* start_seed_arg = get_flag_value(argc, argv, "--seed-start");
    *start_seed = start_seed_arg ? start_seed_arg : "11111111";
    const char* output_file_arg = get_flag_value(argc, argv, "--output-file");
    *output_file = output_file_arg;
}

/**
 * @brief Print GPU info and configuration
 */
GPU_HOST void print_gpu_info(int block_size, int blocks_per_sm) {
    int device;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    printf("GPU: %s (SM %d.%d)\n\n", prop.name, prop.major, prop.minor);
    
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;
    printf("GPU Config: %d blocks × %d threads = %d total threads\n", 
           num_blocks, block_size, num_blocks * block_size);
}

/**
 * @brief Print results summary
 */
GPU_HOST void print_results(
    uint64_t seed_count,
    int result_count,
    std::chrono::milliseconds duration)
{
    fprintf(stderr, "\n=== Results ===\n");
    fprintf(stderr, "Seeds searched: %llu\n", (unsigned long long)seed_count);
    fprintf(stderr, "Matches found: %d\n", result_count);
    fprintf(stderr, "Time: %lld ms\n", (long long)duration.count());
    fprintf(stderr, "Rate: %.2f M seeds/sec\n", 
            (double)seed_count / (duration.count() / 1000.0) / 1000000.0);
    if (seed_count > 0) {
        fprintf(stderr, "Filter rate: %.4f%%\n", 
                (double)result_count / seed_count * 100.0);
    }
}

/**
 * @brief Initialize prefilter config from command line
 */
GPU_HOST void init_prefilter_config(int argc, char** argv, PrefilterConfig* config) {
    // Parse GPU flags
    parse_gpu_flags(argc, argv, &config->block_size, &config->blocks_per_sm);
    
    // Parse common arguments
    parse_common_args(argc, argv, &config->seed_count, config->antes, 
                      &config->num_antes, &config->start_seed, &config->output_file);

    // Print GPU info
    print_gpu_info(config->block_size, config->blocks_per_sm);

    // Calculate start index and num blocks
    config->start_index = seed_string_to_index_host(config->start_seed);
    int device;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    config->num_blocks = prop.multiProcessorCount * config->blocks_per_sm;

    // Setup GPU memory
    setup_cuda_memory(config->antes, config->num_antes, 
                      &config->d_antes, &config->d_result_count);

    // Build keys ONCE on host, then copy to device
    char host_keys[8][16];
    int host_key_lens[8];
    for (int i = 0; i < config->num_antes; i++) {
        host_key_lens[i] = build_shop_edition_key_host(host_keys[i], config->antes[i]);
    }
    
    // Allocate and copy keys to device
    GPU_MALLOC((void**)&config->d_keys, 16 * config->num_antes);
    GPU_MALLOC((void**)&config->d_key_lens, sizeof(int) * config->num_antes);
    GPU_MEMCPY(config->d_keys, host_keys, 16 * config->num_antes, GPU_MEMCPY_HOST_TO_DEVICE);
    GPU_MEMCPY(config->d_key_lens, host_key_lens, sizeof(int) * config->num_antes, GPU_MEMCPY_HOST_TO_DEVICE);

    if (config->output_file) freopen(config->output_file, "w", stdout);
    setvbuf(stdout, NULL, _IONBF, 0);
}

/**
 * @brief Cleanup prefilter config
 */
GPU_HOST void cleanup_prefilter_config(PrefilterConfig* config) {
    if (config->output_file) fclose(stdout);
    cleanup_cuda_memory(config->d_antes, config->d_result_count);
    GPU_FREE(config->d_keys);
    GPU_FREE(config->d_key_lens);
}

/**
 * @brief Generic prefilter main() - handles all boilerplate
 * 
 * Usage: Just define your kernel, then call RUN_PREFILTER(kernel_name)
 * Kernel signature: (start_idx, num_seeds, antes, num_antes, keys, key_lens, result_count)
 */
#define RUN_PREFILTER(kernel_name) \
int main(int argc, char **argv) { \
    if (argc < 2) { \
        printf("Usage: %s --seed-count <value|all> [--antes 1,2,3] [--seed-start 11111111] [--output-file file] [--block-size N] [--blocks-per-sm N]\n", argv[0]); \
        printf("Example: %s --seed-count 1000000 --antes 1,2,3,4 --seed-start 22222222 --output-file results.txt\n", argv[0]); \
        return 1; \
    } \
    PrefilterConfig config; \
    init_prefilter_config(argc, argv, &config); \
    auto start_time = std::chrono::high_resolution_clock::now(); \
    kernel_name<<<config.num_blocks, config.block_size>>>( \
        config.start_index, config.seed_count, config.d_antes, config.num_antes, \
        config.d_keys, config.d_key_lens, config.d_result_count); \
    GPU_DEVICE_SYNCHRONIZE(); \
    auto end_time = std::chrono::high_resolution_clock::now(); \
    int result_count; \
    GPU_MEMCPY(&result_count, config.d_result_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST); \
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time); \
    print_results(config.seed_count, result_count, duration); \
    cleanup_prefilter_config(&config); \
    return 0; \
}

#endif // BALATRO_MAIN_CUH

