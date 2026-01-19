namespace Motely.DuckDB;

/// <summary>
/// SQL schema definitions for Motely tables
/// </summary>
public static class DuckDBSchema
{
    /// <summary>
    /// Schema for seed sources table - seed is PRIMARY KEY
    /// </summary>
    public static string SeedSourcesTableSchema()
    {
        return @"
            CREATE TABLE IF NOT EXISTS seeds (
                seed VARCHAR PRIMARY KEY
            )";
    }

    /// <summary>
    /// Schema for fertilizer table - same as seeds (it's just seeds!)
    /// </summary>
    public static string FertilizerTableSchema()
    {
        return @"
            CREATE TABLE IF NOT EXISTS fertilizer (
                seed VARCHAR PRIMARY KEY
            )";
    }

    /// <summary>
    /// Schema for search queue table
    /// </summary>
    public static string SearchQueueTableSchema()
    {
        return @"
            CREATE TABLE IF NOT EXISTS search_queue (
                id INTEGER PRIMARY KEY,
                filter_name VARCHAR NOT NULL,
                status VARCHAR NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                started_at TIMESTAMP,
                completed_at TIMESTAMP
            )";
    }

    /// <summary>
    /// Schema for search state table (BSO-specific)
    /// </summary>
    public static string SearchStateTableSchema()
    {
        return @"
            CREATE TABLE IF NOT EXISTS search_state (
                search_id VARCHAR PRIMARY KEY,
                filter_path VARCHAR,
                deck VARCHAR,
                stake VARCHAR,
                state_json TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )";
    }
}
