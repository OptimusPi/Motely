using System.Collections.Generic;
using System.Linq;
using DuckDB.NET.Data;

namespace Motely.DuckDB;

/// <summary>
/// High-level abstraction for Motely search result databases
/// Handles schema, indexes, appenders, and queries internally
/// Cross-platform compatible (Desktop, Browser, CLI, TUI, Avalonia, WASM)
/// </summary>
public sealed class MotelySearchDatabase : IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly string _dbPath;
    private readonly List<string> _columnNames;
    private readonly Action<string>? _logCallback;
    private bool _disposed = false;

    public string DatabasePath => _dbPath;

    public MotelySearchDatabase(
        string dbPath,
        List<string> columnNames,
        Action<string>? logCallback = null
    )
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        _columnNames = columnNames ?? throw new ArgumentNullException(nameof(columnNames));
        _logCallback = logCallback;

        // Ensure directory exists
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            Directory.CreateDirectory(dbDir);

        _connection = DuckDBConnectionFactory.CreateConnection(dbPath);

        // Create results table with dynamic columns
        CreateResultsTable();
    }

    private static string QuoteColumn(string name)
    {
        return $"\"{name.Replace("\"", "\"\"")}\"";
    }

    private void CreateResultsTable()
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS results (
                seed VARCHAR PRIMARY KEY, 
                score INTEGER
            )";

        ExecuteNonQuery(createTableSql);
    }

    /// <summary>
    /// Insert a search result row - real DuckDB way
    /// </summary>
    public void InsertRow(string seed, int score, List<int>? tallies = null)
    {
        if (_disposed)
            return;

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO results (seed, score) VALUES (?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(seed));
            cmd.Parameters.Add(new DuckDBParameter(score));
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"Failed to insert row: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Bulk insert seeds using simple INSERT statements
    /// </summary>
    public void InsertBulk(ReadOnlySpan<(string seed, int score)> results)
    {
        if (_disposed || results.IsEmpty)
            return;

        try
        {
            foreach (var (seed, score) in results)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = $"INSERT OR REPLACE INTO results (seed, score) VALUES ('{seed.Replace("'", "''")}', {score})";
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"Failed to bulk insert {results.Length} rows: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Get top N results ordered by score
    /// </summary>
    public List<SearchResultRow> GetTopResults(int limit = 1000)
    {
        if (_disposed)
            return new List<SearchResultRow>();

        var sql = $"SELECT seed, score FROM results ORDER BY score DESC LIMIT {limit}";

        var results = new List<SearchResultRow>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var seed = reader.GetString(0);
            var score = reader.GetInt32(1);
            results.Add(new SearchResultRow { Seed = seed, Score = score });
        }

        return results;
    }

    /// <summary>
    /// Get results page with offset/limit
    /// </summary>
    public List<Dictionary<string, object?>> GetResultsPage(
        int offset,
        int limit,
        string orderBy = "score",
        bool ascending = false
    )
    {
        if (_disposed)
            return new List<Dictionary<string, object?>>();

        var order = ascending ? "ASC" : "DESC";
        var sql =
            $@"
            SELECT seed, score, {string.Join(", ", _columnNames.Skip(2).Select(QuoteColumn))}
            FROM results
            ORDER BY {orderBy} {order}
            LIMIT {limit} OFFSET {offset}";

        return ExecuteQuery(sql);
    }

    /// <summary>
    /// Get results ordered by column
    /// </summary>
    public List<Dictionary<string, object?>> GetResultsOrderedBy(
        string orderBy,
        bool ascending,
        int limit
    )
    {
        if (_disposed)
            return new List<Dictionary<string, object?>>();

        var order = ascending ? "ASC" : "DESC";
        var sql =
            $@"
            SELECT seed, score, {string.Join(", ", _columnNames.Skip(2).Select(QuoteColumn))}
            FROM results
            ORDER BY {orderBy} {order}
            LIMIT {limit}";

        return ExecuteQuery(sql);
    }

    /// <summary>
    /// Save batch position for resume capability
    /// </summary>
    public void SaveBatchPosition(long batch, int batchSize)
    {
        if (_disposed)
            return;

        ExecuteNonQuery(
            @"
            CREATE TABLE IF NOT EXISTS search_meta (
                key VARCHAR PRIMARY KEY,
                value VARCHAR
            )"
        );

        ExecuteNonQuery(
            $@"
            INSERT INTO search_meta (key, value)
            VALUES ('last_batch', '{batch}'), ('last_batch_size', '{batchSize}')
            ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value"
        );
    }

    /// <summary>
    /// Get last batch position for resume
    /// </summary>
    public (long? batch, int? batchSize) GetLastBatchPosition()
    {
        if (_disposed)
            return (null, null);

        ExecuteNonQuery(
            @"
            CREATE TABLE IF NOT EXISTS search_meta (
                key VARCHAR PRIMARY KEY,
                value VARCHAR
            )"
        );

        long? batch = null;
        int? batchSize = null;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT key, value FROM search_meta WHERE key IN ('last_batch', 'last_batch_size')";
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            if (key == "last_batch" && long.TryParse(value, out var b))
                batch = b;
            else if (key == "last_batch_size" && int.TryParse(value, out var bs))
                batchSize = bs;
        }

        return (batch, batchSize);
    }

    /// <summary>
    /// Create indexes after search completes (deferred to avoid conflicts during concurrent writes)
    /// </summary>
    public void CreateIndexes()
    {
        if (_disposed)
            return;

        try
        {
            ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_results_score ON results(score DESC)");
            _logCallback?.Invoke("✓ Score index created successfully");
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"⚠ Failed to create score index (may already exist): {ex.Message}");
        }
    }

    /// <summary>
    /// Checkpoint database (flush writes)
    /// </summary>
    public void Checkpoint()
    {
        if (_disposed)
            return;
        ExecuteNonQuery("CHECKPOINT");
    }

    /// <summary>
    /// Verify data was written (throws if table is empty when it shouldn't be)
    /// </summary>
    public void VerifyDataWritten()
    {
        if (_disposed)
            return;
        var count = GetResultCount();
        _logCallback?.Invoke($"Verified {count} results in database");
    }

    /// <summary>
    /// Get total result count
    /// </summary>
    public int GetResultCount()
    {
        if (_disposed)
            return 0;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM results";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    /// <summary>
    /// Execute a scalar query
    /// </summary>
    public T? ExecuteScalar<T>(string sql)
    {
        if (_disposed)
            return default;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
            return default;
        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <summary>
    /// Execute a query and return results as dictionary
    /// </summary>
    public List<Dictionary<string, object?>> ExecuteQuery(string sql)
    {
        if (_disposed)
            return new List<Dictionary<string, object?>>();

        var results = new List<Dictionary<string, object?>>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();

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

    /// <summary>
    /// Execute a non-query SQL statement
    /// </summary>
    public void ExecuteNonQuery(string sql)
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Execute a non-query SQL statement with parameters
    /// </summary>
    public void ExecuteNonQuery(string sql, params DuckDBParameter[] parameters)
    {
        if (_disposed)
            return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var param in parameters)
        {
            cmd.Parameters.Add(param);
        }
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _connection?.Dispose();
    }

    public class SearchResultRow
    {
        public string Seed { get; set; } = "";
        public int Score { get; set; }
        public List<int>? Tallies { get; set; }
    }
}
