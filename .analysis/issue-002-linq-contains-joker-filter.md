# Issue 002: LINQ .Contains() in MotelyJsonJokerFilterDesc Hotpath

## Severity
**Critical**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonJokerFilterDesc.cs`
- Line 182: `if (clause.EffectiveAntes != null && !clause.EffectiveAntes.Contains(ante))`
- Line 501: `return clause.JokerTypes.Contains(joker);`

## Current Code
```csharp
// Line 182 - Inside early exit check (not AggressiveInlining but called frequently)
bool hasAntesRemaining = false;
for (int futureAnte = ante + 1; futureAnte <= _maxAnte; futureAnte++)
{
    ulong futureBit = 1UL << (futureAnte - 1);
    if (!clause.WantedAntes.Any(x => x) || clause.WantedAntes[futureAnte])
    {
        hasAntesRemaining = true;
        break;
    }
}

// Line 501 - Inside CheckJokerTypeMatch helper
if (clause.JokerTypes?.Count > 0)
{
    return clause.JokerTypes.Contains(joker);
}
```

## Problem
**LINQ in SIMD hotpath** - Both `.Contains()` and `.Any()` are used in methods that are called during the Filter() operation:
1. Line 182: `.Any(x => x)` inside early exit check - allocates lambda
2. Line 501: `.Contains()` inside CheckJokerTypeMatch - O(n) enumeration instead of O(1) lookup

The `Filter()` method at line 41 has `[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]` - this is a **SIMD HOTPATH**.

## Suggested Fix

**For line 182 (.Any() on bool array):**
```csharp
bool hasAntesRemaining = false;
for (int futureAnte = ante + 1; futureAnte <= _maxAnte; futureAnte++)
{
    if (futureAnte < clause.WantedAntes.Length && clause.WantedAntes[futureAnte])
    {
        hasAntesRemaining = true;
        break;
    }
}
```

**For line 501 (.Contains() on List<MotelyJoker>):**
```csharp
// Option 1: Manual loop
if (clause.JokerTypes?.Count > 0)
{
    foreach (var jType in clause.JokerTypes)
    {
        if (jType == joker)
            return true;
    }
    return false;
}

// Option 2: Pre-compute HashSet in clause construction
// In MotelyJsonJokerFilterClause:
public HashSet<MotelyJoker>? JokerTypesSet { get; init; }
// Then use: return clause.JokerTypesSet.Contains(joker);
```

## Impact
- **Allocation**: Lambda allocation on every early exit check (line 182)
- **Performance**: LINQ enumeration overhead in SIMD hotpath - **CRITICAL**
- **Cache Impact**: Poor code locality due to virtual dispatch
- **Fix Difficulty**: Easy - replace with manual loops or HashSet
