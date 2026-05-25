using System.Data.Common;
using DuckDB.NET.Data;
using Motely.Filters;

namespace Motely.DataLake;

/// <summary>
/// Streams scored seeds into a DuckLake catalog at the given root.
/// One table per filter: <c>lake."seeds_&lt;filterId&gt;"</c> with columns
/// <c>seed</c>, <c>score</c>, plus one INTEGER column per JAML tally label.
/// Layout under <paramref name="seedsRoot"/>:
///   <c>catalog.ducklake</c> (DuckDB catalog), <c>data/</c> (Parquet data files).
/// </summary>
public sealed class MotelyLakeSeedSink : IDisposable
{
    private readonly object _gate = new();
    private readonly DuckDBConnection _connection;
    private readonly DuckDBAppender _appender;
    private readonly string _filterId;
    private readonly int _tallyCount;
    private bool _disposed;

    public string SeedsRoot { get; }
    public string CatalogPath { get; }
    public string DataPath { get; }

    public MotelyLakeSeedSink(string seedsRoot, string filterId, IReadOnlyList<string> tallyLabels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedsRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterId);
        ArgumentNullException.ThrowIfNull(tallyLabels);

        SeedsRoot = Path.GetFullPath(seedsRoot);
        Directory.CreateDirectory(SeedsRoot);
        Directory.CreateDirectory(Path.Combine(SeedsRoot, "data"));

        CatalogPath = Path.Combine(SeedsRoot, "catalog.ducklake");
        DataPath = Path.Combine(SeedsRoot, "data");

        _filterId = filterId;
        var tallyColumns = BuildUniqueColumnNames(tallyLabels);
        _tallyCount = tallyColumns.Length;

        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();

        Exec("INSTALL ducklake");
        Exec("LOAD ducklake");
        Exec(
            $"ATTACH 'ducklake:{ToSqlPathLiteral(CatalogPath)}' AS lake (DATA_PATH '{ToSqlPathLiteral(DataPath)}/')"
        );
        Exec(BuildCreateTableSql(filterId, tallyColumns));

        _appender = _connection.CreateAppender("lake", $"seeds_{filterId}");
    }

    /// <summary>Append one scored seed. Thread-safe (serialized via internal lock).</summary>
    public void Append(in MotelySeedScoreTally tally)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var row = _appender.CreateRow();
            row.AppendValue(tally.Seed);
            row.AppendValue(tally.Score);

            var tallies = tally.TallyValuesSpan;
            int written = 0;
            for (; written < tallies.Length && written < _tallyCount; written++)
                row.AppendValue(tallies[written]);

            // Pad missing tallies with NULL so the row width always matches the table.
            for (; written < _tallyCount; written++)
                row.AppendNullValue();

            row.EndRow();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;

            try { _appender.Close(); } catch { /* swallow on dispose */ }
            _appender.Dispose();
            try { Exec("CHECKPOINT lake"); } catch { /* best-effort WAL flush */ }
            _connection.Dispose();
        }
    }

    private void Exec(string sql)
    {
        using DbCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    internal static string[] BuildUniqueColumnNames(IReadOnlyList<string> tallyLabels)
    {
        var result = new string[tallyLabels.Count];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "seed",
            "score",
        };

        for (int i = 0; i < tallyLabels.Count; i++)
        {
            string baseName = string.IsNullOrWhiteSpace(tallyLabels[i])
                ? $"tally_{i + 1}"
                : tallyLabels[i].Trim();

            string candidate = baseName;
            int suffix = 2;
            while (!seen.Add(candidate))
                candidate = $"{baseName}_{suffix++}";

            result[i] = candidate;
        }

        return result;
    }

    private static string BuildCreateTableSql(string filterId, IReadOnlyList<string> tallyColumns)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("CREATE TABLE IF NOT EXISTS lake.\"seeds_");
        sb.Append(EscapeIdent(filterId));
        sb.Append("\" (\"seed\" VARCHAR, \"score\" INTEGER");

        for (int i = 0; i < tallyColumns.Count; i++)
        {
            sb.Append(", \"");
            sb.Append(EscapeIdent(tallyColumns[i]));
            sb.Append("\" INTEGER");
        }

        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>Escape a string for use inside a double-quoted SQL identifier.</summary>
    private static string EscapeIdent(string value) =>
        value.Replace("\"", "\"\"", StringComparison.Ordinal);

    /// <summary>Convert a .NET path to a DuckDB SQL string literal (forward slashes, single-quotes escaped).</summary>
    private static string ToSqlPathLiteral(string path) =>
        path.Replace("\\", "/", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal);
}

/// <summary>
/// Backward-compatible alias for older callers. Prefer <see cref="MotelyLakeSeedSink"/>.
/// </summary>
[Obsolete("Use MotelyLakeSeedSink instead.")]
public sealed class DuckLakeSeedSink : IDisposable
{
    private readonly MotelyLakeSeedSink _inner;

    public string SeedsRoot => _inner.SeedsRoot;
    public string CatalogPath => _inner.CatalogPath;
    public string DataPath => _inner.DataPath;

    public DuckLakeSeedSink(string seedsRoot, string filterId, IReadOnlyList<string> tallyLabels)
    {
        _inner = new MotelyLakeSeedSink(seedsRoot, filterId, tallyLabels);
    }

    public void Append(in MotelySeedScoreTally tally) => _inner.Append(in tally);

    public void Dispose() => _inner.Dispose();
}
