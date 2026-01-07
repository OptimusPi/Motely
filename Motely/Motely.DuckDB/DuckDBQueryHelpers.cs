#if !BROWSER
using DuckDB.NET.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Motely.DuckDB;

/// <summary>
/// Centralized DuckDB query helpers for common query patterns.
/// Provides reusable methods for frequently-used queries like "get top results", "get seeds", etc.
/// This eliminates duplicate SQL across the codebase.
/// </summary>
public static class DuckDBQueryHelpers
{
    /// <summary>
    /// Get top N results from a results table, ordered by score descending.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the results table (default: "results")</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="orderByColumn">Column to order by (default: "score")</param>
    /// <returns>List of result dictionaries (column name -> value)</returns>
    public static List<Dictionary<string, object?>> GetTopResults(
        DuckDBConnection connection,
        string tableName = "results",
        int limit = 1000,
        string orderByColumn = "score")
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        var sql = $"SELECT * FROM {tableName} ORDER BY {orderByColumn} DESC LIMIT ?";
        return DuckDBOperations.ExecuteQuery(connection, sql, new DuckDBParameter(limit));
    }

    /// <summary>
    /// Get top N seed strings from a results table, ordered by score descending.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the results table (default: "results")</param>
    /// <param name="limit">Maximum number of seeds to return</param>
    /// <returns>List of seed strings</returns>
    public static List<string> GetTopSeeds(
        DuckDBConnection connection,
        string tableName = "results",
        int limit = 1000)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        var sql = "SELECT seed FROM {tableName} ORDER BY score DESC LIMIT ?";
        sql = sql.Replace("{tableName}", tableName);
        
        var results = DuckDBOperations.ExecuteQuery(connection, sql, new DuckDBParameter(limit));
        return results.Select(r => r["seed"]?.ToString() ?? string.Empty)
                     .Where(s => !string.IsNullOrEmpty(s))
                     .ToList();
    }

    /// <summary>
    /// Get all seeds from a table.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table (default: "seeds")</param>
    /// <param name="columnName">Name of the seed column (default: "seed")</param>
    /// <param name="orderBy">Optional ORDER BY clause (e.g., "ORDER BY LENGTH(seed)")</param>
    /// <returns>List of seed strings</returns>
    public static List<string> GetAllSeeds(
        DuckDBConnection connection,
        string tableName = "seeds",
        string columnName = "seed",
        string? orderBy = null)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty", nameof(columnName));

        var sql = $"SELECT {columnName} FROM {tableName}";
        if (!string.IsNullOrWhiteSpace(orderBy))
        {
            sql += $" {orderBy}";
        }

        var results = DuckDBOperations.ExecuteQuery(connection, sql);
        return results.Select(r => r[columnName]?.ToString() ?? string.Empty)
                     .Where(s => !string.IsNullOrEmpty(s))
                     .ToList();
    }

    /// <summary>
    /// Get a range of seeds by ID (for efficient batch fetching).
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the table (default: "seeds")</param>
    /// <param name="columnName">Name of the seed column (default: "seed")</param>
    /// <param name="startId">Starting ID (inclusive)</param>
    /// <param name="endId">Ending ID (exclusive)</param>
    /// <returns>List of seed strings in the range</returns>
    public static List<string> GetSeedsByIdRange(
        DuckDBConnection connection,
        string tableName = "seeds",
        string columnName = "seed",
        long startId = 0,
        long endId = 0)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name cannot be empty", nameof(columnName));

        var sql = $"SELECT {columnName} FROM {tableName} WHERE id >= ? AND id < ? ORDER BY id";
        var results = DuckDBOperations.ExecuteQuery(
            connection,
            sql,
            new DuckDBParameter(startId),
            new DuckDBParameter(endId)
        );

        return results.Select(r => r[columnName]?.ToString() ?? string.Empty)
                     .Where(s => !string.IsNullOrEmpty(s))
                     .ToList();
    }

    /// <summary>
    /// Get results with tallies as a structured list.
    /// Each result contains seed, score, and tallies array.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="tableName">Name of the results table (default: "results")</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="tallyColumnStartIndex">Index where tally columns start (default: 2, after seed and score)</param>
    /// <returns>List of result objects with Seed, Score, and Tallies</returns>
    public static List<ResultWithTallies> GetResultsWithTallies(
        DuckDBConnection connection,
        string tableName = "results",
        int limit = 1000,
        int tallyColumnStartIndex = 2)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty", nameof(tableName));

        var sql = $"SELECT * FROM {tableName} ORDER BY score DESC LIMIT ?";
        var results = DuckDBOperations.ExecuteQuery(connection, sql, new DuckDBParameter(limit));

        var structuredResults = new List<ResultWithTallies>();
        foreach (var row in results)
        {
            var seed = row["seed"]?.ToString() ?? string.Empty;
            var score = row["score"] != null ? Convert.ToInt32(row["score"]) : 0;
            var tallies = new List<int>();

            // Get all columns starting from tallyColumnStartIndex
            var columnNames = row.Keys.ToList();
            for (int i = tallyColumnStartIndex; i < columnNames.Count; i++)
            {
                var columnName = columnNames[i];
                var value = row[columnName];
                tallies.Add(value != null ? Convert.ToInt32(value) : 0);
            }

            structuredResults.Add(new ResultWithTallies
            {
                Seed = seed,
                Score = score,
                Tallies = tallies
            });
        }

        return structuredResults;
    }
}

/// <summary>
/// Represents a search result with seed, score, and tallies.
/// </summary>
public class ResultWithTallies
{
    public string Seed { get; set; } = string.Empty;
    public int Score { get; set; }
    public List<int> Tallies { get; set; } = new();
}

#endif
