using System.Data.Common;
using DuckDB.NET.Data;
using Motely.SeedProviders;

namespace Motely.CLI;

public sealed class SeedSourceProvider : IMotelySeedProvider, IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly DbDataReader _reader;
    private readonly System.Threading.Lock _lock = new();
    private bool _disposed;

    public long SeedCount { get; }

    /// <summary>
    /// Streams seeds out of any container DuckDB can open — CSV, TXT, Parquet, or a DuckDB
    /// .db/.duckdb database — local or remote. <paramref name="distinct"/> is the --drown path
    /// over a seed-lake file: dedupes and applies the seed shape test so bare-seed files,
    /// headered legacy files, and stray junk all read clean. The default path keeps the raw
    /// --source/--seeds contract: every line is data.
    /// </summary>
    public SeedSourceProvider(string path, bool distinct = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = path.Replace('\\', '/');

        _connection = new DuckDBConnection("Data Source=:memory:;threads=1");
        _connection.Open();

        if (IsRemotePath(path))
        {
            using var extCmd = _connection.CreateCommand();
            extCmd.CommandText = "INSTALL httpfs; LOAD httpfs;";
            extCmd.ExecuteNonQuery();
        }

        string select = distinct ? "SELECT DISTINCT" : "SELECT";
        string count = distinct ? "COUNT(DISTINCT #1)" : "COUNT(*)";
        // The lake accepts exactly what a Balatro seed is: base-35, [1-9A-Z], up to 8 chars.
        // header = false keeps every row (auto-detect eats a seed in single-column files);
        // the shape test drops legacy header rows ("Seed") and stray junk instead.
        string seedShape = distinct ? " WHERE #1 SIMILAR TO '[1-9A-Z]{1,8}'" : "";

        // A directory is a catalog: every CSV/TXT in it pours in as one source.
        if (Directory.Exists(path))
            path = path.TrimEnd('/') + "/*.csv";

        var ext = Path.GetExtension(path).ToLowerInvariant();
        string from = ext switch
        {
            ".parquet" or ".pq" => $"read_parquet('{EscapeSql(path)}')",
            ".db" or ".duckdb" => AttachSeedTable(path),
            // CSV and TXT both stream through read_csv: seed = first field of each line.
            // DuckDB positional reference (#1) never depends on a generated column name
            // (column0 vs column00 flips at 11+ columns; duckdb#19724). null_padding keeps
            // ragged short rows (e.g. "SEED,1") instead of erroring.
            _ =>
                $"read_csv('{EscapeSql(path)}', header = false, null_padding = true, all_varchar = true)",
        };

        var sql = $"{select} #1 AS seed FROM {from}{seedShape}";
        var countSql = $"SELECT {count} FROM {from}{seedShape}";

        using var countCmd = _connection.CreateCommand();
        countCmd.CommandText = countSql;
        SeedCount = Convert.ToInt64(countCmd.ExecuteScalar());

        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.UseStreamingMode = true;
        _reader = cmd.ExecuteReader();
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_lock)
        {
            if (_disposed)
                return ReadOnlySpan<char>.Empty;

            while (_reader.Read())
            {
                var seed = _reader.GetString(0);
                if (!string.IsNullOrEmpty(seed))
                    return seed;
            }
            return ReadOnlySpan<char>.Empty;
        }
    }

    public int NextSeeds(string[] buffer)
    {
        if (buffer is not { Length: > 0 })
            return 0;

        lock (_lock)
        {
            if (_disposed)
                return 0;

            int count = 0;
            while (count < buffer.Length && _reader.Read())
            {
                var seed = _reader.GetString(0);
                if (!string.IsNullOrEmpty(seed))
                    buffer[count++] = seed;
            }
            return count;
        }
    }

    /// <summary>Attach a DuckDB database read-only and resolve its seed table: a table named
    /// "seeds" when present, otherwise the database's only table. Seeds ride the first column.</summary>
    private string AttachSeedTable(string path)
    {
        using (var attach = _connection.CreateCommand())
        {
            attach.CommandText = $"ATTACH '{EscapeSql(path)}' AS src (READ_ONLY)";
            attach.ExecuteNonQuery();
        }

        var tables = new List<string>();
        using (var q = _connection.CreateCommand())
        {
            q.CommandText = "SELECT table_name FROM duckdb_tables() WHERE database_name = 'src'";
            using var r = q.ExecuteReader();
            while (r.Read())
                tables.Add(r.GetString(0));
        }

        string? table = tables.Find(t => t.Equals("seeds", StringComparison.OrdinalIgnoreCase));
        table ??= tables.Count == 1 ? tables[0] : null;
        if (table is null)
            throw new InvalidOperationException(
                $"Seed database '{path}' resolves with a table named 'seeds' or exactly one table; it has: [{string.Join(", ", tables)}]."
            );

        return $"src.\"{table.Replace("\"", "\"\"")}\"";
    }

    private static string EscapeSql(string path) => path.Replace("'", "''");

    private static bool IsRemotePath(string path) =>
        path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _reader.Dispose();
        _connection.Dispose();
    }
}
