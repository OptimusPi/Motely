using DuckDB.NET.Data;
using System.Collections.Generic;
using System.Linq;

namespace Motely.DuckDB;

/// <summary>
/// Helper methods for common DuckDB queries
/// </summary>
public static class DuckDBQueryHelpers
{
    /// <summary>
    /// Get all seeds from a table
    /// </summary>
    public static List<string> GetAllSeeds(DuckDBConnection connection, string tableName, string seedColumnName = "seed")
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {seedColumnName} FROM {tableName} ORDER BY {seedColumnName}";
        using var reader = cmd.ExecuteReader();
        
        var seeds = new List<string>();
        while (reader.Read())
        {
            seeds.Add(reader.GetString(0));
        }
        return seeds;
    }

    /// <summary>
    /// Get top N seeds ordered by a column
    /// </summary>
    public static List<string> GetTopSeeds(DuckDBConnection connection, string tableName, string orderBy, int limit, string seedColumnName = "seed")
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {seedColumnName} FROM {tableName} ORDER BY {orderBy} DESC LIMIT {limit}";
        using var reader = cmd.ExecuteReader();
        
        var seeds = new List<string>();
        while (reader.Read())
        {
            seeds.Add(reader.GetString(0));
        }
        return seeds;
    }

    /// <summary>
    /// Get results with tallies from a results table
    /// </summary>
    public static List<Dictionary<string, object?>> GetResultsWithTallies(DuckDBConnection connection, int offset, int limit, string orderBy = "score", bool ascending = false)
    {
        var order = ascending ? "ASC" : "DESC";
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM results ORDER BY {orderBy} {order} LIMIT {limit} OFFSET {offset}";
        using var reader = cmd.ExecuteReader();
        
        var results = new List<Dictionary<string, object?>>();
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
}
