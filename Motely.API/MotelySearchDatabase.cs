using DuckDB.NET.Data;
using System;
using System.IO;
using System.Linq;
using Motely.DuckDB;

namespace Motely.API;

/// <summary>
/// Clean DuckDB abstraction for search results with persistent appender.
/// Handles schema validation, persistent appender, and thread-safe operations.
/// 
/// NOTE: DuckDB appenders buffer data for performance. Buffered rows become visible
/// to queries after Checkpoint() closes the appender. This is expected behavior -
/// buffering improves insert performance significantly. For seed searching, we don't
/// need real-time querying during the search, so buffering is perfectly fine.
/// </summary>
public class MotelySearchDatabase : IDisposable
{
    private readonly string _dbPath;
    private readonly List<string> _columnNames;
    private readonly DuckDBConnection _connection; // Single connection for both appender and queries
    private DuckDBAppender? _appender;
    private readonly object _lock = new();
    private bool _disposed = false;
    private readonly Action<string>? _logCallback;
    private readonly int _tallyColumnCount;

    /// <summary>
    /// Creates a new search database with dual connections (write + read).
    /// Opens connections immediately and validates/creates schema.
    /// Creates appender immediately and keeps it open for the entire search.
    /// </summary>
    /// <param name="dbPath">Path to DuckDB database file</param>
    /// <param name="columnNames">Column schema (must start with 'seed', 'score', then tallies)</param>
    /// <param name="logCallback">Optional logging callback</param>
    public MotelySearchDatabase(string dbPath, List<string> columnNames, Action<string>? logCallback = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be empty", nameof(dbPath));
        if (columnNames == null || columnNames.Count < 2)
            throw new ArgumentException("Column names must include at least seed and score", nameof(columnNames));
        if (columnNames[0] != "seed" || columnNames[1] != "score")
            throw new ArgumentException("First two columns must be 'seed' and 'score'", nameof(columnNames));

        _dbPath = dbPath;
        _columnNames = new List<string>(columnNames);
        _logCallback = logCallback;
        _tallyColumnCount = columnNames.Count - 2; // seed + score = 2

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connection = DuckDBConnectionFactory.CreateConnection(_dbPath);

        InitializeSchema();
        
        // Create appender immediately and keep it open for the entire search!
        // Using DuckDB's standard appender API (Mapped Appender requires fixed schemas, we have dynamic columns).
        // Appender buffers data for performance - data becomes visible after Checkpoint() closes the appender.
        // This is fine for seed searching - we don't need real-time querying during the search.
        lock (_lock)
        {
            _appender = _connection.CreateAppender("results");
            _logCallback?.Invoke("[MotelySearchDatabase] Appender created and kept open for entire search (buffering enabled for performance)");
        }
    }

    public string DatabasePath => _dbPath;
    public IReadOnlyList<string> ColumnNames => _columnNames.AsReadOnly();

    /// <summary>
    /// Insert a row into the database.
    /// Thread-safe. Handles duplicate keys gracefully.
    /// NEVER silently swallows exceptions - always logs and reports errors!
    /// </summary>
    public void InsertRow(string seed, int score, List<int>? tallies = null)
    {
        if (string.IsNullOrEmpty(seed)) throw new ArgumentException("Seed cannot be empty", nameof(seed));

        lock (_lock)
        {
            ThrowIfDisposed();

            try
            {
                // Appender should already be created in constructor - but check just in case
                if (_appender == null)
                {
                    _appender = _connection.CreateAppender("results");
                    _logCallback?.Invoke("[MotelySearchDatabase] Appender created lazily (shouldn't happen)");
                }

                // Use DuckDB's standard appender API correctly:
                // 1. CreateRow() - creates a new row builder
                // 2. AppendValue() - appends values in column order
                // 3. EndRow() - finalizes the row and adds it to the buffer
                var row = _appender.CreateRow();
                row.AppendValue(seed);
                row.AppendValue(score);

                int providedTallyCount = tallies?.Count ?? 0;
                
                // Validate column count match
                if (providedTallyCount > _tallyColumnCount)
                {
                    var errorMsg = $"[CRITICAL] Column count mismatch! Expected {_tallyColumnCount} tallies, got {providedTallyCount}. Seed: {seed}, Columns: {string.Join(", ", _columnNames)}";
                    _logCallback?.Invoke(errorMsg);
                    Console.Error.WriteLine($"❌ {errorMsg}");
                    throw new InvalidOperationException(errorMsg);
                }

                // Append all tally values (pad with 0 if needed to match schema)
                for (int i = 0; i < _tallyColumnCount; i++)
                {
                    int value = (tallies != null && i < tallies.Count) ? tallies[i] : 0;
                    row.AppendValue(value);
                }

                row.EndRow();
            }
            catch (Exception ex)
            {
                // Check if it's a duplicate key (acceptable to ignore)
                bool isDuplicate = ex.Message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
                
                if (isDuplicate)
                {
                    // Duplicate is acceptable - just log it quietly
                    _logCallback?.Invoke($"⚠️ DUPLICATE SEED ignored: {seed}");
                    return;
                }

                // ANY OTHER EXCEPTION IS CRITICAL - LOG AND THROW!
                var errorDetails = $@"
[CRITICAL DATABASE ERROR] Failed to insert row!
  Seed: {seed}
  Score: {score}
  Tally Count: {tallies?.Count ?? 0}
  Expected Columns: {_columnNames.Count} ({string.Join(", ", _columnNames)})
  Exception Type: {ex.GetType().Name}
  Exception Message: {ex.Message}
  Stack Trace: {ex.StackTrace}
";
                _logCallback?.Invoke(errorDetails);
                Console.Error.WriteLine($"❌ {errorDetails}");
                
                // Re-throw so caller knows insertion failed
                throw new InvalidOperationException($"Database insert failed for seed '{seed}': {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Save current batch position for resume capability.
    /// </summary>
    public void SaveBatchPosition(long batchNumber, int batchSize)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO search_state (id, batch_size, last_completed_batch)
                VALUES (1, ?, ?)
                ON CONFLICT (id) DO UPDATE SET
                    batch_size = excluded.batch_size,
                    last_completed_batch = excluded.last_completed_batch";
            cmd.Parameters.Add(new DuckDBParameter(batchSize));
            cmd.Parameters.Add(new DuckDBParameter(batchNumber));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Get top N results ordered by score descending.
    /// 
    /// NOTE: If appender is still open, this will only return data that's been flushed.
    /// For complete results, call Checkpoint() first to close the appender and flush all buffered data.
    /// For seed searching, this is fine - we typically query after the search completes.
    /// 
    /// Uses centralized query helper for consistency.
    /// Uses the SAME connection as the appender to avoid file locking issues.
    /// </summary>
    public List<SearchResult> GetTopResults(int limit = 1000)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            // If appender is open, buffered data won't be visible yet.
            // This is fine for seed searching - we usually query after Checkpoint().
            // Uses same connection as appender to avoid DuckDB file locking issues.

            var resultsWithTallies = DuckDBQueryHelpers.GetResultsWithTallies(_connection, "results", limit, 2);
            return resultsWithTallies.Select(r => new SearchResult
            {
                Seed = r.Seed,
                Score = r.Score,
                Tallies = r.Tallies
            }).ToList();
        }
    }

    /// <summary>
    /// Get paginated results with OFFSET support.
    /// Uses the SAME connection as the appender to avoid DuckDB file locking issues.
    /// 
    /// NOTE: If appender is still open, buffered data won't be visible.
    /// </summary>
    public List<Dictionary<string, object?>> GetResultsPage(int offset, int limit, string orderByColumn = "score", bool ascending = false)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            var orderDirection = ascending ? "ASC" : "DESC";
            var sql = $"SELECT * FROM results ORDER BY {orderByColumn} {orderDirection} LIMIT ? OFFSET ?";
            return DuckDBOperations.ExecuteQuery(_connection, sql, 
                new DuckDBParameter(limit), 
                new DuckDBParameter(offset));
        }
    }

    /// <summary>
    /// Get results with custom ORDER BY column.
    /// Uses the SAME connection as the appender to avoid DuckDB file locking issues.
    /// 
    /// NOTE: If appender is still open, buffered data won't be visible.
    /// Column name should be validated by caller (whitelist against ColumnNames).
    /// </summary>
    public List<Dictionary<string, object?>> GetResultsOrderedBy(string orderByColumn, bool ascending, int limit = 1000)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            var orderDirection = ascending ? "ASC" : "DESC";
            var sql = $"SELECT * FROM results ORDER BY {orderByColumn} {orderDirection} LIMIT ?";
            return DuckDBOperations.ExecuteQuery(_connection, sql, new DuckDBParameter(limit));
        }
    }

    /// <summary>
    /// Execute a custom query using the internal connection.
    /// Uses the SAME connection as the appender to avoid DuckDB file locking issues.
    /// 
    /// WARNING: Only use for read-only queries. Do not modify data through this method.
    /// </summary>
    public List<Dictionary<string, object?>> ExecuteQuery(string sql, params DuckDBParameter[] parameters)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            return DuckDBOperations.ExecuteQuery(_connection, sql, parameters);
        }
    }

    /// <summary>
    /// Execute a scalar query using the internal connection.
    /// Uses the SAME connection as the appender to avoid DuckDB file locking issues.
    /// </summary>
    public T? ExecuteScalar<T>(string sql, params DuckDBParameter[] parameters)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            return DuckDBOperations.ExecuteScalar<T>(_connection, sql, parameters);
        }
    }

    /// <summary>
    /// Execute a non-query command using the internal connection.
    /// Uses the SAME connection as the appender to avoid DuckDB file locking issues.
    /// 
    /// WARNING: Use sparingly. Prefer InsertRow() for data insertion.
    /// </summary>
    public void ExecuteNonQuery(string sql, params DuckDBParameter[] parameters)
    {
        lock (_lock)
        {
            ThrowIfDisposed();
            DuckDBOperations.ExecuteNonQuery(_connection, sql, parameters);
        }
    }

    /// <summary>
    /// Get total count of results in database.
    /// 
    /// NOTE: If appender is still open, this will only count data that's been flushed.
    /// For complete count, call Checkpoint() first to close the appender and flush all buffered data.
    /// For seed searching, this is fine - we typically query after the search completes.
    /// 
    /// Uses centralized operation for consistency.
    /// </summary>
    public long GetResultCount()
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            // If appender is open, buffered data won't be counted yet.
            // This is fine for seed searching - we usually query after Checkpoint().

            return DuckDBOperations.GetRowCount(_connection, "results");
        }
    }

    /// <summary>
    /// Get last saved batch position (null if never saved).
    /// </summary>
    public (long? lastBatch, int? batchSize) GetLastBatchPosition()
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT last_completed_batch, batch_size FROM search_state WHERE id = 1";
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                var batch = reader.IsDBNull(0) ? (long?)null : reader.GetInt64(0);
                var size = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                return (batch, size);
            }

            return (null, null);
        }
    }

    /// <summary>
    /// Force flush WAL to main database file.
    /// Closes appender and checkpoints - call this when search is complete.
    /// </summary>
    public void Checkpoint()
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            // NOW we close the appender - search is done!
            // DuckDB.NET appenders must be closed to flush buffered data to the database.
            if (_appender != null)
            {
                try 
                { 
                    _appender.Close(); // Close() automatically flushes buffered data
                    _logCallback?.Invoke("[MotelySearchDatabase] Appender closed successfully before checkpoint");
                }
                catch (Exception ex)
                {
                    // Check if it's just duplicate seeds (acceptable during appender close)
                    bool isDuplicate = ex.Message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
                                       ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
                                       ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
                    
                    if (isDuplicate)
                    {
                        // Duplicates during appender close are acceptable - they came from the appender close operation
                        _logCallback?.Invoke("⚠️ DUPLICATE SEEDS detected during database save - ignoring duplicates");
                        _appender = null; // Still clear the reference
                    }
                    else
                    {
                        // ANY OTHER EXCEPTION IS CRITICAL - LOG AND THROW!
                        var errorMsg = $"[MotelySearchDatabase] CRITICAL: Failed to close appender before checkpoint: {ex.Message}\n{ex.StackTrace}";
                        _logCallback?.Invoke(errorMsg);
                        Console.Error.WriteLine($"❌ {errorMsg}");
                        throw; // Don't silently swallow non-duplicate errors!
                    }
                }
                _appender = null;
            }

            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "FORCE CHECKPOINT";
                cmd.ExecuteNonQuery();
                _logCallback?.Invoke("[MotelySearchDatabase] Checkpoint completed successfully");
            }
            catch (Exception ex)
            {
                // Check if it's just duplicate seeds (acceptable during checkpoint after appender close)
                bool isDuplicate = ex.Message.Contains("PRIMARY KEY", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
                                   ex.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
                
                if (isDuplicate)
                {
                    // Duplicates during checkpoint are acceptable - they came from the appender close operation
                    _logCallback?.Invoke("⚠️ DUPLICATE SEEDS detected during database save - ignoring duplicates");
                    return; // Don't throw for duplicates - checkpoint effectively succeeded
                }
                else
                {
                    var errorMsg = $"[MotelySearchDatabase] CRITICAL: Failed to checkpoint database: {ex.Message}\n{ex.StackTrace}";
                    _logCallback?.Invoke(errorMsg);
                    Console.Error.WriteLine($"❌ {errorMsg}");
                    throw; // Don't silently swallow non-duplicate checkpoint errors!
                }
            }
        }
    }

    /// <summary>
    /// Verify that the database actually contains data.
    /// Throws if database is empty or inaccessible.
    /// NOTE: This should be called AFTER Checkpoint() when appender is closed,
    /// because DuckDB appenders buffer data and queries may not see buffered rows
    /// until the appender is closed/flushed.
    /// </summary>
    public void VerifyDataWritten()
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            // Appender should already be closed by Checkpoint() at this point
            // If it's still open, we might not see all buffered data in queries

            try
            {
                var count = GetResultCount();
                if (count == 0)
                {
                    var errorMsg = "[MotelySearchDatabase] CRITICAL: Database verification failed - no rows found in database!";
                    _logCallback?.Invoke(errorMsg);
                    Console.Error.WriteLine($"❌ {errorMsg}");
                    throw new InvalidOperationException("Database is empty - no data was written successfully!");
                }
                _logCallback?.Invoke($"[MotelySearchDatabase] Verification passed - {count} rows found in database");
            }
            catch (Exception ex)
            {
                var errorMsg = $"[MotelySearchDatabase] CRITICAL: Failed to verify database contents: {ex.Message}\n{ex.StackTrace}";
                _logCallback?.Invoke(errorMsg);
                Console.Error.WriteLine($"❌ {errorMsg}");
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;

            try
            {
                if (_appender != null)
                {
                    try 
                    { 
                        _appender.Close(); // Close() automatically flushes buffered data
                    }
                    catch (Exception ex)
                    {
                        _logCallback?.Invoke($"[MotelySearchDatabase] Failed to close appender during Dispose: {ex.Message}");
                    }
                    _appender = null;
                }

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "FORCE CHECKPOINT";
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logCallback?.Invoke($"[MotelySearchDatabase] Failed to checkpoint during Dispose: {ex.Message}");
            }

            try
            {
                _connection?.Close();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logCallback?.Invoke($"[MotelySearchDatabase] Failed to close/dispose connection: {ex.Message}");
            }

            _disposed = true;
        }
    }

    private void InitializeSchema()
    {
        ValidateOrDeleteDatabase();

        try
        {
            // Use centralized schema for results table
            var resultsSchema = DuckDBSchema.ResultsTableSchema(_columnNames);
            DuckDBTableManager.EnsureTableExists(_connection, resultsSchema);
            
            // Create indexes internally (black box - BSO/CLI/TUI don't need to know about this)
            // Score index for fast sorting
            DuckDBTableManager.CreateIndex(_connection, "CREATE INDEX IF NOT EXISTS idx_score ON results(score DESC);");
            
            // Tally column indexes for fast sorting by individual tallies
            for (int i = 2; i < _columnNames.Count; i++)
            {
                var escapedColumnName = _columnNames[i].Replace("\"", "\"\"");
                var indexName = $"idx_tally_{i}";
                var indexSql = $"CREATE INDEX IF NOT EXISTS {indexName} ON results(\"{escapedColumnName}\" DESC);";
                DuckDBTableManager.CreateIndex(_connection, indexSql);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"[MotelySearchDatabase] Failed to create results table!\nColumns: {string.Join(", ", _columnNames)}\nError: {ex}";
            _logCallback?.Invoke(errorMsg);
            throw new InvalidOperationException($"DuckDB results table creation failed. Columns: {string.Join(", ", _columnNames)}", ex);
        }

        try
        {
            // Use centralized schema for search_state table
            var searchStateSchema = DuckDBSchema.SearchStateTableSchema();
            DuckDBTableManager.EnsureTableExists(_connection, searchStateSchema);
        }
        catch (Exception ex)
        {
            var errorMsg = $"[MotelySearchDatabase] Failed to create search_state table!\nError: {ex}";
            _logCallback?.Invoke(errorMsg);
            throw new InvalidOperationException("DuckDB search_state table creation failed.", ex);
        }
    }

    private void ValidateOrDeleteDatabase()
    {
        try
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_name='results'";
                var result = cmd.ExecuteScalar();
                if (result == null) return;
            }

            var existingColumns = new List<string>();
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name='results' ORDER BY ordinal_position";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    existingColumns.Add(reader.GetString(0));
                }
            }

            // Use centralized schema validation
            bool match = DuckDBTableManager.ValidateTableSchema(_connection, "results", _columnNames);

            if (!match)
            {
                _connection.Close();

                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);
                if (File.Exists(_dbPath + ".wal"))
                    File.Delete(_dbPath + ".wal");

                _connection.Open();
            }
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"[MotelySearchDatabase] Failed to validate/delete database: {ex.Message}");
            // Re-throw to fail fast - database validation is critical
            throw;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MotelySearchDatabase));
    }
}
