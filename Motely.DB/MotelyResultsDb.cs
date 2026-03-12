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

    /// <summary>Number of tally columns (one per should-clause).</summary>
    public int TallyCount => _tallyCount;

    /// <summary>
    /// Opens or creates a result database using DuckLake.
    /// </summary>
    /// <param name="dbPath">File path for persistence (treated as a DuckLake folder base), or ":memory:" for pure in-memory.</param>
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
            var fullPath = Path.GetFullPath(dbPath);
            var directory = Path.GetDirectoryName(fullPath);
            var baseName = Path.GetFileNameWithoutExtension(fullPath);
            var basePath = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Directory.GetCurrentDirectory(), baseName)
                : Path.Combine(directory, baseName);
            var lakeDir = $"{basePath}_lake";
            var metaFile = Path.Combine(lakeDir, "metadata.ducklake");
            var dataDir = Path.Combine(lakeDir, "data");
            
            Directory.CreateDirectory(lakeDir);
            Directory.CreateDirectory(dataDir);

            // Install and attach the DuckLake
            cmd.CommandText = "INSTALL ducklake; LOAD ducklake;";
            cmd.ExecuteNonQuery();

            // Note: Current DuckDB syntax for attaching DuckLake
            cmd.CommandText = $"ATTACH 'ducklake:{EscapeSqlPath(metaFile)}' AS motely_lake (DATA_PATH '{EscapeSqlPath(dataDir)}');";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "USE motely_lake;";
            cmd.ExecuteNonQuery();
        }

        CreateTable();
    }

    private void CreateTable()
    {
        var sb = new StringBuilder();
        sb.Append("CREATE TABLE IF NOT EXISTS results (seed TEXT NOT NULL, score INTEGER NOT NULL");
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
    public void AppendResults(ReadOnlySpan<ResultRow> rows)
    {
        if (rows.Length == 0)
            return;

        lock (_lock)
        {
            using var appender = _conn.CreateAppender("results");
            foreach (ref readonly var row in rows)
            {
                var r = appender.CreateRow();
                r.AppendValue(row.Seed);
                r.AppendValue(row.Score);
                for (int i = 0; i < _tallyCount; i++)
                    r.AppendValue(i < row.Tallies.Length ? row.Tallies[i] : 0);
                r.EndRow();
            }
        }
    }

    /// <summary>
    /// Insert a single result row.
    /// </summary>
    public void AppendResult(string seed, int score, ReadOnlySpan<int> tallies)
    {
        lock (_lock)
        {
            using var appender = _conn.CreateAppender("results");
            var r = appender.CreateRow();
            r.AppendValue(seed);
            r.AppendValue(score);
            for (int i = 0; i < _tallyCount; i++)
                r.AppendValue(i < tallies.Length ? tallies[i] : 0);
            r.EndRow();
        }
    }

    /// <summary>
    /// Get top N results ordered by score descending.
    /// </summary>
    public List<ResultRow> GetTopResults(int limit = 1000)
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM results ORDER BY score DESC LIMIT {limit}";
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

    public List<string> GetSeeds()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT seed FROM results";
            using var reader = cmd.ExecuteReader();

            var results = new List<string>();
            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }

            return results;
        }
    }

    /// <summary>
    /// Total number of stored results.
    /// </summary>
    public long Count
    {
        get
        {
            lock (_lock)
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM results";
                return (long)cmd.ExecuteScalar()!;
            }
        }
    }

    public void ExportParquet(string parquetPath)
    {
        ExportParquet(parquetPath, null);
    }

    public void ExportParquet(string parquetPath, int? limit)
    {
        if (string.IsNullOrWhiteSpace(parquetPath))
            throw new ArgumentException("Parquet path is required.", nameof(parquetPath));

        var fullPath = Path.GetFullPath(parquetPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var escapedPath = fullPath.Replace("'", "''");
        var limitClause = limit is > 0 ? $" LIMIT {limit.Value}" : string.Empty;

        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = $"COPY (SELECT * FROM results ORDER BY score DESC{limitClause}) TO '{EscapeSqlPath(fullPath)}' (FORMAT PARQUET)";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Drop all results.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM results";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _conn.Dispose();
    }

    private static string EscapeSqlPath(string path) => path.Replace("\\", "/").Replace("'", "''");
}

/// <summary>
/// One result row: seed, score, and per-should-clause tallies.
/// </summary>
public readonly record struct ResultRow(string Seed, int Score, int[] Tallies);
