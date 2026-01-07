# BSO DuckDB Migration: Use Motely as Source of Truth

## 🚨 CRITICAL ARCHITECTURE PRINCIPLE

**Motely is the SINGLE SOURCE OF TRUTH for all DuckDB operations.** BalatroSeedOracle (BSO) must use Motely's centralized APIs instead of executing SQL directly.

## 📋 Current Problem

BSO is currently:
- ✅ Using `DuckDBSchema.ResultsTableSchema()` (GOOD - this is correct)
- ❌ Executing SQL directly for indexes, queries, inserts (BAD - should use Motely APIs)
- ❌ Creating connections with `new DuckDBConnection()` (BAD - should use `DuckDBConnectionFactory`)
- ❌ Writing inline `SELECT COUNT(*)` (BAD - should use `DuckDBOperations.GetRowCount()`)
- ❌ Writing inline `SELECT * FROM results ORDER BY score DESC LIMIT ?` (BAD - should use `DuckDBQueryHelpers.GetTopResults()`)

## ✅ What Motely Provides (Use These!)

### 1. Connection Management

**❌ DON'T:**
```csharp
var connection = new DuckDBConnection($"Data Source={dbPath}");
connection.Open();
```

**✅ DO:**
```csharp
using Motely.DuckDB;
var connection = DuckDBConnectionFactory.CreateConnection(dbPath);
// Connection is already opened
```

**Location:** `Motely/Motely.DuckDB/DuckDBConnectionFactory.cs`

---

### 2. Schema Definitions

**✅ Already using correctly:**
```csharp
var schema = DuckDBSchema.ResultsTableSchema(columnNames);
```

**Available schemas:**
- `DuckDBSchema.ResultsTableSchema(List<string> columnNames)` - Search results table
- `DuckDBSchema.SearchStateTableSchema()` - Search progress tracking
- `DuckDBSchema.SeedSourcesTableSchema()` - Seed input sources
- `DuckDBSchema.SeedSourcesIndexSchema()` - Index for seed sources
- `DuckDBSchema.FertilizerTableSchema()` - Composted seeds table
- `DuckDBSchema.SearchQueueTableSchema()` - API search queue

**Location:** `Motely/Motely.DuckDB/DuckDBSchema.cs`

---

### 3. Table Operations

**❌ DON'T:**
```csharp
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "CREATE TABLE IF NOT EXISTS results (...)";
    cmd.ExecuteNonQuery();
}
```

**✅ DO:**
```csharp
using Motely.DuckDB;
var schema = DuckDBSchema.ResultsTableSchema(columnNames);
DuckDBTableManager.EnsureTableExists(connection, schema);
```

**Available methods:**
- `DuckDBTableManager.EnsureTableExists(connection, createTableSql)` - Create table if not exists
- `DuckDBTableManager.CreateIndex(connection, indexSql)` - Create index
- `DuckDBTableManager.ValidateTable(connection, tableName, expectedColumns)` - Validate schema

**Location:** `Motely/Motely.DuckDB/DuckDBTableManager.cs`

---

### 4. Common Operations

**❌ DON'T:**
```csharp
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT COUNT(*) FROM results";
    var count = Convert.ToInt64(cmd.ExecuteScalar());
}
```

**✅ DO:**
```csharp
using Motely.DuckDB;
long count = DuckDBOperations.GetRowCount(connection, "results");
```

**Available operations:**
- `DuckDBOperations.GetRowCount(connection, tableName)` - Get row count
- `DuckDBOperations.TableExists(connection, tableName)` - Check if table exists
- `DuckDBOperations.ColumnExists(connection, tableName, columnName)` - Check if column exists
- `DuckDBOperations.AddColumnIfNotExists(connection, tableName, columnName, columnType)` - Add column
- `DuckDBOperations.DropTableIfExists(connection, tableName)` - Drop table
- `DuckDBOperations.RenameTable(connection, oldName, newName)` - Rename table
- `DuckDBOperations.ExecuteQuery(connection, sql, params)` - Execute query, returns `List<Dictionary<string, object?>>`
- `DuckDBOperations.ExecuteScalar<T>(connection, sql, params)` - Execute scalar query
- `DuckDBOperations.ExecuteNonQuery(connection, sql, params)` - Execute non-query command

**Location:** `Motely/Motely.DuckDB/DuckDBOperations.cs`

---

### 5. Query Helpers (Common Query Patterns)

**❌ DON'T:**
```csharp
using (var cmd = connection.CreateCommand())
{
    cmd.CommandText = "SELECT * FROM results ORDER BY score DESC LIMIT ?";
    cmd.Parameters.Add(new DuckDBParameter(limit));
    // ... execute and parse results manually
}
```

**✅ DO:**
```csharp
using Motely.DuckDB;
var results = DuckDBQueryHelpers.GetTopResults(connection, "results", limit: 1000);
// Returns List<Dictionary<string, object?>>
```

**Available query helpers:**
- `DuckDBQueryHelpers.GetTopResults(connection, tableName, limit, orderByColumn)` - Get top N results ordered by score
- `DuckDBQueryHelpers.GetTopSeeds(connection, tableName, limit)` - Get top N seed strings
- `DuckDBQueryHelpers.GetAllSeeds(connection, tableName, columnName, orderBy)` - Get all seeds
- `DuckDBQueryHelpers.GetSeedsByIdRange(connection, tableName, columnName, startId, endId)` - Get seeds by ID range (for batch fetching)
- `DuckDBQueryHelpers.GetResultsWithTallies(connection, tableName, limit, tallyColumnStartIndex)` - Get results with structured tallies

**Location:** `Motely/Motely.DuckDB/DuckDBQueryHelpers.cs`

---

### 6. High-Level Search Database Wrapper

**For search results specifically, use the high-level wrapper:**

**✅ DO:**
```csharp
using Motely.API;

var db = new MotelySearchDatabase(dbPath, columnNames, logCallback);
db.InsertRow(seed, score, tallies);
var topResults = db.GetTopResults(limit: 1000);
long count = db.GetResultCount();
db.Checkpoint(); // Flush appender and close
db.Dispose();
```

**Features:**
- Automatic schema creation/validation
- Thread-safe appender management
- Handles duplicate keys gracefully
- Proper error logging
- Uses all centralized APIs internally

**Location:** `Motely.API/MotelySearchDatabase.cs`

---

## 🔍 Migration Checklist

### Step 1: Replace Connection Creation
- [ ] Find all `new DuckDBConnection(...)`
- [ ] Replace with `DuckDBConnectionFactory.CreateConnection(dbPath)`
- [ ] Add `using Motely.DuckDB;`

### Step 2: Replace Table Creation
- [ ] Find all inline `CREATE TABLE` SQL
- [ ] Replace with `DuckDBTableManager.EnsureTableExists(connection, DuckDBSchema.*TableSchema())`
- [ ] Remove manual `IF NOT EXISTS` checks (handled by `EnsureTableExists`)

### Step 3: Replace Index Creation
- [ ] Find all inline `CREATE INDEX` SQL
- [ ] Replace with `DuckDBTableManager.CreateIndex(connection, DuckDBSchema.*IndexSchema())`

### Step 4: Replace Common Queries
- [ ] Find all `SELECT COUNT(*) FROM table`
- [ ] Replace with `DuckDBOperations.GetRowCount(connection, "table")`
- [ ] Find all `SELECT * FROM results ORDER BY score DESC LIMIT ?`
- [ ] Replace with `DuckDBQueryHelpers.GetTopResults(connection, "results", limit)`
- [ ] Find all table/column existence checks
- [ ] Replace with `DuckDBOperations.TableExists()` / `ColumnExists()`

### Step 5: Replace Result Insertion (if applicable)
- [ ] If BSO has its own result insertion logic, consider using `MotelySearchDatabase.InsertRow()`
- [ ] Or ensure it uses the same appender pattern as Motely

### Step 6: Verify
- [ ] Build succeeds
- [ ] All DuckDB operations go through Motely APIs
- [ ] No direct SQL execution for common operations
- [ ] Search functionality still works

---

## 📚 Reference: Motely DuckDB Architecture

**Namespace:** `Motely.DuckDB`

**Key Classes:**
1. **DuckDBConnectionFactory** - Connection creation (SINGLE SOURCE OF TRUTH)
2. **DuckDBSchema** - Schema definitions (SINGLE SOURCE OF TRUTH)
3. **DuckDBTableManager** - Table operations (create, validate, index)
4. **DuckDBOperations** - Common operations (COUNT, EXISTS, ALTER, DROP, etc.)
5. **DuckDBQueryHelpers** - Common query patterns (GetTopResults, GetSeeds, etc.)
6. **DuckDBAppenderHelpers** - Appender utilities (flush, buffer management)

**High-Level Wrapper:**
- **MotelySearchDatabase** (`Motely.API`) - Complete search database abstraction

---

## ⚠️ Important Notes

1. **Appender Buffering:** DuckDB appenders buffer data for performance. Buffered rows become visible to queries after `Checkpoint()` closes the appender. This is expected and good for performance.

2. **Thread Safety:** `MotelySearchDatabase` is thread-safe. If BSO creates its own appender, ensure proper locking.

3. **Error Handling:** Motely APIs throw exceptions on errors. Don't silently swallow them.

4. **Schema Changes:** If you need a new table schema, add it to `DuckDBSchema.cs` in Motely, don't create it inline in BSO.

5. **Browser Compatibility:** All Motely DuckDB APIs are wrapped in `#if !BROWSER` - BSO should be fine since it's desktop-only.

---

## 🎯 Goal

**After migration, BSO should have ZERO direct SQL execution for:**
- Connection creation
- Table creation
- Index creation
- Common queries (COUNT, SELECT with ORDER BY LIMIT, etc.)
- Table/column existence checks

**All DuckDB operations should go through Motely's centralized APIs.**

---

## ❓ Questions?

If you need a Motely API that doesn't exist:
1. Check if it's in `DuckDBOperations` or `DuckDBQueryHelpers` first
2. If not, consider if it should be added to Motely (for reuse across projects)
3. If it's BSO-specific, document why it can't use Motely APIs

---

**Last Updated:** Based on Motely commit history - verify against current Motely submodule
