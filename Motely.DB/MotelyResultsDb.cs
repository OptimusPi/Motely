using System.Text;

namespace Motely.DB;

public sealed class MotelyResultsDb : IDisposable
{
    private readonly DuckLakeConnection _lake;
    private readonly int _tallyCount;
    private readonly object _lock = new();
    private const string DefaultFilterId = "";

    public int TallyCount => _tallyCount;

    public MotelyResultsDb(string dbPath, int tallyCount)
    {
        _tallyCount = Math.Max(0, tallyCount);
        _lake = new DuckLakeConnection(dbPath);
        CreateTable();
        EnsureFilterIdColumn();
    }

    private void EnsureFilterIdColumn()
    {
        _lake.Execute("ALTER TABLE results ADD COLUMN IF NOT EXISTS filter_id TEXT");
        _lake.Execute("UPDATE results SET filter_id = '' WHERE filter_id IS NULL");
    }

    private void CreateTable()
    {
        var sb = new StringBuilder();
        sb.Append("CREATE TABLE IF NOT EXISTS results (filter_id TEXT NOT NULL DEFAULT '', seed TEXT NOT NULL, score INTEGER NOT NULL");
        for (int i = 0; i < _tallyCount; i++)
            sb.Append($", tally{i} INTEGER NOT NULL DEFAULT 0");
        sb.Append(')');
        _lake.Execute(sb.ToString());
    }

    public void AppendResults(string filterId, ReadOnlySpan<ResultRow> rows)
    {
        if (rows.Length == 0)
            return;

        lock (_lock)
        {
            using var appender = _lake.CreateAppender("results");
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

    public void AppendResults(ReadOnlySpan<ResultRow> rows) => AppendResults(DefaultFilterId, rows);

    public void AppendResult(string filterId, string seed, int score, ReadOnlySpan<int> tallies)
    {
        lock (_lock)
        {
            using var appender = _lake.CreateAppender("results");
            var r = appender.CreateRow();
            r.AppendValue(filterId);
            r.AppendValue(seed);
            r.AppendValue(score);
            for (int i = 0; i < _tallyCount; i++)
                r.AppendValue(i < tallies.Length ? tallies[i] : 0);
            r.EndRow();
        }
    }

    public void AppendResult(string seed, int score, ReadOnlySpan<int> tallies) =>
        AppendResult(DefaultFilterId, seed, score, tallies);

    public List<ResultRow> GetTopResults(string filterId, int limit = 1000)
    {
        lock (_lock)
        {
            using var cmd = _lake.CreateCommand();
            cmd.CommandText = $"SELECT seed, score{BuildTallySelectList()} FROM results WHERE filter_id = '{DuckLakeConnection.EscapeLiteral(filterId)}' ORDER BY score DESC LIMIT {limit}";
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

    public List<ResultRow> GetTopResults(int limit = 1000) => GetTopResults(DefaultFilterId, limit);

    public List<string> GetSeeds(string filterId)
    {
        lock (_lock)
        {
            using var cmd = _lake.CreateCommand();
            cmd.CommandText = $"SELECT seed FROM results WHERE filter_id = '{DuckLakeConnection.EscapeLiteral(filterId)}'";
            using var reader = cmd.ExecuteReader();

            var results = new List<string>();
            while (reader.Read())
                results.Add(reader.GetString(0));
            return results;
        }
    }

    public List<string> GetSeeds() => GetSeeds(DefaultFilterId);

    public long GetCount(string filterId)
    {
        lock (_lock)
        {
            using var cmd = _lake.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM results WHERE filter_id = '{DuckLakeConnection.EscapeLiteral(filterId)}'";
            return (long)cmd.ExecuteScalar()!;
        }
    }

    public long Count => GetCount(DefaultFilterId);

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
            using var cmd = _lake.CreateCommand();
            cmd.CommandText =
                $"COPY (SELECT seed, score{BuildTallySelectList()} FROM results WHERE filter_id = '{DuckLakeConnection.EscapeLiteral(filterId)}' ORDER BY score DESC{limitClause}) TO '{DuckLakeConnection.EscapePath(fullPath)}' (FORMAT PARQUET)";
            cmd.ExecuteNonQuery();
        }
    }

    public void ExportParquet(string parquetPath) => ExportParquet(parquetPath, DefaultFilterId, null);

    public void ExportParquet(string parquetPath, int? limit) => ExportParquet(parquetPath, DefaultFilterId, limit);

    public void Clear(string filterId)
    {
        lock (_lock)
        {
            _lake.Execute($"DELETE FROM results WHERE filter_id = '{DuckLakeConnection.EscapeLiteral(filterId)}'");
        }
    }

    public void Clear() => Clear(DefaultFilterId);

    public void Dispose() => _lake.Dispose();

    private string BuildTallySelectList()
    {
        if (_tallyCount <= 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < _tallyCount; i++)
            sb.Append($", tally{i}");
        return sb.ToString();
    }
}

public readonly record struct ResultRow(string Seed, int Score, int[] Tallies);
