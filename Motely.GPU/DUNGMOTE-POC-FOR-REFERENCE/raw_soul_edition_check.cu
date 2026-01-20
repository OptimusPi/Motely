/**
 * @file raw_soul_edition_check.cu
 * @brief Simplified negative legendary soul joker checker
 *
 * IMPORTANT: Must compile with --fmad=false to match Lua's floating-point precision exactly!
 *   nvcc --fmad=false -O3 -arch=sm_89 -o raw_soul_edition_check.exe raw_soul_edition_check.cu
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <cuda_runtime.h>
#include <chrono>

#include "balatro_enums.cuh"
#include "balatro_rng.cuh"
#include "balatro_args.cuh"
#include "balatro_batch.cuh"
#include "balatro_batch_kernel.cuh"

// Batching configuration (configurable via command-line)

// Simplified check - first roll for ante 1 only
// Uses batch processing for left-to-right iteration (Motely-style)
__global__ void simplified_negative_check_kernel(
    uint64_t batch_index,
    int batch_chars,
    int* match_count
) {
    __shared__ int block_count;
    if (threadIdx.x == 0) block_count = 0;
    __syncthreads();
    
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    
    double cached_hash = get_cached_suffix_hash(batch_index, batch_chars);
    char seed_str[9];
    
    int local_count = 0;

    for (uint64_t local_idx = tid; local_idx < seeds_per_batch; local_idx += stride) {
        double seed_hash = process_seed_in_batch(batch_index, local_idx, batch_chars, cached_hash, seed_str);
        
        // Key: "edisou" + 1
        char key_buf[16];
        int key_len = 0;
        key_buf[key_len++] = 'e'; key_buf[key_len++] = 'd'; key_buf[key_len++] = 'i';
        key_buf[key_len++] = 's'; key_buf[key_len++] = 'o'; key_buf[key_len++] = 'u';
        key_buf[key_len++] = '1';
        key_buf[key_len] = '\0';
        
        // Balatro's pseudoseed for first roll
        double p_seed = compute_pseudoseed(key_buf, key_len, seed_str, 8, seed_hash);
        double edition_roll = lua_random_static(p_seed);
        
        if (edition_roll > 0.997) {
            local_count++;
            seed_str[8] = '\0';
            printf("%s\n", seed_str);
        }
    }
    
    // Reduce thread-local count to block counter
    if (local_count > 0) {
        atomicAdd(&block_count, local_count);
    }
    __syncthreads();
    
    // Reduce block count to global counter
    if (threadIdx.x == 0 && block_count > 0) {
        atomicAdd(match_count, block_count);
    }
}

int main(int argc, char** argv) {
    if (argc < 2) {
        printf("Usage: %s <seed_count> [start_seed] [--batch-chars N] [--block-size N] [--blocks-per-sm N]\n", argv[0]);
        printf("\n");
        printf("Now uses LEFT-TO-RIGHT iteration (Motely-style) via batch processing!\n");
        printf("\n");
        printf("Options:\n");
        printf("  --batch-chars N   - Batch size (default: 4, controls left-to-right iteration)\n");
        printf("  --block-size N    - Threads per block (default: 256)\n");
        printf("  --blocks-per-sm N - Blocks per SM (default: 32)\n");
        return 1;
    }
    
    // Parse GPU flags
    int block_size, blocks_per_sm;
    parse_gpu_flags(argc, argv, &block_size, &blocks_per_sm);
    
    // Parse batch chars
    int batch_chars = 4;
    const char* batch_chars_arg = get_flag_value(argc, argv, "--batch-chars");
    if (batch_chars_arg) batch_chars = atoi(batch_chars_arg);
    if (batch_chars < 1) batch_chars = 4;
    if (batch_chars > 8) batch_chars = 8;
    
    // Parse positional arguments
    const char* seed_arg = get_positional_arg(argc, argv, 0);
    if (!seed_arg) {
        printf("Error: seed_count is required\n");
        return 1;
    }
    
    uint64_t seed_count = strtoull(seed_arg, NULL, 10);
    const char* start_seed = get_positional_arg(argc, argv, 1);
    if (!start_seed) start_seed = "11111111";
    
    int device; cudaGetDevice(&device);
    cudaDeviceProp prop; cudaGetDeviceProperties(&prop, device);
    printf("GPU: %s (SM %d.%d)\n\n", prop.name, prop.major, prop.minor);
    
    printf("Simplified Negative Check (LEFT-TO-RIGHT iteration)\n");
    printf("====================================================\n");
    printf("First roll for ante 1 only\n");
    printf("Print seeds with negative legendary\n");
    printf("Batch chars: %d (left-to-right iteration)\n", batch_chars);
    printf("Searching %llu seeds...\n", (unsigned long long)seed_count);
    
    // Calculate how many batches we need
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    uint64_t num_batches = (seed_count + seeds_per_batch - 1) / seeds_per_batch;  // Ceiling division
    
    // Find starting batch from start_seed
    uint64_t start_batch = seed_string_to_batch_index_host(start_seed, batch_chars);
    
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;
    printf("GPU Config: %d blocks × %d threads = %d total threads\n", 
           num_blocks, block_size, num_blocks * block_size);
    printf("Batches: %llu (starting from batch %llu)\n\n", 
           (unsigned long long)num_batches, (unsigned long long)start_batch);
    
    // Allocate device counter for matches
    int* d_match_count;
    cudaMalloc(&d_match_count, sizeof(int));
    int zero = 0;
    cudaMemcpy(d_match_count, &zero, sizeof(int), cudaMemcpyHostToDevice);
    
    auto start_time = std::chrono::high_resolution_clock::now();
    
    // Process batches (left-to-right iteration)
    for (uint64_t batch = start_batch; batch < start_batch + num_batches; batch++) {
        simplified_negative_check_kernel<<<num_blocks, block_size>>>(
            batch, batch_chars, d_match_count
        );
        cudaError_t err = cudaGetLastError();
        if (err != cudaSuccess) {
            fprintf(stderr, "CUDA error at batch %llu: %s\n", 
                    (unsigned long long)batch, cudaGetErrorString(err));
        }
    }
    
    cudaDeviceSynchronize();
    
    auto end_time = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time);
    
    // Copy match count back
    int match_count;
    cudaMemcpy(&match_count, d_match_count, sizeof(int), cudaMemcpyDeviceToHost);
    cudaFree(d_match_count);
    
    printf("\n=== Done ===\n");
    printf("Seeds searched: %llu\n", (unsigned long long)seed_count);
    printf("Matches found: %d\n", match_count);
    printf("Time: %lld ms, Rate: %.2f seeds/sec\n", (long long)duration.count(), 
           (double)seed_count / (duration.count() / 1000.0));
    
    return 0;
}
