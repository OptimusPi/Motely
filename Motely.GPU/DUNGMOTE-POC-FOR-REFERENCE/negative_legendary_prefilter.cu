/**
 * @file negative_legendary_prefilter.cu
 * @brief Negative edition legendary joker pre-filter (BATCH MODE)
 *
 * Batch mode (Motely pattern):
 * - Use --batch-chars N to choose batch size (35^N seeds per batch)
 * - Use --start-batch / --end-batch to sweep batches
 * - Use --end-batch -1 to sweep all batches (all seeds)
 *
 * Output:
 * - Result lines: |SEED,HIT_COUNT
 *
 * IMPORTANT: Must compile with --fmad=false to match Lua precision exactly.
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"
#include <chrono>

#include "balatro_args.cuh"
#include "balatro_batch.cuh"
#include "balatro_batch_kernel.cuh"
#include "balatro_enums.cuh"
#include "balatro_progress.cuh"
#include "balatro_results.cuh"
#include "balatro_rng.cuh"

GPU_DEVICE __forceinline__ int check_negative_legendary_prefilter_hits(
    const char* seed_str,
    double seed_hash,
    int ante,
    int rolls_per_ante
) {
    // "edisou" + ante (supports 1-99)
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

    PrngStream edition_stream = create_prng_stream(key_buf, key_len, seed_str, 8, seed_hash);
    int hits = 0;
    int rolls = rolls_per_ante;
    if (rolls < 1) rolls = 1;
    for (int i = 0; i < rolls; i++) {
        if (get_next_random(&edition_stream) > 0.997) hits++;
    }
    return hits;
}

GPU_KERNEL void negative_legendary_prefilter_kernel(
    uint64_t batch_index,
    int batch_chars,
    const int* d_antes,
    int num_antes,
    int min_hits,
    int rolls_per_ante,
    PrefilterResult* d_results,
    int* d_result_count,
    int max_results
) {
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);

    double cached_suffix_hash = get_cached_suffix_hash(batch_index, batch_chars);

    char seed_str[9];
    for (uint64_t local_idx = tid; local_idx < seeds_per_batch; local_idx += stride) {
        double seed_hash = process_seed_in_batch(batch_index, local_idx, batch_chars, cached_suffix_hash, seed_str);

        int hit_count = 0;
        for (int a = 0; a < num_antes; a++) {
            hit_count += check_negative_legendary_prefilter_hits(seed_str, seed_hash, d_antes[a], rolls_per_ante);
        }

        if (hit_count >= min_hits) {
            add_result(d_results, d_result_count, max_results, seed_str, hit_count);
        }
    }
}

int main(int argc, char** argv) {
    int batch_chars = 4;
    int64_t start_batch = 0;
    int64_t end_batch = -1;
    int min_hits = 1;
    int rolls_per_ante = 3;
    const char* antes_str = NULL;
    const char* output_file = "DungOopsSkipper.csv";

    // flags
    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--batch-chars") == 0 && i + 1 < argc) batch_chars = atoi(argv[++i]);
        else if (strcmp(argv[i], "--start-batch") == 0 && i + 1 < argc) start_batch = strtoll(argv[++i], NULL, 10);
        else if (strcmp(argv[i], "--end-batch") == 0 && i + 1 < argc) end_batch = strtoll(argv[++i], NULL, 10);
        else if (strcmp(argv[i], "--antes") == 0 && i + 1 < argc) antes_str = argv[++i];
        else if (strcmp(argv[i], "--min-hits") == 0 && i + 1 < argc) min_hits = atoi(argv[++i]);
        else if (strcmp(argv[i], "--rolls-per-ante") == 0 && i + 1 < argc) rolls_per_ante = atoi(argv[++i]);
        else if (strcmp(argv[i], "--output-file") == 0 && i + 1 < argc) output_file = argv[++i];
        else if (strcmp(argv[i], "--help") == 0) {
            printf("Usage: %s --antes LIST [options]\n", argv[0]);
            printf("Required:\n");
            printf("  --antes LIST        Comma-separated antes to check (e.g. 2,10)\n");
            printf("Options:\n");
            printf("  --min-hits N        Only print seeds with >= N hits (default: 1)\n");
            printf("  --rolls-per-ante N  Rolls per ante (default: 3)\n");
            printf("  --batch-chars N     Batch chars (default: 4)\n");
            printf("  --start-batch N     Start batch (default: 0)\n");
            printf("  --end-batch N       End batch (default: -1 = all)\n");
            printf("  --output-file PATH  Output file (default: DungOopsSkipper.csv)\n");
            printf("  --block-size N      Threads per block (default: 256)\n");
            printf("  --blocks-per-sm N   Blocks per SM (default: 32)\n");
            return 0;
        }
    }

    if (!antes_str) {
        fprintf(stderr, "$Error: --antes required\n");
        return 1;
    }
    if (min_hits < 1) min_hits = 1;
    if (rolls_per_ante < 1) rolls_per_ante = 1;
    if (batch_chars < 1) batch_chars = 1;
    if (batch_chars > 8) batch_chars = 8;

    // Parse GPU flags
    int block_size, blocks_per_sm;
    parse_gpu_flags(argc, argv, &block_size, &blocks_per_sm);

    // Parse antes
    int antes[32];
    int num_antes = parse_int_list(antes_str, antes, 32);
    if (num_antes <= 0) {
        fprintf(stderr, "$Error: failed to parse --antes\n");
        return 1;
    }
    for (int i = 0; i < num_antes; i++) {
        if (antes[i] < 1 || antes[i] > 99) {
            fprintf(stderr, "$Error: Ante %d out of range (1-99)\n", antes[i]);
            return 1;
        }
    }

    // Batch bounds
    uint64_t total_batches = calculate_total_batches(batch_chars);
    uint64_t start_batch_u = (uint64_t)start_batch;
    uint64_t end_batch_u = (end_batch >= 0) ? (uint64_t)end_batch : (total_batches - 1);
    if (end_batch_u >= total_batches) end_batch_u = total_batches - 1;
    if (end_batch_u < start_batch_u) {
        fprintf(stderr, "$Error: end_batch < start_batch\n");
        return 1;
    }

    // GPU info
    int device = 0;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    fprintf(stderr, "$GPU: %s (SM %d.%d)\n", prop.name, prop.major, prop.minor);
    fprintf(stderr, "$Antes: %s\n", antes_str);
    fprintf(stderr, "$Min hits: %d\n", min_hits);
    fprintf(stderr, "$Rolls per ante: %d\n", rolls_per_ante);
    fprintf(stderr, "$Batch chars: %d\n", batch_chars);
    fprintf(stderr, "$Batches: %llu to %llu (total: %llu)\n",
            (unsigned long long)start_batch_u, (unsigned long long)end_batch_u, (unsigned long long)total_batches);

    // Open output file
    FILE* out = fopen(output_file, "w");
    if (!out) {
        fprintf(stderr, "$Error: failed to open output file: %s\n", output_file);
        return 1;
    }

    // Device allocations
    int* d_antes = nullptr;
    GPU_MALLOC((void**)&d_antes, sizeof(int) * num_antes);
    GPU_MEMCPY(d_antes, antes, sizeof(int) * num_antes, GPU_MEMCPY_HOST_TO_DEVICE);

    PrefilterResult* d_results = nullptr;
    int* d_result_count = nullptr;
    int max_results = MAX_RESULTS_BUFFER_SIZE;
    GPUError err = allocate_result_buffer(&d_results, &d_result_count, max_results);
    if (err != GPU_SUCCESS) {
        fprintf(stderr, "$Error: allocate_result_buffer failed: %d\n", (int)err);
        return 1;
    }

    // Progress
    ProgressTracker progress;
    uint64_t total_seeds = (end_batch_u - start_batch_u + 1) * calculate_seeds_per_batch(batch_chars);
    progress_init(&progress, total_seeds, total_batches, batch_chars);

    int num_blocks = prop.multiProcessorCount * blocks_per_sm;
    fprintf(stderr, "$Kernel config: %d blocks × %d threads\n", num_blocks, block_size);

    int zero = 0;
    auto start_time = std::chrono::high_resolution_clock::now();

    for (uint64_t batch = start_batch_u; batch <= end_batch_u; batch++) {
        GPU_MEMCPY(d_result_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE);

        negative_legendary_prefilter_kernel<<<num_blocks, block_size>>>(
            batch, batch_chars,
            d_antes, num_antes, min_hits, rolls_per_ante,
            d_results, d_result_count, max_results
        );
        GPU_DEVICE_SYNCHRONIZE();

        int result_count = 0;
        GPU_MEMCPY(&result_count, d_result_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);

        if (result_count > 0) {
            PrefilterResult* h = (PrefilterResult*)malloc(sizeof(PrefilterResult) * result_count);
            GPU_MEMCPY(h, d_results, sizeof(PrefilterResult) * result_count, GPU_MEMCPY_DEVICE_TO_HOST);
            
            // Write results to file (no | prefix, just SEED,SCORE)
            for (int i = 0; i < result_count; i++) {
                // Ensure seed string is null-terminated and valid
                h[i].seed_str[8] = '\0';
                // Validate seed string (should be 8 chars, all valid seed chars)
                bool valid = true;
                for (int j = 0; j < 8; j++) {
                    if (h[i].seed_str[j] == '\0' || h[i].seed_str[j] < '1' || h[i].seed_str[j] > 'Z') {
                        valid = false;
                        break;
                    }
                }
                if (valid) {
                    fprintf(out, "%s,%d\n", h[i].seed_str, h[i].hit_count);
                }
            }
            fflush(out);
            free(h);
        }

        progress_update(&progress, calculate_seeds_per_batch(batch_chars), (uint64_t)result_count, batch);
    }

    auto end_time = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end_time - start_time);

    fprintf(stderr, "$\n");
    fprintf(stderr, "$Done. Time: %lld ms\n", (long long)duration.count());
    fprintf(stderr, "$Results written to: %s\n", output_file);

    fclose(out);
    GPU_FREE(d_antes);
    free_result_buffer(d_results, d_result_count);
    return 0;
}

