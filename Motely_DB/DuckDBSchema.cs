namespace Motely.DuckDB;

/// <summary>
/// SQL schema definitions for Motely tables
/// </summary>
public static class DuckDBSchema
{
    /// <summary>
    /// Schema for seed sources table with id column for performance
    /// </summary>
    public static string SeedSourcesTableSchema()
    {
        return @"
            CREATE TABLE IF NOT EXISTS seeds (
                id BIGINT PRIMARY KEY,
                seed VARCHAR NOT NULL,
                seed_len INTEGER
            )";
    }

    /// <summary>
    /// Index schema for seed sources table
    /// </summary>
    public static string SeedSourcesIndexSchema()
    {
        return @"
            CREATE INDEX IF NOT EXISTS idx_seeds_id ON seeds(id);
            CREATE INDEX IF NOT EXISTS idx_seeds_seed ON seeds(seed);
            CREATE INDEX IF NOT EXISTS idx_seeds_len ON seeds(seed_len)";
    }

    /// <summary>
    /// Schema for fertilizer table
    /// </summary>
    public static string FertilizerTableSchema()
    {
        return @"
            CREATE TABLE IF NOT EXISTS fertilizer (
                seed VARCHAR PRIMARY KEY,
                added_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
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
}
