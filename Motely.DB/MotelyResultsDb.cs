using System.Text;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Fast DuckLake result store. Backed by Parquet files using the ducklake extension.
/// Schema: results(seed TEXT, score INTEGER, tally0 INTEGER, ..., tallyN INTEGER)
/// </summary>
public sealed class MotelyResultsDb : IDisposable
{
    private readonly DuckDBConnection _conn;
    private readonly int _tallyCount;
    private readonly object _lock = new();
    private const string DefaultFilterId = "";

    /// <summary>Number of tally columns (one per should-clause).</summary>
    public int TallyCount => _tallyCount;

    /// <summary>
    /// Opens or creates a result database using DuckLake.
    /// </summary>
    /// <param name="dbPath">DuckLake root directory, legacy .db path, or ":memory:" for pure in-memory.</param>
    /// <param name="tallyCount">Number of should-clause tally columns.</param>
    public MotelyResultsDb(string dbPath, int tallyCount)
    {
        _tallyCount = Math.Max(0, tallyCount);

        // We always boot up an in-memory core, then attach the ducklake.
        _conn = new DuckDBConnection("Data Source=:memory:");
        _conn.Open();

        using var cmd = _conn.CreateCommand();

        if (dbPath != ":memory:")
        {
            var (lakeDir, metaFile, dataDir) = ResolveLakePaths(dbPath);

            Directory.CreateDirectory(lakeDir);
            Directory.CreateDirectory(dataDir);

            // Install and attach the DuckLake.
            cmd.CommandText = "INSTALL ducklake; LOAD ducklake;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = $"ATTACH 'ducklake:{EscapeSqlPath(metaFile)}' AS motely_lake (DATA_PATH '{EscapeSqlPath(dataDir)}');";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "USE motely_lake;";
            cmd.ExecuteNonQuery();
        }

        CreateTable();
        EnsureFilterIdColumn();
    }

    private static (string LakeDir, string MetaFile, string DataDir) ResolveLakePaths(string dbPath)
    {
        var fullPath = Path.GetFullPath(dbPath);

        if (!Path.HasExtension(fullPath))
        {
            var lakeDirFromDirectory = fullPath;
            return (
                LakeDir: lakeDirFromDirectory,
                MetaFile: Path.Combine(lakeDirFromDirectory, "metadata.ducklake"),
                DataDir: Path.Combine(lakeDirFromDirectory, "data")
            );
        }

        var directory = Path.GetDirectoryName(fullPath);
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var basePath = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Directory.GetCurrentDirectory(), baseName)
            : Path.Combine(directory, baseName);
        var lakeDir = $"{basePath}_lake";
        return (
            LakeDir: lakeDir,
            MetaFile: Path.Combine(lakeDir, "metadata.ducklake"),
            DataDir: Path.Combine(lakeDir, "data")
        );
    }

    private void EnsureFilterIdColumn()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "ALTER TABLE results ADD COLUMN IF NOT EXISTS filter_id TEXT NOT NULL DEFAULT ''";
        cmd.ExecuteNonQuery();
    }

    private void CreateTable()
    {
        var sb = new StringBuilder();
        sb.Append("CREATE TABLE IF NOT EXISTS results (filter_id TEXT NOT NULL DEFAULT '', seed TEXT NOT NULL, score INTEGER NOT NULL");
        for (int i = 0; i < _tallyCount; i++)
            sb.Append($", tally{i} INTEGER NOT NULL DEFAULT 0");
        sb.Append(')');

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Bulk-insert results using DuckDB's fast appender.
    /// </summary>
    public void AppendResults(string filterId, ReadOnlySpan<ResultRow> rows)
    {
        if (rows.Length == 0)
            return;

        lock (_lock)
        {
            using var appender = _conn.CreateAppender("results");
            foreach (ref readonly var row in rows)
            {
                var r = appender.CreateRow();
                r.AppendValue(filterId);
                r.AppendValue(row.Seed);
                r.AppendValue(row.Score);
                for (int i = 0; i < _tallyCount; i++)
                    r.AppendValue(i < row.Tallies.Length ? row.Tallies[i] : 0);
                r.EndRow();
            }
        }
    }

    /// <summary>
    /// Bulk-insert results into the default filter partition.
    /// </summary>
    public void AppendResults(ReadOnlySpan<ResultRow> rows) => AppendResults(DefaultFilterId, rows);

    /// <summary>
    /// Insert a single result row.
    /// </summary>
    public void AppendResult(string filterId, string seed, int score, ReadOnlySpan<int> tallies)
    {
        lock (_lock)
        {
            using var appender = _conn.CreateAppender("results");
            var r = appender.CreateRow();
            r.AppendValue(filterId);
            r.AppendValue(seed);
            r.AppendValue(score);
            for (int i = 0; i < _tallyCount; i++)
                r.AppendValue(i < tallies.Length ? tallies[i] : 0);
            r.EndRow();
        }
    }

    /// <summary>
    /// Insert a single result row into the default filter partition.
    /// </summary>
    public void AppendResult(string seed, int score, ReadOnlySpan<int> tallies) =>
        AppendResult(DefaultFilterId, seed, score, tallies);

    /// <summary>
    /// Get top N results ordered by score descending for one filter.
    /// </summary>
    public List<ResultRow> GetTopResults(string filterId, int limit = 1000)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"SELECT seed, score{BuildTallySelectList()} FROM results WHERE filter_id = '{EscapeSqlLiteral(filterId)}' ORDER BY score DESC LIMIT {limit}";
            using var reader = cmd.ExecuteReader();

            var results = new List<ResultRow>();
            while (reader.Read())
            {
                var seed = reader.GetString(0);
                var score = reader.GetInt32(1);
                var tallies = new int[_tallyCount];
                for (int i = 0; i < _tallyCount; i++)
                    tallies[i] = reader.GetInt32(2 + i);
                results.Add(new ResultRow(seed, score, tallies));
            }
            return results;
        }
    }

    /// <summary>
    /// Get top N results ordered by score descending for the default filter partition.
    /// </summary>
    public List<ResultRow> GetTopResults(int limit = 1000) => GetTopResults(DefaultFilterId, limit);

    public List<string> GetSeeds(string filterId)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"SELECT seed FROM results WHERE filter_id = '{EscapeSqlLiteral(filterId)}'";
            using var reader = cmd.ExecuteReader();

            var results = new List<string>();
            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }

            return results;
        }
    }

    public List<string> GetSeeds() => GetSeeds(DefaultFilterId);

    public long GetCount(string filterId)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM results WHERE filter_id = '{EscapeSqlLiteral(filterId)}'";
            return (long)cmd.ExecuteScalar()!;
        }
    }

    /// <summary>
    /// Total number of stored results in the default filter partition.
    /// </summary>
    public long Count
    {
        get
        {
            return GetCount(DefaultFilterId);
        }
    }

    /// <summary>Alias for <see cref="ExportParquet(string,string,int?)"/> — export one filter partition from the shared lake.</summary>
    public void ExportFilterParquet(string parquetPath, string filterId, int? limit) =>
        ExportParquet(parquetPath, filterId, limit);

    public void ExportParquet(string parquetPath, string filterId, int? limit)
    {
        if (string.IsNullOrWhiteSpace(parquetPath))
            throw new ArgumentException("Parquet path is required.", nameof(parquetPath));

        var fullPath = Path.GetFullPath(parquetPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var limitClause = limit is > 0 ? $" LIMIT {limit.Value}" : string.Empty;

        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText =
                $"COPY (SELECT seed, score{BuildTallySelectList()} FROM results WHERE filter_id = '{EscapeSqlLiteral(filterId)}' ORDER BY score DESC{limitClause}) TO '{EscapeSqlPath(fullPath)}' (FORMAT PARQUET)";
            cmd.ExecuteNonQuery();
        }
    }

    public void ExportParquet(string parquetPath)
    {
        ExportParquet(parquetPath, DefaultFilterId, null);
    }

    public void ExportParquet(string parquetPath, int? limit)
    {
        ExportParquet(parquetPath, DefaultFilterId, limit);
    }

    public void Clear(string filterId)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM results WHERE filter_id = '{EscapeSqlLiteral(filterId)}'";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Drop all results in the default filter partition.
    /// </summary>
    public void Clear()
    {
        Clear(DefaultFilterId);
    }

    public void Dispose()
    {
        _conn.Dispose();
    }

    private string BuildTallySelectList()
    {
        if (_tallyCount <= 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < _tallyCount; i++)
            sb.Append($", tally{i}");
        return sb.ToString();
    }

    private static string EscapeSqlPath(string path) => path.Replace("\\", "/").Replace("'", "''");
    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");
}

/// <summary>
/// One result row: seed, score, and per-should-clause tallies.
/// </summary>
public readonly record struct ResultRow(string Seed, int Score, int[] Tallies);
