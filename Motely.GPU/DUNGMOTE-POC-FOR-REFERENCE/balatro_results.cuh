/**
 * @file balatro_results.cuh
 * @brief Result buffer system for CUDA kernels
 *
 * Device prints (`printf`) are expensive and can interleave with progress output.
 * This provides a simple device-side buffer + host-side streaming output.
 */

#ifndef BALATRO_RESULTS_CUH
#define BALATRO_RESULTS_CUH

#include <stdint.h>
#include <cuda_runtime.h>

struct PrefilterResult {
    char seed_str[9];
    int hit_count;
};

// 1M results ~= 13MB, fine for “find rare hits” workloads.
#define MAX_RESULTS_BUFFER_SIZE 1000000

__host__ __forceinline__ cudaError_t allocate_result_buffer(
    PrefilterResult** d_results,
    int** d_result_count,
    int max_results
) {
    cudaError_t err = cudaMalloc(d_results, sizeof(PrefilterResult) * max_results);
    if (err != cudaSuccess) return err;

    err = cudaMalloc(d_result_count, sizeof(int));
    if (err != cudaSuccess) {
        cudaFree(*d_results);
        return err;
    }

    int zero = 0;
    return cudaMemcpy(*d_result_count, &zero, sizeof(int), cudaMemcpyHostToDevice);
}

GPU_HOST __forceinline__ void free_result_buffer(PrefilterResult* d_results, int* d_result_count) {
    if (d_results) GPU_FREE(d_results);
    if (d_result_count) GPU_FREE(d_result_count);
}

GPU_DEVICE __forceinline__ bool add_result(
    PrefilterResult* results,
    int* result_count,
    int max_results,
    const char* seed_str,
    int hit_count
) {
    int idx = GPU_ATOMIC_ADD(result_count, 1);
    if (idx >= max_results) return false;

    // Memory barrier to ensure atomicAdd is visible before writes
    GPU_THREADFENCE();
    
    // Copy seed string (ensure null-terminated)
    for (int i = 0; i < 8; i++) {
        results[idx].seed_str[i] = seed_str[i];
    }
    results[idx].seed_str[8] = '\0';
    
    // Write hit_count with memory barrier
    results[idx].hit_count = hit_count;
    GPU_THREADFENCE();
    
    return true;
}

#endif // BALATRO_RESULTS_CUH


