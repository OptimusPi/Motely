using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// One-shot export of search results to a .duckdb or .ducklake file.
/// Used by BSO and other callers to write results without a running search.
/// </summary>
public static class ResultsExportHelper
{
    /// <summary>
    /// Export rows to path. Format is inferred from extension: .duckdb (single file, PRIMARY KEY) or .ducklake (catalog + Parquet, MERGE INTO).
    /// </summary>
    /// <param name="path">File path ending in .db/.duckdb or .ducklake.</param>
    /// <param name="columnNames">Names of columns after seed and score (e.g. tally/label column names).</param>
    /// <param name="rows">Each row: seed, score, and optional column values (same count as columnNames).</param>
    public static void ExportResultsTo(
        string path,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<(string seed, int score, IReadOnlyList<object?>? columnValues)> rows)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));
        columnNames ??= Array.Empty<string>();
        rows ??= Array.Empty<(string, int, IReadOnlyList<object?>?)>();

        var isDuckLake = DuckLakeHelper.IsDuckLake(path);
        if (isDuckLake)
            ExportToDuckLake(path, columnNames, rows);
        else
            ExportToDuckDb(path, columnNames, rows);
    }

    private static string QuoteColumn(string name)
    {
        return $"\"{name.Replace("\"", "\"\"")}\"";
    }

    private static void ExportToDuckDb(
        string dbPath,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<(string seed, int score, IReadOnlyList<object?>? columnValues)> rows)
    {
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            Directory.CreateDirectory(dbDir);

        var columnDefs = new List<string> { "seed VARCHAR PRIMARY KEY", "score INTEGER" };
        foreach (var name in columnNames)
            columnDefs.Add($"{QuoteColumn(name)} VARCHAR"); // keep export simple: all extra columns VARCHAR

        using var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
        var createSql = $"CREATE TABLE IF NOT EXISTS results ({string.Join(", ", columnDefs)})";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = createSql;
            cmd.ExecuteNonQuery();
        }

        if (rows.Count == 0)
            return;

        using var appender = conn.CreateAppender("results");
        foreach (var (seed, score, columnValues) in rows)
        {
            var row = appender.CreateRow();
            row.AppendValue(seed);
            row.AppendValue(score);
            for (int i = 0; i < columnNames.Count; i++)
            {
                var v = columnValues != null && i < columnValues.Count ? columnValues[i] : null;
                row.AppendValue(v?.ToString() ?? "");
            }
            row.EndRow();
        }
    }

    private static void ExportToDuckLake(
        string catalogPath,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<(string seed, int score, IReadOnlyList<object?>? columnValues)> rows)
    {
        var path = DuckLakeHelper.GetDuckLakeCatalogPath(catalogPath);
        var catalogDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(catalogDir) && !Directory.Exists(catalogDir))
            Directory.CreateDirectory(catalogDir);

        var dataPath = File.Exists(path) ? null : DuckLakeHelper.GetDuckLakeDataPath(catalogPath);
        if (dataPath != null)
        {
            var dataDir = Path.GetDirectoryName(dataPath.TrimEnd(Path.DirectorySeparatorChar, '/'));
            if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);
        }

        const string schemaName = "dl";
        using var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(path, dataPath, schemaName);
        var tableRef = $"{schemaName}.main.results";

        var columnDefs = new List<string> { "seed VARCHAR", "score INTEGER" };
        foreach (var name in columnNames)
            columnDefs.Add($"{QuoteColumn(name)} VARCHAR");

        var createSql = $"CREATE TABLE IF NOT EXISTS {tableRef} ({string.Join(", ", columnDefs)})";
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = createSql;
            cmd.ExecuteNonQuery();
        }

        if (rows.Count == 0)
            return;

        var quotedCols = columnNames.Select(QuoteColumn).ToList();
        var insertCols = new List<string> { "seed", "score" };
        insertCols.AddRange(quotedCols);

        foreach (var (seed, score, columnValues) in rows)
        {
            var seedEsc = seed.Replace("'", "''");
            var valueParts = new List<string> { $"'{seedEsc}'", score.ToString() };
            for (int i = 0; i < columnNames.Count; i++)
            {
                var v = columnValues != null && i < columnValues.Count ? columnValues[i] : null;
                valueParts.Add("'" + (v?.ToString() ?? "").Replace("'", "''") + "'");
            }
            var insertSql = $"INSERT INTO {tableRef} ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", valueParts)})";
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = insertSql;
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// Export results from DuckDB to CSV using native COPY command.
    /// This is the correct way - let DuckDB handle CSV formatting.
    /// </summary>
    /// <param name="dbPath">Path to the DuckDB results database.</param>
    /// <param name="csvPath">Output CSV file path.</param>
    /// <param name="tableName">Table name (default "results").</param>
    public static void ExportDuckDbToCsv(string dbPath, string csvPath, string tableName = "results")
    {
        if (string.IsNullOrEmpty(dbPath))
            throw new ArgumentNullException(nameof(dbPath));
        if (string.IsNullOrEmpty(csvPath))
            throw new ArgumentNullException(nameof(csvPath));
        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"DuckDB file not found: {dbPath}");

        var csvDir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(csvDir) && !Directory.Exists(csvDir))
            Directory.CreateDirectory(csvDir);

        // Escape path for SQL
        var escapedCsvPath = csvPath.Replace("'", "''").Replace('\\', '/');

        using var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
        using var cmd = conn.CreateCommand();
        
        // Use DuckDB's native COPY TO CSV - handles quoting, escaping, headers properly
        cmd.CommandText = $"COPY {tableName} TO '{escapedCsvPath}' (FORMAT CSV, HEADER true)";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Export results from DuckLake to CSV using native COPY command.
    /// </summary>
    public static void ExportDuckLakeToCsv(string catalogPath, string csvPath, string tableName = "results")
    {
        if (string.IsNullOrEmpty(catalogPath))
            throw new ArgumentNullException(nameof(catalogPath));
        if (string.IsNullOrEmpty(csvPath))
            throw new ArgumentNullException(nameof(csvPath));

        var path = DuckLakeHelper.GetDuckLakeCatalogPath(catalogPath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"DuckLake catalog not found: {path}");

        var csvDir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(csvDir) && !Directory.Exists(csvDir))
            Directory.CreateDirectory(csvDir);

        var escapedCsvPath = csvPath.Replace("'", "''").Replace('\\', '/');
        const string schemaName = "dl";

        using var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(path, dataPath: null, schemaName);
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = $"COPY {schemaName}.main.{tableName} TO '{escapedCsvPath}' (FORMAT CSV, HEADER true)";
        cmd.ExecuteNonQuery();
    }
}
