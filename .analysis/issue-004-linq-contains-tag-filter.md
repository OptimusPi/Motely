# Issue 004: LINQ .Contains() in MotelyJsonTagFilterDesc Hotpath

## Severity
**Critical**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonTagFilterDesc.cs`
- Line 101: `if (clause.EffectiveAntes != null && !clause.EffectiveAntes.Contains(ante))`

## Current Code
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
public VectorMask Filter(ref MotelyVectorSearchContext ctx)
{
    // ... inside ante loop ...
    for (int ante = _minAnte; ante <= _maxAnte; ante++)
    {
        // ... tag stream creation ...
        for (int clauseIndex = 0; clauseIndex < _clauses.Count; clauseIndex++)
        {
            var clause = _clauses[clauseIndex];

            // LINQ HOTPATH VIOLATION!
            if (clause.EffectiveAntes != null && !clause.EffectiveAntes.Contains(ante))
                continue;

            // ... rest of filtering logic ...
        }
    }
}
```

## Problem
**LINQ in SIMD HOTPATH with AggressiveOptimization** - This is inside the main `Filter()` method marked with `AggressiveInlining | AggressiveOptimization`. The `.Contains()` call is executed:
- For EVERY clause
- For EVERY ante
- For EVERY batch of 8 seeds

This is one of the hottest code paths in the entire application.

`EffectiveAntes` is an `int[]` array. `.Contains()` performs a linear search with virtual dispatch overhead.

## Suggested Fix
**Option 1: Use pre-computed bool array (FASTEST - RECOMMENDED)**
```csharp
// In clause definition, add:
// public bool[] WantedAntes { get; init; } = new bool[40];

// Then in hotpath:
if (!clause.WantedAntes[ante])
    continue;
```

**Option 2: Manual loop (if EffectiveAntes must stay as int[])**
```csharp
bool anteWanted = false;
for (int i = 0; i < clause.EffectiveAntes.Length; i++)
{
    if (clause.EffectiveAntes[i] == ante)
    {
        anteWanted = true;
        break;
    }
}
if (!anteWanted)
    continue;
```

## Impact
- **Allocation**: None (Contains is a struct method), but virtual dispatch overhead
- **Performance**: O(n) linear search repeated millions of times - **CRITICAL BOTTLENECK**
- **SIMD Impact**: Prevents effective vectorization and inlining
- **Throughput**: Major impact on seeds/second - this is THE hottest path
- **Fix Difficulty**: Easy - clause already appears to have WantedAntes bool array based on line 101 context

## Verification
Looking at MotelyJsonTagFilterDesc, the clause type is `MotelyJsonConfig.MotleyJsonFilterClause` which should have EffectiveAntes. Check if a bool array version exists in the clause type definition.
