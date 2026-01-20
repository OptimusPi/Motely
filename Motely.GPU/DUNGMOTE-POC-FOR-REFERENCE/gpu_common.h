/**
 * @file gpu_common.h
 * @brief Unified GPU abstraction layer for CUDA/HIP compatibility
 * 
 * This header provides a unified interface that works with both:
 * - NVIDIA CUDA (via nvcc)
 * - AMD HIP (via hipcc)
 * 
 * Usage: Include this instead of cuda_runtime.h or hip_runtime.h
 */

#ifndef GPU_COMMON_H
#define GPU_COMMON_H

// Detect platform and include appropriate headers
#ifdef __HIP_PLATFORM_AMD__
    // AMD HIP (ROCm)
    #include <hip/hip_runtime.h>
    #include <hip/hip_runtime_api.h>
    
    // HIP uses same qualifiers as CUDA
    #define GPU_FUNC __device__ __host__
    #define GPU_KERNEL __global__
    #define GPU_DEVICE __device__
    #define GPU_HOST __host__
    
    // Memory management
    #define GPU_MALLOC hipMalloc
    #define GPU_FREE hipFree
    #define GPU_MEMCPY hipMemcpy
    #define GPU_MEMCPY_ASYNC hipMemcpyAsync
    #define GPU_MEMCPY_HOST_TO_DEVICE hipMemcpyHostToDevice
    #define GPU_MEMCPY_DEVICE_TO_HOST hipMemcpyDeviceToHost
    #define GPU_MEMCPY_DEVICE_TO_DEVICE hipMemcpyDeviceToDevice
    
    // Device management
    #define GPU_GET_DEVICE hipGetDevice
    #define GPU_GET_DEVICE_PROPERTIES hipGetDeviceProperties
    #define GPU_DEVICE_SYNCHRONIZE hipDeviceSynchronize
    #define GPU_GET_LAST_ERROR hipGetLastError
    
    // Device properties
    typedef hipDeviceProp_t GPUDeviceProp;
    
    // Error codes
    typedef hipError_t GPUError;
    #define GPU_SUCCESS hipSuccess
    
    // Synchronization
    #define GPU_SYNC_THREADS __syncthreads
    #define GPU_THREADFENCE __threadfence
    
    // Warp-level primitives (same in HIP)
    #define GPU_BALLOT_SYNC __ballot_sync
    #define GPU_SHUFFLE_SYNC __shfl_sync
    #define GPU_SYNCWARP __syncwarp
    
    // Atomic operations
    #define GPU_ATOMIC_ADD atomicAdd
    #define GPU_ATOMIC_CAS atomicCAS
    
    // Built-in variables (same in HIP)
    #define GPU_BLOCK_IDX blockIdx
    #define GPU_THREAD_IDX threadIdx
    #define GPU_BLOCK_DIM blockDim
    #define GPU_GRID_DIM gridDim
    
    // Launch bounds
    #define GPU_LAUNCH_BOUNDS __launch_bounds__
    
    // Platform name
    #define GPU_PLATFORM_NAME "HIP (AMD ROCm)"
    
    // Error string function (HIP doesn't have hipGetErrorString, use code)
    #define GPU_GET_ERROR_STRING(err) "HIP Error"  // HIP uses numeric codes
    
#elif defined(__CUDACC__) || defined(__CUDA__)
    // NVIDIA CUDA
    #include <cuda_runtime.h>
    #include <cuda.h>
    
    // CUDA qualifiers
    #define GPU_FUNC __device__ __host__
    #define GPU_KERNEL __global__
    #define GPU_DEVICE __device__
    #define GPU_HOST __host__
    
    // Memory management
    #define GPU_MALLOC cudaMalloc
    #define GPU_FREE cudaFree
    #define GPU_MEMCPY cudaMemcpy
    #define GPU_MEMCPY_ASYNC cudaMemcpyAsync
    #define GPU_MEMCPY_HOST_TO_DEVICE cudaMemcpyHostToDevice
    #define GPU_MEMCPY_DEVICE_TO_HOST cudaMemcpyDeviceToHost
    #define GPU_MEMCPY_DEVICE_TO_DEVICE cudaMemcpyDeviceToDevice
    
    // Device management
    #define GPU_GET_DEVICE cudaGetDevice
    #define GPU_GET_DEVICE_PROPERTIES cudaGetDeviceProperties
    #define GPU_DEVICE_SYNCHRONIZE cudaDeviceSynchronize
    #define GPU_GET_LAST_ERROR cudaGetLastError
    
    // Device properties
    typedef cudaDeviceProp GPUDeviceProp;
    
    // Error codes
    typedef cudaError_t GPUError;
    #define GPU_SUCCESS cudaSuccess
    
    // Synchronization
    #define GPU_SYNC_THREADS __syncthreads
    #define GPU_THREADFENCE __threadfence
    
    // Warp-level primitives
    #define GPU_BALLOT_SYNC __ballot_sync
    #define GPU_SHUFFLE_SYNC __shfl_sync
    #define GPU_SYNCWARP __syncwarp
    
    // Atomic operations
    #define GPU_ATOMIC_ADD atomicAdd
    #define GPU_ATOMIC_CAS atomicCAS
    
    // Built-in variables
    #define GPU_BLOCK_IDX blockIdx
    #define GPU_THREAD_IDX threadIdx
    #define GPU_BLOCK_DIM blockDim
    #define GPU_GRID_DIM gridDim
    
    // Launch bounds
    #define GPU_LAUNCH_BOUNDS __launch_bounds__
    
    // Platform name
    #define GPU_PLATFORM_NAME "CUDA (NVIDIA)"
    
    // Error string function
    #define GPU_GET_ERROR_STRING(err) cudaGetErrorString(err)
    
#else
    #error "Neither CUDA nor HIP detected! Please compile with nvcc or hipcc."
#endif

// Common types and utilities
#include <stdint.h>
#include <stdio.h>

// Helper macros for error checking
#define GPU_CHECK(call) do { \
    GPUError err = call; \
    if (err != GPU_SUCCESS) { \
        fprintf(stderr, "GPU error at %s:%d: %d\n", __FILE__, __LINE__, (int)err); \
        return err; \
    } \
} while(0)

#define GPU_CHECK_NORET(call) do { \
    GPUError err = call; \
    if (err != GPU_SUCCESS) { \
        fprintf(stderr, "GPU error at %s:%d: %d\n", __FILE__, __LINE__, (int)err); \
    } \
} while(0)

#endif // GPU_COMMON_H
