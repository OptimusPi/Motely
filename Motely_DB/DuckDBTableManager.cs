using DuckDB.NET.Data;

namespace Motely.DuckDB;

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
}
