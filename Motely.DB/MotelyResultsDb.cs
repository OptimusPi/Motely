using System.Text;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Dead-simple DuckDB result store. File-backed, in-memory fast.
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
    /// Opens or creates a result database.
    /// </summary>
    /// <param name="dbPath">File path for persistence, or ":memory:" for pure in-memory.</param>
    /// <param name="tallyCount">Number of should-clause tally columns.</param>
    public MotelyResultsDb(string dbPath, int tallyCount)
    {
        _tallyCount = Math.Max(0, tallyCount);
        _conn = new DuckDBConnection($"Data Source={dbPath}");
        _conn.Open();
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
        if (rows.Length == 0) return;

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

    /// <summary>
    /// Export all results as CSV string (seed,score,tally0,...,tallyN).
    /// </summary>
    public string ExportCsv()
    {
        lock (_lock)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM results ORDER BY score DESC";
            using var reader = cmd.ExecuteReader();

            var sb = new StringBuilder();
            // Header
            sb.Append("seed,score");
            for (int i = 0; i < _tallyCount; i++)
                sb.Append($",tally{i}");
            sb.AppendLine();

            while (reader.Read())
            {
                sb.Append(reader.GetString(0));
                sb.Append(',');
                sb.Append(reader.GetInt32(1));
                for (int i = 0; i < _tallyCount; i++)
                {
                    sb.Append(',');
                    sb.Append(reader.GetInt32(2 + i));
                }
                sb.AppendLine();
            }
            return sb.ToString();
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
}

/// <summary>
/// One result row: seed, score, and per-should-clause tallies.
/// </summary>
public readonly record struct ResultRow(string Seed, int Score, int[] Tallies);
