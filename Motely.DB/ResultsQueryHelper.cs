using System.Collections.Generic;
using System.IO;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Internal one-shot read helpers for search result DuckLakes.
/// Public API is <see cref="ResultsSetReader"/> — open a result set, then read from it.
/// </summary>
internal static class ResultsQueryHelper
{
    private const string DuckLakeSchemaName = "dl";
    private static readonly string ResultsTableRef = $"{DuckLakeSchemaName}.main.results";
    private static readonly string MetaTableRef = $"{DuckLakeSchemaName}.main.search_meta";

    /// <summary>Normalize path to DuckLake catalog path (.db -> .ducklake, else ensure .ducklake).</summary>
    internal static string ToDuckLakeCatalogPath(string path)
    {
        if (path.EndsWith(".ducklake", StringComparison.OrdinalIgnoreCase))
            return path;
        if (path.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            return path[..^3] + ".ducklake";
        return path + ".ducklake";
    }

    internal static List<string> GetTopSeedsFromPath(string dbPath, int limit)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return new List<string>();

        var catalogPath = ToDuckLakeCatalogPath(dbPath);
        if (!File.Exists(catalogPath))
            return new List<string>();

        try
        {
            using var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
                catalogPath,
                dataPath: null,
                DuckLakeSchemaName
            );
            return DuckDBQueryHelpers.GetTopSeeds(conn, ResultsTableRef, "score", limit);
        }
        catch
        {
            return new List<string>();
        }
    }

    internal static List<string> GetColumnNamesFromPath(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return new List<string> { "seed", "score" };

        var catalogPath = ToDuckLakeCatalogPath(dbPath);
        if (!File.Exists(catalogPath))
            return new List<string> { "seed", "score" };

        try
        {
            using var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
                catalogPath,
                dataPath: null,
                DuckLakeSchemaName
            );
            var cols = GetColumnNamesFromConnection(conn, DuckLakeSchemaName, "main", "results");
            return cols.Count > 0 ? cols : new List<string> { "seed", "score" };
        }
        catch
        {
            return new List<string> { "seed", "score" };
        }
    }

    private static List<string> GetColumnNamesFromConnection(
        DuckDBConnection connection,
        string tableCatalog,
        string tableSchema,
        string tableName
    )
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"SELECT column_name FROM information_schema.columns WHERE table_catalog = '{tableCatalog}' AND table_schema = '{tableSchema}' AND table_name = '{tableName}' ORDER BY ordinal_position";
        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
            columns.Add(reader.GetString(0));
        return columns;
    }

    internal static (long startBatch, int batchSize, string? lastSeed) GetResumeCursorFromPath(
        string dbPath
    )
    {
        long startBatch = 0;
        int batchSize = 0;
        string? lastSeed = null;

        if (string.IsNullOrWhiteSpace(dbPath))
            return (startBatch, batchSize, lastSeed);

        var catalogPath = ToDuckLakeCatalogPath(dbPath);
        if (!File.Exists(catalogPath))
            return (0, 0, null);

        try
        {
            using var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
                catalogPath,
                dataPath: null,
                DuckLakeSchemaName
            );
            ReadResumeFromConnection(
                conn,
                MetaTableRef,
                ref startBatch,
                ref batchSize,
                ref lastSeed
            );
            return (startBatch, batchSize, lastSeed);
        }
        catch
        {
            return (0, 0, null);
        }
    }

    private static void ReadResumeFromConnection(
        DuckDBConnection connection,
        string metaTableRef,
        ref long startBatch,
        ref int batchSize,
        ref string? lastSeed
    )
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                $"SELECT key, value FROM {metaTableRef} WHERE key IN ('last_batch', 'last_batch_size', 'last_seed')";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                var value = reader.GetString(1);
                if (key == "last_batch" && long.TryParse(value, out var b))
                    startBatch = b;
                else if (key == "last_batch_size" && int.TryParse(value, out var bs))
                    batchSize = bs;
                else if (key == "last_seed")
                    lastSeed = value;
            }
        }
        catch
        {
            // search_meta may not exist
        }
    }

    internal static List<Dictionary<string, object?>> GetTopResultsFromPath(
        string dbPath,
        int offset,
        int limit
    )
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            return new List<Dictionary<string, object?>>();

        var catalogPath = ToDuckLakeCatalogPath(dbPath);
        if (!File.Exists(catalogPath))
            return new List<Dictionary<string, object?>>();

        try
        {
            using var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
                catalogPath,
                dataPath: null,
                DuckLakeSchemaName
            );
            return GetResultsWithTalliesFromConnection(conn, ResultsTableRef, offset, limit);
        }
        catch
        {
            return new List<Dictionary<string, object?>>();
        }
    }

    private static List<Dictionary<string, object?>> GetResultsWithTalliesFromConnection(
        DuckDBConnection connection,
        string tableRef,
        int offset,
        int limit
    )
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"SELECT * FROM {tableRef} ORDER BY score DESC LIMIT {limit} OFFSET {offset}";
        using var reader = cmd.ExecuteReader();
        var results = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }
        return results;
    }
}
