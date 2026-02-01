using System;
using System.IO;
using Motely;
using Motely.Filters;

namespace Motely.DB;

/// <summary>
/// Single entry point for creating result storage. All database/file implementation stays in Motely.DB;
/// callers (Orchestration, API) use only this factory and <see cref="IResultStorage"/>.
/// </summary>
public static class ResultStorageFactory
{
    /// <summary>
    /// Create result storage for a search by ID. Uses <see cref="ResultsSetReader.GetPathForFilter"/>.
    /// Call after <see cref="ResultsSetReader.SetLibraryRoot"/> has been set.
    /// </summary>
    /// <exception cref="InvalidOperationException">Results library root not set or invalid searchId.</exception>
    public static IResultStorage CreateResultStorage(string searchId, MotelyRunConfig runConfig)
    {
        var path = ResultsSetReader.GetPathForFilter(searchId)
            ?? throw new InvalidOperationException($"Results library root not set or invalid searchId: {searchId}");
        var db = new MotelySearchDatabase(path, runConfig);
        return new ResultStorageAdapter(db);
    }

    /// <summary>
    /// Create or open result storage at a path. Checks schema compatibility; optionally overwrites on mismatch.
    /// </summary>
    /// <param name="dbPath">Path to .db or .ducklake.</param>
    /// <param name="runConfig">Run config for schema.</param>
    /// <param name="forceOverwrite">If true, delete existing file on schema mismatch.</param>
    /// <param name="schemaMismatchPrompt">If non-null and not forceOverwrite, called on mismatch (path, message); return true to overwrite.</param>
    /// <exception cref="InvalidOperationException">Existing DB has schema mismatch and overwrite not allowed.</exception>
    public static IResultStorage CreateOrOpenStorage(
        string dbPath,
        MotelyRunConfig runConfig,
        bool forceOverwrite = false,
        Func<string, string, bool>? schemaMismatchPrompt = null)
    {
        bool exists = File.Exists(DuckLakeHelper.IsDuckLake(dbPath) ? DuckLakeHelper.GetDuckLakeCatalogPath(dbPath) : dbPath);
        bool compatible = exists && MotelySearchDatabase.IsSchemaCompatible(dbPath, runConfig, out _);

        if (exists && !compatible)
        {
            bool shouldOverwrite = forceOverwrite;
            if (!shouldOverwrite && schemaMismatchPrompt != null)
                shouldOverwrite = schemaMismatchPrompt(dbPath, "Database schema mismatch. Existing database has different columns or types than current search config.");

            if (shouldOverwrite)
            {
                try
                {
                    if (DuckLakeHelper.IsDuckLake(dbPath))
                    {
                        var catalogPath = DuckLakeHelper.GetDuckLakeCatalogPath(dbPath);
                        if (File.Exists(catalogPath)) File.Delete(catalogPath);
                        var dataPath = DuckLakeHelper.GetDuckLakeDataPath(dbPath);
                        if (Directory.Exists(dataPath)) Directory.Delete(dataPath, recursive: true);
                    }
                    else if (File.Exists(dbPath))
                    {
                        File.Delete(dbPath);
                    }
                }
                catch { /* Ignore; let DB open fail if needed */ }
            }
            else
                throw new InvalidOperationException($"Cannot use existing database '{dbPath}' due to schema mismatch. Use --force to overwrite.");
        }

        return new ResultStorageAdapter(new MotelySearchDatabase(dbPath, runConfig));
    }
}
