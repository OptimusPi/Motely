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
    private readonly string _tableName;
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

        // Schema-mismatch handling is NON-DESTRUCTIVE. If the JAML's scored-clause shape
        // changed since the table was created (e.g. a `should:` clause added/removed/renamed),
        // the IF NOT EXISTS above silently keeps the stale schema. Rather than DROP+recreate
        // (which silently wiped every previously-saved seed), we resolve a target table that
        // preserves existing data: ALTER TABLE to add new tally columns when the change is
        // purely additive, otherwise append to a fresh versioned table. See ResolveTargetTable.
        _tableName = ResolveTargetTable(filterId, tallyColumns);

        _appender = _connection.CreateAppender("lake", "main", _tableName);
    }

    /// <summary>
    /// Decides which table to append into without ever destroying existing data.
    /// <list type="bullet">
    /// <item>Schema matches → the base <c>seeds_&lt;filterId&gt;</c> table.</item>
    /// <item>Purely additive drift (existing columns are a prefix of the expected columns,
    /// only new tally columns appended) → <c>ALTER TABLE ... ADD COLUMN</c> on the base table.</item>
    /// <item>Incompatible drift (columns renamed/removed/reordered) → a new versioned table
    /// <c>seeds_&lt;filterId&gt;_v2/_v3/...</c>; old data is left intact (warning to stderr).</item>
    /// <item>Schema read failed → fail safe: keep existing data, append to the base table as-is
    /// (warning to stderr). We never DROP on a read error.</item>
    /// </list>
    /// </summary>
    private string ResolveTargetTable(string filterId, IReadOnlyList<string> tallyColumns)
    {
        string baseTable = $"seeds_{filterId}";

        var expected = new List<string>(2 + tallyColumns.Count) { "seed", "score" };
        expected.AddRange(tallyColumns);

        List<string> actual;
        try
        {
            actual = ReadColumns(baseTable);
        }
        catch (Exception ex)
        {
            // A schema-READ failure must NOT trigger a destructive recreate. Fail safe by
            // preserving whatever exists and appending to the base table as-is.
            Console.Error.WriteLine(
                $"[MotelyLakeSeedSink] WARNING: could not read schema for table '{baseTable}' "
                    + $"({ex.Message}). Preserving existing data; appending to base table without recreate."
            );
            return baseTable;
        }

        // Exact match → use the base table.
        if (ColumnsEqual(actual, expected))
            return baseTable;

        // Purely additive drift → ALTER TABLE ADD COLUMN for each new tally column. No data loss.
        if (IsPrefix(actual, expected))
        {
            for (int i = actual.Count; i < expected.Count; i++)
                Exec(
                    $"ALTER TABLE lake.\"{EscapeIdent(baseTable)}\" "
                        + $"ADD COLUMN \"{EscapeIdent(expected[i])}\" INTEGER"
                );
            return baseTable;
        }

        // Incompatible drift (renamed/removed/reordered columns): do NOT drop. Append to a new
        // versioned table, leaving the old data untouched.
        string versioned = NextVersionedTableName(baseTable);
        Exec(BuildCreateTableSqlForTable(versioned, tallyColumns));
        Console.Error.WriteLine(
            $"[MotelyLakeSeedSink] WARNING: scored-clause schema for '{baseTable}' changed in a "
                + $"non-additive way (existing columns are not a prefix of the new schema). Old data "
                + $"preserved; new results will be written to '{versioned}'."
        );
        return versioned;
    }

    /// <summary>Read the column names (in ordinal order) of a table in <c>lake.main</c>.
    /// Returns an empty list when the table does not exist. Exceptions propagate to the caller,
    /// which fails safe (no recreate).</summary>
    private List<string> ReadColumns(string tableName)
    {
        var cols = new List<string>();
        using DbCommand cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT column_name FROM information_schema.columns "
            + "WHERE table_catalog = 'lake' AND table_schema = 'main' "
            + $"AND table_name = '{tableName.Replace("'", "''", StringComparison.Ordinal)}' "
            + "ORDER BY ordinal_position";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            cols.Add(reader.GetString(0));
        return cols;
    }

    /// <summary>Pick the next free <c>seeds_&lt;filterId&gt;_vN</c> table name (N starts at 2).</summary>
    private string NextVersionedTableName(string baseTable)
    {
        string prefix = baseTable + "_v";
        int max = 1;

        using DbCommand cmd = _connection.CreateCommand();
        cmd.CommandText =
            "SELECT table_name FROM information_schema.tables "
            + "WHERE table_catalog = 'lake' AND table_schema = 'main'";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            if (
                name.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(name.AsSpan(prefix.Length), out int v)
                && v > max
            )
                max = v;
        }

        return $"{prefix}{max + 1}";
    }

    /// <summary>True when <paramref name="a"/> and <paramref name="b"/> hold the same column
    /// names in the same order (case-insensitive).</summary>
    private static bool ColumnsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
            if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                return false;
        return true;
    }

    /// <summary>True when <paramref name="prefix"/> is a strict leading subsequence of
    /// <paramref name="full"/> (same names, same order, and <paramref name="full"/> has at least
    /// one extra trailing column) — i.e. the change is purely additive.</summary>
    private static bool IsPrefix(IReadOnlyList<string> prefix, IReadOnlyList<string> full)
    {
        if (prefix.Count >= full.Count)
            return false;
        for (int i = 0; i < prefix.Count; i++)
            if (!string.Equals(prefix[i], full[i], StringComparison.OrdinalIgnoreCase))
                return false;
        return true;
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

            try
            {
                _appender.Close();
            }
            catch
            { /* swallow on dispose */
            }
            _appender.Dispose();
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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "seed", "score" };

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

    private static string BuildCreateTableSql(string filterId, IReadOnlyList<string> tallyColumns) =>
        BuildCreateTableSqlForTable($"seeds_{filterId}", tallyColumns);

    private static string BuildCreateTableSqlForTable(
        string tableName,
        IReadOnlyList<string> tallyColumns
    )
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("CREATE TABLE IF NOT EXISTS lake.\"");
        sb.Append(EscapeIdent(tableName));
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
