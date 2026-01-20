# Erratic Search Optimization Plan

## Current Problems

### 1. **NO Caching - Computing Pseudohash from Scratch Every Time**
```cuda
// Current code (SLOW):
__device__ void init_erratic_rng(ErraticRNG* e, const char* seed, int len) {
    e->hashedSeed = pseudohash(seed, len);  // ❌ Computes full pseudohash from scratch
    // ...
}
```

**Problem:** Every seed computes `pseudohash(seed)` from scratch, even though seeds in a batch share the same **suffix** (rightmost characters).

### 2. **Wrong Terminology in Old Files**
- Old backup files say "prefix caching" ❌
- **Correct:** It's **SUFFIX caching** ✅
- **Why:** Batch index encodes **RIGHTMOST** (8 - batch_chars) characters
- All seeds in a batch share the same **suffix**, not prefix

### 3. **No Batch Processing**
- `erratic_search` processes seeds sequentially with strided loop
- No batching, no suffix caching, no optimization

## How Tavodiva/Motely Does It (SUFFIX Caching)

### Batch Structure (Motely Convention)
```
batch_chars = 3 (leftmost 3 chars vary)
Batch 0: All seeds ending in "111" (suffix fixed)
  - "11111111", "11211111", "11311111", ..., "ZZZ11111"
  - Rightmost 5 chars = "11111" (FIXED - this is the SUFFIX)
  - Leftmost 3 chars vary (111, 112, 113, ..., ZZZ)

Batch 1: All seeds ending in "112" (suffix fixed)
  - "11111112", "11211112", "11311112", ..., "ZZZ11112"
  - Rightmost 5 chars = "11112" (FIXED - this is the SUFFIX)
  - Leftmost 3 chars vary
```

### Suffix Caching (Correct Approach)
1. **Cache pseudohash of SUFFIX once per batch**
   ```cuda
   // Batch 0: suffix = "11111" (rightmost 5 chars)
   double cached_suffix_hash = pseudohash("11111", 5);
   ```

2. **For each seed in batch, incrementally add leftmost chars**
   ```cuda
   // Seed "11111111": use cached "11111" + add "111" (leftmost 3)
   // Seed "11211111": use cached "11111" + add "112" (leftmost 3)
   double seed_hash = pseudohash_with_cached_suffix(
       cached_suffix_hash,  // "11111" hash (cached)
       5,                    // suffix length
       "11111111"            // full seed
   );
   ```

3. **Why this works:**
   - Pseudohash processes **RIGHT-TO-LEFT** (position 7 → 0)
   - Suffix (rightmost chars) processed first
   - Cache the suffix hash, then incrementally add leftmost chars
   - **2-3x speedup** from avoiding redundant suffix computation

## Optimization Plan for erratic_search

### Phase 1: Add Suffix Caching (CRITICAL - 2-3x speedup)

**Add functions to `erratic_search.cu`:**

```cuda
// Compute pseudohash of suffix (for caching)
__device__ __forceinline__ double pseudohash_suffix(
    const char* suffix, 
    int suffix_len
) {
    double num = 1.0;
    for (int i = suffix_len - 1; i >= 0; i--) {
        double term1 = (1.1239285023 / num) * (double)(unsigned char)suffix[i] * M_PI_CUDA;
        double term2 = M_PI_CUDA * (double)(i + 1);
        num = lua_mod(term1 + term2, 1.0);
    }
    return num;
}

// Compute full pseudohash using cached suffix
__device__ __forceinline__ double pseudohash_with_cached_suffix(
    double cached_suffix_hash,
    int suffix_len,
    const char* seed_full,
    int seed_len
) {
    int leftmost_chars = seed_len - suffix_len;
    double num = cached_suffix_hash;
    
    // Process leftmost chars from rightmost to leftmost (matching pseudohash order)
    for (int i = leftmost_chars - 1; i >= 0; i--) {
        int char_pos = suffix_len + i;
        int char_index = char_pos + 1;
        double term1 = (1.1239285023 / num) * (double)(unsigned char)seed_full[char_pos] * M_PI_CUDA;
        double term2 = M_PI_CUDA * (double)char_index;
        num = lua_mod(term1 + term2, 1.0);
    }
    return num;
}
```

**Modify `init_erratic_rng` to accept cached suffix hash:**

```cuda
__device__ void init_erratic_rng_with_suffix(
    ErraticRNG* e, 
    const char* seed, 
    int len,
    double cached_suffix_hash,  // NEW: cached suffix hash
    int suffix_len              // NEW: suffix length
) {
    // Use cached suffix hash instead of computing from scratch
    e->hashedSeed = pseudohash_with_cached_suffix(
        cached_suffix_hash, suffix_len, seed, len
    );
    
    // For rngState, we still need full pseudohash of "erratic" + seed
    // But we can optimize this too if needed
    char combined[24]; int clen = 0;
    const char* key = "erratic";
    for (int i = 0; key[i]; i++) combined[clen++] = key[i];
    for (int i = 0; i < len; i++) combined[clen++] = seed[i];
    e->rngState = pseudohash(combined, clen);
}
```

### Phase 2: Add Batch Processing (1.5-2x speedup)

**Restructure kernel to process seeds in batches:**

```cuda
__global__ void erratic_full_search_kernel_batched(
    uint64_t start_batch_index,
    uint64_t end_batch_index,
    int batch_chars,
    int seed_len,
    int rank_threshold, int suit_threshold,
    int target_rank,
    FullResult* results, int* count, int max_results
) {
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    int warp_id = tid / 32;
    int lane_id = tid % 32;
    
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    int suffix_chars = seed_len - batch_chars;
    
    // Process one batch per warp
    for (uint64_t batch = start_batch_index + warp_id; 
         batch <= end_batch_index; 
         batch += (gridDim.x * blockDim.x) / 32) {
        
        // Compute cached suffix hash once per batch (first thread in warp)
        __shared__ double batch_suffix_hash[32];
        __shared__ char batch_suffix[32][9];
        
        if (lane_id == 0) {
            // Extract suffix from batch index (rightmost chars)
            uint64_t temp = batch;
            for (int i = suffix_chars - 1; i >= 0; i--) {
                batch_suffix[warp_id][i] = SEED_CHARS[temp % 35];
                temp /= 35;
            }
            batch_suffix[warp_id][suffix_chars] = '\0';
            
            // Cache suffix hash
            batch_suffix_hash[warp_id] = pseudohash_suffix(
                batch_suffix[warp_id], suffix_chars
            );
        }
        __syncwarp();
        
        double cached_hash = batch_suffix_hash[warp_id];
        
        // Each thread processes seeds at stride 32 (left-to-right within batch)
        for (uint64_t base = 0; base < seeds_per_batch; base += 32) {
            uint64_t local_idx = base + lane_id;
            if (local_idx >= seeds_per_batch) break;
            
            uint64_t seed_idx = local_index_to_seed_index(batch, local_idx, batch_chars);
            char seed[9];
            seed_index_to_string_varlen(seed_idx, seed_len, seed);
            
            // Use cached suffix hash
            ErraticRNG e;
            init_erratic_rng_with_suffix(&e, seed, seed_len, cached_hash, suffix_chars);
            
            DeckStats s;
            generate_erratic_deck_stats_from_rng(&e, &s);
            
            // Check thresholds...
        }
    }
}
```

### Phase 3: Optimize RNG State Computation (1.2-1.5x speedup)

The `rngState` computation (`pseudohash("erratic" + seed)`) can also be optimized:
- Cache `pseudohash("erratic")` once
- Incrementally add seed chars

### Expected Total Speedup: **4-6x**
- Phase 1 (Suffix caching): 2-3x
- Phase 2 (Batch processing): 1.5-2x  
- Phase 3 (RNG state optimization): 1.2-1.5x
- **Total: 3.6-9x speedup** (conservative: 4-6x)

## Key Points

1. **SUFFIX caching, not prefix** - Batch index encodes rightmost chars
2. **Left-to-right iteration** - Enables incremental pseudohash computation
3. **Warp-level batching** - 32 threads process one batch together
4. **Shared memory** - Cache suffix hash per warp

## Implementation Order

1. ✅ Add suffix caching functions
2. ✅ Modify `init_erratic_rng` to use cached suffix
3. ✅ Restructure kernel for batch processing
4. ✅ Test correctness
5. ✅ Benchmark speedup
