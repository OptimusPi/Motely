using DuckDB.NET.Data;

namespace Motely.DuckDB;

/// <summary>
/// Cross-platform DuckDB operations helper
/// </summary>
public static class DuckDBOperations
{
    /// <summary>
    /// Get row count for a table
    /// </summary>
    public static long GetRowCount(DuckDBConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    /// <summary>
    /// Check if a table exists
    /// </summary>
    public static bool TableExists(DuckDBConnection connection, string tableName)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
            cmd.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Execute a query and return results
    /// </summary>
    public static List<Dictionary<string, object?>> ExecuteQuery(
        DuckDBConnection connection,
        string sql
    )
    {
        var results = new List<Dictionary<string, object?>>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Execute a scalar query
    /// </summary>
    public static T? ExecuteScalar<T>(DuckDBConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
            return default;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Drop a table if it exists (safe operation)
    /// </summary>
    public static void DropTableIfExists(DuckDBConnection connection, string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return;

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DROP TABLE IF EXISTS {tableName}";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Ignore errors - table might not exist
        }
    }

    /// <summary>
    /// Rename a table
    /// </summary>
    public static void RenameTable(DuckDBConnection connection, string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Table names cannot be null or empty");

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {oldName} RENAME TO {newName}";
        cmd.ExecuteNonQuery();
    }
}
