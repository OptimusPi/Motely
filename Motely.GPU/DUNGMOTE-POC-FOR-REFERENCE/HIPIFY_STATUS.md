# HIPIFY Conversion Status

## ✅ Completed

1. **Created `gpu_common.h`** - Unified GPU abstraction layer
   - Supports both CUDA (NVIDIA) and HIP (AMD ROCm)
   - All runtime API calls abstracted via macros

2. **Updated Core Headers:**
   - `balatro_filter_main.cuh` - Uses `GPUDeviceProp`, `GPU_GET_DEVICE`, etc.
   - `balatro_batch_main.cuh` - Uses `GPU_HOST` qualifier
   - `balatro_results.cuh` - Uses `GPU_MALLOC`, `GPU_FREE`, `GPU_ATOMIC_ADD`, etc.

3. **Updated Build System:**
   - `build.ps1` - Added `-HIP` flag support
   - `build_hip.ps1` - Convenience wrapper for HIP builds
   - Auto-detects platform via `$env:HIP` or `-HIP` parameter

4. **Updated Example Filter:**
   - `showman_consecutive_filter.cu` - Fully converted to use GPU abstraction

5. **Documentation:**
   - `HIPIFY_GUIDE.md` - Complete guide for using HIP
   - `HIPIFY_STATUS.md` - This file

## ✅ All Files Converted!

All `.cu` and `.cuh` files have been converted to use `gpu_common.h`:

### High Priority (Most Used):
- [x] `negative_tag_skipper.cu`
- [x] `negative_joker_prefilter.cu`
- [x] `negative_legendary_prefilter.cu`
- [x] `ultimate_filter.cu`

### Medium Priority:
- [x] `consecutive_negative_checker.cu`
- [x] `raw_soul_edition_check.cu`
- [x] `erratic_search.cu`

### Low Priority (Less Used):
- [x] `soul_joker_filter_search.cu`
- [x] `soul_edition_search.cu`
- [x] `negative_rare_prefilter.cu`
- [x] `negative_uncommon_prefilter.cu`
- [x] `economy_rush_search.cu`

### Core Headers:
- [x] `balatro_rng.cuh`
- [x] `balatro_main.cuh`
- [x] `verify_rng.cu`

## Conversion Pattern

For each `.cu` file:

1. Replace `#include <cuda_runtime.h>` with `#include "gpu_common.h"`
2. Replace CUDA API calls:
   - `cudaMalloc` → `GPU_MALLOC`
   - `cudaFree` → `GPU_FREE`
   - `cudaMemcpy` → `GPU_MEMCPY`
   - `cudaDeviceSynchronize` → `GPU_DEVICE_SYNCHRONIZE`
   - `cudaGetDevice` → `GPU_GET_DEVICE`
   - `cudaGetDeviceProperties` → `GPU_GET_DEVICE_PROPERTIES`
   - `cudaError_t` → `GPUError`
   - `cudaSuccess` → `GPU_SUCCESS`
   - `cudaMemcpyHostToDevice` → `GPU_MEMCPY_HOST_TO_DEVICE`
   - `cudaMemcpyDeviceToHost` → `GPU_MEMCPY_DEVICE_TO_HOST`
3. Replace qualifiers:
   - `__global__` → `GPU_KERNEL` (optional, same in HIP)
   - `__host__` → `GPU_HOST`
   - `__device__` → `GPU_DEVICE`
4. **No changes needed for:**
   - `blockIdx`, `threadIdx`, `blockDim`, `gridDim` (same in HIP)
   - `__syncthreads()`, `__threadfence()` (same in HIP)
   - `atomicAdd`, `atomicCAS` (same in HIP)
   - `__ballot_sync`, `__shfl_sync`, `__syncwarp` (same in HIP)
   - Kernel launch syntax `<<<>>>` (same in HIP)

## Testing

### Test CUDA Build:
```powershell
.\build.ps1 showman_consecutive
```

### Test HIP Build:
```powershell
.\build_hip.ps1 showman_consecutive
# OR
$env:HIP=1; .\build.ps1 showman_consecutive
```

## Notes

- All SIMT optimizations (warp-level primitives, launch bounds) work on both platforms
- `printf` in kernels works on both CUDA and HIP
- Performance should be similar, but AMD-specific tuning may be needed
- ROCm installation required for HIP builds (see `HIPIFY_GUIDE.md`)
