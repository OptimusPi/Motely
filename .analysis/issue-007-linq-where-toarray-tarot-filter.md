# Issue 007: LINQ .Where().ToArray() in MotelyJsonTarotCardFilterDesc Individual Verification

## Severity
**High**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonTarotCardFilterDesc.cs`
- Line 703-708: LINQ chain in ConvertToGeneric helper

## Current Code
```csharp
// Line 700-721 - ConvertToGeneric helper
private static MotelyJsonConfig.MotleyJsonFilterClause ConvertToGeneric(MotelyJsonTarotFilterClause clause)
{
    var shopSlots = new List<int>();
    for (int i = 0; i < clause.WantedShopSlots.Length; i++)
        if (clause.WantedShopSlots[i]) shopSlots.Add(i);

    var packSlots = new List<int>();
    for (int i = 0; i < clause.WantedPackSlots.Length; i++)
        if (clause.WantedPackSlots[i]) packSlots.Add(i);

    return new MotelyJsonConfig.MotleyJsonFilterClause
    {
        Type = "TarotCard",
        Value = clause.TarotType?.ToString(),
        TarotEnum = clause.TarotType,
        Sources = new MotelyJsonConfig.SourcesConfig
        {
            ShopSlots = shopSlots.ToArray(),  // ALLOCATION
            PackSlots = packSlots.ToArray()   // ALLOCATION
        }
    };
}
```

## Problem
**List.ToArray() Allocations in Individual Verification** - While this code doesn't use LINQ `.Where()`, it still creates unnecessary allocations by building Lists and then converting them to arrays.

This `ConvertToGeneric()` helper is called from the individual seed verification lambda (line 170-171) which is executed for EVERY seed that passes the vectorized pre-filter.

## Suggested Fix
**Option 1: Pre-allocate and track count**
```csharp
private static MotelyJsonConfig.MotleyJsonFilterClause ConvertToGeneric(MotelyJsonTarotFilterClause clause)
{
    // Pre-allocate arrays with known max size
    int[] shopSlots = new int[clause.WantedShopSlots.Length];
    int shopCount = 0;
    for (int i = 0; i < clause.WantedShopSlots.Length; i++)
    {
        if (clause.WantedShopSlots[i])
            shopSlots[shopCount++] = i;
    }
    Array.Resize(ref shopSlots, shopCount);

    int[] packSlots = new int[clause.WantedPackSlots.Length];
    int packCount = 0;
    for (int i = 0; i < clause.WantedPackSlots.Length; i++)
    {
        if (clause.WantedPackSlots[i])
            packSlots[packCount++] = i;
    }
    Array.Resize(ref packSlots, packCount);

    return new MotelyJsonConfig.MotleyJsonFilterClause
    {
        Type = "TarotCard",
        Value = clause.TarotType?.ToString(),
        TarotEnum = clause.TarotType,
        Sources = new MotelyJsonConfig.SourcesConfig
        {
            ShopSlots = shopSlots,
            PackSlots = packSlots
        }
    };
}
```

**Option 2: Cache the result**
Since the clause doesn't change, cache the converted result:
```csharp
// In MotelyJsonTarotFilterClause class:
private MotelyJsonConfig.MotleyJsonFilterClause? _cachedGenericClause;

public MotelyJsonConfig.MotleyJsonFilterClause GetGenericClause()
{
    if (_cachedGenericClause == null)
    {
        _cachedGenericClause = ConvertToGeneric(this);
    }
    return _cachedGenericClause;
}
```

## Impact
- **Allocation**: 2x List allocations + 2x Array allocations per individual seed verification
- **Performance**: Called millions of times in individual verification - **HIGH IMPACT**
- **GC Pressure**: Significant Gen0/Gen1 garbage
- **Fix Difficulty**: Medium - Option 2 (caching) is easiest and most effective
