using DuckDB.NET.Data;

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
    }

    public string DatabasePath => _dbPath;
    public IReadOnlyList<string> ColumnNames => _columnNames.AsReadOnly();

    /// <summary>
    /// Insert a row into the database.
    /// Thread-safe. Handles duplicate keys gracefully.
    /// </summary>
    public void InsertRow(string seed, int score, List<int>? tallies = null)
    {
        if (string.IsNullOrEmpty(seed)) throw new ArgumentException("Seed cannot be empty", nameof(seed));

        lock (_lock)
        {
            ThrowIfDisposed();

            try
            {
                _appender ??= _connection.CreateAppender("results");

                var row = _appender.CreateRow();
                row.AppendValue(seed);
                row.AppendValue(score);

                int tallyCount = _columnNames.Count - 2;
                for (int i = 0; i < tallyCount; i++)
                {
                    int value = (tallies != null && i < tallies.Count) ? tallies[i] : 0;
                    row.AppendValue(value);
                }

                row.EndRow();
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("PRIMARY KEY") && !ex.Message.Contains("Duplicate"))
                {
                    throw;
                }
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
                INSERT INTO search_state (id, batch_size, last_completed_batch, updated_at)
                VALUES (1, ?, ?, CURRENT_TIMESTAMP)
                ON CONFLICT (id) DO UPDATE SET
                    batch_size = excluded.batch_size,
                    last_completed_batch = excluded.last_completed_batch,
                    updated_at = excluded.updated_at";
            cmd.Parameters.Add(new DuckDBParameter(batchSize));
            cmd.Parameters.Add(new DuckDBParameter(batchNumber));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Get top N results ordered by score descending.
    /// Closes appender to flush buffered rows before querying.
    /// </summary>
    public List<SearchResult> GetTopResults(int limit = 1000)
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            if (_appender != null)
            {
                try { _appender.Close(); }
                catch { }
                _appender = null;
            }

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
    /// </summary>
    public long GetResultCount()
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            if (_appender != null)
            {
                try { _appender.Close(); }
                catch { }
                _appender = null;
            }

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
    /// </summary>
    public void Checkpoint()
    {
        lock (_lock)
        {
            ThrowIfDisposed();

            if (_appender != null)
            {
                try { _appender.Close(); }
                catch { }
                _appender = null;
            }

            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "FORCE CHECKPOINT";
            cmd.ExecuteNonQuery();
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
                    catch { }
                    _appender = null;
                }

                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "FORCE CHECKPOINT";
                cmd.ExecuteNonQuery();
            }
            catch { }

            try
            {
                _connection?.Close();
                _connection?.Dispose();
            }
            catch { }

            _disposed = true;
        }
    }

    private void InitializeSchema()
    {
        ValidateOrDeleteDatabase();

        var columnDefs = new List<string> { "seed VARCHAR PRIMARY KEY", "score INTEGER" };
        for (int i = 2; i < _columnNames.Count; i++)
        {
            // Quote column names to handle special characters and reserved words
            columnDefs.Add($"\"{_columnNames[i]}\" INTEGER");
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
                    last_completed_batch BIGINT,
                    updated_at TIMESTAMP
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

            bool match = existingColumns.Count == _columnNames.Count &&
                         existingColumns.SequenceEqual(_columnNames);

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
        catch
        {
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MotelySearchDatabase));
    }
}
