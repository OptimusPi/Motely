# Issue 009: String.IsNullOrEmpty() in MotelyJsonScoring Hotpath

## Severity
**Medium**

## Location
`X:\BalatroSeedOracle\external\Motely\Motely\filters\MotelyJson\MotelyJsonScoring.cs`
- Line 1492: `!string.IsNullOrEmpty(clause.Mode)`
- Line 1542: `!string.IsNullOrEmpty(clause.Mode)`

## Current Code
```csharp
// Line 1488-1494 - Inside AND clause aggregation
// Determine aggregation mode: "Sum" or "Max" (default)
bool useSum =
    !string.IsNullOrEmpty(clause.Mode)
    && clause.Mode.Equals("Sum", StringComparison.OrdinalIgnoreCase);

// Line 1540-1543 - Inside OR clause aggregation
bool useSum =
    !string.IsNullOrEmpty(clause.Mode)
    && clause.Mode.Equals("Sum", StringComparison.OrdinalIgnoreCase);
```

## Problem
**String Operations in Scoring Hotpath** - The `CountOccurrences` method is called during individual seed verification and scoring. While `string.IsNullOrEmpty()` is relatively fast, it's still unnecessary overhead in a hotpath.

Issues:
1. Null check + empty check on every clause evaluation
2. String comparison with culture-insensitive overhead
3. Should be pre-computed or use enum instead of string

## Suggested Fix

**Option 1: Pre-compute during clause construction**
```csharp
// In clause definition (MotelyJsonConfig.MotleyJsonFilterClause):
public bool UseSumMode { get; init; }

// In clause constructor/initialization:
UseSumMode = Mode != null && Mode.Equals("Sum", StringComparison.OrdinalIgnoreCase);

// In hotpath - simple bool check:
bool useSum = clause.UseSumMode;
```

**Option 2: Use enum instead of string**
```csharp
public enum AggregationMode
{
    Max = 0,  // Default
    Sum = 1
}

public AggregationMode Mode { get; init; } = AggregationMode.Max;

// In hotpath:
bool useSum = clause.Mode == AggregationMode.Sum;
```

## Impact
- **Allocation**: None (string is interned), but method call overhead
- **Performance**: String operations in hotpath called millions of times - **MEDIUM**
- **CPU Cache**: String comparisons pollute instruction cache
- **Fix Difficulty**: Easy - pre-compute or use enum
