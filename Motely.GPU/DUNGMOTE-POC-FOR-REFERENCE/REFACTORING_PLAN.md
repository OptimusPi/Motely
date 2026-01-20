# Dungmot Repository Refactoring Plan
## Production-Quality CUDA/HIP Balatro Seed Searcher

**Goal:** Transform this from prototype code into a production-ready, maintainable repository with:
- Clean architecture
- AMD GPU support (HIP/ROCm)
- Unified interface with CPU searchers
- Proper code organization
- Comprehensive documentation

---

## 1. Research Summary: Existing Searchers

### Tavodiva/Motely (CPU, SIMD)
- **Architecture:** C# with SIMD (AVX512) for CPU acceleration
- **Key Features:**
  - Suffix caching (batch-based optimization)
  - Left-to-right iteration (suffix fixed, prefix varies)
  - High-performance CPU search
- **Lessons:** Batch structure, suffix caching pattern

### OptimusPi/Ouija (OpenCL)
- **Architecture:** OpenCL for cross-platform GPU support
- **Key Features:**
  - JAML-based filter system
  - Python GUI
  - Cross-platform (NVIDIA/AMD/Intel)
- **Lessons:** JAML integration, cross-platform approach

### OptimusPi/Motley (JAML)
- **Architecture:** JAML configuration files
- **Key Features:**
  - Declarative filter syntax
  - Reusable search configurations
- **Lessons:** Configuration-driven design

### Current Dungmot (CUDA)
- **Strengths:**
  - Verified accurate RNG
  - Batch processing with suffix caching
  - Dynamic cutoff optimization
  - Multiple search tools
- **Weaknesses:**
  - Code duplication
  - No AMD support
  - Mixed test/production code
  - Inconsistent architecture
  - No unified interface

---

## 2. Architecture Redesign

### 2.1 Directory Structure

```
dungmot/
├── src/
│   ├── core/              # Core RNG and game logic
│   │   ├── rng.cuh        # Unified RNG (CUDA/HIP compatible)
│   │   ├── streams.cuh    # Item stream generation
│   │   ├── evaluator.cuh  # Seed evaluation engine
│   │   └── filters.cuh    # Filter system
│   ├── gpu/               # GPU-specific code
│   │   ├── cuda/          # CUDA implementations
│   │   │   ├── batch_kernel.cuh
│   │   │   └── kernels.cuh
│   │   └── hip/           # HIP implementations (AMD)
│   │       ├── batch_kernel.cuh
│   │       └── kernels.cuh
│   ├── cpu/               # CPU fallback (optional)
│   │   └── evaluator.cpp
│   └── tools/             # Search tools
│       ├── negative_joker_prefilter.cu
│       ├── negative_tag_skipper.cu
│       ├── ultimate_filter.cu
│       └── ...
├── include/               # Public headers
│   └── dungmot.h
├── tests/                 # Test files
│   ├── test_rng.cu
│   └── test_filters.cu
├── scripts/               # Build/run scripts
│   ├── build.ps1
│   ├── build.sh
│   └── run_*.ps1
├── config/                # JAML examples
│   └── examples/
├── docs/                  # Documentation
│   ├── ARCHITECTURE.md
│   ├── HIP_PORTING.md
│   └── API.md
├── CMakeLists.txt         # Modern build system
├── README.md
└── LICENSE
```

### 2.2 Unified GPU Abstraction Layer

Create `gpu_common.h` that abstracts CUDA/HIP:

```cpp
// gpu_common.h
#ifdef __HIP_PLATFORM_AMD__
    #include <hip/hip_runtime.h>
    #define GPU_FUNC __device__ __host__
    #define GPU_KERNEL __global__
    #define GPU_SYNC hipDeviceSynchronize()
    #define GPU_MALLOC hipMalloc
    #define GPU_FREE hipFree
    #define GPU_MEMCPY hipMemcpy
    namespace gpu = hip;
#else
    #include <cuda_runtime.h>
    #define GPU_FUNC __device__ __host__
    #define GPU_KERNEL __global__
    #define GPU_SYNC cudaDeviceSynchronize()
    #define GPU_MALLOC cudaMalloc
    #define GPU_FREE cudaFree
    #define GPU_MEMCPY cudaMemcpy
    namespace gpu = cuda;
#endif
```

### 2.3 Core Module Refactoring

#### RNG Module (`core/rng.cuh`)
- ✅ Already accurate (verified)
- ✅ Keep as-is, add HIP compatibility macros
- Remove any CUDA-specific assumptions

#### Streams Module (`core/streams.cuh`)
- ✅ Good separation of concerns
- Add HIP compatibility
- Document stream ordering guarantees

#### Evaluator Module (`core/evaluator.cuh`)
- ✅ Clean filter system
- Remove TODOs or implement them
- Add comprehensive error handling

#### Batch Processing (`gpu/batch_kernel.cuh`)
- ✅ Suffix caching is correct
- ✅ Motely-style batching implemented
- Consolidate duplicate batch code

---

## 3. Code Quality Improvements

### 3.1 Remove Code Duplication

**Current Issues:**
- Multiple implementations of batch processing
- Duplicate seed string conversion
- Repeated RNG initialization

**Solution:**
- Single `batch_kernel.cuh` used by all tools
- Centralized seed utilities
- Shared RNG initialization

### 3.2 Clean Up Test Files

**Action Items:**
1. Move all `test_*.cu` to `tests/`
2. Remove obsolete files:
   - `raw_soul_edition_check.cu` (if deprecated)
   - `simple_soul_search.cu` (if superseded)
   - `soul_joker_search.cu` (if superseded)
3. Create proper test suite with CMake

### 3.3 Remove DEBUG Code

**Action Items:**
- Search for all `fprintf(stderr, "DEBUG:`
- Remove or convert to proper logging system
- Use `#ifdef DEBUG` guards if needed

### 3.4 Fix TODOs

**Critical TODOs:**
- `balatro_evaluator.cuh:532` - Track vouchers for shop slots
- Implement or document as "future work"

**Non-Critical:**
- Document in `docs/FUTURE.md`

---

## 4. HIP/ROCm Porting Strategy

### 4.1 Compatibility Layer

Create `gpu_compat.h`:
```cpp
// Unified GPU API
#define GPU_SUCCESS 0

inline int gpu_malloc(void** ptr, size_t size) {
#ifdef __HIP_PLATFORM_AMD__
    return hipMalloc(ptr, size);
#else
    return cudaMalloc(ptr, size);
#endif
}
// ... more wrappers
```

### 4.2 Kernel Compatibility

**CUDA → HIP Changes:**
- `__shared__` → `__shared__` (same)
- `__syncthreads()` → `__syncthreads()` (same)
- `atomicAdd` → `atomicAdd` (same)
- `__device__` → `__device__` (same)
- `__global__` → `__global__` (same)

**Main differences:**
- Launch syntax: `hipLaunchKernelGGL` vs `kernel<<<>>>`
- Error codes: `hipError_t` vs `cudaError_t`
- Architecture detection

### 4.3 Build System Updates

**CMakeLists.txt:**
```cmake
option(USE_HIP "Build with HIP (AMD GPU)" OFF)
option(USE_CUDA "Build with CUDA (NVIDIA GPU)" ON)

if(USE_HIP)
    find_package(hip REQUIRED)
    set(GPU_LANGUAGE HIP)
elseif(USE_CUDA)
    find_package(CUDA REQUIRED)
    set(GPU_LANGUAGE CUDA)
endif()
```

---

## 5. Integration with CPU Searchers

### 5.1 Unified Interface

Create `dungmot.h` API:
```cpp
namespace dungmot {
    struct SearchConfig {
        std::vector<std::string> jokers;
        int min_score;
        int start_ante, end_ante;
        // ...
    };
    
    class Searcher {
    public:
        virtual ~Searcher() = default;
        virtual void search(const SearchConfig& config) = 0;
    };
    
    class CUDASearcher : public Searcher { /* ... */ };
    class HIPSearcher : public Searcher { /* ... */ };
    class CPUSearcher : public Searcher { /* ... */ };
}
```

### 5.2 JAML Support

- Integrate JAML parser (from backup)
- Support Ouija/Motely JAML format
- Unified filter system

---

## 6. Performance Optimizations

### 6.1 Current Performance
- ~4-5M seeds/sec (RTX 4070 SUPER)
- Dynamic cutoff working
- Suffix caching implemented

### 6.2 Optimization Opportunities
1. **Remove printf from kernels** (already done in most tools)
2. **Vectorization** (CUDA vector types)
3. **Shared memory optimization**
4. **Warp-level primitives**

See `OPTIMIZE_PLAN.md` for details.

---

## 7. Documentation

### 7.1 Required Docs
- `ARCHITECTURE.md` - System design
- `HIP_PORTING.md` - AMD GPU setup
- `API.md` - Public API reference
- `CONTRIBUTING.md` - Development guide
- `PERFORMANCE.md` - Benchmarks and tuning

### 7.2 Code Documentation
- Doxygen-style comments
- Function documentation
- Algorithm explanations

---

## 8. Migration Plan

### Phase 1: Cleanup (Week 1)
1. ✅ Remove obsolete files
2. ✅ Move tests to `tests/`
3. ✅ Remove DEBUG code
4. ✅ Fix build script inconsistencies

### Phase 2: Refactoring (Week 2)
1. ✅ Create directory structure
2. ✅ Consolidate duplicate code
3. ✅ Create GPU abstraction layer
4. ✅ Update build system

### Phase 3: HIP Support (Week 3)
1. ✅ Create compatibility layer
2. ✅ Port kernels to HIP
3. ✅ Test on AMD GPU
4. ✅ Update documentation

### Phase 4: Integration (Week 4)
1. ✅ Unified API
2. ✅ JAML support
3. ✅ CPU fallback
4. ✅ Final testing

---

## 9. Comparison: CUDA vs OpenCL vs CPU

### Performance (Estimated)
- **CUDA (NVIDIA):** 4-5M seeds/sec (RTX 4070 SUPER)
- **HIP (AMD):** 3-4M seeds/sec (estimated, RX 7900 XT)
- **OpenCL (Ouija):** 1-2M seeds/sec (estimated)
- **CPU SIMD (Motely):** 10-50M seeds/sec (AVX512, high-end CPU)

**Note:** CPU can be faster for simple filters due to:
- Lower latency
- Better branch prediction
- SIMD vectorization
- No GPU overhead

### When to Use What
- **CUDA:** NVIDIA GPUs, complex filters
- **HIP:** AMD GPUs, cross-platform
- **CPU:** Simple filters, no GPU available
- **OpenCL:** Legacy support (deprecated)

---

## 10. Success Criteria

### Code Quality
- ✅ No code duplication
- ✅ All tests pass
- ✅ No DEBUG code in production
- ✅ Comprehensive documentation
- ✅ Clean build system

### Functionality
- ✅ CUDA support (NVIDIA)
- ✅ HIP support (AMD)
- ✅ CPU fallback
- ✅ JAML configuration
- ✅ Unified API

### Performance
- ✅ Maintain current CUDA performance
- ✅ HIP within 20% of CUDA
- ✅ CPU competitive for simple filters

---

## Next Steps

1. **Review this plan** - Get feedback
2. **Create GitHub issues** - Break down into tasks
3. **Start Phase 1** - Cleanup
4. **Iterate** - Refactor incrementally

---

## References

- [HIP Documentation](https://rocmdocs.amd.com/projects/HIP/en/latest/)
- [CUDA Best Practices](https://docs.nvidia.com/cuda/cuda-c-best-practices-guide/)
- Motely (Tavodiva) - CPU SIMD approach
- Ouija (OptimusPi) - OpenCL/JAML approach
