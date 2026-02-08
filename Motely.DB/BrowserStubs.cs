#nullable enable
using System;
using Motely;
using Motely.Filters;

namespace Motely.DB;

public static class DuckDBConnectionFactory
{
    public static DuckDBConnectionStub CreateConnection(string dbPath) =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");
}

public sealed class DuckDBConnectionStub : IDisposable
{
    public void Close() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");

    public void Dispose() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");

    public DuckDBCommandStub CreateCommand() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");

    public DuckDBAppenderStub CreateAppender(string table) =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");
}

public sealed class DuckDBCommandStub : IDisposable
{
    public string CommandText { get; set; } = "";

    public void ExecuteNonQuery() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");

    public void Dispose() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");
}

public sealed class DuckDBAppenderStub : IDisposable
{
    public DuckDBAppenderRowStub CreateRow() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");

    public void Close() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");

    public void Dispose() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");
}

public sealed class DuckDBAppenderRowStub
{
    public void AppendValue(string value) =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");

    public void EndRow() =>
        throw new PlatformNotSupportedException("DuckDB not available in browser.");
}

public sealed class DataLakeSeedProvider : IMotelySeedProvider, IDisposable
{
    public int SeedCount =>
        throw new PlatformNotSupportedException(
            "File-based seed sources not available in browser."
        );

    public DataLakeSeedProvider(string path) =>
        throw new PlatformNotSupportedException(
            "File-based seed sources not available in browser."
        );

    public ReadOnlySpan<char> NextSeed() =>
        throw new PlatformNotSupportedException(
            "File-based seed sources not available in browser."
        );

    public int NextSeeds(string[] seeds) =>
        throw new PlatformNotSupportedException(
            "File-based seed sources not available in browser."
        );

    public void Dispose() { }
}

public sealed class ResultsSetReader
{
    public static ResultsSetReader? Open(string searchId) => null;

    public static void Delete(string filterId) { }

    public List<string> GetTopSeeds(int limit) =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public List<Dictionary<string, object?>> GetTopResults(int offset, int limit) =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public List<string> GetColumnNames() =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public (long startBatch, int batchSize, string? lastSeed) GetResumeCursor() =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");
}

public sealed class SearchMeta
{
    public string SearchId { get; set; } = "";
    public string TableName { get; set; } = "";
    public string? JamlFilter { get; set; }
    public string? Deck { get; set; }
    public string? Stake { get; set; }
    public string? SeedSource { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastAccessed { get; set; }
    public string? LastSeed { get; set; }
    public long TotalSeedsProcessed { get; set; }
    public long TotalMatches { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public sealed class SequentialLibrary : IDisposable
{
    public static SequentialLibrary Instance =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public SearchMeta? GetSearchMeta(string searchId) =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public void UpsertSearchMeta(SearchMeta meta) =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public void UpdateLastSeed(
        string searchId,
        string lastSeed,
        long totalSeeds,
        long matchingSeeds
    ) => throw new PlatformNotSupportedException("Database storage not available in browser.");

    public void SetSearchActive(string searchId, bool active) =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public List<string> GetAllActiveSearchIds() =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public void Dispose() { }
}

public static class ResultsExportHelper
{
    public static void ExportDuckDbToCsv(string dbPath, string csvPath, string tableName) =>
        throw new PlatformNotSupportedException("Database export not available in browser.");
}

public sealed class DuckDBSeedStorage : IDisposable
{
    public DuckDBSeedStorage(string dbPath) =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public long BulkInsertSeeds(IEnumerable<string> seeds) =>
        throw new PlatformNotSupportedException("Database storage not available in browser.");

    public void Dispose() { }
}
