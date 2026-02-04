using System;
using System.IO;
using Motely;
using Motely.Filters;
using Motely.Repository;

namespace Motely.DB;

/// <summary>
/// Accept by moniker: SEED SOURCE. Output by moniker: SEED SINK.
/// Implements <see cref="IMotelyRepository"/>; all resolution and DB/file logic stays in Motely.DB.
/// </summary>
public sealed class MotelyRepository : IMotelyRepository
{
    /// <inheritdoc />
    public IMotelySeedProvider GetSource(string moniker)
    {
        if (string.IsNullOrWhiteSpace(moniker))
            throw new ArgumentException("Source moniker is required.", nameof(moniker));

        // Library references pass through
        if (
            moniker.StartsWith("seq:", StringComparison.OrdinalIgnoreCase)
            || moniker.StartsWith("gen:", StringComparison.OrdinalIgnoreCase)
        )
            return new DataLakeSeedProvider(moniker);

        string resolved = ResolveSourceMoniker(moniker);
        return new DataLakeSeedProvider(resolved);
    }

    /// <inheritdoc />
    public IResultStorage GetSink(string moniker, MotelyRunConfig runConfig)
    {
        if (string.IsNullOrWhiteSpace(moniker))
            throw new ArgumentException("Sink moniker is required.", nameof(moniker));
        if (LooksLikePath(moniker))
            return ResultStorageFactory.CreateOrOpenStorage(moniker, runConfig);

        EnsureLibraryRoot();
        return ResultStorageFactory.CreateResultStorage(moniker, runConfig);
    }

    /// <summary>
    /// Resolve a path-like moniker (e.g. "seed1.txt") to a full path. Same logic as legacy LoadSeedSources.
    /// </summary>
    private static string ResolveSourceMoniker(string moniker)
    {
        if (File.Exists(moniker))
        {
            string ext = Path.GetExtension(moniker).ToLowerInvariant();
            if (ext is ".txt" or ".csv" or ".db" or ".duckdb" or ".ducklake" or ".parquet")
                return Path.GetFullPath(moniker);
            throw new NotSupportedException($"Unsupported seed source extension: {ext}");
        }

        string storageDirectory = "seeds";
        if (Path.HasExtension(moniker))
        {
            string direct = Path.Combine(storageDirectory, moniker);
            if (File.Exists(direct))
            {
                string ext = Path.GetExtension(direct).ToLowerInvariant();
                if (ext is ".txt" or ".csv" or ".db" or ".duckdb" or ".ducklake" or ".parquet")
                    return Path.GetFullPath(direct);
                throw new NotSupportedException($"Unsupported seed source extension: {ext}");
            }
        }

        string dbPath = Path.Combine(storageDirectory, moniker + ".db");
        string csvPath = Path.Combine(storageDirectory, moniker + ".csv");
        string txtPath = Path.Combine(storageDirectory, moniker + ".txt");
        if (File.Exists(dbPath))
            return Path.GetFullPath(dbPath);
        if (File.Exists(csvPath))
            return Path.GetFullPath(csvPath);
        if (File.Exists(txtPath))
            return Path.GetFullPath(txtPath);

        throw new FileNotFoundException($"Seed source not found for moniker: {moniker}");
    }

    private static bool LooksLikePath(string moniker)
    {
        if (Path.IsPathRooted(moniker))
            return true;
        if (
            moniker.Contains(Path.DirectorySeparatorChar)
            || moniker.Contains(Path.AltDirectorySeparatorChar)
        )
            return true;
        var ext = Path.GetExtension(moniker);
        return !string.IsNullOrEmpty(ext);
    }

    private static void EnsureLibraryRoot()
    {
        var probe = ResultsSetReader.GetPathForFilter("probe");
        if (!string.IsNullOrEmpty(probe))
            return;

        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Motely",
            "SearchResults"
        );
        Directory.CreateDirectory(root);
        ResultsSetReader.SetLibraryRoot(root);
    }
}
