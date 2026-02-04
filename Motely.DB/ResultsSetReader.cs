using System.Collections.Generic;
using System.IO;

namespace Motely.DB;

/// <summary>
/// Read-only view of a search result set (DuckLake). This is the library of seeds for that result set.
/// Identity is always <strong>searchId</strong> (source of truth). The library lives in ONE spot;
/// callers never pass or see the real location — the host sets it once at startup.
/// </summary>
public sealed class ResultsSetReader
{
    private static string? _libraryRoot;

    private readonly string _path;

    private ResultsSetReader(string path) => _path = path;

    /// <summary>
    /// Set the single library root (DuckLake location). Call once at host startup.
    /// Callers steer away from this — they only use <see cref="Open(string)"/> with searchId.
    /// </summary>
    public static void SetLibraryRoot(string path)
    {
        _libraryRoot = path;
    }

    /// <summary>
    /// Get the path for a filter in the library (so callers can create/open the DB there).
    /// Only valid after <see cref="SetLibraryRoot"/> has been called.
    /// </summary>
    public static string? GetPathForFilter(string filterId)
    {
        if (string.IsNullOrWhiteSpace(filterId) || string.IsNullOrWhiteSpace(_libraryRoot))
            return null;
        return Path.Combine(_libraryRoot, $"{filterId}.ducklake");
    }

    /// <summary>
    /// Delete a search result (catalog + _data). Motely.DB is the only layer that touches DuckDB/DuckLake storage.
    /// Call after dumping seeds elsewhere if needed; this only removes the on-disk result.
    /// </summary>
    public static void Delete(string filterId)
    {
        var path = GetPathForFilter(filterId);
        if (string.IsNullOrEmpty(path))
            return;
        var catalogPath = ResultsQueryHelper.ToDuckLakeCatalogPath(path);
        if (File.Exists(catalogPath))
            File.Delete(catalogPath);
        var dataPath = DuckLakeHelper.GetDuckLakeDataPath(catalogPath);
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, recursive: true);
    }

    /// <summary>
    /// Open a result set (DuckLake) for read by <strong>filterId</strong> (source of truth).
    /// Motely.DB knows: "OK, filterId? Got it — read the catalog thing in DuckLake for that filter."
    /// Path is derived inside from the one library root + filterId.ducklake. Returns null if root not set or catalog does not exist.
    /// </summary>
    public static ResultsSetReader? Open(string filterId)
    {
        if (string.IsNullOrWhiteSpace(filterId) || string.IsNullOrWhiteSpace(_libraryRoot))
            return null;
        var path = Path.Combine(_libraryRoot, $"{filterId}.ducklake");
        var catalogPath = ResultsQueryHelper.ToDuckLakeCatalogPath(path);
        if (!File.Exists(catalogPath))
            return null;
        return new ResultsSetReader(path);
    }

    /// <summary>Top N seeds by score descending.</summary>
    public List<string> GetTopSeeds(int limit) =>
        ResultsQueryHelper.GetTopSeedsFromPath(_path, limit);

    /// <summary>Column names (seed, score, ...).</summary>
    public List<string> GetColumnNames() => ResultsQueryHelper.GetColumnNamesFromPath(_path);

    /// <summary>Resume cursor (last_batch, last_batch_size, last_seed).</summary>
    public (long startBatch, int batchSize, string? lastSeed) GetResumeCursor() =>
        ResultsQueryHelper.GetResumeCursorFromPath(_path);

    /// <summary>Result rows (seed, score, and any extra columns as dictionary).</summary>
    public List<Dictionary<string, object?>> GetTopResults(int offset, int limit) =>
        ResultsQueryHelper.GetTopResultsFromPath(_path, offset, limit);
}
