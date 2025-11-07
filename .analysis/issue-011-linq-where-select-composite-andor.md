# Issue 011: LINQ in MotelyCompositeFilterDesc AND/OR Filter Creation

## Severity
**Low**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyCompositeFilterDesc.cs`
- Line 31: `bool isInverted = clauses.All(c => c.IsInverted);`

## Current Code
```csharp
// Line 22-58 - CreateFilter method
foreach (var kvp in clausesByCategory)
{
    var category = kvp.Key;
    var clauses = kvp.Value;

    // Check if ALL clauses in this category are inverted (mustNot)
    bool isInverted = clauses.All(c => c.IsInverted);

    IMotelySeedFilter filter = category switch
    {
        FilterCategory.Joker => new MotelyJsonJokerFilterDesc(/* ... */).CreateFilter(ref ctx),
        // ...
    };
    filterEntries.Add((filter, isInverted));
}
```

## Problem
**LINQ in Filter Creation (Initialization Path)** - This is called during filter setup, not in the hot path. However, it's still unnecessary:
- `.All()` allocates an enumerator
- Lambda allocation
- Can be replaced with manual loop

Since this is initialization code (called once per filter setup), the impact is low.

## Suggested Fix
```csharp
// Check if ALL clauses in this category are inverted (mustNot)
bool isInverted = true;
foreach (var clause in clauses)
{
    if (!clause.IsInverted)
    {
        isInverted = false;
        break;
    }
}
```

## Impact
- **Allocation**: Lambda + enumerator allocation during filter creation
- **Performance**: Low - only called during initialization
- **Startup**: Minor impact on filter creation time
- **Fix Difficulty**: Trivial - 5 line change
