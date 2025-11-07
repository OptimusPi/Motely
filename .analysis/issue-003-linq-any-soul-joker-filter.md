# Issue 003: LINQ .Any() in MotelyJsonSoulJokerFilterDesc Individual Verification

## Severity
**High**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonSoulJokerFilterDesc.cs`
- Line 214: `if (clause.WantedPackSlots != null && clause.WantedPackSlots.Any(x => x))`

## Current Code
```csharp
// Inside individual seed verification lambda (line 144-288)
// Check if this pack slot is wanted
if (clause.WantedPackSlots != null && clause.WantedPackSlots.Any(x => x))
{
    if (packIndex >= clause.WantedPackSlots.Length || !clause.WantedPackSlots[packIndex])
        continue;
}
```

## Problem
**LINQ in Individual Verification Hotpath** - The `Filter()` method calls `ctx.SearchIndividualSeeds()` which invokes this lambda for **EVERY** seed that passed the vectorized pre-filter. This lambda is executed millions of times in a seed search.

`.Any(x => x)` on a bool array:
- Allocates a lambda closure
- Uses LINQ enumerator
- Should be a simple array scan

## Suggested Fix
Replace with the existing helper method `BoolArrayHasTrue()` from `MotelyJsonScoring.cs`:
```csharp
// Check if this pack slot is wanted
if (clause.WantedPackSlots != null && BoolArrayHasTrue(clause.WantedPackSlots))
{
    if (packIndex >= clause.WantedPackSlots.Length || !clause.WantedPackSlots[packIndex])
        continue;
}

// Copy helper from MotelyJsonScoring.cs if not accessible:
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static bool BoolArrayHasTrue(bool[] array)
{
    for (int i = 0; i < array.Length; i++)
    {
        if (array[i])
            return true;
    }
    return false;
}
```

## Impact
- **Allocation**: Lambda allocation for EVERY individual seed verification
- **Performance**: Individual seed verification is called millions of times - **HIGH IMPACT**
- **Throughput**: Significant reduction in seeds/second
- **Fix Difficulty**: Trivial - use existing helper method
