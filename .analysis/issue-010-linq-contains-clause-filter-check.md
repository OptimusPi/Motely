# Issue 010: LINQ .Contains() in MotelyJsonScoring Array Contains Check

## Severity
**High**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonScoring.cs`
- Line 1374: `|| !ArrayContains(nestedClause.EffectiveAntes, ante)`

## Current Code
```csharp
// Line 1368-1380 - Inside CountOccurrences AND clause nested loop
foreach (var nestedClause in clause.Clauses)
{
    // Skip if this clause doesn't apply to this ante - USE ARRAY SEARCH, NO LINQ!
    if (
        nestedClause.EffectiveAntes == null
        || !ArrayContains(nestedClause.EffectiveAntes, ante)
    )
    {
        allMatch = false;
        break;
    }
    // ...
}

// Helper method (lines 30-39):
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static bool ArrayContains(int[] array, int value)
{
    for (int i = 0; i < array.Length; i++)
    {
        if (array[i] == value)
            return true;
    }
    return false;
}
```

## Problem
**Good - No LINQ Issue Here!** - Actually, this code is CORRECT! The comment says "NO LINQ!" and indeed it uses a custom `ArrayContains` helper with a manual loop.

However, there's a performance optimization opportunity: The code searches an `int[]` array when it should use a `bool[]` array for O(1) lookup instead of O(n).

## Suggested Fix
Change from `int[] EffectiveAntes` to `bool[] WantedAntes`:
```csharp
// Instead of:
if (nestedClause.EffectiveAntes == null || !ArrayContains(nestedClause.EffectiveAntes, ante))

// Use direct bool array indexing (O(1) instead of O(n)):
if (ante >= nestedClause.WantedAntes.Length || !nestedClause.WantedAntes[ante])
```

This requires that clauses use `bool[] WantedAntes` (like other filters do) instead of `int[] EffectiveAntes`.

## Impact
- **Allocation**: None - this code is already good
- **Performance**: O(n) array scan instead of O(1) array lookup - **HIGH** impact in nested loops
- **Note**: This is NOT a LINQ violation - the code is manually optimized already
- **Fix Difficulty**: Medium - requires changing data structure from int[] to bool[]
