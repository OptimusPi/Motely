#if !BROWSER

using System.Text;
using DuckDB.NET.Data;
using Motely;
using Motely.Filters;

namespace Motely.Data;

/// <summary>
/// Writes a run's scored seeds into the DuckLake. Rows buffer in memory — only the survivors past
/// cutoff, never the input volume — then flush as a single <c>MERGE INTO</c> that dedupes on seed
/// (so re-scanning a range never duplicates). On dispose a <c>CHECKPOINT</c> flushes inlined rows
/// to Parquet and clears the catalog WAL before the connection closes. One
/// <c>seeds_&lt;filterId&gt;</c> table per filter; the tally columns are fixed (<c>t0..tN</c>).
/// </summary>
public sealed class MotelyLakeResultSink : IMotelyResultSink
{
    private const int FlushThreshold = 10_000;

    private readonly object _gate = new();
    private readonly DuckDBConnection _connection;
    private readonly string _table;
    private readonly int _tallyCount;
    private readonly List<string> _rows = [];
    private bool _tableReady;
    private bool _disposed;

    public MotelyLakeResultSink(string filterId, int tallyCount)
    {
        _table = $"seeds_{filterId}";
        _tallyCount = tallyCount;
        _connection = MotelyDuckLake.Open(attachLake: true);
    }

    public void OnSeed(string seed) { }

    public void OnScored(in MotelyScoredSeedResult result)
    {
        var row = new StringBuilder("('")
            .Append(result.Seed.Replace("'", "''"))
            .Append("',")
            .Append(result.Score);
        int[] tallies = result.Tallies;
        for (int i = 0; i < _tallyCount; i++)
            row.Append(',').Append(i < tallies.Length ? tallies[i] : 0);
        row.Append(')');

        lock (_gate)
        {
            if (_disposed)
                return;
            _rows.Add(row.ToString());
            if (_rows.Count >= FlushThreshold)
                FlushLocked();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            FlushLocked();
            // Flush inlined rows to Parquet, run maintenance, and clear the catalog WAL.
            TryExecute($"CHECKPOINT {MotelyDuckLake.LakeAlias}");
            _connection.Dispose();
        }
    }

    private void FlushLocked()
    {
        if (_rows.Count == 0)
            return;
        EnsureTable();

        MotelyDuckLake.Execute(
            _connection,
            $"MERGE INTO {MotelyDuckLake.LakeAlias}.\"{_table}\" AS target "
                + $"USING (VALUES {string.Join(",", _rows)}) AS source({SourceColumns()}) "
                + "ON target.seed = source.seed "
                + $"WHEN NOT MATCHED THEN INSERT VALUES ({InsertValues()})"
        );
        _rows.Clear();
    }

    private void EnsureTable()
    {
        if (_tableReady)
            return;
        var schema = new StringBuilder(
            $"CREATE TABLE IF NOT EXISTS {MotelyDuckLake.LakeAlias}.\"{_table}\" (seed VARCHAR, score INTEGER"
        );
        for (int i = 0; i < _tallyCount; i++)
            schema.Append(", \"t").Append(i).Append("\" INTEGER");
        schema.Append(')');
        MotelyDuckLake.Execute(_connection, schema.ToString());
        _tableReady = true;
    }

    private string SourceColumns()
    {
        var columns = new StringBuilder("seed, score");
        for (int i = 0; i < _tallyCount; i++)
            columns.Append(", t").Append(i);
        return columns.ToString();
    }

    private string InsertValues()
    {
        var values = new StringBuilder("source.seed, source.score");
        for (int i = 0; i < _tallyCount; i++)
            values.Append(", source.t").Append(i);
        return values.ToString();
    }

    private void TryExecute(string sql)
    {
        try
        {
            MotelyDuckLake.Execute(_connection, sql);
        }
        catch
        {
            // Checkpoint maintenance is best-effort; a clean connection close still flushes.
        }
    }
}

#endif
