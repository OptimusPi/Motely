using System.IO;

namespace Motely.DuckDB;

/// <summary>
/// Helper for DuckLake (distributed DuckDB) integration
/// </summary>
public static class DuckLakeHelper
{
    /// <summary>
    /// Check if a path is a DuckLake catalog
    /// </summary>
    public static bool IsDuckLake(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // DuckLake catalogs have .ducklake extension
        return path.EndsWith(".ducklake", System.StringComparison.OrdinalIgnoreCase)
            || File.Exists(path + ".ducklake");
    }

    /// <summary>
    /// Get the catalog path for a DuckLake
    /// </summary>
    public static string GetDuckLakeCatalogPath(string path)
    {
        if (path.EndsWith(".ducklake", System.StringComparison.OrdinalIgnoreCase))
            return path;

        return path + ".ducklake";
    }

    /// <summary>
    /// Get the data path for a DuckLake (Parquet files directory)
    /// </summary>
    public static string GetDuckLakeDataPath(string path)
    {
        var basePath = path.EndsWith(".ducklake", System.StringComparison.OrdinalIgnoreCase)
            ? path.Substring(0, path.Length - 9)
            : path;

        return Path.Combine(
            Path.GetDirectoryName(basePath) ?? "",
            Path.GetFileNameWithoutExtension(basePath) + "_data"
        );
    }
}
