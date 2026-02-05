using System.IO;
using Motely.Filters;
using Motely.Repository;

namespace Motely.DB;

/// <summary>
/// Repository implementation - handles all source/sink creation by moniker.
/// Motely.DB owns all file/database access.
/// </summary>
public sealed class MotelyRepository : IMotelyRepository
{
    public IMotelySeedProvider GetSource(string moniker)
    {
        return new DataLakeSeedProvider(moniker);
    }

    public IResultStorage GetSink(string moniker, MotelyRunConfig runConfig)
    {
        // Resolve moniker to full path if it's not already a path
        string dbPath = moniker;
        if (
            string.IsNullOrEmpty(dbPath)
            || (
                !Path.IsPathRooted(dbPath)
                && !dbPath.Contains(Path.DirectorySeparatorChar)
                && !dbPath.Contains(Path.AltDirectorySeparatorChar)
            )
        )
        {
            // Treat as filterId and resolve to default location
            var searchResultsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Motely",
                "SearchResults"
            );
            Directory.CreateDirectory(searchResultsDir);
            dbPath = Path.Combine(searchResultsDir, $"{moniker}.db");
        }

        return MotelySearchDatabase.CreateOrOpen(dbPath, runConfig);
    }
}
