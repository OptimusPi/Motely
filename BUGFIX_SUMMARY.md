# Bug Fix Summary - ErraticRank/ErraticSuit Tally Issue

**Date**: 2026-03-18  
**Reporter**: pi  
**Status**: Fixed

## Problem

The tally columns for `ErraticRank` and `ErraticSuit` clauses were showing impossible values (>52) for a 52-card deck.

### Example Output (Before Fix)
```
SEED,SCORE,"Label","Label","Label","Label","Label","Label","Label","Label"
6TUSFKK7,129,24,104,0,0,0,1
67IMATKS,135,24,80,2,0,0,1
IMFCKT67,149,56,48,3,0,0,0
```

Tally values like **104**, **80**, **72** are impossible for rank counts in a 52-card deck.

## Root Cause

**File**: `Motely/filters/Jaml/JamlScoring.cs`

### Buggy Code (Lines 431-439, 441-449)

```csharp
private static int CountErraticRankOccurrences(ref MotelySingleSearchContext ctx, ErraticRankClause clause)
{
    int count = 0;
    var stream = ctx.CreateErraticDeckPrngStream();
    for (int i = 0; i < 52; i++)
        if (ctx.GetNextErraticDeckCard(ref stream).PlayingCardRank == clause.Rank)
            count++;
    return count * Math.Max(1, clause.Antes.Length);  // ❌ BUG: Multiplying by antes count
}

private static int CountErraticSuitOccurrences(ref MotelySingleSearchContext ctx, ErraticSuitClause clause)
{
    int count = 0;
    var stream = ctx.CreateErraticDeckPrngStream();
    for (int i = 0; i < 52; i++)
        if (ctx.GetNextErraticDeckCard(ref stream).PlayingCardSuit == clause.Suit)
            count++;
    return count * Math.Max(1, clause.Antes.Length);  // ❌ BUG: Multiplying by antes count
}
```

### Why This Was Wrong

The tally was being **multiplied by the number of antes** specified in the clause:
- If a clause had no `antes` array, `Antes.Length = 0` → `Math.Max(1, 0) = 1` (still wrong logic)
- If a clause had `antes: [1,2,3,4]`, the count would be multiplied by 4

For example:
- Deck has **26 Sevens**
- Clause has 4 antes
- Tally shows: `26 * 4 = 104` ❌

This multiplication logic might have been copied from event-based clauses (like joker/tarot occurrences across multiple antes), but it **doesn't make sense for static deck composition** like Erratic rank/suit counts.

## Fix

**Changed Lines**: 438, 448

### Fixed Code

```csharp
private static int CountErraticRankOccurrences(ref MotelySingleSearchContext ctx, ErraticRankClause clause)
{
    int count = 0;
    var stream = ctx.CreateErraticDeckPrngStream();
    for (int i = 0; i < 52; i++)
        if (ctx.GetNextErraticDeckCard(ref stream).PlayingCardRank == clause.Rank)
            count++;
    return count;  // ✅ Return raw count (0-52 range)
}

private static int CountErraticSuitOccurrences(ref MotelySingleSearchContext ctx, ErraticSuitClause clause)
{
    int count = 0;
    var stream = ctx.CreateErraticDeckPrngStream();
    for (int i = 0; i < 52; i++)
        if (ctx.GetNextErraticDeckCard(ref stream).PlayingCardSuit == clause.Suit)
            count++;
    return count;  // ✅ Return raw count (0-52 range)
}
```

## Verification

### Other Clause Types Checked

Audited all `Count*Occurrences` methods in `JamlScoring.cs`:
- ✅ `CountJokerOccurrences` - correct (counts across antes/sources)
- ✅ `CountTarotCardOccurrences` - correct (counts across antes/sources)
- ✅ `CountSpectralCardOccurrences` - correct (counts across antes/sources)
- ✅ `CountPlanetCardOccurrences` - correct (counts across antes/sources)
- ✅ `CountVoucherOccurrences` - correct (counts across antes)
- ✅ `CountTagOccurrences` - correct (counts across antes)
- ✅ `CountBossOccurrences` - correct (counts across antes)
- ✅ `CountStandardCardOccurrences` - correct (counts across antes/sources)
- ✅ `CountErraticCardOccurrences` - **already correct** (returns raw count)
- ✅ `CountLuckyMoneyOccurrences` - correct (counts specific rolls)
- ✅ `CountLuckyMultOccurrences` - correct (counts specific rolls)
- ✅ `CountMisprintMultOccurrences` - correct (counts specific rolls)
- ✅ `CountWheelOfFortuneOccurrences` - correct (counts specific rolls)
- ✅ `CountCavendishExtinctOccurrences` - correct (counts specific rolls)
- ✅ `CountGrosMichelExtinctOccurrences` - correct (counts specific rolls)
- ✅ `CountLegendaryJokerOccurrences` - correct (counts across antes)
- ✅ `CountStartingDrawOccurrences` - correct (counts across antes)

**No other similar bugs found.**

## Impact

- **Scope**: Only affects `ErraticRank` and `ErraticSuit` clause tallies
- **Severity**: High (produces incorrect/misleading tally values)
- **Filters affected**: Any filter using `erraticRank` or `erraticSuit` clauses
- **Scoring**: Not affected (scoring logic was correct, only tally display was wrong)

## Testing

After rebuild, tally values should now show correct rank/suit counts in the 0-52 range for Erratic deck filters.

### Example Filter (Six/Seven)
```yaml
should:
  - erraticRank: Six
    score: 20
  - erraticRank: Seven
    score: 20
```

Expected tally range: 0-52 for each rank count.

## Related Files

- `x:\JammySeedFinder\src\MotelyJAML\Motely\filters\Jaml\JamlScoring.cs` (fixed)
- `x:\JammySeedFinder\src\MotelyJAML\Motely\filters\Jaml\ErraticRankFilterDesc.cs` (filter logic - no changes needed)
- `x:\JammySeedFinder\src\MotelyJAML\Motely\filters\Jaml\ErraticSuitFilterDesc.cs` (filter logic - no changes needed)
- `x:\JAMMY\data\filters\six-seven.jaml` (test filter)
- `x:\JAMMY\data\filters\walkie-talkie.jaml` (test filter)
