# DuckDB Input/Output Flow for BalatroSeedOracle

## Overview
BalatroSeedOracle **always uses DuckDB** in its search flow. This document explains the complete data pipeline from input to output.

## Input Flow: Seed Sources

### Input Formats
```
SeedSources/
├── *.txt          → Converted to DuckDB after loading
├── *.csv          → Converted to DuckDB after loading  
└── *.db           → Used directly for --seedsource
```

### Conversion Process
1. **User specifies seed source**: `--seedsource _Erratic_Deck__9s`
2. **System checks for**: `_Erratic_Deck__9s.db` → `_Erratic_Deck__9s.csv` → `_Erratic_Deck__9s.txt`
3. **If .txt or .csv found**: 
   - Converted to DuckDB format automatically
   - Stored as `_Erratic_Deck__9s.db` (or `.ducklake` in future)
   - Validated: seeds must be 1-8 chars, 1-9/A-Z, no 0
4. **If .db found**: Used directly (no conversion needed)

### Seed Source Schema
```sql
CREATE TABLE seeds (
    id BIGINT,           -- Required for performance (range queries)
    seed VARCHAR(8)      -- Balatro seed string
);
CREATE INDEX idx_seeds_id ON seeds(id);
```

## Search Flow: Motely Execution

### Search Execution
```
Motely.CLI / Motely.API / Motely.TUI
    ↓ Reads from
Seed Source (DuckDB .db or DuckLake)
    ↓ Searches seeds
    ↓ Finds matching seeds
    ↓ Writes results to
Output (DuckDB .db or CSV)
```

### Search Process
1. **Load seed source**: DuckDB connection to seed source database
2. **Execute search**: SIMD-optimized seed filtering
3. **Collect results**: Seeds that match filter criteria
4. **Write output**: Based on `--output-db` or `--output-csv` flags

## Output Flow: Search Results

### Output Options

#### Option 1: DuckDB Database (`--output-db`)
```bash
dotnet run --project Motely.CLI -- --jaml MyFilter --output-db results.db
```
- **Format**: DuckDB `.db` file
- **Schema**: Dynamic based on filter (seed, score, + tally columns)
- **Usage**: Loaded by BalatroSeedOracle for results datatable
- **Location**: Saved to specified path

#### Option 2: CSV File (`--output-csv`)
```bash
dotnet run --project Motely.CLI -- --jaml MyFilter --output-csv results.csv
```
- **Format**: Plain CSV file
- **Schema**: Same as DuckDB (seed, score, tallies)
- **Usage**: Import into Excel, share, etc.
- **Location**: Saved to specified path

#### Option 3: Console Output (Default)
```bash
dotnet run --project Motely.CLI -- --jaml MyFilter
```
- **Format**: CSV-style output to console
- **Usage**: Quick preview, piping to other tools
- **Color**: Tally values are colorized if terminal supports it

### Results Database Schema
```sql
CREATE TABLE results (
    seed VARCHAR PRIMARY KEY,    -- Balatro seed (unique)
    score INTEGER,                -- Filter score
    "Tally1" INTEGER,            -- Dynamic tally columns
    "Tally2" INTEGER,             -- Based on filter config
    ...
);
```

**Note**: Tally column names come from filter configuration (JAML/JSON).

## BalatroSeedOracle Integration

### Results Display Flow
```
User runs search in BalatroSeedOracle
    ↓
Motely.API executes search
    ↓
Results saved to DuckDB (--output-db)
    ↓
User opens results modal
    ↓
BalatroSeedOracle loads from DuckDB
    ↓
Displays in datatable
```

### Expected Database Location
- **Search results**: Always saved to DuckDB format
- **Location**: Configurable (default: `SearchResults/` or specified path)
- **Schema**: Matches filter configuration (dynamic columns)
- **Access**: Direct DuckDB queries for datatable display

## Current Appender Implementation

### Standard Appender (Current)
```csharp
// Motely.API/MotelySearchDatabase.cs
var row = _appender.CreateRow();
row.AppendValue(seed);      // VARCHAR
row.AppendValue(score);      // INTEGER
for (int i = 0; i < _tallyColumnCount; i++)
{
    row.AppendValue(tallies[i]);  // INTEGER
}
row.EndRow();
```

**Why Standard Appender?**
- ✅ Dynamic columns (tallies vary by filter)
- ✅ Simple, direct control
- ✅ Works with variable column counts

**Limitations:**
- ⚠️ Manual type management
- ⚠️ No compile-time type safety
- ⚠️ Easy to make mistakes (column order, types)

### Mapped Appender (Potential Improvement)

**Challenge**: Mapped appender requires fixed schema, but we have dynamic tally columns.

**Solution Options**:

#### Option A: Hybrid Approach
```csharp
// Fixed columns via mapped appender
public class SearchResultRow
{
    public string Seed { get; set; } = string.Empty;
    public int Score { get; set; }
    // Dynamic tallies still use standard appender
}

// Use mapped appender for seed + score
// Use standard appender for dynamic tallies
```

#### Option B: Dynamic Mapping
```csharp
// Create mapping at runtime based on column names
var map = new DynamicAppenderMap(columnNames);
var appender = connection.CreateAppender<SearchResultRow, DynamicAppenderMap>("results");
```

#### Option C: Keep Standard Appender
- Current approach works well
- Dynamic columns are the main requirement
- Type safety less critical (we control the code)

## Recommendation

**Keep Standard Appender** for now because:
1. ✅ Dynamic columns are essential (tallies vary by filter)
2. ✅ Current implementation is working
3. ✅ Type safety less critical (controlled codebase)
4. ✅ Simpler code (no mapping classes needed)

**Consider Mapped Appender** if:
- We standardize on a fixed schema
- We want compile-time type checking
- We add more complex data types

## Data Flow Diagram

```
┌─────────────────┐
│ Seed Sources    │
│ *.txt, *.csv    │
└────────┬────────┘
         │ Convert to DuckDB
         ↓
┌─────────────────┐
│ Seed Source DB  │
│ seeds(id, seed) │
└────────┬────────┘
         │ Read via DuckDBSeedProvider
         ↓
┌─────────────────┐
│ Motely Search   │
│ (SIMD filtering)│
└────────┬────────┘
         │ Write results
         ↓
┌─────────────────┐      ┌──────────────┐
│ Results DuckDB  │  OR   │ Results CSV  │
│ results table   │       │ Plain CSV    │
└────────┬────────┘      └──────────────┘
         │
         ↓
┌─────────────────┐
│ BalatroSeedOracle│
│ Results Datatable│
│ (Loads from DB) │
└─────────────────┘
```

## Future: DuckLake Integration

### Seed Sources → DuckLake
- Convert existing `.db` seed sources to DuckLake format
- Store on R2 for cloud distribution
- Enable multiplayer access (multiple Motely instances)

### Results → DuckLake (Optional)
- Could migrate results to DuckLake for time travel
- Enable querying historical search results
- Less critical than seed sources (results are smaller)

## References
- [DuckDB.NET Standard Appender](https://duckdb.net/docs/standard-appender.html)
- [DuckDB.NET Mapped Appender](https://duckdb.net/docs/mapped-appender.html)
- [DuckDB.NET Type Mapping](https://duckdb.net/docs/type-mapping.html)
- [DuckDB.NET Composite Types](https://duckdb.net/docs/composite-types.html)
