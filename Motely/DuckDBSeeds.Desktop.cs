// Desktop-specific DuckDB implementation using DuckDB.NET.Data
#if !BROWSER && !ANDROID && !IOS
using DuckDB.NET.Data;
using System.Collections.Generic;
using Motely.DuckDB;

namespace Motely;

/// <summary>
/// Desktop implementation of DuckDBSeeds using DuckDB.NET.Data
/// </summary>
public static partial class DuckDBSeeds
{
    public static IEnumerable<string> Stream(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        using var connection = DuckDBConnectionFactory.CreateConnection(dbPath);
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
/// 
/// NOW SUPPORTS DUCKLAKE: Auto-detects DuckLake vs .db files for concurrent access!
/// </summary>
public sealed partial class DuckDBSeedProvider : IMotelySeedProvider, IDisposable
{
    private readonly string _dbPath;
    private readonly string _tableName;
    private readonly string _columnName;
    private bool _disposed = false;

    // DuckLake support: track if this is a DuckLake or standard database
    private readonly bool _isDuckLake;
    private readonly string? _duckLakeCatalogPath;
    private readonly string? _duckLakeDataPath;
    private readonly string _duckLakeSchemaName = "seed_source";

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
    
    // Agent debug: limit invalid-seed logging to avoid huge logs
    private int _agentInvalidSeedLogs = 0;

    public int SeedCount { get; }

    private readonly bool _hasIdColumn;

    /// <summary>
    /// Create provider from a DuckDB database file or DuckLake - queries directly from in-memory DB
    /// Auto-detects DuckLake vs .db format
    /// </summary>
    public DuckDBSeedProvider(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        _dbPath = dbPath;
        _tableName = tableName;
        _columnName = columnName;

        // Auto-detect DuckLake vs legacy format
        _isDuckLake = DuckLakeHelper.IsDuckLake(dbPath);
        
        if (_isDuckLake)
        {
            // DuckLake: extract catalog and data paths
            _duckLakeCatalogPath = DuckLakeHelper.GetDuckLakeCatalogPath(dbPath);
            _duckLakeDataPath = DuckLakeHelper.GetDuckLakeDataPath(dbPath);
            
            // Use DuckLake connection
            using (var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
                _duckLakeCatalogPath, _duckLakeDataPath, _duckLakeSchemaName))
            {
                // Query from DuckLake schema
                var fullTableName = $"{_duckLakeSchemaName}.{tableName}";
                SeedCount = (int)DuckDBOperations.GetRowCount(conn, fullTableName);
                _hasIdColumn = DuckDBOperations.ColumnExists(conn, fullTableName, "id");
            }

            // Pre-build queries for DuckLake (with schema prefix)
            _rangeQuery = $"SELECT {_columnName} FROM {_duckLakeSchemaName}.{_tableName} WHERE id >= ? AND id < ? ORDER BY id";
            _rangeQueryRowId = $"SELECT {_columnName} FROM {_duckLakeSchemaName}.{_tableName} WHERE ROWID >= ? AND ROWID < ? ORDER BY ROWID";

            _threadConnection = new ThreadLocal<DuckDBConnection>(() =>
            {
                var c = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
                    _duckLakeCatalogPath!, _duckLakeDataPath!, _duckLakeSchemaName);
                using var configCmd = c.CreateCommand();
                configCmd.CommandText = "SET threads=4;";
                configCmd.ExecuteNonQuery();
                return c;
            });
        }
        else
        {
            // Standard .db file: use existing logic
            using (var conn = DuckDBConnectionFactory.CreateConnection(dbPath))
            {
                // Get count - DuckDB is in-memory, this is instant
                // Use centralized operation for consistency
                try
                {
                    SeedCount = (int)DuckDBOperations.GetRowCount(conn, tableName);
                }
                catch (DuckDBException ex) when (ex.Message.Contains("does not exist") || ex.Message.Contains("Table with name"))
                {
                    // Database is corrupted - delete it so JsonSearchExecutor can re-import
                    try
                    {
                        conn.Close();
                        conn.Dispose();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        Thread.Sleep(500);
                        if (File.Exists(dbPath))
                        {
                            File.Delete(dbPath);
                        }
                    }
                    catch { }
                    throw new InvalidOperationException(
                        $"Database {dbPath} exists but '{tableName}' table is missing. " +
                        $"Deleted corrupted database. Re-run to trigger automatic re-import."
                    );
                }

                // Check if table has 'id' column for optimized fetching
                // If not, we'll use ROWID (DuckDB's built-in row identifier)
                // Use centralized operation for consistency
                _hasIdColumn = DuckDBOperations.ColumnExists(conn, tableName, "id");
            }

            // Pre-build parameterized range queries (identifiers are fixed; bounds are parameters).
            _rangeQuery = $"SELECT {_columnName} FROM {_tableName} WHERE id >= ? AND id < ? ORDER BY id";
            _rangeQueryRowId =
                $"SELECT {_columnName} FROM {_tableName} WHERE ROWID >= ? AND ROWID < ? ORDER BY ROWID";

            _threadConnection = new ThreadLocal<DuckDBConnection>(() =>
            {
                var c = DuckDBConnectionFactory.CreateConnection(_dbPath);
                // Configure connection for optimal performance (per DuckDB docs)
                // - Limit threads per connection to avoid too many threads causing slowdowns
                // - DuckDB parallelizes within queries, so we don't need many threads per connection
                using var configCmd = c.CreateCommand();
                configCmd.CommandText = "SET threads=4;";
                configCmd.ExecuteNonQuery();
                return c;
            });
        }
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
                string raw = reader.GetString(0);
                
                // Sanitize seed (extract first field, filter invalid chars)
                string seed = SeedValidator.SanitizeSeed(raw);
                
                // Skip invalid seeds (safety net for corrupted databases)
                if (!SeedValidator.IsValidSeed(seed))
                {
                    // Log first few invalid seeds for debugging
                    if (_agentInvalidSeedLogs < 5)
                    {
                        _agentInvalidSeedLogs++;
                        AgentNdjsonLog.Log(
                            hypothesisId: "A",
                            location: "DuckDBSeeds.Desktop.cs:FetchChunk",
                            message: "provider_seed_invalid_skipped",
                            data: new
                            {
                                dbPath = _dbPath,
                                table = _tableName,
                                startIndex,
                                rawSeed = raw.Length <= 80 ? raw : raw.Substring(0, 80),
                                sanitizedSeed = seed,
                                seedLength = seed.Length,
                                hasZero = seed.IndexOf('0') >= 0,
                            }
                        );
                    }
                    continue; // Skip invalid seed
                }
                
                buffer[count++] = seed;
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

