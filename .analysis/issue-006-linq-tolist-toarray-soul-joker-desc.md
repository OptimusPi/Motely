# Issue 006: LINQ .ToArray() in MotelyJsonSoulJokerFilterDesc Individual Verification

## Severity
**High**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonSoulJokerFilterDesc.cs`
- Line 302: `.Select((wanted, idx) => wanted ? idx : -1).Where(x => x >= 0).ToArray()`

## Current Code
```csharp
// Line 291-304 - ConvertToGeneric helper (called during individual verification)
private static MotelyJsonConfig.MotleyJsonFilterClause ConvertToGeneric(MotelyJsonSoulJokerFilterClause clause)
{
    var effectiveAntes = new List<int>();
    for (int i = 0; i < clause.WantedAntes.Length; i++)
    {
        if (clause.WantedAntes[i])
            effectiveAntes.Add(i);
    }

    var sources = new MotelyJsonConfig.SourcesConfig
    {
        // LINQ VIOLATION!
        PackSlots = clause.WantedPackSlots?.Select((wanted, idx) => wanted ? idx : -1).Where(x => x >= 0).ToArray() ?? Array.Empty<int>(),
        RequireMega = clause.RequireMega
    };
    // ...
}
```

## Problem
**LINQ in Individual Verification Helper** - The `ConvertToGeneric()` method is called from the individual seed verification lambda in `Filter()`. This LINQ chain creates:
1. `.Select((wanted, idx) => ...)` - Lambda allocation + enumerator
2. `.Where(x => x >= 0)` - Another lambda + enumerator
3. `.ToArray()` - Final allocation + copy

This is particularly bad because it's converting a bool array (O(1) lookups) into an int array just for a data structure conversion.

## Suggested Fix
Replace with manual loop (matches the pattern used for effectiveAntes just above):
```csharp
var sources = new MotelyJsonConfig.SourcesConfig
{
    PackSlots = ConvertBoolArrayToIndices(clause.WantedPackSlots),
    RequireMega = clause.RequireMega
};

// Helper method (add to class):
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static int[] ConvertBoolArrayToIndices(bool[]? boolArray)
{
    if (boolArray == null)
        return Array.Empty<int>();

    var indices = new List<int>(boolArray.Length);
    for (int i = 0; i < boolArray.Length; i++)
    {
        if (boolArray[i])
            indices.Add(i);
    }
    return indices.ToArray();
}
```

**Better Option: Don't convert at all**
Question why this conversion is needed. If the downstream code can work with bool arrays directly, skip the conversion entirely:
```csharp
var sources = new MotelyJsonConfig.SourcesConfig
{
    PackSlots = null, // Or keep the bool array if SourcesConfig can be updated
    RequireMega = clause.RequireMega
};
```

## Impact
- **Allocation**: 3 separate allocations per individual seed verification
- **Performance**: Individual verification called millions of times - **HIGH IMPACT**
- **GC Pressure**: Significant Gen0 garbage creation
- **Fix Difficulty**: Easy - manual loop or eliminate conversion
