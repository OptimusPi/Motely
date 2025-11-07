# Issue 001: LINQ .Any() in MotelyCompositeFilterDesc

## Severity
**High**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyCompositeFilterDesc.cs`
- Line 31: `bool isInverted = clauses.All(c => c.IsInverted);`

## Current Code
```csharp
// Check if ALL clauses in this category are inverted (mustNot)
bool isInverted = clauses.All(c => c.IsInverted);
```

## Problem
**LINQ in non-hotpath but initialization code** - While this is in `CreateFilter` (initialization), it's called frequently during filter setup. LINQ `.All()` creates an enumerator allocation and uses virtual dispatch.

## Suggested Fix
Replace with manual loop:
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
- **Allocation**: Enumerator allocation per filter category
- **Performance**: Minor - this is initialization code, not in the SIMD hotpath
- **Fix Difficulty**: Trivial - 5 line change
