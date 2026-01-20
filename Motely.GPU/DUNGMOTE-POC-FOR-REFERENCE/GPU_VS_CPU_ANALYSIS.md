# GPU vs CPU for Erratic Deck Searching

## Why GPU is Faster for Erratic Deck

### Erratic Deck Workload Characteristics

**Per-seed computation:**
1. Generate 52 cards (full deck)
2. Each card requires multiple RNG calls:
   - Rank selection (1-13)
   - Suit selection (0-3)
   - Potential resampling
3. Count ranks/suits
4. Check thresholds

**Total RNG calls per seed:** ~100-150 calls

### GPU Advantages

**1. Massive Parallelism**
- **GPU:** 10,000+ threads simultaneously (RTX 4090: 16,384 CUDA cores)
- **CPU:** 8-16 SIMD lanes (AVX512) × 8-16 cores = 64-256 parallel operations
- **GPU wins:** 40-250x more parallel units

**2. Memory Bandwidth**
- **GPU:** 900+ GB/s (RTX 4090)
- **CPU:** 50-100 GB/s (DDR5)
- **GPU wins:** 9-18x more bandwidth

**3. Compute Throughput**
- **GPU:** 83 TFLOPS (RTX 4090)
- **CPU:** 1-2 TFLOPS (high-end)
- **GPU wins:** 40-80x more compute

**4. Workload Fit**
- Erratic deck is **embarrassingly parallel** (each seed independent)
- No data dependencies between seeds
- Perfect for GPU's SIMT model

### CPU Advantages (When It Wins)

**1. Lower Latency**
- CPU: ~1-10ns per operation
- GPU: ~100-1000ns (kernel launch overhead)
- **CPU wins:** For small workloads (<1000 seeds)

**2. Better Branch Prediction**
- CPU: Sophisticated branch predictors
- GPU: Divergence hurts performance
- **CPU wins:** For complex, branch-heavy filters

**3. Cache Hierarchy**
- CPU: L1/L2/L3 caches optimized for sequential access
- GPU: Smaller caches, optimized for coalesced access
- **CPU wins:** For memory-bound, non-coalesced patterns

### When CPU is Faster

**Simple filters (e.g., single joker check):**
- CPU SIMD can process 8-16 seeds per cycle
- Low overhead
- Better for small searches (<1M seeds)

**Complex branching:**
- CPU handles divergent branches better
- GPU threads serialize on divergence

---

## CUDA vs ROCm (HIP) Comparison

### Performance (2025 Benchmarks)

**General compute workloads:**
- **CUDA:** 10-30% faster on average
- **ROCm:** Closing the gap, within 20% in most cases

**Specific to seed searching:**
- **CUDA:** ~4-5M seeds/sec (RTX 4070 SUPER)
- **ROCm (estimated):** ~3-4M seeds/sec (RX 7900 XT)
- **Difference:** ~20-25% (CUDA faster)

### Why CUDA is Faster

**1. Maturity**
- 15+ years of optimization
- Extensive compiler optimizations
- Better driver integration

**2. Hardware Integration**
- Tight NVIDIA hardware coupling
- Proprietary optimizations
- Better memory management

**3. Ecosystem**
- More libraries (cuBLAS, cuDNN, etc.)
- Better profiling tools (Nsight)
- Larger community

### Why ROCm is Competitive

**1. Open Source**
- Community-driven improvements
- No vendor lock-in
- Cross-platform (AMD/Intel GPUs)

**2. HIP Compatibility**
- CUDA-like syntax (easy porting)
- Same kernel code works on both
- Minimal code changes needed

**3. Cost**
- AMD GPUs often cheaper
- More VRAM per dollar
- Better value for some workloads

### For This Codebase

**Recommendation: Support Both**

**CUDA (NVIDIA):**
- Primary target (most users)
- Best performance
- Mature tooling

**ROCm (AMD):**
- Secondary target
- 20% performance hit acceptable
- Opens to AMD users
- Minimal code changes (HIP compatibility layer)

**Implementation:**
- Use GPU abstraction layer (see REFACTORING_PLAN.md)
- Same kernel code, different launch syntax
- Automatic detection at build time

---

## Performance Estimates

### Erratic Deck Search

| Platform | GPU/CPU | Seeds/sec | Notes |
|----------|---------|-----------|-------|
| CUDA | RTX 4090 | ~8-10M | Optimized |
| CUDA | RTX 4070 SUPER | ~4-5M | Current |
| ROCm | RX 7900 XT | ~3-4M | Estimated |
| CPU SIMD | i9-14900K | ~1-2M | AVX512 |
| CPU SIMD | High-end | ~10-50M | Simple filters only |

**Note:** CPU can be faster for very simple filters due to lower overhead.

### Other Searches

**Complex filters (multiple antes, jokers, tags):**
- GPU always wins (more compute needed)
- CPU can't keep up with complexity

**Simple prefilters:**
- CPU competitive (low overhead)
- GPU still usually faster (parallelism)

---

## Conclusion

**For erratic deck:**
- **GPU is faster** because:
  - Massive parallelism (10,000+ threads)
  - High memory bandwidth
  - Perfect workload fit (embarrassingly parallel)

**CUDA vs ROCm:**
- **CUDA is 20-30% faster** but:
  - ROCm is close enough
  - Support both for maximum compatibility
  - ROCm opens AMD market

**Best approach:**
- Support CUDA (primary)
- Support ROCm (secondary)
- Support CPU fallback (simple filters)
- Let users choose based on hardware
