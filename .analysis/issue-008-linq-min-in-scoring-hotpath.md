# Issue 008: LINQ .Min() in MotelyJsonScoring AND Clause Hotpath

## Severity
**Critical**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonScoring.cs`
- Line 1343: `int minAnte = allAntes.Min();`

## Current Code
```csharp
// Line 1318-1343 - Inside CountOccurrences for AND clauses
if (clause.ItemTypeEnum == MotelyFilterItemType.And)
{
    if (clause.Clauses == null || clause.Clauses.Count == 0)
        return 0; // Empty And clause scores 0

    // Get the union of all antes from nested clauses
    var allAntes = new HashSet<int>();
    foreach (var nestedClause in clause.Clauses)
    {
        if (nestedClause.EffectiveAntes != null)
        {
            foreach (var ante in nestedClause.EffectiveAntes)
                allAntes.Add(ante);
        }
    }

    if (allAntes.Count == 0)
        return 0; // No antes to check

    // LINQ VIOLATION!
    int minAnte = allAntes.Min();
    MotelySingleJokerFixedRarityStream? sharedGlobalFaceStream = null;
    // ...
}
```

## Problem
**LINQ in Scoring Hotpath** - The `CountOccurrences` method is called from:
1. Individual seed verification (after vectorized pre-filter)
2. Scoring phase for "should" clauses

The method is marked with `AggressiveInlining` and is called millions of times. Using `.Min()` on a HashSet:
- Enumerates the entire HashSet
- Virtual dispatch overhead
- Prevents inlining

## Suggested Fix
Track min/max while building the HashSet:
```csharp
// Get the union of all antes from nested clauses
var allAntes = new HashSet<int>();
int minAnte = int.MaxValue;
int maxAnteFound = int.MinValue;

foreach (var nestedClause in clause.Clauses)
{
    if (nestedClause.EffectiveAntes != null)
    {
        foreach (var ante in nestedClause.EffectiveAntes)
        {
            allAntes.Add(ante);
            if (ante < minAnte) minAnte = ante;
            if (ante > maxAnteFound) maxAnteFound = ante;
        }
    }
}

if (allAntes.Count == 0)
    return 0; // No antes to check

// minAnte is already calculated - no LINQ needed!
MotelySingleJokerFixedRarityStream? sharedGlobalFaceStream = null;
```

## Impact
- **Allocation**: Enumerator allocation per AND clause evaluation
- **Performance**: O(n) enumeration in scoring hotpath - **CRITICAL**
- **Inlining**: Prevents method inlining due to virtual dispatch
- **Throughput**: Impacts both filtering and scoring phases
- **Fix Difficulty**: Trivial - track min/max during HashSet construction
