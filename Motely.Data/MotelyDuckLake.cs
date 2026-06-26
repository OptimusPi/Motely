#if !BROWSER

using DuckDB.NET.Data;

namespace Motely.Data;

/// <summary>
/// The single owner of Motely's DuckLake: a SQLite catalog at <c>./catalog.sqlite</c> (repo
/// root) with Parquet data under <c>./Seeds/</c>, plus <c>httpfs</c> so seed sources can be local
/// files, globs, or public http(s) URLs (e.g. a Cloudflare R2 bucket). No per-run folders, no
/// loose <c>.ducklake</c> catalogs — one catalog, one data dir.
/// </summary>
public static class MotelyDuckLake
{
    public const string LakeAlias = "lake";

    /// <summary>Root holding the catalog and the <c>Seeds/</c> data dir. Defaults to the current
    /// directory; override with <c>MOTELY_DATA_ROOT</c>.</summary>
    public static string Root { get; set; } =
        Environment.GetEnvironmentVariable("MOTELY_DATA_ROOT") ?? Directory.GetCurrentDirectory();

    public static string CatalogPath => Path.Combine(Root, "catalog.sqlite");
    public static string SeedsPath => Path.Combine(Root, "Seeds");

    /// <summary>Open an in-memory DuckDB with <c>ducklake</c> + <c>httpfs</c> loaded. When
    /// <paramref name="attachLake"/> is true the SQLite-backed DuckLake is attached as
    /// <see cref="LakeAlias"/>; pure file/url reads don't need it.</summary>
    public static DuckDBConnection Open(bool attachLake)
    {
        var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        Execute(connection, "INSTALL ducklake");
        Execute(connection, "LOAD ducklake");
        Execute(connection, "INSTALL httpfs");
        Execute(connection, "LOAD httpfs");
        if (attachLake)
        {
            Directory.CreateDirectory(SeedsPath);
            Execute(
                connection,
                $"ATTACH 'ducklake:sqlite:{Sql(CatalogPath)}' AS {LakeAlias} (DATA_PATH '{Sql(SeedsPath)}')"
            );
        }
        return connection;
    }

    public static void Execute(DuckDBConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>A bare identifier (no path separator, dot, or scheme colon) is a DuckLake table;
    /// anything else is a file, glob, or url.</summary>
    public static bool IsLakeTable(string source) => source.IndexOfAny(['/', '\\', '.', ':']) < 0;

    /// <summary>Build a streaming <c>SELECT</c> of the seed column for any source. The seed is read
    /// by ordinal 0, so external files just need the seed in their first column.</summary>
    public static string SeedQuery(string source)
    {
        if (IsLakeTable(source))
            return $"SELECT seed FROM {LakeAlias}.\"{source.Replace("\"", "\"\"")}\"";
        return $"SELECT * FROM {ReadFunction(source)}";
    }

    private static string ReadFunction(string source)
    {
        // Strip a url query string before sniffing the extension.
        int query = source.IndexOf('?');
        string path = query >= 0 ? source[..query] : source;
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".parquet" => $"read_parquet('{Sql(source)}')",
            ".csv" => $"read_csv('{Sql(source)}')",
            ".txt" => $"read_csv('{Sql(source)}', header = false, columns = {{'seed': 'VARCHAR'}})",
            // Let DuckDB's replacement scan auto-detect (handles globs and bare https urls).
            _ => $"'{Sql(source)}'",
        };
    }

    private static string Sql(string value) => value.Replace("\\", "/").Replace("'", "''");
}

#endif
