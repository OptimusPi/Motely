# HIPIFY Conversion Guide

This guide explains how to use the HIP-ified version of dungmot on AMD GPUs.

## What is HIPIFY?

HIP (Heterogeneous Interface for Portability) is AMD's CUDA-compatible API that allows CUDA code to run on AMD GPUs via ROCm. This codebase has been converted to use a unified GPU abstraction layer (`gpu_common.h`) that works with both:
- **NVIDIA CUDA** (via `nvcc`)
- **AMD HIP** (via `hipcc`)

## Prerequisites

### For AMD GPUs (ROCm/HIP):
1. Install ROCm: https://rocm.docs.amd.com/
2. Verify installation:
   ```bash
   rocm-smi
   hipcc --version
   ```

### For NVIDIA GPUs (CUDA):
1. Install CUDA Toolkit: https://developer.nvidia.com/cuda-downloads
2. Verify installation:
   ```bash
   nvcc --version
   nvidia-smi
   ```

## Building

### Build Script (PowerShell - Windows)

The `build.ps1` script automatically detects the platform:

```powershell
# For NVIDIA (default)
.\build.ps1 showman_consecutive

# For AMD (set HIP=1 environment variable)
$env:HIP=1; .\build.ps1 showman_consecutive
```

### Manual Build

#### NVIDIA CUDA:
```bash
nvcc -O3 -arch=sm_89 --fmad=false -o showman_consecutive_filter.exe showman_consecutive_filter.cu
```

#### AMD HIP:
```bash
hipcc -O3 --offload-arch=gfx1100 --fmad=false -o showman_consecutive_filter.exe showman_consecutive_filter.cu
```

**Note:** Replace `gfx1100` with your AMD GPU architecture:
- `gfx1100` = RDNA 3 (RX 7000 series)
- `gfx1030` = RDNA 2 (RX 6000 series)
- `gfx906` = CDNA (MI50, MI100)
- Check with: `rocminfo | grep gfx`

## What Changed?

### Unified GPU Abstraction (`gpu_common.h`)

All CUDA-specific code now uses macros that work with both platforms:

| CUDA | HIP | Macro |
|------|-----|-------|
| `cudaMalloc` | `hipMalloc` | `GPU_MALLOC` |
| `cudaFree` | `hipFree` | `GPU_FREE` |
| `cudaMemcpy` | `hipMemcpy` | `GPU_MEMCPY` |
| `cudaDeviceSynchronize` | `hipDeviceSynchronize` | `GPU_DEVICE_SYNCHRONIZE` |
| `cudaDeviceProp` | `hipDeviceProp_t` | `GPUDeviceProp` |
| `__global__` | `__global__` | `GPU_KERNEL` |
| `__device__` | `__device__` | `GPU_DEVICE` |
| `__host__` | `__host__` | `GPU_HOST` |

### Files Updated

All `.cu` and `.cuh` files now:
1. Include `gpu_common.h` instead of `cuda_runtime.h`
2. Use GPU abstraction macros instead of direct CUDA calls
3. Maintain 100% compatibility with both platforms

## Testing

### Verify Platform Detection:
```bash
# Should print "CUDA (NVIDIA)" or "HIP (AMD ROCm)"
./showman_consecutive_filter.exe --help
```

### Run a Test:
```bash
./showman_consecutive_filter.exe --start-batch 0 --end-batch 10 --batch-chars 4 --antes 2 --min-score 1
```

## Performance Notes

- **NVIDIA**: No performance change (same CUDA code)
- **AMD**: Performance depends on GPU architecture and ROCm version
- **SIMT optimizations** (warp-level primitives, launch bounds) work on both platforms

## Troubleshooting

### "Neither CUDA nor HIP detected!"
- Make sure you're compiling with `nvcc` or `hipcc`, not `gcc`/`clang`
- Check that CUDA/ROCm is properly installed

### "hipcc: command not found"
- Install ROCm: https://rocm.docs.amd.com/
- Add ROCm to PATH: `export PATH=/opt/rocm/bin:$PATH`

### Wrong GPU architecture
- Check your GPU: `rocminfo | grep gfx`
- Update `--offload-arch` flag in build script

### Performance issues on AMD
- Ensure ROCm drivers are up to date
- Check GPU utilization: `rocm-smi`
- Some optimizations may need AMD-specific tuning

## Future Work

- [ ] Add CMake build system with automatic platform detection
- [ ] Add CI/CD testing for both CUDA and HIP
- [ ] Optimize for AMD-specific features (wavefronts, etc.)
- [ ] Add performance benchmarks comparing platforms

## References

- [HIPIFY Documentation](https://rocm.docs.amd.com/projects/HIPIFY/en/latest/)
- [ROCm Documentation](https://rocm.docs.amd.com/)
- [HIP Porting Guide](https://rocm.docs.amd.com/projects/HIP/en/docs-6.4.3/how-to/hip_porting_guide.html)
