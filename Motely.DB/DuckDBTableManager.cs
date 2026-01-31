using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Helper for managing DuckDB tables
/// </summary>
public static class DuckDBTableManager
{
    /// <summary>
    /// Create an index using the provided schema SQL
    /// </summary>
    public static void CreateIndex(DuckDBConnection connection, string indexSchema)
    {
        if (string.IsNullOrWhiteSpace(indexSchema))
            return;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = indexSchema;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Ensure a table exists using the provided schema SQL
    /// </summary>
    public static void EnsureTableExists(DuckDBConnection connection, string tableSchema)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = tableSchema;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Apply partitioning to a table using the provided partition schema SQL
    /// </summary>
    public static void ApplyPartitioning(DuckDBConnection connection, string partitionSchema)
    {
        if (string.IsNullOrWhiteSpace(partitionSchema))
            return;

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = partitionSchema;
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Partitioning might not be supported or already applied - ignore
        }
    }
}
