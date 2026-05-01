#if !BROWSER

using DuckDB.NET.Data;

namespace Motely.Datalake;

public static class MotelyLake
{
    private const string DefaultLakeRoot = "./Seeds";

    public static ISeedResultSink GetSink(string filterId, int tallyCount, string? lakeRoot = null)
    {
        var root = lakeRoot ?? DefaultLakeRoot;
        var sanitized = SanitizeTableName(filterId);
        var conn = OpenLake(root);
        EnsureTable(conn, sanitized, tallyCount);
        return new DuckLakeSink(conn, sanitized, tallyCount, Path.GetFullPath(Path.Combine(root, "motely.ducklake")));
    }

    public static List<ResultRow> QueryResults(string filterId, int limit = 1000, string? lakeRoot = null)
    {
        var root = lakeRoot ?? DefaultLakeRoot;
        var sanitized = SanitizeTableName(filterId);
        using var conn = OpenLake(root);

        if (!TableExists(conn, sanitized)) return [];

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM lake.{sanitized} ORDER BY score DESC LIMIT {limit}";
        using var reader = cmd.ExecuteReader();

        var results = new List<ResultRow>();
        while (reader.Read())
        {
            var seed = reader.GetString(0);
            var score = reader.GetInt32(1);
            var tallies = new int[reader.FieldCount - 2];
            for (int i = 0; i < tallies.Length; i++)
                tallies[i] = reader.GetInt32(i + 2);
            results.Add(new ResultRow(seed, score, tallies));
        }
        return results;
    }

    public static void InvalidateFilter(string filterId, string? lakeRoot = null)
    {
        var root = lakeRoot ?? DefaultLakeRoot;
        var sanitized = SanitizeTableName(filterId);
        using var conn = OpenLake(root);
        if (!TableExists(conn, sanitized)) return;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS lake.{sanitized}";
        cmd.ExecuteNonQuery();
    }

    private static DuckDBConnection OpenLake(string lakeRoot)
    {
        Directory.CreateDirectory(lakeRoot);
        var lakePath = Path.Combine(lakeRoot, "motely.ducklake").Replace("\\", "/");
        var dataPath = (lakeRoot.TrimEnd('/', '\\') + "/").Replace("\\", "/");

        var conn = new DuckDBConnection("Data Source=:memory:");
        conn.Open();

        using var setup = conn.CreateCommand();
        setup.CommandText = $"""
            INSTALL ducklake;
            LOAD ducklake;
            ATTACH 'ducklake:{lakePath}' AS lake (DATA_PATH '{dataPath}');
            """;
        setup.ExecuteNonQuery();

        return conn;
    }

    private static void EnsureTable(DuckDBConnection conn, string tableName, int tallyCount)
    {
        if (TableExists(conn, tableName)) return;

        var tallyCols = string.Join(", ", Enumerable.Range(0, tallyCount).Select(i => $"t{i} INTEGER DEFAULT 0"));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE TABLE lake.{tableName} (seed TEXT NOT NULL, score INTEGER NOT NULL{(tallyCount > 0 ? ", " + tallyCols : "")})";
        cmd.ExecuteNonQuery();
    }

    private static bool TableExists(DuckDBConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM duckdb_tables() WHERE database_name = 'lake' AND table_name = '{tableName}'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    internal static string SanitizeTableName(string filterId)
    {
        var sanitized = new char[filterId.Length];
        for (int i = 0; i < filterId.Length; i++)
        {
            var c = filterId[i];
            sanitized[i] = char.IsLetterOrDigit(c) || c == '_' ? c : '_';
        }
        var name = new string(sanitized);
        return char.IsDigit(name[0]) ? $"f_{name}" : name;
    }
}

#endif
