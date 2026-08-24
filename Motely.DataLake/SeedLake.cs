using System.Data.Common;
using System.Globalization;
using System.Text;
using DuckDB.NET.Data;

namespace Motely.DataLake;

/// <summary>
/// The seed lake: one DuckLake that every filter and every writer share.
///
/// <list type="bullet">
/// <item><b>catalog</b> — <c>ducklake.sqlite</c> beside the data root (the repo root for the default
/// <c>Seeds/</c>), or <c>MOTELY_DATALAKE_CATALOG</c>. SQLite because several local clients write at
/// once — the CLI, helper-api's in-process pool worker and MotelyWorker — which is exactly the case the
/// DuckLake manual assigns to SQLite (a DuckDB catalog is single-client; Postgres is for remote
/// clients, and is the same ATTACH string with <c>postgres:</c> — nothing else here changes).</item>
/// <item><b>data</b> — the root itself (<c>Seeds/</c>): Parquet files DuckLake owns. Small flushes are
/// inlined into the catalog (<c>DATA_INLINING_ROW_LIMIT</c>) so a short run does not spray files.</item>
/// <item><b>tables</b> — <c>results(filter_id, seed, score, tallies, found_at)</c>: every find, scored or
/// not; <c>filters(filter_id, tally_labels, updated_at)</c>: what the tally positions mean.</item>
/// </list>
///
/// DuckLake has no constraints, so uniqueness is the writer's job: each instance skips seeds it has
/// already written for a filter (seeded from the catalog on first use) and every reader selects
/// DISTINCT. Writes are buffered and flushed as one transaction per batch (<see cref="FlushRows"/> rows
/// or <see cref="FlushInterval"/>, whichever first, and on Dispose) — a commit per find would be a catalog
/// transaction per find. A flush that keeps failing never drops rows: they stay buffered and, as a last
/// resort, spill to a CSV beside the data.
/// </summary>
public sealed class SeedLake : IDisposable
{
    public const string CatalogFileName = "ducklake.sqlite";
    public const string CatalogEnv = "MOTELY_DATALAKE_CATALOG";
    public const string DataRootEnv = "MOTELY_DATALAKE_PATH";

    /// <summary>Inserts at or below this many rows live in the catalog instead of a new Parquet file.</summary>
    public const int InliningRowLimit = 4096;
    /// <summary>Buffered rows that trigger a flush on the writing thread.</summary>
    public const int FlushRows = 512;
    /// <summary>The longest a find waits in memory before the timer flushes it.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(250);

    private const int MaxRetries = 8;
    private const int SpillAfterRows = 100_000;
    private const char LabelSeparator = (char)31; // ASCII unit separator: never appears in a tally label

    private readonly DuckDBConnection _connection;
    private readonly object _gate = new();
    private readonly List<Row> _buffer = new();
    private readonly Dictionary<string, HashSet<string>> _seen = new(StringComparer.Ordinal);
    private readonly Timer _timer;
    private long _written;
    private int _flushFailures;
    private bool _disposed;

    private readonly record struct Row(string FilterId, string Seed, int? Score, int[]? Tallies);

    /// <summary>A row of <c>results</c> read back.</summary>
    public readonly record struct Result(string Seed, int? Score, int[]? Tallies);

    public string DataPath { get; }
    public string CatalogPath { get; }

    /// <summary>The data root, absolute: <paramref name="root"/>, else <c>MOTELY_DATALAKE_PATH</c>, else <c>Seeds</c>.</summary>
    public static string DataRoot(string? root)
    {
        root ??= Environment.GetEnvironmentVariable(DataRootEnv);
        return Path.GetFullPath(string.IsNullOrWhiteSpace(root) ? "Seeds" : root);
    }

    /// <summary><c>MOTELY_DATALAKE_CATALOG</c>, else <c>ducklake.sqlite</c> beside the data root.</summary>
    public static string CatalogPathFor(string? root)
    {
        var env = Environment.GetEnvironmentVariable(CatalogEnv);
        if (!string.IsNullOrWhiteSpace(env))
            return Path.GetFullPath(env);
        var data = DataRoot(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(Path.GetDirectoryName(data) ?? data, CatalogFileName);
    }

    /// <summary>Is there a lake to read — has any writer created the catalog?</summary>
    public static bool Exists(string? root, string? catalogPath = null) =>
        File.Exists(catalogPath ?? CatalogPathFor(root));

    /// <summary>Open (creating if needed) the lake for <paramref name="root"/>. Throws when DuckLake
    /// cannot be attached — the extensions are not installed and cannot be downloaded, or the catalog
    /// path is unusable; callers fall back rather than lose finds.</summary>
    public static SeedLake Open(string? root, string? catalogPath = null) =>
        new(DataRoot(root), catalogPath is null ? CatalogPathFor(root) : Path.GetFullPath(catalogPath));

    private SeedLake(string dataPath, string catalogPath)
    {
        DataPath = dataPath;
        CatalogPath = catalogPath;
        Directory.CreateDirectory(dataPath);
        var catalogDir = Path.GetDirectoryName(catalogPath);
        if (!string.IsNullOrEmpty(catalogDir))
            Directory.CreateDirectory(catalogDir);

        _connection = new DuckDBConnection("Data Source=:memory:");
        try
        {
            _connection.Open();
            Attach(_connection, catalogPath, dataPath);
            // Two writers opening a brand-new lake at once both try to create the tables; the loser
            // sees a transaction conflict and simply tries again.
            ExecuteWithRetry(_connection,
                "CREATE TABLE IF NOT EXISTS lake.results (filter_id VARCHAR, seed VARCHAR, score INTEGER, tallies INTEGER[], found_at TIMESTAMP)");
            ExecuteWithRetry(_connection,
                "CREATE TABLE IF NOT EXISTS lake.filters (filter_id VARCHAR, tally_labels VARCHAR[], updated_at TIMESTAMP)");
        }
        catch
        {
            _connection.Dispose();
            throw;
        }
        _timer = new Timer(_ => TimerFlush(), null, FlushInterval, FlushInterval);
    }

    /// <summary>Attach the lake into <paramref name="connection"/> as catalog <c>lake</c>. Two
    /// processes attaching a brand-new catalog at once both try to create DuckLake's own metadata
    /// tables in it, and the loser sees "database is locked" from SQLite — so the attach itself
    /// retries like every other write (INSTALL/LOAD are idempotent).</summary>
    internal static void Attach(DuckDBConnection connection, string catalogPath, string dataPath)
    {
        // META_* options pass straight through to the sqlite catalog. WAL is not optional: in
        // SQLite's default rollback-journal mode two DuckLake writers each hold a SHARED read lock
        // on the catalog and both wait forever to upgrade it (measured: two processes, zero rows,
        // ten minutes). In WAL mode readers never block writers; commits then serialize on the
        // busy timeout plus DuckLake's own snapshot conflicts, which ExecuteWithRetry absorbs.
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "INSTALL ducklake; INSTALL sqlite; LOAD ducklake; LOAD sqlite; "
            + $"ATTACH 'ducklake:sqlite:{Sql(Slash(catalogPath))}' AS lake "
            + $"(DATA_PATH '{Sql(Slash(dataPath).TrimEnd('/'))}/', DATA_INLINING_ROW_LIMIT {InliningRowLimit}, "
            + "META_JOURNAL_MODE 'WAL', META_BUSY_TIMEOUT 10000)";
        ExecuteWithRetry(cmd);
    }

    // ── writing ────────────────────────────────────────────────────────────────────────────

    /// <summary>Record what this filter's tally positions mean (replaces any earlier row).</summary>
    public void RegisterFilter(string filterId, IReadOnlyList<string> tallyLabels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterId);
        var labels = new StringBuilder("[");
        for (int i = 0; i < tallyLabels.Count; i++)
        {
            if (i > 0)
                labels.Append(',');
            labels.Append((char)39).Append(Sql(tallyLabels[i])).Append((char)39);
        }
        labels.Append(']');

        lock (_gate)
        {
            ThrowIfDisposed();
            using var del = _connection.CreateCommand();
            del.CommandText = "DELETE FROM lake.filters WHERE filter_id = ?";
            del.Parameters.Add(new DuckDBParameter { Value = filterId });
            using var ins = _connection.CreateCommand();
            ins.CommandText = $"INSERT INTO lake.filters VALUES (?, {labels}, now())";
            ins.Parameters.Add(new DuckDBParameter { Value = filterId });
            ExecuteWithRetry(del);
            ExecuteWithRetry(ins);
        }
    }

    /// <summary>Queue one find. Thread-safe; returns once the row is buffered (or flushed, when the
    /// buffer is full). Seeds already in the lake for this filter are skipped.</summary>
    public void Write(string filterId, string seed, int? score, ReadOnlySpan<int> tallies)
    {
        if (string.IsNullOrEmpty(seed))
            return;
        ArgumentException.ThrowIfNullOrWhiteSpace(filterId);

        lock (_gate)
        {
            if (_disposed)
                return;
            if (!SeenFor(filterId).Add(seed))
                return;
            _buffer.Add(new Row(filterId, seed, score, tallies.IsEmpty ? null : tallies.ToArray()));
            if (_buffer.Count >= FlushRows)
                FlushOrKeep();
        }
    }

    /// <summary>Push every buffered row into the lake now. Throws if the lake refused the batch
    /// (the rows stay buffered).</summary>
    public void Flush()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            FlushLocked();
        }
    }

    /// <summary>Maintenance: move inlined rows out to Parquet and merge small files.</summary>
    public void Compact()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            FlushLocked();
            ExecuteWithRetry(_connection, "CALL ducklake_flush_inlined_data('lake')");
            ExecuteWithRetry(_connection, "CALL ducklake_merge_adjacent_files('lake')");
        }
    }

    private HashSet<string> SeenFor(string filterId)
    {
        if (_seen.TryGetValue(filterId, out var set))
            return set;
        set = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT seed FROM lake.results WHERE filter_id = ?";
        cmd.Parameters.Add(new DuckDBParameter { Value = filterId });
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            set.Add(reader.GetString(0));
        _seen[filterId] = set;
        return set;
    }

    private void TimerFlush()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            FlushOrKeep();
        }
    }

    /// <summary>Flush; on failure keep the rows for the next attempt (spilling to CSV once the buffer
    /// passes <see cref="SpillAfterRows"/>) and say so once, then every 50th time.</summary>
    private void FlushOrKeep()
    {
        try
        {
            FlushLocked();
            _flushFailures = 0;
        }
        catch (Exception ex)
        {
            if (_flushFailures++ % 50 == 0)
                Console.Error.WriteLine($"[SeedLake] flush of {_buffer.Count} rows failed ({FirstLine(ex.Message)}); keeping them buffered and retrying.");
            if (_buffer.Count >= SpillAfterRows)
                Spill("flush kept failing");
        }
    }

    private void FlushLocked()
    {
        if (_buffer.Count == 0)
            return;

        var sql = new StringBuilder("INSERT INTO lake.results (filter_id, seed, score, tallies, found_at) VALUES ");
        using var cmd = _connection.CreateCommand();
        for (int i = 0; i < _buffer.Count; i++)
        {
            var row = _buffer[i];
            if (i > 0)
                sql.Append(',');
            sql.Append("(?, ?, ?, ");
            if (row.Tallies is null)
                sql.Append("NULL");
            else
            {
                sql.Append('[');
                for (int t = 0; t < row.Tallies.Length; t++)
                {
                    if (t > 0) sql.Append(',');
                    sql.Append(row.Tallies[t].ToString(CultureInfo.InvariantCulture));
                }
                sql.Append(']');
            }
            sql.Append(", now())");
            cmd.Parameters.Add(new DuckDBParameter { Value = row.FilterId });
            cmd.Parameters.Add(new DuckDBParameter { Value = row.Seed });
            cmd.Parameters.Add(new DuckDBParameter { Value = row.Score.HasValue ? row.Score.Value : DBNull.Value });
        }
        cmd.CommandText = sql.ToString();
        ExecuteWithRetry(cmd);
        _written += _buffer.Count;
        _buffer.Clear();
    }

    /// <summary>Last resort so a find is never lost: the buffer goes to <c>&lt;data&gt;/_unflushed_*.csv</c>
    /// (seed-first, so <c>--drown</c> pours it like any other CSV) and is cleared.</summary>
    private void Spill(string why)
    {
        if (_buffer.Count == 0)
            return;
        var path = Path.Combine(DataPath, $"_unflushed_{Environment.ProcessId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}.csv");
        try
        {
            using var w = new StreamWriter(path, append: false, Encoding.UTF8);
            foreach (var row in _buffer)
            {
                w.Write(row.Seed);
                w.Write(',');
                w.Write(row.FilterId);
                w.Write(',');
                w.Write(row.Score?.ToString(CultureInfo.InvariantCulture) ?? "");
                if (row.Tallies is not null)
                    foreach (var t in row.Tallies)
                    {
                        w.Write(',');
                        w.Write(t.ToString(CultureInfo.InvariantCulture));
                    }
                w.Write('\n');
            }
            Console.Error.WriteLine($"[SeedLake] {why}: {_buffer.Count} rows spilled to {path}");
            _buffer.Clear();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SeedLake] could not even spill {_buffer.Count} rows to {path}: {FirstLine(ex.Message)}");
        }
    }

    // ── reading ────────────────────────────────────────────────────────────────────────────

    /// <summary>Distinct seeds, sorted — one filter's, or the whole lake's when <paramref name="filterId"/> is null.
    /// This instance's own buffered rows are flushed first so the answer includes them.</summary>
    public IReadOnlyList<string> Seeds(string? filterId = null)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            FlushLocked();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT seed FROM lake.results" + (filterId is null ? "" : " WHERE filter_id = ?") + " ORDER BY seed";
            if (filterId is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = filterId });
            var seeds = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                seeds.Add(reader.GetString(0));
            return seeds;
        }
    }

    public long DistinctSeedCount(string? filterId = null)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            FlushLocked();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(DISTINCT seed) FROM lake.results" + (filterId is null ? "" : " WHERE filter_id = ?");
            if (filterId is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = filterId });
            return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Every filter that has written at least one row.</summary>
    public IReadOnlyList<string> FilterIds()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            FlushLocked();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT filter_id FROM lake.results ORDER BY filter_id";
            var ids = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                ids.Add(reader.GetString(0));
            return ids;
        }
    }

    /// <summary>One filter's rows, one per distinct seed (the latest row wins), sorted by seed.</summary>
    public IReadOnlyList<Result> Results(string filterId)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            FlushLocked();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                SELECT seed, score, array_to_string(tallies, ',')
                FROM (SELECT seed, score, tallies, row_number() OVER (PARTITION BY seed ORDER BY found_at DESC) AS rn
                      FROM lake.results WHERE filter_id = ?)
                WHERE rn = 1 ORDER BY seed
                """;
            cmd.Parameters.Add(new DuckDBParameter { Value = filterId });
            var rows = new List<Result>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int? score = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                int[]? tallies = reader.IsDBNull(2) ? null : ParseInts(reader.GetString(2));
                rows.Add(new Result(reader.GetString(0), score, tallies));
            }
            return rows;
        }
    }

    /// <summary>The tally labels last registered for a filter, or null.</summary>
    public IReadOnlyList<string>? TallyLabels(string filterId)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT array_to_string(tally_labels, chr(31)) FROM lake.filters WHERE filter_id = ? ORDER BY updated_at DESC LIMIT 1";
            cmd.Parameters.Add(new DuckDBParameter { Value = filterId });
            var joined = cmd.ExecuteScalar() as string;
            return joined?.Split(LabelSeparator);
        }
    }

    /// <summary>Pour distinct seeds (one filter's, or every filter's) into <paramref name="table"/> of
    /// another DuckDB connection, then detach. False when there is no lake yet. Throws if the lake
    /// exists but cannot be attached.</summary>
    internal static bool TryPourInto(DuckDBConnection connection, string table, string? root, string? filterId, string? catalogPath = null)
    {
        catalogPath ??= CatalogPathFor(root);
        if (!File.Exists(catalogPath))
            return false;
        Attach(connection, catalogPath, DataRoot(root));
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"INSERT INTO {table} SELECT DISTINCT seed FROM lake.results" + (filterId is null ? "" : " WHERE filter_id = ?");
            if (filterId is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = filterId });
            cmd.ExecuteNonQuery();
        }
        finally
        {
            using var detach = connection.CreateCommand();
            detach.CommandText = "DETACH lake";
            detach.ExecuteNonQuery();
        }
        return true;
    }

    // ── plumbing ───────────────────────────────────────────────────────────────────────────

    private static void ExecuteWithRetry(DuckDBConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        ExecuteWithRetry(cmd);
    }

    /// <summary>Concurrent writers on one SQLite catalog see "database is locked" from SQLite and
    /// "Failed to commit DuckLake transaction" from DuckLake's optimistic snapshot commit (measured:
    /// two writers, about half of each one's commits lose the race). Both are transient — the
    /// statement is whole and re-runnable — so back off and try again.</summary>
    private static void ExecuteWithRetry(DbCommand cmd)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                cmd.ExecuteNonQuery();
                return;
            }
            catch (DuckDBException ex) when (attempt < MaxRetries && IsContention(ex))
            {
                Thread.Sleep((20 << attempt) + Random.Shared.Next(0, 20 << attempt));
            }
        }
    }

    private static bool IsContention(Exception ex) =>
        ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("busy", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Failed to commit", StringComparison.OrdinalIgnoreCase);

    private static int[] ParseInts(string joined)
    {
        if (joined.Length == 0)
            return [];
        var parts = joined.Split(',');
        var values = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            values[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
        return values;
    }

    private static string Sql(string s) => s.Replace("'", "''");
    private static string Slash(string path) => path.Replace('\\', '/');
    private static string FirstLine(string s) { int i = s.IndexOf('\n'); return i < 0 ? s : s[..i]; }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _timer.Dispose();
            try
            {
                FlushLocked();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SeedLake] final flush failed ({FirstLine(ex.Message)})");
                Spill("final flush failed");
            }
            // A run big enough to have crossed the inlining limit leaves its rows in Parquet rather than
            // in the catalog; small runs stay inlined and are cheap to keep there.
            if (_written >= InliningRowLimit)
            {
                try { ExecuteWithRetry(_connection, "CALL ducklake_flush_inlined_data('lake')"); }
                catch (Exception ex) { Console.Error.WriteLine($"[SeedLake] could not flush inlined data ({FirstLine(ex.Message)}); it stays in the catalog."); }
            }
            _connection.Dispose();
        }
    }
}
