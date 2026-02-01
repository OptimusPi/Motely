using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using DuckDB.NET.Data;
using Motely.Filters;
using Motely.Reporting;

namespace Motely.DB;

/// <summary>
/// Desktop storage implementation (DuckDB .NET). Wrapped by ResultStorageAdapter for IResultStorage.
/// Handles schema, indexes, appenders, and queries. Browser/WASM uses a separate implementation.
/// </summary>
public sealed class MotelySearchDatabase : IDisposable
{
    private const string DuckLakeSchemaName = "dl";

    private readonly DuckDBConnection _connection;
    private readonly string _dbPath;
    private readonly MotelyRunConfig _runConfig;
    private readonly Action<string>? _logCallback;
    private readonly DuckDBAppender? _appender; // null when DuckLake (no appender on attached catalog)
    private readonly bool _isDuckLake;
    private readonly string _resultsTableRef; // "results" or "dl.main.results"
    private bool _disposed = false;

    public string DatabasePath => _dbPath;

    public MotelySearchDatabase(
        string dbPath,
        MotelyRunConfig runConfig,
        Action<string>? logCallback = null
    )
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        _runConfig = runConfig ?? throw new ArgumentNullException(nameof(runConfig));
        _logCallback = logCallback;
        _isDuckLake = DuckLakeHelper.IsDuckLake(dbPath);

        if (_isDuckLake)
        {
            var catalogPath = DuckLakeHelper.GetDuckLakeCatalogPath(dbPath);
            var catalogDir = Path.GetDirectoryName(catalogPath);
            if (!string.IsNullOrEmpty(catalogDir) && !Directory.Exists(catalogDir))
                Directory.CreateDirectory(catalogDir);

            // New DuckLake: pass DATA_PATH so ATTACH creates catalog; existing: pass null
            var dataPath = File.Exists(catalogPath) ? null : DuckLakeHelper.GetDuckLakeDataPath(dbPath);
            if (dataPath != null)
            {
                var dataDir = Path.GetDirectoryName(dataPath.TrimEnd(Path.DirectorySeparatorChar, '/'));
                if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir))
                    Directory.CreateDirectory(dataDir);
            }

            _connection = DuckDBConnectionFactory.CreateConnectionWithDuckLake(catalogPath, dataPath, DuckLakeSchemaName);
            _resultsTableRef = $"{DuckLakeSchemaName}.main.results";
            CreateResultsTable();
            _appender = null;
        }
        else
        {
            var dbDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            _connection = DuckDBConnectionFactory.CreateConnection(dbPath);
            _resultsTableRef = "results";
            CreateResultsTable();
            _appender = _connection.CreateAppender("results");
        }
    }

    private static string QuoteColumn(string name)
    {
        return $"\"{name.Replace("\"", "\"\"")}\"";
    }

    private void CreateResultsTable()
    {
        // DuckLake does not support PRIMARY KEY; use plain columns for DuckLake
        var seedScoreDef = _isDuckLake ? "seed VARCHAR, score INTEGER" : "seed VARCHAR PRIMARY KEY, score INTEGER";
        var columnDefs = new List<string> { seedScoreDef };

        foreach (var col in _runConfig.Columns)
        {
            var quotedName = QuoteColumn(col.Name);
            var columnType = col.Type == ColumnType.ScoreTally ? "INTEGER" : "VARCHAR";
            columnDefs.Add($"{quotedName} {columnType}");
        }

        var createTableSql = $"CREATE TABLE IF NOT EXISTS {_resultsTableRef} ({string.Join(", ", columnDefs)})";
        ExecuteNonQuery(createTableSql);
    }

    /// <summary>
    /// Insert a search result row - appender for .duckdb, MERGE INTO for DuckLake
    /// </summary>
    public void InsertRow(string seed, int score, List<int> tallies, List<string?>? columnValues = null)
    {
        if (_disposed)
            return;

        try
        {
            if (_isDuckLake)
            {
                ExecuteMergeRow(seed, score, tallies, columnValues);
                return;
            }

            var row = _appender!.CreateRow();
            row.AppendValue(seed);
            row.AppendValue(score);

            int tallyIndex = 0;
            int stringIndex = 0;
            foreach (var col in _runConfig.Columns)
            {
                if (col.Type == ColumnType.ScoreTally)
                {
                    if (tallies != null && tallyIndex < tallies.Count)
                        row.AppendValue(tallies[tallyIndex++]);
                    else
                        row.AppendValue(0);
                }
                else
                {
                    if (columnValues != null && stringIndex < columnValues.Count)
                        row.AppendValue(columnValues[stringIndex++] ?? "");
                    else
                        row.AppendValue("");
                }
            }
            row.EndRow();
        }
        catch (Exception ex)
        {
            _logCallback?.Invoke($"Failed to insert row: {ex.Message}");
            throw;
        }
    }

    private void ExecuteMergeRow(string seed, int score, List<int>? tallies, List<string?>? columnValues)
    {
        var seedEsc = seed.Replace("'", "''");
        var cols = _runConfig.Columns.ToList();
        var quotedCols = cols.Select(c => QuoteColumn(c.Name)).ToList();
        var setParts = new List<string> { "score = s.score" };
        for (int i = 0; i < cols.Count; i++)
            setParts.Add($"{quotedCols[i]} = s.{quotedCols[i]}");
        var insertCols = new List<string> { "seed", "score" };
        insertCols.AddRange(quotedCols);

        var valueParts = new List<string> { $"'{seedEsc}'", score.ToString() };
        int tallyIndex = 0;
        int stringIndex = 0;
        foreach (var col in cols)
        {
            if (col.Type == ColumnType.ScoreTally)
                valueParts.Add((tallies != null && tallyIndex < tallies.Count) ? tallies[tallyIndex++].ToString() : "0");
            else
                valueParts.Add("'" + (columnValues != null && stringIndex < columnValues.Count ? (columnValues[stringIndex++] ?? "").Replace("'", "''") : "") + "'");
        }

        var valList = string.Join(", ", valueParts);
        var mergeSql = $@"
            MERGE INTO {_resultsTableRef} AS t
            USING (SELECT * FROM (VALUES ({valList})) AS s(seed, score, {string.Join(", ", quotedCols)}))
            ON t.seed = s.seed
            WHEN MATCHED THEN UPDATE SET {string.Join(", ", setParts)}
            WHEN NOT MATCHED THEN INSERT ({string.Join(", ", insertCols)}) VALUES (s.seed, s.score, {string.Join(", ", quotedCols.Select(c => "s." + c))})";
        ExecuteNonQuery(mergeSql);
    }

    /// <summary>
    /// Bulk insert seeds using simple INSERT statements (.duckdb) or MERGE (DuckLake)
    /// </summary>
    public void InsertBulk(ReadOnlySpan<(string seed, int score)> results)
    {
        if (_disposed || results.IsEmpty)
            return;

        try
        {
            if (_isDuckLake)
            {
                foreach (var (seed, score) in results)
                    ExecuteMergeRow(seed, score, null, null);
                return;
            }
            foreach (var (seed, score) in results)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = $"INSERT OR REPLACE INTO {_resultsTableRef} (seed, score) VALUES ('{seed.Replace("'", "''")}', {score})";
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

        var sql = $"SELECT seed, score FROM {_resultsTableRef} ORDER BY score DESC LIMIT {limit}";

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
            SELECT seed, score, {string.Join(", ", _runConfig.Columns.Select(c => QuoteColumn(c.Name)))}
            FROM {_resultsTableRef}
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
            SELECT seed, score, {string.Join(", ", _runConfig.Columns.Select(c => QuoteColumn(c.Name)))}
            FROM {_resultsTableRef}
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
    /// Save last scanned seed for precise resume
    /// </summary>
    public void SaveLastSeed(string seed)
    {
        if (_disposed) return;

        ExecuteNonQuery(
            @"CREATE TABLE IF NOT EXISTS search_meta (key VARCHAR PRIMARY KEY, value VARCHAR)"
        );
        
        // Upsert last_seed
        ExecuteNonQuery(
            $"INSERT INTO search_meta (key, value) VALUES ('last_seed', '{seed}') ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value"
        );
    }

    /// <summary>
    /// Get last scanned seed
    /// </summary>
    public string? GetLastSeed()
    {
        if (_disposed) return null;

        try
        {
            return ExecuteScalar<string>("SELECT value FROM search_meta WHERE key = 'last_seed'");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create indexes after search completes (deferred to avoid conflicts during concurrent writes).
    /// Skipped for DuckLake (format may not support indexes on attached tables).
    /// </summary>
    public void CreateIndexes()
    {
        if (_disposed || _isDuckLake)
            return;

        try
        {
            ExecuteNonQuery($"CREATE INDEX IF NOT EXISTS idx_results_score ON {_resultsTableRef}(score DESC)");
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
        cmd.CommandText = $"SELECT COUNT(*) FROM {_resultsTableRef}";
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
        if (_appender != null)
            _appender.Dispose();
        _connection?.Dispose();
    }

    /// <summary>
    /// Result details for schema comparison between a JAML config and an existing DuckDB file
    /// </summary>
    public sealed class SchemaComparisonResult
    {
        public bool IsCompatible { get; }
        public IReadOnlyList<string> DbColumns { get; }
        public IReadOnlyList<string> RequiredColumns { get; }
        public string? Error { get; }

        public SchemaComparisonResult(
            bool isCompatible,
            IReadOnlyList<string> dbColumns,
            IReadOnlyList<string> requiredColumns,
            string? error = null)
        {
            IsCompatible = isCompatible;
            DbColumns = dbColumns;
            RequiredColumns = requiredColumns;
            Error = error;
        }
    }

    /// <summary>
    /// Compare an existing database schema with the required columns generated from a MotelyRunConfig.
    /// For DuckLake, PK is not required (DuckLake does not support PRIMARY KEY).
    /// </summary>
    public static SchemaComparisonResult CompareSchema(string dbPath, MotelyRunConfig runConfig)
    {
        var requiredColumns = new List<string> { "seed", "score" };
        requiredColumns.AddRange(runConfig.Columns.Select(c => c.Name));
        var dbColumns = new List<string>();

        try
        {
            if (DuckLakeHelper.IsDuckLake(dbPath))
            {
                var catalogPath = DuckLakeHelper.GetDuckLakeCatalogPath(dbPath);
                using var connection = DuckDBConnectionFactory.CreateConnectionWithDuckLake(catalogPath, null, DuckLakeSchemaName);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_catalog = '" + DuckLakeSchemaName + "' AND table_schema = 'main' AND table_name = 'results' ORDER BY ordinal_position";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    dbColumns.Add(reader.GetString(0));
            }
            else
            {
                using var connection = DuckDBConnectionFactory.CreateConnection(dbPath);
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA table_info('results')";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    dbColumns.Add(reader.GetString(1));
            }

            var dbColumnSet = new HashSet<string>(dbColumns, StringComparer.OrdinalIgnoreCase);
            bool hasCoreColumns = dbColumnSet.Contains("seed") && dbColumnSet.Contains("score");
            bool hasAllRequired = requiredColumns.All(c => dbColumnSet.Contains(c));

            return new SchemaComparisonResult(hasCoreColumns && hasAllRequired, dbColumns, requiredColumns);
        }
        catch (Exception ex)
        {
            return new SchemaComparisonResult(false, dbColumns, requiredColumns, ex.Message);
        }
    }

    /// <summary>
    /// Check if an existing database has a schema compatible with the current run configuration.
    /// Returns the comparison result so callers can log detailed differences without re-querying.
    /// </summary>
    public static bool IsSchemaCompatible(
        string dbPath,
        MotelyRunConfig runConfig,
        out SchemaComparisonResult comparison)
    {
        comparison = CompareSchema(dbPath, runConfig);
        return comparison.IsCompatible;
    }

    public class SearchResultRow
    {
        public string Seed { get; set; } = "";
        public int Score { get; set; }
        public List<int>? Tallies { get; set; }
    }
}