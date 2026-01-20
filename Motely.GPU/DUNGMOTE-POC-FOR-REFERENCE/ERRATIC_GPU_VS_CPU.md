# Why GPU Beats CPU SIMD for Erratic Deck

## The Key Difference: Work Per Seed

### Erratic Deck Workload

**Per seed, you must:**
1. Generate **52 cards** (full deck)
2. Each card = multiple RNG calls:
   - `next_erratic_pseudoseed()` - 1 call
   - `lua_static_randint_range()` - 1 call  
   - Potential resampling
3. Count ranks (13 values)
4. Count suits (4 values)
5. Check thresholds

**Total operations per seed:** ~100-150 operations

### CPU SIMD (Motely - AVX512)

**Strengths:**
- 8-16 seeds processed simultaneously (AVX512 lanes)
- Very fast per-operation (low latency)
- Excellent for simple filters (1-5 operations per seed)

**For erratic deck:**
- Can process 8-16 seeds in parallel
- But each seed needs 100+ operations
- **Total:** 8-16 seeds × 100 ops = 800-1600 operations in flight
- Limited by: CPU cores (8-16) × SIMD lanes (8-16) = 64-256 parallel operations max

### GPU (CUDA)

**Strengths:**
- 10,000+ threads simultaneously
- High memory bandwidth
- Optimized for many independent operations

**For erratic deck:**
- Can process 10,000+ seeds in parallel
- Each seed needs 100+ operations
- **Total:** 10,000+ seeds × 100 ops = 1,000,000+ operations in flight
- Limited by: GPU cores (thousands) × threads per core

## The Math

### CPU SIMD (Motely)
```
8 cores × 16 SIMD lanes = 128 parallel operations
Each seed = 100 operations
Throughput = 128 / 100 = ~1.28 seeds/cycle (theoretical)
Reality: ~10-50M seeds/sec for simple filters
         ~1-2M seeds/sec for erratic deck (100x more work)
```

### GPU (CUDA)
```
16,384 CUDA cores (RTX 4090)
Each core can handle 1 seed
Throughput = 16,384 seeds in parallel
Reality: ~4-5M seeds/sec for erratic deck
```

## Why GPU Wins for Erratic Deck

**1. Operation Count**
- Erratic deck: **100+ operations per seed**
- Simple filter: **1-5 operations per seed**
- GPU's parallelism advantage grows with operation count

**2. Parallelism Scale**
- CPU: 64-256 parallel operations max
- GPU: 1,000,000+ parallel operations
- **GPU has 4,000-15,000x more parallelism**

**3. Memory Bandwidth**
- CPU: 50-100 GB/s
- GPU: 900+ GB/s
- **GPU has 9-18x more bandwidth**

**4. Work Distribution**
- Erratic deck is **embarrassingly parallel**
- No dependencies between seeds
- Perfect for GPU's SIMT model

## When CPU SIMD Wins

**Simple filters (1-5 operations per seed):**
- CPU latency advantage matters
- Lower overhead
- Better branch prediction
- **CPU can be 2-5x faster** for simple checks

**Complex branching:**
- CPU handles divergence better
- GPU threads serialize on branches
- **CPU wins** for highly divergent code

## The Bottom Line

**Erratic deck = many operations per seed**
- GPU's massive parallelism (10,000+ threads) beats CPU's limited parallelism (64-256 ops)
- Even though CPU is faster per operation, GPU wins on total throughput

**Simple filters = few operations per seed**
- CPU's low latency and better branching beats GPU's overhead
- CPU SIMD can be faster for simple checks

**This is why:**
- Motely (CPU SIMD) is fast for simple filters
- GPU is faster for erratic deck (complex per-seed workload)
