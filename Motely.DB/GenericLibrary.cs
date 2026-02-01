using System;
using System.Collections.Generic;
using System.IO;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Manages the Generic Library DuckLake catalog (seeds/generic.ducklake).
/// This is for all inputs (keywords, wordlists, curated seeds) and non-sequential outputs.
/// No batch tracking - these are standalone datasets that can be used as seed sources.
/// </summary>
public sealed class GenericLibrary : IDisposable
{
    private const string CatalogSchemaName = "gen";

    private static string? _libraryRoot;
    private static readonly object _initLock = new();
    private static GenericLibrary? _instance;

    private readonly string _catalogPath;
    private readonly string _dataPath;
    private bool _disposed;

    /// <summary>
    /// Set the library root directory. Call once at host startup.
    /// The catalog will be at {root}/generic.ducklake with data in {root}/generic_data/
    /// </summary>
    public static void SetLibraryRoot(string rootPath)
    {
        lock (_initLock)
        {
            _libraryRoot = rootPath;
            _instance?.Dispose();
            _instance = null;
        }
    }

    /// <summary>
    /// Get the singleton instance. Throws if SetLibraryRoot hasn't been called.
    /// </summary>
    public static GenericLibrary Instance
    {
        get
        {
            lock (_initLock)
            {
                if (string.IsNullOrWhiteSpace(_libraryRoot))
                    throw new InvalidOperationException("GenericLibrary.SetLibraryRoot must be called before accessing Instance");

                if (_instance == null || _instance._disposed)
                {
                    _instance = new GenericLibrary(_libraryRoot);
                }
                return _instance;
            }
        }
    }

    private GenericLibrary(string rootPath)
    {
        _catalogPath = Path.Combine(rootPath, "generic.ducklake");
        _dataPath = Path.Combine(rootPath, "generic_data");

        // Ensure directories exist
        if (!Directory.Exists(rootPath))
            Directory.CreateDirectory(rootPath);
        if (!Directory.Exists(_dataPath))
            Directory.CreateDirectory(_dataPath);
    }

    /// <summary>
    /// Get a connection to the DuckLake catalog. Caller must dispose.
    /// </summary>
    private DuckDBConnection GetConnection()
    {
        // For existing catalog, data path is loaded from catalog
        var dataPath = File.Exists(_catalogPath) ? null : _dataPath;
        return DuckDBConnectionFactory.CreateConnectionWithDuckLake(_catalogPath, dataPath, CatalogSchemaName);
    }

    #region Table Operations

    /// <summary>
    /// Create a new table for a dataset.
    /// </summary>
    public void CreateTable(string tableName, IEnumerable<(string Name, string Type)>? extraColumns = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("tableName is required", nameof(tableName));

        var sanitizedName = SanitizeTableName(tableName);

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();

        var columnDefs = new List<string> { "seed VARCHAR", "score INTEGER" };
        if (extraColumns != null)
        {
            foreach (var (name, type) in extraColumns)
            {
                columnDefs.Add($"\"{EscapeSql(name)}\" {type}");
            }
        }

        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {CatalogSchemaName}.main.""{sanitizedName}"" (
                {string.Join(", ", columnDefs)}
            )";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// List all tables in the generic library.
    /// </summary>
    public List<string> ListTables()
    {
        var result = new List<string>();

        try
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT table_name FROM information_schema.tables 
                WHERE table_catalog = '{CatalogSchemaName}' 
                AND table_schema = 'main'
                ORDER BY table_name";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }
        }
        catch
        {
            // Catalog may not exist yet
        }

        return result;
    }

    /// <summary>
    /// Check if a table exists.
    /// </summary>
    public bool TableExists(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return false;

        var sanitizedName = SanitizeTableName(tableName);

        try
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_catalog = '{CatalogSchemaName}' 
                AND table_schema = 'main' 
                AND table_name = '{EscapeSql(sanitizedName)}'";
            var result = cmd.ExecuteScalar();
            return result != null && Convert.ToInt64(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Delete a table from the library.
    /// </summary>
    public void DeleteTable(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return;

        var sanitizedName = SanitizeTableName(tableName);

        try
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DROP TABLE IF EXISTS {CatalogSchemaName}.main.\"{sanitizedName}\"";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Table may not exist
        }
    }

    /// <summary>
    /// Get the count of rows in a table.
    /// </summary>
    public long GetRowCount(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return 0;

        var sanitizedName = SanitizeTableName(tableName);

        try
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {CatalogSchemaName}.main.\"{sanitizedName}\"";
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt64(result) : 0;
        }
        catch
        {
            return 0;
        }
    }

    #endregion

    #region Seed Operations

    /// <summary>
    /// Open a stateful seed reader: one connection, streaming reader, batch API.
    /// Caller must dispose the returned reader when done.
    /// </summary>
    public ISeedReader OpenSeedReader(string tableName)
    {
        var sanitizedName = SanitizeTableName(tableName);
        var conn = GetConnection();
        return new GenericLibrarySeedReader(conn, CatalogSchemaName, sanitizedName);
    }

    /// <summary>
    /// Stateful reader: one connection + one DuckDBDataReader (streaming). Batch fill via ReadSeeds.
    /// </summary>
    public sealed class GenericLibrarySeedReader : ISeedReader
    {
        private DuckDBConnection? _conn;
        private DuckDBCommand? _cmd;
        private DuckDBDataReader? _reader;
        private bool _disposed;

        internal GenericLibrarySeedReader(DuckDBConnection conn, string schemaName, string tableName)
        {
            _conn = conn;
            _cmd = conn.CreateCommand();
            _cmd.CommandText = $"SELECT seed FROM {schemaName}.main.\"{tableName}\"";
            _cmd.UseStreamingMode = true;
            _reader = _cmd.ExecuteReader();
        }

        /// <inheritdoc />
        public int ReadSeeds(string[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _disposed || _reader == null)
                return 0;
            int count = 0;
            while (count < buffer.Length && _reader.Read())
                buffer[count++] = _reader.GetString(0);
            return count;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _reader?.Dispose();
            _cmd?.Dispose();
            _conn?.Dispose();
            _reader = null;
            _cmd = null;
            _conn = null;
        }
    }

    /// <summary>
    /// Get top seeds by score from a table.
    /// </summary>
    public List<(string Seed, int Score)> GetTopSeeds(string tableName, int limit = 100)
    {
        var results = new List<(string, int)>();
        if (string.IsNullOrWhiteSpace(tableName))
            return results;

        var sanitizedName = SanitizeTableName(tableName);

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT seed, score FROM {CatalogSchemaName}.main.""{sanitizedName}""
            ORDER BY score DESC
            LIMIT {limit}";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var seed = reader.GetString(0);
            var score = reader.GetInt32(1);
            results.Add((seed, score));
        }
        return results;
    }

    /// <summary>
    /// Insert a seed result using MERGE INTO (upsert by seed).
    /// </summary>
    public void InsertSeed(string tableName, string seed, int score, Dictionary<string, object>? extraValues = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return;

        var sanitizedName = SanitizeTableName(tableName);

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();

        var cols = new List<string> { "seed", "score" };
        var vals = new List<string> { $"'{EscapeSql(seed)}'", score.ToString() };
        var updates = new List<string> { "score = s.score" };

        if (extraValues != null)
        {
            foreach (var (key, value) in extraValues)
            {
                var colName = $"\"{EscapeSql(key)}\"";
                cols.Add(colName);
                var valStr = value is string s ? $"'{EscapeSql(s)}'" : value?.ToString() ?? "NULL";
                vals.Add(valStr);
                updates.Add($"{colName} = s.{colName}");
            }
        }

        cmd.CommandText = $@"
            MERGE INTO {CatalogSchemaName}.main.""{sanitizedName}"" AS t
            USING (SELECT {string.Join(", ", vals.Select((v, i) => $"{v} as {cols[i]}"))}) AS s
            ON t.seed = s.seed
            WHEN MATCHED THEN UPDATE SET {string.Join(", ", updates)}
            WHEN NOT MATCHED THEN INSERT ({string.Join(", ", cols)}) VALUES ({string.Join(", ", cols.Select(c => $"s.{c}"))})";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Bulk insert seeds (for imports).
    /// </summary>
    public void BulkInsertSeeds(string tableName, IEnumerable<(string Seed, int Score)> seeds)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return;

        var sanitizedName = SanitizeTableName(tableName);

        using var conn = GetConnection();

        // Create table if it doesn't exist
        using (var createCmd = conn.CreateCommand())
        {
            createCmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS {CatalogSchemaName}.main.""{sanitizedName}"" (
                    seed VARCHAR, score INTEGER
                )";
            createCmd.ExecuteNonQuery();
        }

        // Bulk insert using MERGE
        foreach (var (seed, score) in seeds)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                MERGE INTO {CatalogSchemaName}.main.""{sanitizedName}"" AS t
                USING (SELECT '{EscapeSql(seed)}' as seed, {score} as score) AS s
                ON t.seed = s.seed
                WHEN MATCHED THEN UPDATE SET score = s.score
                WHEN NOT MATCHED THEN INSERT (seed, score) VALUES (s.seed, s.score)";
            cmd.ExecuteNonQuery();
        }
    }

    #endregion

    #region Import Operations

    /// <summary>
    /// Import seeds from a file (CSV, TXT, Parquet) into a table.
    /// </summary>
    public void ImportFromFile(string filePath, string tableName)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}", filePath);

        var sanitizedName = SanitizeTableName(tableName);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        using var conn = GetConnection();

        switch (extension)
        {
            case ".csv":
                ImportCsv(conn, filePath, sanitizedName);
                break;
            case ".txt":
                ImportTxt(conn, filePath, sanitizedName);
                break;
            case ".parquet":
                ImportParquet(conn, filePath, sanitizedName);
                break;
            default:
                throw new NotSupportedException($"File format not supported: {extension}");
        }
    }

    private void ImportCsv(DuckDBConnection conn, string filePath, string tableName)
    {
        using var cmd = conn.CreateCommand();

        // Create table from CSV schema
        var escapedPath = filePath.Replace("'", "''").Replace('\\', '/');
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {CatalogSchemaName}.main.""{tableName}"" AS 
            SELECT * FROM read_csv('{escapedPath}', header=true) LIMIT 0";
        cmd.ExecuteNonQuery();

        // Insert data
        cmd.CommandText = $@"
            INSERT INTO {CatalogSchemaName}.main.""{tableName}"" 
            SELECT * FROM read_csv('{escapedPath}', header=true)";
        cmd.ExecuteNonQuery();
    }

    private void ImportTxt(DuckDBConnection conn, string filePath, string tableName)
    {
        // TXT files are assumed to be one seed per line
        var seeds = File.ReadAllLines(filePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim());

        using var cmd = conn.CreateCommand();

        // Create simple seed table
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {CatalogSchemaName}.main.""{tableName}"" (
                seed VARCHAR, score INTEGER DEFAULT 0
            )";
        cmd.ExecuteNonQuery();

        // Insert seeds
        foreach (var seed in seeds)
        {
            cmd.CommandText = $@"
                INSERT INTO {CatalogSchemaName}.main.""{tableName}"" (seed, score) 
                VALUES ('{EscapeSql(seed)}', 0)";
            cmd.ExecuteNonQuery();
        }
    }

    private void ImportParquet(DuckDBConnection conn, string filePath, string tableName)
    {
        using var cmd = conn.CreateCommand();
        var escapedPath = filePath.Replace("'", "''").Replace('\\', '/');

        // Create table from Parquet schema
        cmd.CommandText = $@"
            CREATE TABLE IF NOT EXISTS {CatalogSchemaName}.main.""{tableName}"" AS 
            SELECT * FROM read_parquet('{escapedPath}') LIMIT 0";
        cmd.ExecuteNonQuery();

        // Insert data
        cmd.CommandText = $@"
            INSERT INTO {CatalogSchemaName}.main.""{tableName}"" 
            SELECT * FROM read_parquet('{escapedPath}')";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Export a table to Parquet file.
    /// </summary>
    public void ExportToParquet(string tableName, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("tableName is required", nameof(tableName));

        var sanitizedName = SanitizeTableName(tableName);
        var escapedPath = outputPath.Replace("'", "''").Replace('\\', '/');

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            COPY {CatalogSchemaName}.main.""{sanitizedName}"" 
            TO '{escapedPath}' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Escapes a string value for safe SQL interpolation.
    /// Note: Parameterized queries (DuckDBParameter) are preferred for values where possible,
    /// but MERGE statements with dynamic columns require string building.
    /// </summary>
    private static string EscapeSql(string value)
    {
        if (value == null) return "";
        // Escape single quotes (SQL string delimiter)
        // Backslashes don't need escaping in DuckDB standard SQL mode
        return value.Replace("'", "''");
    }

    private static string SanitizeTableName(string name)
    {
        // Replace invalid characters with underscores
        var sanitized = name.Replace(" ", "_").Replace("-", "_").Replace(".", "_");
        // Ensure it starts with a letter or underscore
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;
        return sanitized;
    }

    #endregion

    public void Dispose()
    {
        _disposed = true;
    }
}
