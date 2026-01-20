# CUDA Vectorization Plan: Motely 8-Lane SIMD Equivalent

## Overview

Motely uses CPU SIMD (AVX-512) to process **8 seeds in parallel** within a single instruction. This plan implements equivalent performance optimizations in CUDA using vector types, warp-level primitives, and batch-aware pseudohash caching.

## Motely's 8-Lane SIMD Approach

### CPU SIMD (Motely)
- **8 lanes** = 8 seeds processed simultaneously
- Single instruction operates on 8 `double` values at once
- All 8 seeds share the same batch suffix (rightmost chars)
- Pseudohash of suffix computed once, cached
- Leftmost varying chars processed in parallel across 8 lanes

### Key Insight
```
Batch: All seeds ending in "111" (suffix fixed)
Lane 0: "11111111" → pseudohash(suffix="111") + incremental(left="11111")
Lane 1: "11211111" → pseudohash(suffix="111") + incremental(left="11211")
Lane 2: "11311111" → pseudohash(suffix="111") + incremental(left="11311")
...
Lane 7: "11811111" → pseudohash(suffix="111") + incremental(left="11811")
```

## CUDA Vectorization Strategy

### Option 1: Vector Types (Similar to SIMD)
**Use CUDA vector types (`double2`, `double4`, `double8`) to process multiple seeds per thread**

```cuda
// Process 4 seeds per thread using double4
__device__ void process_seeds_vectorized(
    uint64_t batch_index,
    uint64_t local_start_idx,
    int batch_chars,
    double cached_suffix_hash,
    int suffix_chars,
    double4* seed_hashes_out  // Output: 4 pseudohashes
) {
    char seed_strs[4][9];
    double4 hashes;
    
    // Generate 4 consecutive seeds (left-to-right iteration)
    for (int lane = 0; lane < 4; lane++) {
        uint64_t seed_idx = local_index_to_seed_index(
            batch_index, 
            local_start_idx + lane, 
            batch_chars
        );
        seed_index_to_string(seed_idx, seed_strs[lane]);
        
        // Use cached suffix hash + incremental leftmost chars
        hashes.x = (lane == 0) ? pseudohash8_with_batch_prefix(
            cached_suffix_hash, suffix_chars, seed_strs[0]) : 0.0;
        hashes.y = (lane == 1) ? pseudohash8_with_batch_prefix(
            cached_suffix_hash, suffix_chars, seed_strs[1]) : 0.0;
        hashes.z = (lane == 2) ? pseudohash8_with_batch_prefix(
            cached_suffix_hash, suffix_chars, seed_strs[2]) : 0.0;
        hashes.w = (lane == 3) ? pseudohash8_with_batch_prefix(
            cached_suffix_hash, suffix_chars, seed_strs[3]) : 0.0;
    }
    
    *seed_hashes_out = hashes;
}
```

**Limitations:**
- CUDA vector types don't have SIMD operations like AVX-512
- Each component still computed sequentially
- **Better for memory coalescing than true SIMD**

### Option 2: Warp-Level Parallelism (CUDA Native)
**Use warp (32 threads) to process 32 seeds in parallel - CUDA's natural SIMT model**

```cuda
__device__ void process_batch_warp_parallel(
    uint64_t batch_index,
    int batch_chars,
    double cached_suffix_hash,
    int suffix_chars
) {
    int lane_id = threadIdx.x % 32;  // Warp lane ID (0-31)
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    
    // Each thread in warp processes seeds at stride 32
    for (uint64_t base_idx = 0; base_idx < seeds_per_batch; base_idx += 32) {
        uint64_t local_idx = base_idx + lane_id;
        if (local_idx >= seeds_per_batch) break;
        
        uint64_t seed_idx = local_index_to_seed_index(
            batch_index, local_idx, batch_chars
        );
        
        char seed_str[9];
        seed_index_to_string(seed_idx, seed_str);
        
        // Use cached suffix hash
        double seed_hash = pseudohash8_with_batch_prefix(
            cached_suffix_hash, suffix_chars, seed_str
        );
        
        // Process seed...
    }
}
```

**Advantages:**
- Natural CUDA parallelism (32 threads = 32 seeds)
- Better than vector types for independent computations
- Warp executes in lockstep (SIMT)

### Option 3: Hybrid - Warp + Vector Types (Best Performance)
**Combine warp parallelism with vectorized memory operations**

```cuda
__global__ void negative_tag_skipper_kernel_vectorized(
    uint64_t start_batch_index,
    uint64_t end_batch_index,
    int batch_chars,
    int* antes,
    int num_antes,
    // ... other params
) {
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    int warp_id = tid / 32;
    int lane_id = tid % 32;
    
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    
    // Process one batch per warp (or multiple warps per batch for large batches)
    for (uint64_t batch = start_batch_index + warp_id; 
         batch <= end_batch_index; 
         batch += (gridDim.x * blockDim.x) / 32) {
        
        // Compute cached suffix hash once per batch (first thread in warp)
        __shared__ double batch_suffix_hash[32];  // One per warp
        if (lane_id == 0) {
            int suffix_chars = 8 - batch_chars;
            char suffix[9];
            batch_index_to_suffix_host(batch, batch_chars, suffix);
            batch_suffix_hash[warp_id] = pseudohash_prefix(suffix, suffix_chars);
        }
        __syncwarp();
        double cached_hash = batch_suffix_hash[warp_id];
        
        // Each thread processes seeds at stride of total threads
        // Left-to-right iteration within batch
        for (uint64_t base = 0; base < seeds_per_batch; base += (gridDim.x * blockDim.x)) {
            uint64_t local_idx = base + tid;
            if (local_idx >= seeds_per_batch) break;
            
            uint64_t seed_idx = local_index_to_seed_index(batch, local_idx, batch_chars);
            char seed_str[9];
            seed_index_to_string(seed_idx, seed_str);
            
            // Use cached suffix hash (KEY OPTIMIZATION)
            double seed_hash = pseudohash8_with_batch_prefix(
                cached_hash, 8 - batch_chars, seed_str
            );
            
            // Process seed with cached hash...
        }
    }
}
```

## Implementation Plan

### Phase 1: Pseudohash Caching (Foundation)
**Priority: CRITICAL - Enables all other optimizations**

1. **Add `pseudohash8_with_batch_prefix` to `balatro_rng.cuh`**
   - Already exists in backup files
   - Copy from `local_untracked_backup_20260101-174250/balatro_hash_cache.cuh`
   - Function: `pseudohash8_with_batch_prefix(cached_suffix_hash, suffix_chars, seed_str)`

2. **Modify kernel to cache suffix hash per batch**
   ```cuda
   // Per-batch initialization
   int suffix_chars = 8 - batch_chars;
   char suffix[9];
   batch_index_to_suffix_host(batch_index, batch_chars, suffix);
   double cached_suffix_hash = pseudohash_prefix(suffix, suffix_chars);
   
   // Per-seed: use cached hash
   double seed_hash = pseudohash8_with_batch_prefix(
       cached_suffix_hash, suffix_chars, seed_str
   );
   ```

3. **Expected gain: 2-3x speedup** (avoids recomputing suffix hash for every seed)

### Phase 2: Warp-Optimized Batch Processing
**Priority: HIGH - Matches Motely's parallelism model**

1. **Restructure kernel for warp-level batch processing**
   - One warp (32 threads) processes one batch
   - Each thread processes seeds at stride 32 (left-to-right)
   - Shared memory for batch suffix hash (one per warp)

2. **Left-to-right iteration within batch**
   ```cuda
   // Within a batch, iterate leftmost chars left-to-right
   // This enables incremental pseudohash computation
   for (uint64_t local_idx = lane_id; 
        local_idx < seeds_per_batch; 
        local_idx += 32) {
       // Process seed...
   }
   ```

3. **Expected gain: 1.5-2x speedup** (better memory coalescing, reduced divergence)

### Phase 3: Vectorized Memory Operations
**Priority: MEDIUM - Memory bandwidth optimization**

1. **Use `double2`/`double4` for coalesced memory loads**
   ```cuda
   // Load antes array with vectorized access
   double2 antes_vec = *((double2*)&antes[lane_id * 2]);
   ```

2. **Structure of Arrays (SoA) for results**
   - Instead of array of structs, use separate arrays
   - Enables vectorized writes

3. **Expected gain: 1.2-1.5x speedup** (memory bandwidth)

### Phase 4: Warp-Level Primitives
**Priority: MEDIUM - Reduce atomic contention**

1. **Use `__ballot_sync` for result reduction**
   ```cuda
   // Count matches within warp
   unsigned int match_mask = __ballot_sync(0xFFFFFFFF, total_hits >= min_hits);
   int warp_matches = __popc(match_mask);
   
   // Only first thread in warp does atomic add
   if (lane_id == 0) {
       atomicAdd(result_count, warp_matches);
   }
   ```

2. **Use `__shfl_sync` for sharing batch suffix hash**
   - Already using shared memory, but can optimize further

3. **Expected gain: 1.1-1.3x speedup** (reduced atomics)

## Performance Targets

### Current Baseline
- **~700k seeds/sec** (single-threaded per GPU thread)
- No pseudohash caching
- Strided loop across all batches

### Target Performance (Combined Optimizations)
- **Phase 1 (Caching):** 1.4M - 2.1M seeds/sec (2-3x)
- **Phase 2 (Warp):** 2.1M - 4.2M seeds/sec (1.5-2x additional)
- **Phase 3 (Vectorized):** 2.5M - 6.3M seeds/sec (1.2-1.5x additional)
- **Phase 4 (Warp primitives):** 2.8M - 8.2M seeds/sec (1.1-1.3x additional)

### **Total Expected: 4-12x speedup = 2.8M - 8.4M seeds/sec**

### Motely Equivalent
- Motely: ~8 lanes × CPU core speed
- CUDA: 32 threads/warp × GPU speed (much faster per thread)
- **CUDA should exceed Motely's throughput** due to GPU parallelism

## Implementation Details

### File Structure
```
balatro_rng.cuh
  - Add: pseudohash8_with_batch_prefix()
  - Add: pseudohash_prefix() (for suffix caching)

negative_tag_skipper.cu
  - Modify kernel to use cached suffix hash
  - Restructure for warp-level batch processing
  - Add left-to-right iteration within batch
  - Use warp primitives for result reduction
```

### Key Functions to Add

```cuda
// In balatro_rng.cuh or balatro_hash_cache.cuh

// Compute pseudohash of prefix (for caching)
__device__ __forceinline__ double pseudohash_prefix(
    const char* prefix, 
    int prefix_len
) {
    double num = 1.0;
    for (int i = prefix_len - 1; i >= 0; i--) {
        double term1 = (1.1239285023 / num) * (double)(unsigned char)prefix[i] * M_PI_CUDA;
        double term2 = M_PI_CUDA * (double)(i + 1);
        num = fmod(term1 + term2, 1.0);
        if (num < 0) num += 1.0;
    }
    return num;
}

// Compute full pseudohash using cached suffix
__device__ __forceinline__ double pseudohash8_with_batch_prefix(
    double cached_suffix_hash,
    int suffix_chars,
    const char* seed_full
) {
    int leftmost_chars = 8 - suffix_chars;
    double num = cached_suffix_hash;
    
    // Process leftmost chars from rightmost to leftmost (matching pseudohash order)
    for (int i = leftmost_chars - 1; i >= 0; i--) {
        int char_pos = suffix_chars + i;
        int char_index = char_pos + 1;
        double term1 = (1.1239285023 / num) * (double)(unsigned char)seed_full[char_pos] * M_PI_CUDA;
        double term2 = M_PI_CUDA * (double)char_index;
        num = fmod(term1 + term2, 1.0);
        if (num < 0) num += 1.0;
    }
    return num;
}
```

## Testing Strategy

1. **Benchmark each phase separately**
   - Baseline: Current code
   - Phase 1: + pseudohash caching
   - Phase 2: + warp optimization
   - Phase 3: + vectorized memory
   - Phase 4: + warp primitives

2. **Verify correctness**
   - Compare results against non-optimized version
   - Test with various `batch_chars` values (1, 2, 3, 4)
   - Test with different `--antes` combinations

3. **Profile with Nsight Compute**
   - Measure occupancy
   - Measure memory bandwidth utilization
   - Identify remaining bottlenecks

## Success Criteria

- ✅ Pseudohash caching reduces redundant computations
- ✅ Warp-level processing matches/exceeds Motely's 8-lane parallelism
- ✅ Left-to-right iteration enables incremental pseudohash
- ✅ **4-12x total speedup** (2.8M - 8.4M seeds/sec)
- ✅ No correctness regressions
- ✅ Works with all `batch_chars` values

## Notes

- **Motely's 8-lane SIMD** = 8 seeds per CPU instruction
- **CUDA's 32-thread warp** = 32 seeds per warp instruction (4x more!)
- **Key difference:** CUDA threads are independent (SIMT), not true SIMD
- **Solution:** Use warp-level parallelism + pseudohash caching to achieve similar effect

## Next Steps

1. Copy `pseudohash8_with_batch_prefix` from backup files
2. Modify `negative_tag_skipper_kernel` to use cached suffix hash
3. Test Phase 1 (caching) - verify 2-3x speedup
4. Implement Phase 2 (warp optimization)
5. Benchmark and iterate
