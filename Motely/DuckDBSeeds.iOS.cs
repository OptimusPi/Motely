// iOS-specific DuckDB implementation using DuckDB.NET.Data
#if IOS
using DuckDB.NET.Data;
using Motely.DuckDB;

namespace Motely;

/// <summary>
/// iOS implementation of DuckDBSeeds using DuckDB.NET.Data
/// </summary>
public static partial class DuckDBSeeds
{
    public static IEnumerable<string> Stream(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        using var connection = DuckDBConnectionFactory.CreateConnection(dbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT {columnName} FROM {tableName} ORDER BY LENGTH({columnName})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            yield return reader.GetString(0);
    }
}

/// <summary>
/// iOS implementation of DuckDBSeedProvider - queries DuckDB directly (in-memory, fast!)
/// Uses OFFSET/LIMIT queries with atomic counter for thread-safe parallel access
/// </summary>
public sealed partial class DuckDBSeedProvider : IMotelySeedProvider, IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly string _tableName;
    private readonly string _columnName;
    private long _currentIndex = -1; // Atomic counter for parallel access
    private bool _disposed = false;

    public int SeedCount { get; }

    /// <summary>
    /// Create provider from a DuckDB database file - queries directly from in-memory DB
    /// </summary>
    public DuckDBSeedProvider(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        _connection = DuckDBConnectionFactory.CreateConnection(dbPath);
        _tableName = tableName;
        _columnName = columnName;

        // Get count - DuckDB is in-memory, this is instant
        // Use centralized operation for consistency
        SeedCount = (int)DuckDBOperations.GetRowCount(_connection, tableName);
    }

    public ReadOnlySpan<char> NextSeed()
    {
        if (_disposed)
            return ReadOnlySpan<char>.Empty;

        // Atomically get next index - each thread gets unique offset
        long index = Interlocked.Increment(ref _currentIndex);
        
        if (index >= SeedCount)
            return ReadOnlySpan<char>.Empty;

        // Query DuckDB directly with OFFSET/LIMIT - in-memory, so this is fast!
        // Each thread queries independently, no locking needed
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT {_columnName} FROM {_tableName} ORDER BY LENGTH({_columnName}) LIMIT 1 OFFSET {index}";
        using var reader = cmd.ExecuteReader();
        
        if (reader.Read())
            return reader.GetString(0);
        
        return ReadOnlySpan<char>.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection?.Dispose();
    }
}
#endif

