using System.IO;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Helper for DuckLake (concurrent read/write DuckDB) integration.
/// DuckLake uses ATTACH 'ducklake:&lt;catalog&gt;' (DATA_PATH '...'); extension autoloads on first ATTACH.
/// </summary>
public static class DuckLakeHelper
{
    /// <summary>
    /// Check if a path is a DuckLake catalog path (must end with .ducklake).
    /// </summary>
    public static bool IsDuckLake(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && path.EndsWith(".ducklake", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get the catalog path for a DuckLake (always ends with .ducklake).
    /// </summary>
    public static string GetDuckLakeCatalogPath(string path)
    {
        if (path.EndsWith(".ducklake", StringComparison.OrdinalIgnoreCase))
            return path;

        return path + ".ducklake";
    }

    /// <summary>
    /// Get the default data path for a DuckLake (Parquet directory: &lt;name&gt;_data next to catalog).
    /// Only needed when creating a new DuckLake; when attaching existing, catalog stores data path.
    /// </summary>
    public static string GetDuckLakeDataPath(string path)
    {
        var basePath = path.EndsWith(".ducklake", StringComparison.OrdinalIgnoreCase)
            ? path.Substring(0, path.Length - 9)
            : path;

        return Path.Combine(
            Path.GetDirectoryName(basePath) ?? "",
            Path.GetFileNameWithoutExtension(basePath) + "_data"
        );
    }

    /// <summary>
    /// Attach a DuckLake catalog to an open connection. Extension autoloads on first ATTACH.
    /// </summary>
    /// <param name="connection">Open DuckDB connection (e.g. :memory: or a persistent DB).</param>
    /// <param name="catalogPath">Path or URL to the .ducklake catalog file.</param>
    /// <param name="dataPath">Optional. Parquet data directory; null for existing DuckLake (loaded from catalog).</param>
    /// <param name="schemaName">Attached catalog alias (default seed_source).</param>
    public static void AttachDuckLake(
        DuckDBConnection connection,
        string catalogPath,
        string? dataPath = null,
        string schemaName = "seed_source"
    )
    {
        var catalogSql = catalogPath.Replace("'", "''").Replace('\\', '/');

        string attachSql = string.IsNullOrEmpty(dataPath)
            ? $"ATTACH 'ducklake:{catalogSql}' AS {schemaName}"
            : $"ATTACH 'ducklake:{catalogSql}' AS {schemaName} (DATA_PATH '{dataPath.Replace("'", "''").Replace('\\', '/')}')";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = attachSql;
        cmd.ExecuteNonQuery();
    }
}
