#if !BROWSER

using DuckDB.NET.Data;

namespace Motely.Datalake;

public sealed class DuckLakeSink : ISeedResultSink
{
    private const int FlushThreshold = 1000;

    private readonly DuckDBConnection _conn;
    private readonly string _tableName;
    private readonly int _tallyCount;
    private readonly List<(string Seed, int Score, int[] Tallies)> _buffer = new();
    private readonly System.Threading.Lock _lock = new();

    public string OutputPath { get; }

    internal DuckLakeSink(DuckDBConnection conn, string tableName, int tallyCount, string outputPath)
    {
        _conn = conn;
        _tableName = tableName;
        _tallyCount = tallyCount;
        OutputPath = outputPath;
    }

    public void AppendScoredResult(string seed, int score, ReadOnlySpan<int> tallies)
    {
        var tallyArr = tallies.ToArray();
        lock (_lock)
        {
            _buffer.Add((seed, score, tallyArr));
            if (_buffer.Count >= FlushThreshold)
                FlushLocked();
        }
    }

    private void FlushLocked()
    {
        if (_buffer.Count == 0) return;

        var tallyCols = string.Join(", ", Enumerable.Range(0, _tallyCount).Select(i => $"t{i}"));
        var tallyParams = string.Join(", ", Enumerable.Range(0, _tallyCount).Select(i => $"$t{i}"));
        var insertSql = $"INSERT INTO lake.{_tableName} (seed, score{(_tallyCount > 0 ? ", " + tallyCols : "")}) VALUES ($seed, $score{(_tallyCount > 0 ? ", " + tallyParams : "")})";

        using var tx = _conn.BeginTransaction();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = insertSql;

        foreach (var (seed, score, tallies) in _buffer)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new DuckDBParameter("seed", seed));
            cmd.Parameters.Add(new DuckDBParameter("score", score));
            for (int i = 0; i < _tallyCount; i++)
                cmd.Parameters.Add(new DuckDBParameter($"t{i}", i < tallies.Length ? tallies[i] : 0));
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        _buffer.Clear();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            FlushLocked();
        }
        _conn.Dispose();
    }
}

#endif
