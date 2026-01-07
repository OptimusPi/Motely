#if !BROWSER
using DuckDB.NET.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Motely.DuckDB;

/// <summary>
/// Centralized DuckDB operations and common query patterns.
/// This is the SINGLE SOURCE OF TRUTH for DuckDB query patterns and operations.
/// Provides reusable methods for common operations like COUNT, SELECT with ORDER BY LIMIT, etc.
/// </summary>
public static class DuckDBOperations
{
    /// <summary>
    /// Get the count of rows in a table.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table</param>
    /// <returns>Number of rows in the table</returns>
    public static long GetRowCount(DuckDBConnection connection, string tableName)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
        var result = cmd.ExecuteScalar();
        return result == null ? 0 : Convert.ToInt64(result);
    }

    /// <summary>
    /// Check if a table exists.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table</param>
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
    /// Check if a column exists in a table.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table</param>
    /// <param name="columnName">Name of the column</param>
    /// <returns>True if column exists, false otherwise</returns>
    public static bool ColumnExists(DuckDBConnection connection, string tableName, string columnName)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty", nameof(columnName));

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'";
            var result = cmd.ExecuteScalar();
            return Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Add a column to a table if it doesn't exist.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table</param>
    /// <param name="columnName">Name of the column to add</param>
    /// <param name="columnType">SQL type of the column (e.g., "BIGINT", "VARCHAR")</param>
    public static void AddColumnIfNotExists(DuckDBConnection connection, string tableName, string columnName, string columnType)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty", nameof(columnName));
        if (string.IsNullOrWhiteSpace(columnType))
            throw new ArgumentException("Column type cannot be empty", nameof(columnType));

        if (!ColumnExists(connection, tableName, columnName))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Drop a table if it exists.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table to drop</param>
    public static void DropTableIfExists(DuckDBConnection connection, string tableName)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        if (TableExists(connection, tableName))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DROP TABLE {tableName}";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Rename a table.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="oldName">Current table name</param>
    /// <param name="newName">New table name</param>
    public static void RenameTable(DuckDBConnection connection, string oldName, string newName)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(oldName))
            throw new ArgumentException("Old table name cannot be empty", nameof(oldName));
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("New table name cannot be empty", nameof(newName));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {oldName} RENAME TO {newName}";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Execute a query and return results as a list of dictionaries (column name -> value).
    /// Useful for dynamic queries where column structure is unknown.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="sql">SQL query to execute</param>
    /// <param name="parameters">Optional parameters for the query</param>
    /// <returns>List of dictionaries representing rows</returns>
    public static List<Dictionary<string, object?>> ExecuteQuery(DuckDBConnection connection, string sql, params DuckDBParameter[] parameters)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL cannot be empty", nameof(sql));

        var results = new List<Dictionary<string, object?>>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                cmd.Parameters.Add(param);
            }
        }

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columnName] = value;
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Execute a scalar query and return the result.
    /// </summary>
    /// <typeparam name="T">Type of the result</typeparam>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="sql">SQL query to execute</param>
    /// <param name="parameters">Optional parameters for the query</param>
    /// <returns>The scalar result, or default(T) if null</returns>
    public static T? ExecuteScalar<T>(DuckDBConnection connection, string sql, params DuckDBParameter[] parameters)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL cannot be empty", nameof(sql));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                cmd.Parameters.Add(param);
            }
        }

        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
            return default(T);

        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Execute a non-query command (INSERT, UPDATE, DELETE, CREATE, etc.).
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="sql">SQL command to execute</param>
    /// <param name="parameters">Optional parameters for the command</param>
    /// <returns>Number of rows affected</returns>
    public static int ExecuteNonQuery(DuckDBConnection connection, string sql, params DuckDBParameter[] parameters)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL cannot be empty", nameof(sql));

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                cmd.Parameters.Add(param);
            }
        }

        return cmd.ExecuteNonQuery();
    }
}

#endif
