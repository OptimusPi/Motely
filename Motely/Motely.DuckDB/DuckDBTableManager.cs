#if !BROWSER
using DuckDB.NET.Data;

namespace Motely.DuckDB;

/// <summary>
/// Centralized DuckDB table management operations.
/// Provides helpers for table creation, validation, and schema management.
/// </summary>
public static class DuckDBTableManager
{
    /// <summary>
    /// Ensure a table exists by executing the provided CREATE TABLE SQL.
    /// Uses IF NOT EXISTS to avoid errors if table already exists.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="createTableSql">CREATE TABLE SQL statement</param>
    public static void EnsureTableExists(DuckDBConnection connection, string createTableSql)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(createTableSql))
            throw new ArgumentException("CREATE TABLE SQL cannot be empty", nameof(createTableSql));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = createTableSql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Create an index on a table.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="indexSql">CREATE INDEX SQL statement</param>
    public static void CreateIndex(DuckDBConnection connection, string indexSql)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(indexSql))
            throw new ArgumentException("CREATE INDEX SQL cannot be empty", nameof(indexSql));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = indexSql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Validate that a table exists and optionally check its schema.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table to check</param>
    /// <returns>True if table exists, false otherwise</returns>
    public static bool TableExists(DuckDBConnection connection, string tableName)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT table_name FROM information_schema.tables WHERE table_name='{tableName}'";
            var result = cmd.ExecuteScalar();
            return result != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get column names for a table.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table</param>
    /// <returns>List of column names in ordinal order</returns>
    public static List<string> GetTableColumns(DuckDBConnection connection, string tableName)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        var columns = new List<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT column_name FROM information_schema.columns WHERE table_name='{tableName}' ORDER BY ordinal_position";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    /// <summary>
    /// Validate that a table's schema matches expected columns.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table</param>
    /// <param name="expectedColumns">Expected column names (in order)</param>
    /// <returns>True if schema matches, false otherwise</returns>
    public static bool ValidateTableSchema(DuckDBConnection connection, string tableName, List<string> expectedColumns)
    {
        if (expectedColumns == null || expectedColumns.Count == 0)
            return false;

        var actualColumns = GetTableColumns(connection, tableName);
        
        if (actualColumns.Count != expectedColumns.Count)
            return false;

        // DuckDB preserves quoted identifiers as-is, so compare directly
        // Note: information_schema returns column names without quotes, so we compare the actual names
        return expectedColumns.SequenceEqual(actualColumns, StringComparer.OrdinalIgnoreCase);
    }
}
#endif
