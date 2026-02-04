using System;
using System.Collections.Generic;
using System.IO;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Manages the Sequential Library DuckLake catalog (seeds/sequential.ducklake).
/// This is for front-to-back exhaustive searches that track batch position, progress, and is_active state.
/// Each filter+deck+stake combination gets its own table; search_meta tracks all searches.
/// </summary>
public sealed class SequentialLibrary : IDisposable
{
    private const string CatalogSchemaName = "seq";
    private const string SearchMetaTable = "search_meta";

    private static string? _libraryRoot;
    private static readonly object _initLock = new();
    private static SequentialLibrary? _instance;

    private readonly string _catalogPath;
    private readonly string _dataPath;
    private bool _disposed;

    /// <summary>
    /// Set the library root directory. Call once at host startup.
    /// The catalog will be at {root}/sequential.ducklake with data in {root}/sequential_data/
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
    public static SequentialLibrary Instance
    {
        get
        {
            lock (_initLock)
            {
                if (string.IsNullOrWhiteSpace(_libraryRoot))
                    throw new InvalidOperationException(
                        "SequentialLibrary.SetLibraryRoot must be called before accessing Instance"
                    );

                if (_instance == null || _instance._disposed)
                {
                    _instance = new SequentialLibrary(_libraryRoot);
                }
                return _instance;
            }
        }
    }

    private SequentialLibrary(string rootPath)
    {
        _catalogPath = Path.Combine(rootPath, "sequential.ducklake");
        _dataPath = Path.Combine(rootPath, "sequential_data");

        // Ensure directories exist
        if (!Directory.Exists(rootPath))
            Directory.CreateDirectory(rootPath);
        if (!Directory.Exists(_dataPath))
            Directory.CreateDirectory(_dataPath);

        // Initialize the catalog with search_meta table
        EnsureSearchMetaTable();
    }

    /// <summary>
    /// Get a connection to the DuckLake catalog. Caller must dispose.
    /// </summary>
    private DuckDBConnection GetConnection()
    {
        // For existing catalog, data path is loaded from catalog
        var dataPath = File.Exists(_catalogPath) ? null : _dataPath;
        return DuckDBConnectionFactory.CreateConnectionWithDuckLake(
            _catalogPath,
            dataPath,
            CatalogSchemaName
        );
    }

    private void EnsureSearchMetaTable()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();

        // DuckLake does not support PRIMARY KEY - we'll use MERGE INTO for upserts
        cmd.CommandText =
            $@"
            CREATE TABLE IF NOT EXISTS {CatalogSchemaName}.main.{SearchMetaTable} (
                search_id VARCHAR,
                table_name VARCHAR,
                jaml_filter VARCHAR,
                deck VARCHAR,
                stake VARCHAR,
                seed_source VARCHAR,
                is_active BOOLEAN DEFAULT false,
                last_accessed TIMESTAMP,
                last_seed VARCHAR,
                total_seeds_processed BIGINT DEFAULT 0,
                total_matches BIGINT DEFAULT 0,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )";
        cmd.ExecuteNonQuery();
    }

    #region Search Meta Operations

    /// <summary>
    /// Get metadata for a specific search.
    /// </summary>
    public SearchMeta? GetSearchMeta(string searchId)
    {
        if (string.IsNullOrWhiteSpace(searchId))
            return null;

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $@"
            SELECT search_id, table_name, jaml_filter, deck, stake, seed_source, 
                   is_active, last_accessed, last_seed, total_seeds_processed, 
                   total_matches, created_at
            FROM {CatalogSchemaName}.main.{SearchMetaTable}
            WHERE search_id = '{EscapeSql(searchId)}'";

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return ReadSearchMeta(reader);
        }
        return null;
    }

    /// <summary>
    /// Get all search IDs that are marked as active.
    /// </summary>
    public List<string> GetAllActiveSearchIds()
    {
        var result = new List<string>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $@"
            SELECT search_id FROM {CatalogSchemaName}.main.{SearchMetaTable}
            WHERE is_active = true";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    /// <summary>
    /// Get all search metadata entries.
    /// </summary>
    public List<SearchMeta> GetAllSearchMeta()
    {
        var result = new List<SearchMeta>();
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $@"
            SELECT search_id, table_name, jaml_filter, deck, stake, seed_source, 
                   is_active, last_accessed, last_seed, total_seeds_processed, 
                   total_matches, created_at
            FROM {CatalogSchemaName}.main.{SearchMetaTable}
            ORDER BY last_accessed DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(ReadSearchMeta(reader));
        }
        return result;
    }

    /// <summary>
    /// Create or update search metadata using MERGE INTO (DuckLake doesn't support PRIMARY KEY).
    /// </summary>
    public void UpsertSearchMeta(SearchMeta meta)
    {
        if (string.IsNullOrWhiteSpace(meta.SearchId))
            throw new ArgumentException("SearchId is required", nameof(meta));

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();

        var searchId = EscapeSql(meta.SearchId);
        var tableName = EscapeSql(meta.TableName ?? meta.SearchId);
        var jamlFilter = EscapeSql(meta.JamlFilter ?? "");
        var deck = EscapeSql(meta.Deck ?? "");
        var stake = EscapeSql(meta.Stake ?? "");
        var seedSource = EscapeSql(meta.SeedSource ?? "");
        var lastSeed = EscapeSql(meta.LastSeed ?? "");
        var isActive = meta.IsActive ? "true" : "false";
        var lastAccessed = meta.LastAccessed?.ToString("yyyy-MM-dd HH:mm:ss") ?? "NULL";
        var createdAt = meta.CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "NULL";

        cmd.CommandText =
            $@"
            MERGE INTO {CatalogSchemaName}.main.{SearchMetaTable} AS t
            USING (SELECT 
                '{searchId}' as search_id,
                '{tableName}' as table_name,
                '{jamlFilter}' as jaml_filter,
                '{deck}' as deck,
                '{stake}' as stake,
                '{seedSource}' as seed_source,
                {isActive} as is_active,
                {(lastAccessed == "NULL" ? "NULL" : $"TIMESTAMP '{lastAccessed}'")} as last_accessed,
                '{lastSeed}' as last_seed,
                {meta.TotalSeedsProcessed} as total_seeds_processed,
                {meta.TotalMatches} as total_matches,
                {(createdAt == "NULL" ? "CURRENT_TIMESTAMP" : $"TIMESTAMP '{createdAt}'")} as created_at
            ) AS s
            ON t.search_id = s.search_id
            WHEN MATCHED THEN UPDATE SET
                table_name = s.table_name,
                jaml_filter = s.jaml_filter,
                deck = s.deck,
                stake = s.stake,
                seed_source = s.seed_source,
                is_active = s.is_active,
                last_accessed = s.last_accessed,
                last_seed = s.last_seed,
                total_seeds_processed = s.total_seeds_processed,
                total_matches = s.total_matches
            WHEN NOT MATCHED THEN INSERT (
                search_id, table_name, jaml_filter, deck, stake, seed_source,
                is_active, last_accessed, last_seed, total_seeds_processed, total_matches, created_at
            ) VALUES (
                s.search_id, s.table_name, s.jaml_filter, s.deck, s.stake, s.seed_source,
                s.is_active, s.last_accessed, s.last_seed, s.total_seeds_processed, s.total_matches, s.created_at
            )";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Mark a search as active or dormant.
    /// </summary>
    public void SetSearchActive(string searchId, bool isActive)
    {
        var meta = GetSearchMeta(searchId);
        if (meta == null)
        {
            meta = new SearchMeta { SearchId = searchId, TableName = searchId };
        }
        meta.IsActive = isActive;
        meta.LastAccessed = DateTime.UtcNow;
        UpsertSearchMeta(meta);
    }

    /// <summary>
    /// Update the last seed position for a search (for resume capability).
    /// </summary>
    public void UpdateLastSeed(
        string searchId,
        string lastSeed,
        long totalProcessed,
        long totalMatches
    )
    {
        var meta = GetSearchMeta(searchId);
        if (meta == null)
        {
            meta = new SearchMeta { SearchId = searchId, TableName = searchId };
        }
        meta.LastSeed = lastSeed;
        meta.TotalSeedsProcessed = totalProcessed;
        meta.TotalMatches = totalMatches;
        meta.LastAccessed = DateTime.UtcNow;
        UpsertSearchMeta(meta);
    }

    /// <summary>
    /// Delete a search entry and optionally its results table.
    /// </summary>
    public void DeleteSearch(string searchId, bool deleteResultsTable = true)
    {
        if (string.IsNullOrWhiteSpace(searchId))
            return;

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();

        // Get the table name first
        var meta = GetSearchMeta(searchId);
        var tableName = meta?.TableName ?? searchId;

        // Delete from search_meta
        cmd.CommandText =
            $@"
            DELETE FROM {CatalogSchemaName}.main.{SearchMetaTable}
            WHERE search_id = '{EscapeSql(searchId)}'";
        cmd.ExecuteNonQuery();

        // Optionally delete the results table
        if (deleteResultsTable)
        {
            try
            {
                cmd.CommandText =
                    $"DROP TABLE IF EXISTS {CatalogSchemaName}.main.\"{EscapeSql(tableName)}\"";
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // Table may not exist
            }
        }
    }

    private static SearchMeta ReadSearchMeta(DuckDBDataReader reader)
    {
        return new SearchMeta
        {
            SearchId = reader.IsDBNull(0) ? "" : reader.GetString(0),
            TableName = reader.IsDBNull(1) ? "" : reader.GetString(1),
            JamlFilter = reader.IsDBNull(2) ? null : reader.GetString(2),
            Deck = reader.IsDBNull(3) ? null : reader.GetString(3),
            Stake = reader.IsDBNull(4) ? null : reader.GetString(4),
            SeedSource = reader.IsDBNull(5) ? null : reader.GetString(5),
            IsActive = !reader.IsDBNull(6) && reader.GetBoolean(6),
            LastAccessed = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            LastSeed = reader.IsDBNull(8) ? null : reader.GetString(8),
            TotalSeedsProcessed = reader.IsDBNull(9) ? 0 : reader.GetInt64(9),
            TotalMatches = reader.IsDBNull(10) ? 0 : reader.GetInt64(10),
            CreatedAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
        };
    }

    #endregion

    #region Results Table Operations

    /// <summary>
    /// Create a results table for a search if it doesn't exist.
    /// </summary>
    public void CreateResultsTable(
        string searchId,
        IEnumerable<(string Name, string Type)>? extraColumns = null
    )
    {
        if (string.IsNullOrWhiteSpace(searchId))
            throw new ArgumentException("searchId is required", nameof(searchId));

        var tableName = SanitizeTableName(searchId);

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

        cmd.CommandText =
            $@"
            CREATE TABLE IF NOT EXISTS {CatalogSchemaName}.main.""{tableName}"" (
                {string.Join(", ", columnDefs)}
            )";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Open a stateful seed reader: one connection, streaming reader, batch API.
    /// Caller must dispose the returned reader when done.
    /// </summary>
    public ISeedReader OpenSeedReader(string searchId)
    {
        var tableName = SanitizeTableName(searchId);
        var conn = GetConnection();
        return new SequentialLibrarySeedReader(conn, CatalogSchemaName, tableName);
    }

    /// <summary>
    /// Stateful reader: one connection + one DuckDBDataReader (streaming). Batch fill via ReadSeeds.
    /// </summary>
    public sealed class SequentialLibrarySeedReader : ISeedReader
    {
        private DuckDBConnection? _conn;
        private DuckDBCommand? _cmd;
        private DuckDBDataReader? _reader;
        private bool _disposed;

        internal SequentialLibrarySeedReader(
            DuckDBConnection conn,
            string schemaName,
            string tableName
        )
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
            if (_disposed)
                return;
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
    /// Get top seeds by score from a search results table.
    /// </summary>
    public List<(string Seed, int Score)> GetTopSeeds(string searchId, int limit = 100)
    {
        var results = new List<(string, int)>();
        if (string.IsNullOrWhiteSpace(searchId))
            return results;

        var tableName = SanitizeTableName(searchId);

        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $@"
            SELECT seed, score FROM {CatalogSchemaName}.main.""{tableName}""
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
    /// Insert a result row using MERGE INTO (upsert by seed).
    /// </summary>
    public void InsertResult(
        string searchId,
        string seed,
        int score,
        Dictionary<string, object>? extraValues = null
    )
    {
        if (string.IsNullOrWhiteSpace(searchId))
            return;

        var tableName = SanitizeTableName(searchId);

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

        cmd.CommandText =
            $@"
            MERGE INTO {CatalogSchemaName}.main.""{tableName}"" AS t
            USING (SELECT {string.Join(", ", vals.Select((v, i) => $"{v} as {cols[i]}"))}) AS s
            ON t.seed = s.seed
            WHEN MATCHED THEN UPDATE SET {string.Join(", ", updates)}
            WHEN NOT MATCHED THEN INSERT ({string.Join(", ", cols)}) VALUES ({string.Join(", ", cols.Select(c => $"s.{c}"))})";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get the count of results in a search table.
    /// </summary>
    public long GetResultCount(string searchId)
    {
        if (string.IsNullOrWhiteSpace(searchId))
            return 0;

        var tableName = SanitizeTableName(searchId);

        try
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {CatalogSchemaName}.main.\"{tableName}\"";
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt64(result) : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Check if a search results table exists.
    /// </summary>
    public bool TableExists(string searchId)
    {
        if (string.IsNullOrWhiteSpace(searchId))
            return false;

        var tableName = SanitizeTableName(searchId);

        try
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $@"
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_catalog = '{CatalogSchemaName}' 
                AND table_schema = 'main' 
                AND table_name = '{EscapeSql(tableName)}'";
            var result = cmd.ExecuteScalar();
            return result != null && Convert.ToInt64(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Helpers

    private static string EscapeSql(string value)
    {
        return value?.Replace("'", "''") ?? "";
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

/// <summary>
/// Metadata for a sequential search.
/// </summary>
public sealed class SearchMeta
{
    public string SearchId { get; set; } = "";
    public string TableName { get; set; } = "";
    public string? JamlFilter { get; set; }
    public string? Deck { get; set; }
    public string? Stake { get; set; }
    public string? SeedSource { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastAccessed { get; set; }
    public string? LastSeed { get; set; }
    public long TotalSeedsProcessed { get; set; }
    public long TotalMatches { get; set; }
    public DateTime? CreatedAt { get; set; }
}
