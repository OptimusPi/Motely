using DuckDB.NET.Data;
using System;
using System.IO;
using System.Linq;

namespace Motely.API;

/// <summary>
/// Clean DuckDB abstraction for search results with dual read/write connections.
/// Handles schema validation, persistent appender, and thread-safe operations.
/// </summary>
public class MotelySearchDatabase : IDisposable
{
    private readonly string _dbPath;
    private readonly List<string> _columnNames;
    private readonly DuckDBConnection _connection;
    private DuckDBAppender? _appender;
    private readonly object _lock = new();
    private bool _disposed = false;
    private readonly Action<string>? _logCallback;

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

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var connectionString = $"Data Source={_dbPath}";
        _connection = new DuckDBConnection(connectionString);
        _connection.Open();

        InitializeSchema();
        
        // CRITICAL: Create appender immediately and keep it open for the entire search!
        // This is an in-memory database - we can keep the appender open!
        lock (_lock)
        {
            _appender = _connection.CreateAppender("results");
            _logCallback?.Invoke("[MotelySearchDatabase] Appender created and kept open for entire search");
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

                var row = _appender.CreateRow();
                row.AppendValue(seed);
                row.AppendValue(score);

                int tallyCount = _columnNames.Count - 2;
                int providedTallyCount = tallies?.Count ?? 0;
                
                // Validate column count match
                if (providedTallyCount > tallyCount)
                {
                    var errorMsg = $"[CRITICAL] Column count mismatch! Expected {tallyCount} tallies, got {providedTallyCount}. Seed: {seed}, Columns: {string.Join(", ", _columnNames)}";
                    _logCallback?.Invoke(errorMsg);
                    Console.Error.WriteLine($"❌ {errorMsg}");
                    throw new InvalidOperationException(errorMsg);
                }

                for (int i = 0; i < tallyCount; i++)
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
                    _logCallback?.Invoke($"[MotelySearchDatabase] Duplicate seed skipped: {seed}");
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
    /// Appender stays open - DuckDB appenders handle buffering internally.
    /// </summary>
    public List<SearchResult> GetTopResults(int limit = 1000)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            // Appender stays open - no need to flush or close it!
            // DuckDB appenders handle buffering internally and can be queried while open.

            var results = new List<SearchResult>();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM results ORDER BY score DESC LIMIT ?";
            cmd.Parameters.Add(new DuckDBParameter(limit));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tallies = new List<int>();
                for (int i = 2; i < reader.FieldCount; i++)
                {
                    tallies.Add(reader.IsDBNull(i) ? 0 : reader.GetInt32(i));
                }

                results.Add(new SearchResult
                {
                    Seed = reader.GetString(0),
                    Score = reader.GetInt32(1),
                    Tallies = tallies
                });
            }

            return results;
        }
    }

    /// <summary>
    /// Get total count of results in database.
    /// Appender stays open - DuckDB appenders handle buffering internally.
    /// </summary>
    public long GetResultCount()
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            // Appender stays open - no need to flush or close it!
            // DuckDB appenders handle buffering internally and can be queried while open.

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM results";
            var result = cmd.ExecuteScalar();
            return result == null ? 0 : Convert.ToInt64(result);
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
            if (_appender != null)
            {
                try 
                { 
                    _appender.Close(); 
                    _logCallback?.Invoke("[MotelySearchDatabase] Appender closed successfully before checkpoint");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"[MotelySearchDatabase] CRITICAL: Failed to close appender before checkpoint: {ex.Message}\n{ex.StackTrace}";
                    _logCallback?.Invoke(errorMsg);
                    Console.Error.WriteLine($"❌ {errorMsg}");
                    throw; // Don't silently swallow appender close errors!
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
                var errorMsg = $"[MotelySearchDatabase] CRITICAL: Failed to checkpoint database: {ex.Message}\n{ex.StackTrace}";
                _logCallback?.Invoke(errorMsg);
                Console.Error.WriteLine($"❌ {errorMsg}");
                throw; // Don't silently swallow checkpoint errors!
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
                    try { _appender.Close(); }
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

        var columnDefs = new List<string> { "seed VARCHAR PRIMARY KEY", "score INTEGER" };
        for (int i = 2; i < _columnNames.Count; i++)
        {
            // Sanitize column names: replace spaces/special chars with underscores, remove quotes for DuckDB compatibility
            var safeName = _columnNames[i]
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace(".", "_")
                .Replace("\"", "");
            columnDefs.Add($"\"{safeName}\" INTEGER");
        }

        var createTableSql = $@"
                CREATE TABLE IF NOT EXISTS results (
                    {string.Join(",\n                    ", columnDefs)}
                )";

        try
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = createTableSql;
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"[MotelySearchDatabase] Failed to create table!\nSQL: {createTableSql}\nColumns: {string.Join(", ", _columnNames)}\nError: {ex}";
            _logCallback?.Invoke(errorMsg);
            throw new InvalidOperationException($"DuckDB table creation failed. SQL: {createTableSql}", ex);
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS search_state (
                    id INTEGER PRIMARY KEY,
                    batch_size INTEGER,
                    last_completed_batch BIGINT
                )";
            cmd.ExecuteNonQuery();
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

            // Sanitize column names for comparison (same logic as InitializeSchema)
            var sanitizedColumnNames = _columnNames.Select((name, i) => 
                i < 2 ? name : name.Replace(" ", "_").Replace("-", "_").Replace(".", "_")
            ).ToList();
            
            bool match = existingColumns.Count == sanitizedColumnNames.Count &&
                         existingColumns.SequenceEqual(sanitizedColumnNames);

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
