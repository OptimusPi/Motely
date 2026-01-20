# How HIP Compiles .cu Files (The "Magic" Explained)

## TL;DR
**HIP can compile `.cu` files because it's designed to be CUDA-compatible.** The file extension doesn't matter - what matters is:
1. Using `hipcc` instead of `nvcc` as the compiler
2. Including HIP headers instead of CUDA headers
3. Using the unified abstraction layer (`gpu_common.h`)

## The "Magic" Explained

### Why `.cu` Works with HIP

HIP (Heterogeneous Interface for Portability) was designed by AMD to be **source-compatible** with CUDA. This means:

1. **Same syntax**: HIP kernels use the same `__global__`, `__device__`, `__host__` qualifiers
2. **Same kernel launch**: `<<<blocks, threads>>>` syntax works in both
3. **Same built-ins**: `blockIdx`, `threadIdx`, `blockDim`, `gridDim` are identical
4. **Same primitives**: `__syncthreads()`, `atomicAdd()`, etc. work the same

### What Actually Happens

When you compile with `hipcc`:
```bash
hipcc -O3 --offload-arch=gfx1100 file.cu
```

1. **hipcc** is a wrapper around clang that:
   - Preprocesses the `.cu` file
   - Detects CUDA/HIP syntax (`__global__`, `<<<>>>`, etc.)
   - Converts CUDA API calls to HIP equivalents (if needed)
   - Compiles to AMD GPU code (ROCm)

2. **gpu_common.h** does the heavy lifting:
   ```c
   #ifdef __HIP_PLATFORM_AMD__
       #include <hip/hip_runtime.h>  // HIP headers
       #define GPU_MALLOC hipMalloc   // Map to HIP
   #else
       #include <cuda_runtime.h>      // CUDA headers
       #define GPU_MALLOC cudaMalloc  // Map to CUDA
   #endif
   ```

3. **Result**: Same source code, different runtime libraries:
   - NVIDIA: Links against `libcudart.so` (CUDA runtime)
   - AMD: Links against `libhip_hcc.so` or `libamdhip64.so` (HIP runtime)

### File Extensions Don't Matter

- `.cu` = CUDA source (but HIP can compile it)
- `.hip` = HIP source (but it's the same syntax)
- `.cpp` = C++ (but can contain GPU code if compiled with `hipcc`/`nvcc`)

**The compiler (`hipcc` vs `nvcc`) determines the target, not the file extension!**

## Why We Keep `.cu` Extensions

1. **Familiarity**: CUDA developers recognize `.cu` as GPU code
2. **Tooling**: IDEs, syntax highlighters work with `.cu`
3. **Compatibility**: Both `nvcc` and `hipcc` accept `.cu` files
4. **No migration needed**: We don't need to rename 50+ files

## The Abstraction Layer (`gpu_common.h`)

This is the REAL magic - it provides a unified API:

| What You Write | NVIDIA (nvcc) | AMD (hipcc) |
|----------------|---------------|-------------|
| `GPU_MALLOC(...)` | `cudaMalloc(...)` | `hipMalloc(...)` |
| `GPU_KERNEL void foo()` | `__global__ void foo()` | `__global__ void foo()` |
| `GPUDeviceProp` | `cudaDeviceProp` | `hipDeviceProp_t` |

**Same source code, different backends!**

## Example: Building for Both Platforms

```powershell
# NVIDIA build
nvcc -O3 -arch=sm_89 file.cu -o file.exe
# Links: libcudart.dll, runs on NVIDIA GPUs

# AMD build  
hipcc -O3 --offload-arch=gfx1100 file.cu -o file.exe
# Links: libamdhip64.dll, runs on AMD GPUs
```

**Same `.cu` file, different compilers, different GPU targets!**

## References

- [HIP Porting Guide](https://rocm.docs.amd.com/projects/HIP/en/latest/how-to/hip_porting_guide.html)
- [HIPIFY Tool](https://rocm.docs.amd.com/projects/HIPIFY/en/latest/) - Can auto-convert CUDA to HIP, but we don't need it because we use the abstraction layer
