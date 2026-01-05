// Desktop-specific DuckDB implementation using DuckDB.NET.Data
#if !BROWSER && !ANDROID && !IOS
using DuckDB.NET.Data;
using System.Collections.Generic;

namespace Motely;

/// <summary>
/// Desktop implementation of DuckDBSeeds using DuckDB.NET.Data
/// </summary>
public static partial class DuckDBSeeds
{
    public static IEnumerable<string> Stream(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        using var connection = new DuckDBConnection($"Data Source={dbPath}");
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {columnName} FROM {tableName} ORDER BY LENGTH({columnName})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            yield return reader.GetString(0);
    }
}

/// <summary>
/// Desktop implementation of DuckDBSeedProvider - queries DuckDB directly (in-memory, fast!)
/// Uses ROWID-based batch fetching for efficient multi-threaded access
/// Each thread fetches a batch of seeds at once using ID ranges (much faster than OFFSET!)
/// </summary>
public sealed partial class DuckDBSeedProvider : IMotelySeedProvider, IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly string _tableName;
    private readonly string _columnName;
    private long _currentIndex = -1; // Atomic counter for parallel access
    private bool _disposed = false;
    
    // Batch fetching: each thread fetches BATCH_SIZE seeds at once for efficiency
    private const int BATCH_SIZE = 1000; // Fetch 1000 seeds per query
    private readonly ThreadLocal<Queue<string>> _seedCache = new(() => new Queue<string>());
    private readonly ThreadLocal<long> _cacheStartIndex = new(() => -1);

    public int SeedCount { get; }

    /// <summary>
    /// Create provider from a DuckDB database file - queries directly from in-memory DB
    /// </summary>
    public DuckDBSeedProvider(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        _connection = new DuckDBConnection($"Data Source={dbPath}");
        _connection.Open();
        _tableName = tableName;
        _columnName = columnName;

        // Get count - DuckDB is in-memory, this is instant
        using var countCmd = _connection.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
        SeedCount = Convert.ToInt32(countCmd.ExecuteScalar());
    }

    /// <summary>
    /// Fetch a batch of seeds from the database using OFFSET/LIMIT (reliable and fast)
    /// </summary>
    private void FetchBatch(long startIndex)
    {
        var cache = _seedCache.Value!;
        cache.Clear();
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT {_columnName} FROM {_tableName} LIMIT {BATCH_SIZE} OFFSET {startIndex}";
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cache.Enqueue(reader.GetString(0));
        }
        
        _cacheStartIndex.Value = startIndex;
    }

    public ReadOnlySpan<char> NextSeed()
    {
        if (_disposed)
            return ReadOnlySpan<char>.Empty;

        var cache = _seedCache.Value!;
        
        // If cache is empty, fetch a new batch
        if (cache.Count == 0)
        {
            // Atomically get next batch start index - each thread gets unique range
            long batchStart = Interlocked.Add(ref _currentIndex, BATCH_SIZE) - BATCH_SIZE;
            
            // Ensure batchStart is never negative and within bounds
            if (batchStart < 0)
                batchStart = 0;
            
            if (batchStart >= SeedCount)
                return ReadOnlySpan<char>.Empty;
            
            FetchBatch(batchStart);
        }
        
        // Return next seed from cache
        if (cache.Count > 0)
        {
            return cache.Dequeue();
        }
        
        return ReadOnlySpan<char>.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _seedCache.Dispose();
        _cacheStartIndex.Dispose();
        _connection?.Dispose();
    }
}
#endif

