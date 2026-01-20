# DUNGMOT CUDA Optimization Plan

## Current Bottlenecks
- printf in kernel (kills performance)
- String operations per thread (expensive)
- Single roll per iteration (not vectorized)
- Memory latency from random access
- Low GPU occupancy

## Optimization Techniques

### 1. Vectorization (CUDA equivalent of AVX512)
```cpp
// Use CUDA vector types for 8 rolls at once
double8 rolls = make_double8(
    get_next_random(&stream),
    get_next_random(&stream),
    // ... 8 total
);

// Vector comparison
int8 mask = __vcmpgt_pd(rolls, make_double8(threshold));
if (__any(mask)) printf("%s\n", seed_str);
```

### 2. Remove printf from kernel
- Use device buffers instead
- Copy results back to host in batches
- Only print from host

### 3. Shared memory optimization
- Load SEED_CHARS into shared memory
- Reduce global memory access

### 4. Warp-level primitives
- `__ballot_sync` for thread communication
- `__shfl_sync` for data sharing
- Reduce divergence

### 5. Memory coalescing
- Align memory access patterns
- Use structure of arrays (SoA) layout

### 6. Loop unrolling
- `#pragma unroll` for predictable loops
- Reduce branch prediction misses

### 7. Increase occupancy
- More blocks per SM
- Optimize register usage
- Use `__launch_bounds__`

### 8. Async operations
- CUDA streams for overlapping compute/transfer
- Async memory copies

## Implementation Priority
1. **Remove printf** (biggest win)
2. **Vectorize rolls** (AVX512 equivalent)
3. **Shared memory** for charset
4. **Warp primitives** for reduction
5. **Memory coalescing**

## Expected Performance Gains
- Remove printf: 10-50x speedup
- Vectorization: 2-4x speedup  
- Memory optimization: 1.5-2x speedup
- **Total potential: 30-400x faster**

## Target Performance
- Goal: 10M+ seeds/sec
- Current: ~700k seeds/sec
- Need: 15-50x improvement

## Testing Strategy
- Benchmark each optimization separately
- Use CUDA profiler (nvprof/Nsight)
- Compare against CPU AVX512 baseline
