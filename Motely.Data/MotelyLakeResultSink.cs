#if !BROWSER

using System.Text;
using DuckDB.NET.Data;
using Motely;
using Motely.Filters;

namespace Motely.Data;

/// <summary>
/// Writes a run's scored seeds into the DuckLake the way the DuckLake maintainers prescribe. The
/// DuckDB appender can't target a lake table directly — it's specific to DuckDB's storage format
/// and bypasses the catalog (see commit history / ducklake#46) — so rows are appended into an
/// in-memory staging table, then moved into the lake with a single <c>MERGE … SELECT</c> that goes
/// through the catalog and dedupes on seed. On dispose a <c>CHECKPOINT</c> flushes inlined rows to
/// Parquet, runs maintenance, and clears the catalog WAL before the connection closes. One
/// <c>seeds_&lt;filterId&gt;</c> table per filter; the tally columns are fixed (<c>t0..tN</c>).
/// </summary>
public sealed class MotelyLakeResultSink : IMotelyResultSink
{
    private readonly object _gate = new();
    private readonly DuckDBConnection _connection;
    private readonly DuckDBAppender _appender;
    private readonly string _table;
    private readonly string _staging;
    private readonly int _tallyCount;
    private long _staged;
    private bool _disposed;

    public MotelyLakeResultSink(string filterId, int tallyCount)
    {
        _table = $"seeds_{filterId}";
        _staging = $"staging_{filterId}";
        _tallyCount = tallyCount;
        _connection = MotelyDuckLake.Open(attachLake: true);

        // The lake table (in the catalog) and an in-memory staging table with the same shape.
        MotelyDuckLake.Execute(_connection, Schema($"{MotelyDuckLake.LakeAlias}.\"{_table}\""));
        MotelyDuckLake.Execute(_connection, Schema($"\"{_staging}\""));
        _appender = _connection.CreateAppender(_staging);
    }

    public void OnSeed(string seed) { }

    public void OnScored(in MotelyScoredSeedResult result)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            var row = _appender.CreateRow();
            row.AppendValue(result.Seed).AppendValue(result.Score);
            int[] tallies = result.Tallies;
            for (int i = 0; i < _tallyCount; i++)
                row.AppendValue(i < tallies.Length ? tallies[i] : 0);
            row.EndRow();
            _staged++;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;

            _appender.Close(); // flush appended rows into the staging table
            if (_staged > 0)
            {
                MotelyDuckLake.Execute(
                    _connection,
                    $"MERGE INTO {MotelyDuckLake.LakeAlias}.\"{_table}\" AS target "
                        + $"USING \"{_staging}\" AS source ON target.seed = source.seed "
                        + $"WHEN NOT MATCHED THEN INSERT VALUES ({InsertValues()})"
                );
            }
            // Flush inlined rows to Parquet, run maintenance, and clear the catalog WAL.
            TryExecute($"CHECKPOINT {MotelyDuckLake.LakeAlias}");
            _connection.Dispose();
        }
    }

    private string Schema(string target)
    {
        var schema = new StringBuilder(
            $"CREATE TABLE IF NOT EXISTS {target} (seed VARCHAR, score INTEGER"
        );
        for (int i = 0; i < _tallyCount; i++)
            schema.Append(", \"t").Append(i).Append("\" INTEGER");
        return schema.Append(')').ToString();
    }

    private string InsertValues()
    {
        var values = new StringBuilder("source.seed, source.score");
        for (int i = 0; i < _tallyCount; i++)
            values.Append(", source.\"t").Append(i).Append('"');
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
