# Issue 005: LINQ .Where() and .Count() in MotelyJsonFilterClauseTypes

## Severity
**Medium**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonFilterClauseTypes.cs`
- Line 148: `int shopCount = wantedShopSlots.Count(s => s);`
- Line 149: `int packCount = wantedPackSlots.Count(s => s);`
- Line 209-212: `.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Joker).Select(FromJsonClause).ToList();`
- Line 336-339: `.Where(c => c.ItemTypeEnum == MotelyFilterItemType.SoulJoker).Select(FromJsonClause).ToList();`
- Line 475-478, 569-573, 696-700, 833-836: Similar LINQ chains in other ConvertClauses methods
- Line 577-578: `var antesStr = string.Join(",", Enumerable.Range(0, clause.WantedAntes.Length).Where(i => clause.WantedAntes[i]));`

## Current Code
```csharp
// Line 148-149 - In FromJsonClause (called during filter initialization)
int shopCount = wantedShopSlots.Count(s => s);
int packCount = wantedPackSlots.Count(s => s);

// Line 209-212 - ConvertClauses
public static List<MotelyJsonJokerFilterClause> ConvertClauses(List<MotelyJsonConfig.MotleyJsonFilterClause> genericClauses)
{
    return genericClauses
        .Where(c => c.ItemTypeEnum == MotelyFilterItemType.Joker)
        .Select(FromJsonClause)
        .ToList();
}

// Line 577-578 - Debug logging with nested LINQ
var antesStr = string.Join(",", Enumerable.Range(0, clause.WantedAntes.Length).Where(i => clause.WantedAntes[i]));
```

## Problem
**LINQ in Initialization Code** - While not in the SIMD hotpath, these are called during filter construction which happens:
1. On every JSON config load
2. During filter pipeline setup
3. Potentially multiple times in initialization

Issues:
- `.Count(s => s)` - lambda allocation + enumeration
- `.Where().Select().ToList()` - 3 enumerator allocations + virtual dispatch chain
- `Enumerable.Range().Where()` - nested allocations for debug string building

## Suggested Fix

**For lines 148-149 (Count):**
```csharp
// Replace with manual count
int shopCount = 0;
for (int i = 0; i < wantedShopSlots.Length; i++)
    if (wantedShopSlots[i]) shopCount++;

int packCount = 0;
for (int i = 0; i < wantedPackSlots.Length; i++)
    if (wantedPackSlots[i]) packCount++;
```

**For ConvertClauses methods:**
```csharp
public static List<MotelyJsonJokerFilterClause> ConvertClauses(List<MotelyJsonConfig.MotleyJsonFilterClause> genericClauses)
{
    var result = new List<MotelyJsonJokerFilterClause>(genericClauses.Count);
    foreach (var clause in genericClauses)
    {
        if (clause.ItemTypeEnum == MotelyFilterItemType.Joker)
            result.Add(FromJsonClause(clause));
    }
    return result;
}
```

**For debug logging (line 577-578):**
```csharp
// Option 1: Manual StringBuilder
var sb = new StringBuilder();
for (int i = 0; i < clause.WantedAntes.Length; i++)
{
    if (clause.WantedAntes[i])
    {
        if (sb.Length > 0) sb.Append(',');
        sb.Append(i);
    }
}
var antesStr = sb.ToString();

// Option 2: Only build string when debugging is enabled
if (DebugLogger.IsEnabled)
{
    // ... expensive string building
}
```

## Impact
- **Allocation**: Multiple enumerator allocations during initialization
- **Performance**: Initialization overhead - **MEDIUM** impact (not in hotpath but called frequently)
- **Startup Time**: Slows down filter creation
- **Fix Difficulty**: Easy - straightforward loop replacements
