using System.Data.Common;
using DuckDB.NET.Data;
using Motely.SeedProviders;

namespace Motely.DataLake;

public sealed class SeedSourceProvider : IMotelySeedProvider, IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly DbDataReader _reader;
    private readonly System.Threading.Lock _lock = new();
    private bool _disposed;

    public long SeedCount { get; }

    /// <summary>
    /// Streams seeds out of any container DuckDB can open — CSV, TXT, Parquet, JSON, a JAML
    /// file's <c>seeds:</c> block, or a DuckDB/SQLite .db database — local or remote.
    /// <paramref name="distinct"/> is the --drown path over a seed-lake file: dedupes and
    /// applies the seed shape test so bare-seed files, headered legacy files, and stray junk
    /// all read clean. The default path keeps the raw --source/--seeds contract: every line
    /// is data. JSON and JAML sources always shape-test — they mix seeds with structure.
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

        bool wholeLake = false;
        if (Directory.Exists(path))
        {
            if (distinct)
            {
                // --drown: the directory *is* the lake. Every .duckdb lake file plus every
                // CSV/TXT in it pours into one in-memory table; the file locks are released
                // before the search starts.
                ImportLakeRoot(path);
                wholeLake = true;
            }
            else
            {
                // A directory as --source is a catalog: every CSV in it pours in as one source.
                path = path.TrimEnd('/') + "/*.csv";
            }
        }

        var ext = wholeLake ? ".lake" : Path.GetExtension(path).ToLowerInvariant();
        // JSON/JAML sources carry structure around their seeds, so the shape test is
        // what separates seed from scaffolding — it always applies for them.
        // .json here is a seed-list file (DuckDB), not a JAML filter loader.
        bool structured = ext is ".json" or ".jaml";

        string select = distinct ? "SELECT DISTINCT" : "SELECT";
        // Seeds arrive scrubbed: stray whitespace and carriage returns (mixed-newline
        // lakes) trim away at the SQL layer, and NULL rows from tolerant parsing stay out
        // of the stream entirely.
        const string seedExpr = "trim(#1, ' ' || chr(9) || chr(13))";
        string count = distinct ? $"COUNT(DISTINCT {seedExpr})" : "COUNT(*)";
        // The lake accepts exactly what a Balatro seed is: base-35, [1-9A-Z], up to 8 chars.
        // header = false keeps every row (auto-detect eats a seed in single-column files);
        // the shape test drops legacy header rows ("Seed") and stray junk instead.
        string seedShape =
            $" WHERE {seedExpr} IS NOT NULL AND length({seedExpr}) > 0"
            + (distinct || structured ? $" AND {seedExpr} SIMILAR TO '[1-9A-Z]{{1,8}}'" : "");

        string from = ext switch
        {
            ".lake" => LakeTable,
            ".parquet" or ".pq" => $"read_parquet('{EscapeSql(path)}')",
            ".db" or ".duckdb" or ".sqlite" or ".sqlite3" => ImportDatabaseSeeds(path),
            // JSON seed lists in every shape they've historically taken:
            // ["SEED", ...] · {"seeds": ["SEED", ...]} · [{"seed": "SEED", ...}, ...]
            ".json" => $"""
                (SELECT unnest(
                    coalesce(json_extract_string(j, '$.seeds[*]'), []) ||
                    coalesce(json_extract_string(j, '$[*].seed'), []) ||
                    CASE WHEN json_type(j) = 'ARRAY' AND json_type(j, '$[0]') = 'VARCHAR'
                         THEN coalesce(json_extract_string(j, '$[*]'), []) ELSE [] END
                ) AS seed FROM (SELECT content::JSON AS j FROM read_text('{EscapeSql(path)}')))
                """,
            // A JAML file is a seed source too: its seeds: block lists one seed per line.
            ".jaml" => $$"""
                (SELECT unnest(regexp_extract_all(
                    regexp_extract(content, '(?ms)^seeds:(.*)$', 1),
                    '(?m)^\s*-\s*([1-9A-Z]{1,8})\s*$', 1
                )) AS seed FROM read_text('{{EscapeSql(path)}}'))
                """,
            // CSV and TXT both stream through read_csv: seed = first field of each line.
            // DuckDB positional reference (#1) never depends on a generated column name
            // (column0 vs column00 flips at 11+ columns; duckdb#19724). null_padding keeps
            // ragged short rows (e.g. "SEED,1"); strict_mode=false + ignore_errors keep a
            // lake readable even when writers disagree on line endings (COPY emits LF,
            // SeedLakeSink appends CRLF) — the seed shape test guards the stream regardless.
            _ =>
                $"read_csv('{EscapeSql(path)}', header = false, null_padding = true, all_varchar = true, strict_mode = false, ignore_errors = true)",
        };

        var sql = $"{select} {seedExpr} AS seed FROM {from}{seedShape}";
        var countSql = $"SELECT {count} FROM {from}{seedShape}";

        using var countCmd = _connection.CreateCommand();
        countCmd.CommandText = countSql;
        SeedCount = Convert.ToInt64(countCmd.ExecuteScalar());

        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.UseStreamingMode = true;
        _reader = cmd.ExecuteReader();
    }

    /// <summary>One filter's lake file, deduped and shape-tested.</summary>
    public static SeedSourceProvider FromLake(string lakeFile) => new(lakeFile, distinct: true);

    /// <summary>--drown: every seed ever saved under the lake root, across every filter.</summary>
    public static SeedSourceProvider FromLakeRoot(string lakeRoot) => new(lakeRoot, distinct: true);

    public string NextSeed()
    {
        lock (_lock)
        {
            if (_disposed)
                return string.Empty;

            while (_reader.Read())
            {
                var seed = _reader.GetString(0);
                if (!string.IsNullOrEmpty(seed))
                    return seed;
            }
            return string.Empty;
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

    /// <summary>In-memory staging table every database/lake import lands in. Seeds are copied
    /// here and the source file is DETACHed *before* the search starts: a streaming reader would
    /// otherwise pin a READ_ONLY lock on the lake for the whole run, and SeedLakeSink (which
    /// writes finds back into that same lake) could never open it read-write.</summary>
    private const string LakeTable = "lake_seeds";

    private void EnsureLakeTable()
    {
        using var create = _connection.CreateCommand();
        create.CommandText = $"CREATE TABLE IF NOT EXISTS {LakeTable} (seed VARCHAR)";
        create.ExecuteNonQuery();
    }

    /// <summary>--drown: pour every lake file (*.duckdb / *.db) and every CSV/TXT sitting in
    /// the lake root into <see cref="LakeTable"/>. One filter's finds are another filter's
    /// candidates; the whole lake is the haystack.</summary>
    private void ImportLakeRoot(string root)
    {
        EnsureLakeTable();
        var files = Directory
            .EnumerateFiles(root)
            .Where(f =>
                Path.GetExtension(f).ToLowerInvariant()
                    is ".duckdb" or ".db" or ".sqlite" or ".sqlite3" or ".csv" or ".txt"
            )
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var normalized = file.Replace('\\', '/');
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".csv" or ".txt")
            {
                using var csv = _connection.CreateCommand();
                csv.CommandText =
                    $"INSERT INTO {LakeTable} SELECT #1 FROM read_csv('{EscapeSql(normalized)}', header = false, null_padding = true, all_varchar = true, strict_mode = false, ignore_errors = true)";
                csv.ExecuteNonQuery();
            }
            else
            {
                ImportDatabaseSeeds(normalized);
            }
        }
    }

    /// <summary>Attach a database read-only and resolve its seed table: "seeds" when present,
    /// then "results" (the BSO archive shape: seed, score, tally0-9), then the database's only
    /// table. Seeds ride the first column. DuckDB files attach natively; anything else gets a
    /// second chance through the sqlite extension, so 16 months of .db history all pours.
    /// The seed column is copied into <see cref="LakeTable"/> and the file DETACHed; returns
    /// the staging table name for use as a FROM clause.</summary>
    private string ImportDatabaseSeeds(string path)
    {
        EnsureLakeTable();
        try
        {
            using var attach = _connection.CreateCommand();
            attach.CommandText = $"ATTACH '{EscapeSql(path)}' AS src (READ_ONLY)";
            attach.ExecuteNonQuery();
        }
        catch (Exception)
        {
            using var sqlite = _connection.CreateCommand();
            sqlite.CommandText =
                $"INSTALL sqlite; LOAD sqlite; ATTACH '{EscapeSql(path)}' AS src (TYPE sqlite, READ_ONLY)";
            sqlite.ExecuteNonQuery();
        }

        try
        {
            var tables = new List<string>();
            using (var q = _connection.CreateCommand())
            {
                q.CommandText =
                    "SELECT table_name FROM duckdb_tables() WHERE database_name = 'src'";
                using var r = q.ExecuteReader();
                while (r.Read())
                    tables.Add(r.GetString(0));
            }

            string? table = tables.Find(t =>
                t.Equals("seeds", StringComparison.OrdinalIgnoreCase)
            );
            table ??= tables.Find(t => t.Equals("results", StringComparison.OrdinalIgnoreCase));
            table ??= tables.Count == 1 ? tables[0] : null;
            if (table is null)
                throw new InvalidOperationException(
                    $"Seed database '{path}' resolves with a table named 'seeds' or 'results', or exactly one table; it has: [{string.Join(", ", tables)}]."
                );

            string quoted = $"src.\"{table.Replace("\"", "\"\"")}\"";
            using var copy = _connection.CreateCommand();
            copy.CommandText = $"INSERT INTO {LakeTable} SELECT #1 FROM {quoted}";
            copy.ExecuteNonQuery();
        }
        finally
        {
            using var detach = _connection.CreateCommand();
            detach.CommandText = "DETACH src";
            detach.ExecuteNonQuery();
        }
        return LakeTable;
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
