using System;
using System.Collections.Generic;
using DuckDB.NET.Data;

namespace Motely.DuckDB;

/// <summary>
/// specialized storage for seeds using DuckDB Appender for high performance
/// </summary>
public sealed class DuckDBSeedStorage : IDisposable
{
    private readonly DuckDBConnection _connection;
    private bool _disposed;

    public DuckDBSeedStorage(string dbPath)
    {
        _connection = DuckDBConnectionFactory.CreateConnection(dbPath);
        _connection.Open();
        
        // Ensure table exists
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS seeds (
                id BIGINT,
                seed VARCHAR
            );
            CREATE INDEX IF NOT EXISTS idx_seeds_id ON seeds(id);
        ";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Bulk insert seeds using the efficient DuckDB Appender
    /// </summary>
    public long BulkInsertSeeds(IEnumerable<string> seeds, long startId = 0)
    {
        using var appender = _connection.CreateAppender("seeds");
        long count = 0;
        
        foreach (var seed in seeds)
        {
            var row = appender.CreateRow();
            row.AppendValue(startId + count);
            row.AppendValue(seed);
            row.EndRow();
            count++;
        }
        
        return count;
    }
    
    /// <summary>
    /// Get the total count of seeds in the database
    /// </summary>
    public long GetSeedCount()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM seeds";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }
    
    /// <summary>
    /// Clear all seeds from the database
    /// </summary>
    public void ClearSeeds()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM seeds";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection?.Dispose();
    }
}
