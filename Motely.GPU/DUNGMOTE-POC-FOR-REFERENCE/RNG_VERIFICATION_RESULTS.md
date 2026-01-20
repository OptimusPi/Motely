# RNG Verification Results

## Test Date
2025-01-XX

## Summary
**v2 (balatro_rng_v2.cuh) is CORRECT** - v1 is missing precision rounding.

## Test Results

### Pseudohash
✓ **MATCH** - Both implementations produce identical pseudohash values.

### Random Values  
✗ **MISMATCH** - v1 produces different random values than v2.

### Warmup Iterations
✓ **MATCH** - Both produce identical results despite different iteration counts:
- v1: 10 warmup iterations
- v2: 11 warmup iterations (5x2 + 1)

The warmup count difference doesn't affect the first random value output.

## Root Cause

**v1 is missing precision rounding** that matches Balatro's Lua code:

```lua
-- Balatro's actual code:
_pseed = abs(tonumber(string.format("%.13f", (2.134453429141+_pseed*1.72431234)%1)))
```

**v1 implementation:**
```cuda
pseed = (2.134453429141 + pseed * 1.72431234);
pseed = pseed - floor(pseed);  // Missing precision rounding!
pseed = fabs(pseed);
```

**v2 implementation (CORRECT):**
```cuda
pseed = iterate_prng_state(pseed);  // Includes: round(state * 10^13) / 10^13
pseed = fabs(pseed);
```

## Recommendation

**Use v2 (balatro_rng_v2.cuh)** as the canonical RNG implementation because:
1. ✓ Matches Balatro's precision requirements
2. ✓ Has PrngStream support (essential for prefilters)
3. ✓ Produces correct random values
4. ✓ All working prefilters already use it

## Migration Plan

1. Replace `balatro_rng.cuh` with `balatro_rng_v2.cuh` content
2. Update all includes from `balatro_rng_v2.cuh` to `balatro_rng.cuh`
3. Migrate `balatro_game.cuh` and `simple_soul_search.cu` to use the new implementation
4. Remove `balatro_rng_v2.cuh` file

