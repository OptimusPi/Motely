#if !BROWSER

using System.Text;
using DuckDB.NET.Data;
using Motely;
using Motely.Filters;

namespace Motely.Data;

/// <summary>
/// Persists scored seeds to a per-run Parquet file the drown provider can glob:
/// <c>&lt;root&gt;/&lt;filterId&gt;/&lt;UTC-stamp&gt;.parquet</c>. One file per run; the lake
/// accumulates across runs by directory. No catalog, no schema-drift detection — every run
/// writes a fresh file with the same column shape (seed, score, one INTEGER per tally label).
/// </summary>
public sealed class MotelyParquetSeedSink : IMotelyResultSink
{
    private readonly object _gate = new();
    private readonly string _lakeDir;
    private readonly string[] _tallyLabels;
    private readonly List<(string Seed, int Score, int[] Tallies)> _rows = [];
    private bool _disposed;

    public MotelyParquetSeedSink(string? root, string filterId, IReadOnlyList<string> tallyLabels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterId);
        _tallyLabels = [.. tallyLabels];
        _lakeDir = MotelyLakePaths.LakeDir(root, filterId);
    }

    public void OnSeed(string seed) { }

    public void OnScored(in MotelySeedScoreTally tally)
    {
        var tallies = tally.TallyValuesSpan;
        var copy = new int[_tallyLabels.Length];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = i < tallies.Length ? tallies[i] : 0;

        lock (_gate)
        {
            if (_disposed)
                return;
            _rows.Add((tally.Seed, tally.Score, copy));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;

            if (_rows.Count == 0)
                return;

            Directory.CreateDirectory(_lakeDir);
            string file = Path.Combine(_lakeDir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.parquet");

            using var conn = new DuckDBConnection("Data Source=:memory:");
            conn.Open();

            using (var create = conn.CreateCommand())
            {
                create.CommandText = BuildCreateTableSql();
                create.ExecuteNonQuery();
            }

            var appender = conn.CreateAppender("r");
            foreach (var (seed, score, tallies) in _rows)
            {
                var row = appender.CreateRow();
                row.AppendValue(seed);
                row.AppendValue(score);
                for (int i = 0; i < tallies.Length; i++)
                    row.AppendValue(tallies[i]);
                row.EndRow();
            }
            appender.Close();
            appender.Dispose();

            using var copy = conn.CreateCommand();
            copy.CommandText =
                $"COPY r TO '{file.Replace("\\", "/", StringComparison.Ordinal)}' (FORMAT PARQUET)";
            copy.ExecuteNonQuery();
        }
    }

    private string BuildCreateTableSql()
    {
        var sb = new StringBuilder("CREATE TABLE r (\"seed\" VARCHAR, \"score\" INTEGER");
        foreach (var label in _tallyLabels)
        {
            sb.Append(", \"");
            sb.Append(label.Replace("\"", "\"\"", StringComparison.Ordinal));
            sb.Append("\" INTEGER");
        }
        sb.Append(')');
        return sb.ToString();
    }
}

#endif
