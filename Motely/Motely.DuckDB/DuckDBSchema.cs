namespace Motely.DuckDB;

/// <summary>
/// Centralized DuckDB table schema definitions.
/// This is the SINGLE SOURCE OF TRUTH for all DuckDB table schemas.
/// All projects (Motely.API, Motely.CLI, Motely.TUI, BalatroSeedOracle) reference this.
/// 
/// RELATED CENTRALIZED DUCKDB CLASSES:
/// - DuckDBConnectionFactory: Creates and configures DuckDB connections
/// - DuckDBTableManager: Table operations (create, validate, index management)
/// - DuckDBOperations: Common operations (COUNT, EXISTS checks, ALTER, DROP, etc.)
/// - DuckDBQueryHelpers: Common query patterns (GetTopResults, GetSeeds, etc.)
/// - DuckDBAppenderHelpers: Appender utilities (flush, buffer management)
/// 
/// APPENDER BEHAVIOR:
/// - DuckDB appenders buffer data for performance (this is good!)
/// - Buffered rows become visible to queries after appender is flushed/closed
/// - For seed searching, buffering is fine - we query after Checkpoint()
/// - If you need real-time querying during appending, use a separate read connection
/// 
/// USAGE PATTERN:
/// 1. Use DuckDBConnectionFactory.CreateConnection() to create connections
/// 2. Use DuckDBSchema.*TableSchema() to get CREATE TABLE SQL
/// 3. Use DuckDBTableManager.EnsureTableExists() to create tables
/// 4. Use DuckDBOperations.* for common operations (COUNT, EXISTS, etc.)
/// 5. Use DuckDBQueryHelpers.* for common query patterns
/// 
/// NEVER:
/// - Create connections with "new DuckDBConnection()" - use DuckDBConnectionFactory
/// - Write inline CREATE TABLE SQL - use DuckDBSchema
/// - Write inline SELECT COUNT(*) - use DuckDBOperations.GetRowCount()
/// - Write inline "SELECT * FROM results ORDER BY score DESC LIMIT ?" - use DuckDBQueryHelpers.GetTopResults()
/// </summary>
public static class DuckDBSchema
{
    /// <summary>
    /// Returns CREATE TABLE SQL for SeedSources table.
    /// Purpose: Stores the pool of valid Balatro seed strings to search through.
    /// This is the INPUT source of seeds - the database that contains all seeds that will be tested against filters.
    /// 
    /// The 'id BIGINT' column is REQUIRED for performance:
    /// - Enables fast range queries (WHERE id &gt;= ? AND id &lt; ?) instead of slow OFFSET queries
    /// - Used by DuckDBSeeds.Desktop.cs for efficient batch fetching in multi-threaded searches
    /// - Without id, we'd have to use OFFSET which is O(n) and becomes extremely slow with billions of seeds
    /// - The id is assigned via ROW_NUMBER() during import to ensure consistent ordering
    /// </summary>
    public static string SeedSourcesTableSchema()
    {
        return @"
            CREATE TABLE seeds (
                id BIGINT,
                seed VARCHAR(8)
            )";
    }

    /// <summary>
    /// Returns CREATE INDEX SQL for SeedSources table.
    /// </summary>
    public static string SeedSourcesIndexSchema()
    {
        return "CREATE INDEX idx_seeds_id ON seeds(id)";
    }

    /// <summary>
    /// Returns CREATE TABLE SQL for Fertilizer table.
    /// Purpose: Stores seeds from previous search results that are no longer valid due to filter/scoring changes.
    /// Instead of discarding them, they're "composted" into fertilizer to be reused as a seed source.
    /// Seed IS the ID - no BIGINT id needed.
    /// </summary>
    public static string FertilizerTableSchema()
    {
        return @"
            CREATE TABLE seeds (
                seed VARCHAR PRIMARY KEY
            )";
    }

    /// <summary>
    /// Returns CREATE TABLE SQL for Results table with dynamic columns.
    /// Purpose: Stores search results (seed, score, tallies) when using --output-db.
    /// </summary>
    /// <param name="columnNames">Column names (must start with 'seed', 'score', then tallies)</param>
    public static string ResultsTableSchema(List<string> columnNames)
    {
        if (columnNames == null || columnNames.Count < 2)
            throw new ArgumentException("Column names must include at least seed and score", nameof(columnNames));
        if (columnNames[0] != "seed" || columnNames[1] != "score")
            throw new ArgumentException("First two columns must be 'seed' and 'score'", nameof(columnNames));

        var columnDefs = new List<string> { "seed VARCHAR PRIMARY KEY", "score INTEGER" };
        for (int i = 2; i < columnNames.Count; i++)
        {
            // DuckDB supports quoted identifiers - use original column name as-is!
            // Just escape any quotes in the name by doubling them (SQL standard)
            var columnName = columnNames[i].Replace("\"", "\"\"");
            columnDefs.Add($"\"{columnName}\" INTEGER");
        }

        return $@"
            CREATE TABLE IF NOT EXISTS results (
                {string.Join(",\n                ", columnDefs)}
            )";
    }

    /// <summary>
    /// Returns CREATE TABLE SQL for SearchState table.
    /// Purpose: Tracks search progress for resume capability.
    /// </summary>
    public static string SearchStateTableSchema()
    {
        return @"
            CREATE TABLE IF NOT EXISTS search_state (
                id INTEGER PRIMARY KEY,
                batch_size INTEGER,
                last_completed_batch BIGINT
            )";
    }

    /// <summary>
    /// Returns CREATE TABLE SQL for SearchQueue table.
    /// Purpose: Stores queued searches in the API.
    /// </summary>
    public static string SearchQueueTableSchema()
    {
        return @"
            CREATE TABLE IF NOT EXISTS SearchQueue (
                searchId TEXT PRIMARY KEY,
                jamlFilter TEXT NOT NULL,
                dateCreated TIMESTAMP NOT NULL DEFAULT (current_timestamp),
                lastAccessed TIMESTAMP NOT NULL DEFAULT (current_timestamp),
                status TEXT NOT NULL DEFAULT 'queued',
                batchMarker BIGINT DEFAULT 0,
                seedsSearched BIGINT DEFAULT 0,
                resultsFound INTEGER DEFAULT 0,
                threadCount INTEGER DEFAULT 1,
                isBurst BOOLEAN DEFAULT FALSE
            )";
    }

}
