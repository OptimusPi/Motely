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
        cmd.CommandText = $"SELECT {columnName} FROM {tableName}";
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
    private readonly string _dbPath;
    private readonly string _tableName;
    private readonly string _columnName;
    private bool _disposed = false;

    // IMPORTANT: Use one DuckDB connection per thread.
    // DuckDB.NET connections are not intended to be hammered concurrently from many threads;
    // sharing a single connection can serialize work and destroy throughput.
    private readonly ThreadLocal<DuckDBConnection> _threadConnection;
    private readonly string _rangeQuery;
    private readonly string _rangeQueryRowId;
    
    // ========= SHARED CHUNK BUFFER =========
    // Provider-mode threads currently drive work via a global batch index, but they *pull seeds*
    // from the provider. If we "reserve" large per-thread chunks, we can end up reserving seeds
    // that never get consumed (and lose results) when using many threads.
    //
    // Fix: Hand out seeds by a single global seed index, backed by a shared chunk fetched from DuckDB.
    // - NextSeed() is cheap: one Interlocked.Increment + array index
    // - Chunk refills are rare (1 query per _chunkSize seeds)
    // - No per-thread reservation => no missing "final batch" with multiple threads
    private readonly object _chunkLock = new();
    private readonly int _chunkSize = 100_000;
    private long _nextSeedIndex = -1; // global seed cursor (0-based)
    private long _chunkStartIndex = 0;
    private int _chunkCount = 0;
    private string[] _chunk = Array.Empty<string>();

    public int SeedCount { get; }

    private readonly bool _hasIdColumn;

    /// <summary>
    /// Create provider from a DuckDB database file - queries directly from in-memory DB
    /// </summary>
    public DuckDBSeedProvider(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        _dbPath = dbPath;
        _tableName = tableName;
        _columnName = columnName;

        using (var conn = new DuckDBConnection($"Data Source={dbPath}"))
        {
            conn.Open();

            // Get count - DuckDB is in-memory, this is instant
            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
            SeedCount = Convert.ToInt32(countCmd.ExecuteScalar());

            // Check if table has 'id' column for optimized fetching
            // If not, we'll use ROWID (DuckDB's built-in row identifier)
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='id'";
            _hasIdColumn = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
        }

        // Pre-build parameterized range queries (identifiers are fixed; bounds are parameters).
        _rangeQuery = $"SELECT {_columnName} FROM {_tableName} WHERE id >= ? AND id < ? ORDER BY id";
        _rangeQueryRowId =
            $"SELECT {_columnName} FROM {_tableName} WHERE ROWID >= ? AND ROWID < ? ORDER BY ROWID";

        _threadConnection = new ThreadLocal<DuckDBConnection>(() =>
        {
            var c = new DuckDBConnection($"Data Source={_dbPath}");
            c.Open();
            // Configure connection for optimal performance (per DuckDB docs)
            // - Limit threads per connection to avoid too many threads causing slowdowns
            // - DuckDB parallelizes within queries, so we don't need many threads per connection
            using var configCmd = c.CreateCommand();
            configCmd.CommandText = "SET threads=4;";
            configCmd.ExecuteNonQuery();
            return c;
        });
    }

    /// <summary>
    /// Fetch a batch of seeds from the database using efficient ID ranges or OFFSET fallback
    /// </summary>
    private void FetchChunk(long startIndex)
    {
        // Check if disposed before attempting fetch
        if (_disposed)
            return;
        
        // Bounds check - don't fetch if invalid
        if (startIndex < 0 || startIndex >= SeedCount)
            return;
        
        long limit = Math.Min(_chunkSize, SeedCount - startIndex);
        if (limit <= 0)
            return;
        
        try
        {
            var conn = _threadConnection.Value!;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = _hasIdColumn ? _rangeQuery : _rangeQueryRowId;

            // id is 0-based in our schema; ROWID is 1-based in DuckDB
            long lower = _hasIdColumn ? startIndex : (startIndex + 1);
            long upperExclusive = _hasIdColumn ? (startIndex + limit) : (startIndex + limit + 1);

            cmd.Parameters.Add(new DuckDBParameter(lower));
            cmd.Parameters.Add(new DuckDBParameter(upperExclusive));
            
            using var reader = cmd.ExecuteReader();
            
            // Reuse/resize shared buffer to avoid churn.
            var buffer = _chunk;
            if (buffer.Length < (int)limit)
                buffer = new string[(int)limit];

            int count = 0;
            while (reader.Read())
            {
                if (_disposed) break; // Check again during read
                buffer[count++] = reader.GetString(0);
            }
            
            // Publish chunk
            _chunk = buffer;
            _chunkStartIndex = startIndex;
            _chunkCount = count;
        }
        catch (DuckDBException)
        {
            // Connection closed or query failed (e.g., during shutdown) - ignore
            _chunk = Array.Empty<string>();
            _chunkStartIndex = startIndex;
            _chunkCount = 0;
        }
        catch (ObjectDisposedException)
        {
            _chunk = Array.Empty<string>();
            _chunkStartIndex = startIndex;
            _chunkCount = 0;
        }
    }

    public ReadOnlySpan<char> NextSeed()
    {
        if (_disposed)
            return ReadOnlySpan<char>.Empty;

        long index = Interlocked.Increment(ref _nextSeedIndex);
        if (index < 0 || index >= SeedCount)
            return ReadOnlySpan<char>.Empty;

        // Fast path: current chunk contains this index
        long chunkStart = _chunkStartIndex;
        int chunkCount = _chunkCount;
        if (index >= chunkStart && index < chunkStart + chunkCount)
        {
            return _chunk[(int)(index - chunkStart)];
        }

        // Slow path: refill chunk (rare)
        lock (_chunkLock)
        {
            if (_disposed)
                return ReadOnlySpan<char>.Empty;

            chunkStart = _chunkStartIndex;
            chunkCount = _chunkCount;
            if (index >= chunkStart && index < chunkStart + chunkCount)
            {
                return _chunk[(int)(index - chunkStart)];
            }

            // Fetch a chunk aligned to chunkSize. Since index is global-monotonic, this usually
            // just advances forward.
            long newStart = (index / _chunkSize) * _chunkSize;
            FetchChunk(newStart);

            chunkStart = _chunkStartIndex;
            chunkCount = _chunkCount;
            if (chunkCount <= 0 || index < chunkStart || index >= chunkStart + chunkCount)
                return ReadOnlySpan<char>.Empty;

            return _chunk[(int)(index - chunkStart)];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            foreach (var c in _threadConnection.Values)
            {
                try { c?.Dispose(); }
                catch { /* ignore */ }
            }
        }
        catch
        {
            // Some runtimes may throw if Values is accessed during shutdown; ignore.
        }
        _threadConnection.Dispose();
    }
}
#endif

