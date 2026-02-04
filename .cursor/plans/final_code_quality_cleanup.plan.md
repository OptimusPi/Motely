---
name: Final Code Quality Cleanup
overview: Comprehensive cleanup plan consolidating system-wide code review findings. Addresses dead code, security issues, inconsistencies, and maintainability problems across all Motely projects.
todos:
  - id: remove_dead_code
    content: Remove dead code - unused methods, commented blocks, obsolete features
    status: pending
  - id: extract_magic_numbers
    content: Extract magic numbers to named constants (52 deck size, 1024 shop slots, etc.)
    status: pending
  - id: fix_sql_safety
    content: Fix SQL injection risks - sanitize table/column names, use parameters
    status: pending
  - id: standardize_error_output
    content: Standardize error output - Console.Error for errors, consistent formats
    status: pending
  - id: remove_unused_cli_options
    content: Remove 6 unused CLI options and add missing validation
    status: pending
  - id: fix_n_plus_1_queries
    content: Fix N+1 query patterns in GenericLibrary and MotelySearchDatabase
    status: pending
  - id: refactor_long_methods
    content: Refactor overly long methods (PrintReport, ThreadMain, BatchSeeds)
    status: pending
  - id: consolidate_formatting
    content: Consolidate duplicated formatting logic in FormatUtils
    status: pending
isProject: true
---

# Final Code Quality Cleanup Plan

## Executive Summary

System-wide code review identified **150+ specific issues** across all Motely projects:


| Project                 | Issues | Critical | Medium | Low |
| ----------------------- | ------ | -------- | ------ | --- |
| Motely Core             | 47     | 6        | 15     | 26  |
| Motely.CLI              | 37     | 2        | 12     | 23  |
| Motely.DB/Orchestration | 22     | 11       | 6      | 5   |
| Motely.API              | 42     | 9        | 15     | 18  |


---

## 1. CRITICAL: Security & SQL Safety

### SQL Injection Risks (11 instances)

**Files:**

- `Motely.DB/DuckDBOperations.cs:16,29,107` - Table names in SQL
- `Motely.DB/DuckDBQueryHelpers.cs:22,43,67,92` - Column/table names in SQL
- `Motely.DB/ResultsQueryHelper.cs:77,169` - Schema names in SQL
- `Motely.DB/MotelySearchQueueDatabase.cs:155` - TimeSpan interpolation

**Fix:** Create `DuckDBSanitizer` helper:

```csharp
public static class DuckDBSanitizer
{
    private static readonly Regex ValidIdentifier = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$");
    
    public static string SanitizeIdentifier(string name)
    {
        if (!ValidIdentifier.IsMatch(name))
            throw new ArgumentException($"Invalid SQL identifier: {name}");
        return name;
    }
}
```

---

## 2. CRITICAL: Performance - N+1 Queries

### Locations:

- `Motely.DB/GenericLibrary.cs:367-377` - BulkInsertSeeds one query per seed
- `Motely.DB/MotelySearchDatabase.cs:193-198` - InsertBulk one query per result
- `Motely.DB/SequentialLibrary.cs:446-479` - InsertResult individual queries

**Fix:** Use DuckDB appender or batch VALUES:

```csharp
// Before (N queries):
foreach (var seed in seeds) { cmd.CommandText = $"INSERT..."; cmd.Execute(); }

// After (1 query):
using var appender = connection.CreateAppender("results");
foreach (var seed in seeds) { appender.CreateRow().AppendValue(seed)...; }
```

---

## 3. HIGH: Dead Code Removal

### Motely Core

- `Motely/MotelyVectorSearchContext.cs:324-399` - Commented `Fract` and `Round13` methods
- `Motely/FormatUtils.cs:175-179` - Obsolete `FormatTarotName` method
- `Motely/MotelySingleSearchContext.cs:71` - Duplicate `GetSeedString()` method
- `Motely/MotelySearch.cs:249-250` - Unused `_currentSeed` and `_seedIndex` fields

### Motely.CLI

- `Program.cs:1047-1089` - `ConvertSeedToBatch` (replaced by `SeedMath.SeedToBatchIndex`)
- `Program.cs:1114-1123` - `SaveSeedsToDuckDB` never called
- `Program.cs:1005-1041` - `GenerateAllCombinations` and `GenerateCombinationsRecursive`
- `Program.cs:1141-1142` - Legacy `_unused1` and `_unused2` parameters

### Motely.Orchestration

- `Executors/JsonSearchExecutor.cs_new_method_snippet` - Snippet file in repo

### Motely.API

- `Models/Requests.cs` - Multiple unused request models
- `SearchService.cs:125-127` - Placeholder `Task.Delay(1000)` code
- `R2ConfigurationHelper.cs:62` - Commented TODO code

---

## 4. HIGH: Unused CLI Options

**Remove from `Program.cs`:**

- Line 67-71: `--convert` option
- Line 77-81: `--csvScore` option
- Line 151-155: `--regenerate-keyword-db` option
- Line 215-219: `--dungmot` option
- Line 220-224: `--dungmot-path` option
- Line 82-86: `--time` option (default set but never used)

---

## 5. MEDIUM: Magic Numbers to Constants

### Create `MotelyConstants.cs`:

```csharp
public static class MotelyConstants
{
    public const int DECK_SIZE = 52;
    public const int MAX_SHOP_SLOT = 1024;
    public const int MAX_ANTE = 40;
    public const int REPORT_INTERVAL_MS = 2000;
    public const int MAX_SEED_WAIT_MS = 5000;
    public const int THREAD_TIMEOUT_MS = 100;
    public const int SAFE_NAME_MAX_LENGTH = 63; // PostgreSQL limit
}
```

### Locations to update:

- `MotelySearch.cs:639,759,923,1047,1071`
- `MotelyAnalyzerFilterDesc.cs:51,89,156`
- `ValueFunctionScorer.cs:63,125,142,153`
- `ErraticFinderDesc.cs:25`
- `MotelyJsonErraticRankFilterDesc.cs:42`
- `MotelyJsonConfig.cs:1924-1925`
- `MotelyJsonFilterClauseTypes.cs:108,137`
- `MotelyVectorSearchContext.cs:379-381`

---

## 6. MEDIUM: Error Output Standardization

### Pattern:

```csharp
// Errors → stderr
Console.Error.WriteLine($"❌ Error: {message}");

// Warnings → stderr
Console.Error.WriteLine($"⚠️ Warning: {message}");

// Info/Progress → stderr (when --quiet affects stdout)
Console.Error.WriteLine($"ℹ️ {message}");

// Results → stdout (for piping)
Console.WriteLine(result);
```

### Files to fix:

- `Motely.CLI/Program.cs:380-390,402,416,427,444,470,477,686-693`
- `Motely/filters/MotelyJson/MotelyJsonConfig.cs` - Already fixed in previous work

---

## 7. MEDIUM: Code Duplication

### FormatUtils consolidation:

- `FormatUtils.cs:217-249` duplicates rank/suit formatting
- `ValueFunctionScorer.cs:210-235` duplicates rank formatting
- `MotelyAnalyzerFilterDesc.cs:216-223` duplicates rank formatting

**Fix:** Consolidate into `FormatUtils.FormatPlayingCard()` and use everywhere.

### CLI keyword logic:

- `Program.cs:361-375` and `Program.cs:554-558` - duplicate seed generation
- `Program.cs:788-798` and `Program.cs:813-820` - duplicate char validation

---

## 8. MEDIUM: Long Methods to Refactor


| Method        | File                      | Lines | Recommendation                         |
| ------------- | ------------------------- | ----- | -------------------------------------- |
| `PrintReport` | MotelySearch.cs:834-963   | 129   | Split into FormatProgress, FormatStats |
| `ThreadMain`  | MotelySearch.cs:1173-1304 | 131   | Extract BatchProcessor class           |
| `BatchSeeds`  | MotelySearch.cs:1460-1601 | 141   | Split processing stages                |
| `SearchBatch` | MotelySearch.cs:1728-1885 | 157   | Extract SeedBatchHandler               |


---

## 9. LOW: Naming & Style

### Typos:

- `MotelySearch.cs:723` - "immediatly" → "immediately"

### Audit comments to resolve:

- `MotelyVectorSearchContext.cs:37,138,401,574` - AUDIT ISSUE #1, #3, #4 comments

### TODO comments:

- `MotelyRunSeedScoreDesc.cs:24` - Phase2 scoring implementation
- `VectorEnum256.cs:17` - AVX-512 optimization

---

## 10. API-Specific Issues

### Missing validation (add to endpoints):

```csharp
if (string.IsNullOrWhiteSpace(id))
    return Results.BadRequest(new { error = "ID is required" });
if (id.Length > 100)
    return Results.BadRequest(new { error = "ID too long" });
```

### Inconsistent response format - standardize:

```csharp
// Success
return Results.Ok(new { data = result, success = true });

// Error
return Results.BadRequest(new { error = message, success = false });
```

### Resource leaks:

- `SearchService.cs:65,170,238` - CancellationTokenSource not disposed

---

## Implementation Order

1. **Week 1 - Critical Security**
  - SQL injection fixes
  - N+1 query fixes
2. **Week 2 - Dead Code**
  - Remove all identified dead code
  - Remove unused CLI options
3. **Week 3 - Constants & Consistency**
  - Extract magic numbers
  - Standardize error output
4. **Week 4 - Refactoring**
  - Break up long methods
  - Consolidate duplicates

---

## Files Changed Summary


| File                                  | Changes                                                    |
| ------------------------------------- | ---------------------------------------------------------- |
| `Motely/MotelySearch.cs`              | Remove dead code, extract constants, refactor long methods |
| `Motely/FancyConsole*.cs`             | Already fixed                                              |
| `Motely/MotelyVectorSearchContext.cs` | Remove commented code, resolve audit comments              |
| `Motely/FormatUtils.cs`               | Remove obsolete, consolidate formatting                    |
| `Motely.CLI/Program.cs`               | Remove dead code, unused options, standardize errors       |
| `Motely.DB/*.cs`                      | SQL sanitization, batch queries                            |
| `Motely.API/*.cs`                     | Validation, response consistency, resource disposal        |
| NEW: `Motely/MotelyConstants.cs`      | Magic number constants                                     |
| NEW: `Motely.DB/DuckDBSanitizer.cs`   | SQL identifier validation                                  |


