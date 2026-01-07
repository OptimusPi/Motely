# Mapped Appender Analysis for Motely

## Current Implementation: Standard Appender

### Why Standard Appender is Used
```csharp
// Motely.API/MotelySearchDatabase.cs:95-121
var row = _appender.CreateRow();
row.AppendValue(seed);      // VARCHAR - fixed
row.AppendValue(score);     // INTEGER - fixed
for (int i = 0; i < _tallyColumnCount; i++)
{
    row.AppendValue(tallies[i]);  // INTEGER - DYNAMIC COUNT!
}
row.EndRow();
```

**Key Challenge**: Dynamic tally columns!
- Each filter has different tally columns
- Column count varies: 0 to N tallies
- Column names come from filter config (JAML/JSON)

## Mapped Appender Requirements

From [DuckDB.NET Mapped Appender docs](https://duckdb.net/docs/mapped-appender.html):
- ✅ **Type Safety**: Compile-time type checking
- ✅ **Automatic Validation**: Runtime type verification
- ⚠️ **Fixed Schema**: Requires fixed column mapping
- ⚠️ **Mapping Order**: Must match table column order exactly

## The Problem: Dynamic Columns

### Example Filter Configurations
```yaml
# Filter 1: 2 tallies
name: ShowmanCloudNine
tallies: ["Cloud9", "Showman"]

# Filter 2: 5 tallies  
name: SuperAI
tallies: ["Blueprint", "Brainstorm", "Cavendish", "Baron", "Mime"]

# Filter 3: 0 tallies
name: SimpleFilter
tallies: []
```

**Result Schema Varies**:
- Filter 1: `seed, score, "Cloud9", "Showman"`
- Filter 2: `seed, score, "Blueprint", "Brainstorm", "Cavendish", "Baron", "Mime"`
- Filter 3: `seed, score`

## Solution Options

### Option 1: Keep Standard Appender (Current) ✅ RECOMMENDED
**Pros**:
- ✅ Works with dynamic columns
- ✅ Simple, direct code
- ✅ No mapping classes needed
- ✅ Already working well

**Cons**:
- ⚠️ No compile-time type safety
- ⚠️ Manual type management
- ⚠️ Easy to make column order mistakes

**Verdict**: **Keep this** - dynamic columns are essential!

### Option 2: Hybrid Approach (Fixed + Dynamic)
```csharp
// Use mapped appender for fixed columns
public class SearchResultBase
{
    public string Seed { get; set; } = string.Empty;
    public int Score { get; set; }
}

public class SearchResultBaseMap : DuckDBAppenderMap<SearchResultBase>
{
    public SearchResultBaseMap()
    {
        Map(r => r.Seed);   // Column 0
        Map(r => r.Score);  // Column 1
    }
}

// Then use standard appender for dynamic tallies
var baseAppender = connection.CreateAppender<SearchResultBase, SearchResultBaseMap>("results");
baseAppender.AppendRecords(baseResults);

// For tallies, still use standard appender
var row = _appender.CreateRow();
row.AppendValue(seed);
row.AppendValue(score);
// ... append tallies manually
```

**Pros**:
- ✅ Type safety for fixed columns
- ✅ Still supports dynamic tallies

**Cons**:
- ⚠️ More complex code
- ⚠️ Two appenders needed
- ⚠️ Less benefit (only 2 fixed columns)

**Verdict**: **Not worth it** - too complex for minimal benefit

### Option 3: Runtime Dynamic Mapping
```csharp
// Create mapping class at runtime
public class DynamicSearchResultMap : DuckDBAppenderMap<SearchResult>
{
    private readonly List<string> _tallyColumns;
    
    public DynamicSearchResultMap(List<string> columnNames)
    {
        _tallyColumns = columnNames.Skip(2).ToList(); // Skip seed, score
        Map(r => r.Seed);
        Map(r => r.Score);
        // Can't map dynamic tallies - would need reflection
    }
}
```

**Problem**: Mapped appender can't handle dynamic property access easily.

**Verdict**: **Not feasible** - mapped appender requires compile-time property access

### Option 4: Dictionary-Based Approach
```csharp
// Store tallies as dictionary
public class SearchResult
{
    public string Seed { get; set; }
    public int Score { get; set; }
    public Dictionary<string, int> Tallies { get; set; }
}

// Map to fixed columns (but we don't know column names at compile time)
```

**Problem**: Still can't map dynamic columns to fixed properties.

**Verdict**: **Not feasible** - same issue as Option 3

## Type Safety Considerations

### Current Type Safety
```csharp
// We control the types:
row.AppendValue(seed);      // string → VARCHAR ✅
row.AppendValue(score);      // int → INTEGER ✅
row.AppendValue(tallies[i]); // int → INTEGER ✅
```

**Risk Level**: **Low**
- We control all the code
- Types are simple (string, int)
- Column order is deterministic (seed, score, tallies)

### Potential Issues
1. **Column Order**: Must match schema exactly
   - **Mitigation**: Schema created from same config that generates columns
2. **Type Mismatch**: Wrong type for column
   - **Mitigation**: Simple types (string, int) - hard to mess up
3. **Null Handling**: Tallies can be missing
   - **Mitigation**: Pad with 0 (already handled)

## Recommendation: Keep Standard Appender

### Why
1. **Dynamic columns are essential** - filters have different tallies
2. **Current code works well** - no bugs, good performance
3. **Type safety less critical** - simple types, controlled codebase
4. **Mapped appender doesn't help** - can't handle dynamic columns

### When to Reconsider
- If we standardize on fixed schema (unlikely)
- If we add complex types (structs, arrays, etc.)
- If we want compile-time validation (nice-to-have, not critical)

## Alternative: Improve Standard Appender Usage

### Add Helper Methods
```csharp
// In MotelySearchDatabase.cs
private void AppendRowToAppender(string seed, int score, List<int> tallies)
{
    var row = _appender.CreateRow();
    row.AppendValue(seed);
    row.AppendValue(score);
    
    // Validate and pad tallies
    int providedCount = tallies?.Count ?? 0;
    if (providedCount > _tallyColumnCount)
    {
        throw new ArgumentException($"Too many tallies: {providedCount} > {_tallyColumnCount}");
    }
    
    for (int i = 0; i < _tallyColumnCount; i++)
    {
        int value = (tallies != null && i < tallies.Count) ? tallies[i] : 0;
        row.AppendValue(value);
    }
    
    row.EndRow();
}
```

**Benefit**: Encapsulates appender logic, easier to maintain

## Summary

**Current Approach**: Standard Appender ✅
- Works with dynamic columns
- Simple and maintainable
- Good performance
- Type safety less critical for this use case

**Mapped Appender**: Not suitable
- Requires fixed schema
- Can't handle dynamic tally columns
- Would require complex workarounds
- Minimal benefit for this use case

**Future Consideration**: If we add complex types or want stronger type safety, we could:
1. Use mapped appender for fixed columns only
2. Keep standard appender for dynamic tallies
3. But current approach is fine!

## References
- [DuckDB.NET Standard Appender](https://duckdb.net/docs/standard-appender.html)
- [DuckDB.NET Mapped Appender](https://duckdb.net/docs/mapped-appender.html)
- [DuckDB.NET Type Mapping](https://duckdb.net/docs/type-mapping.html)
