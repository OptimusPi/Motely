using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Motely.DB;

/// <summary>
/// Cleanup of filter result databases. Motely.DB owns all DuckDB file operations.
/// Dumps seeds to fertilizer.txt then deletes .duckdb and .duckdb.wal files.
/// </summary>
public static class FilterDatabaseCleanup
{
    /// <summary>
    /// Dump seeds from all result databases for this filter to fertilizer.txt, then delete only those files.
    /// Only touches files matching this filter (filterName_*); never deletes other filters' results.
    /// </summary>
    /// <param name="filterName">Filter name (e.g. file name without extension).</param>
    /// <param name="searchResultsDir">Directory containing result DBs (filterName_*.db, filterName_*.duckdb, filterName_*.ducklake).</param>
    /// <param name="fertilizerTxtPath">Path to fertilizer.txt to append seeds to.</param>
    public static async Task CleanupFilterDatabasesAsync(
        string filterName,
        string searchResultsDir,
        string fertilizerTxtPath
    )
    {
        if (string.IsNullOrWhiteSpace(filterName) || string.IsNullOrWhiteSpace(searchResultsDir))
            return;

        if (!Directory.Exists(searchResultsDir))
            return;

        // Only this filter's result files: filterName_*.db and filterName_*.duckdb (BSO uses .db; support both)
        var prefix = filterName + "_";
        var allDbFiles = new List<string>();
        foreach (var path in Directory.EnumerateFileSystemEntries(searchResultsDir))
        {
            if (!File.Exists(path))
                continue;
            var name = Path.GetFileName(path);
            if (name != null && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var ext = Path.GetExtension(name).ToLowerInvariant();
                if (
                    ext is ".db" or ".duckdb"
                    || name.EndsWith(".ducklake", StringComparison.OrdinalIgnoreCase)
                )
                    allDbFiles.Add(path);
            }
        }

        var allSeeds = new List<string>();

        foreach (var dbFile in allDbFiles)
        {
            try
            {
                if (DuckLakeHelper.IsDuckLake(dbFile))
                {
                    var catalogPath = DuckLakeHelper.GetDuckLakeCatalogPath(dbFile);
                    if (!File.Exists(catalogPath))
                        continue;
                    using var connection = DuckDBConnectionFactory.CreateConnectionWithDuckLake(
                        catalogPath,
                        null,
                        "dl"
                    );
                    var seeds = DuckDBQueryHelpers.GetAllSeeds(
                        connection,
                        "dl.main.results",
                        "seed"
                    );
                    allSeeds.AddRange(seeds);
                }
                else
                {
                    using var connection = DuckDBConnectionFactory.CreateConnection(dbFile);
                    var seeds = DuckDBQueryHelpers.GetAllSeeds(connection, "results", "seed");
                    allSeeds.AddRange(seeds);
                }
            }
            catch
            {
                // Skip broken DBs; we still delete them below
            }
        }

        if (allSeeds.Count > 0 && !string.IsNullOrWhiteSpace(fertilizerTxtPath))
        {
            var dir = Path.GetDirectoryName(fertilizerTxtPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            await File.AppendAllLinesAsync(fertilizerTxtPath, allSeeds).ConfigureAwait(false);
        }

        // Delete only this filter's DB files
        foreach (var dbFile in allDbFiles)
        {
            try
            {
                if (File.Exists(dbFile))
                    File.Delete(dbFile);
            }
            catch
            {
                // Log and continue
            }
        }

        // Delete .wal for this filter's .db / .duckdb only
        foreach (var f in Directory.GetFiles(searchResultsDir, $"{filterName}_*.duckdb.wal"))
        {
            try
            {
                if (File.Exists(f))
                    File.Delete(f);
            }
            catch { }
        }
        foreach (var f in Directory.GetFiles(searchResultsDir, $"{filterName}_*.db.wal"))
        {
            try
            {
                if (File.Exists(f))
                    File.Delete(f);
            }
            catch { }
        }
        // Delete DuckLake _data dirs only for .ducklake files we just deleted
        foreach (var dbFile in allDbFiles)
        {
            if (!DuckLakeHelper.IsDuckLake(dbFile))
                continue;
            var catalogPath = DuckLakeHelper.GetDuckLakeCatalogPath(dbFile);
            var dataPath = DuckLakeHelper.GetDuckLakeDataPath(catalogPath);
            try
            {
                if (Directory.Exists(dataPath))
                    Directory.Delete(dataPath, recursive: true);
            }
            catch { }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
