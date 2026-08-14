using Motely;
using Motely.Filters;
using DuckDB.NET.Data;

namespace Motely.DataLake;

/// <summary>
/// The seed lake: a per-filter DuckDB database with a deduplicated <c>seeds</c> table.
/// Every find reaches the disk immediately; scores live in console output and JAML save-back.
/// </summary>
public sealed class SeedLakeSink : IMotelyResultSink
{
    private readonly object _gate = new();
    private readonly string _path;
    private DuckDBConnection? _connection;
    private bool _disposed;

    /// <summary>Seeds/&lt;filterId&gt;.duckdb — root via --results-path or MOTELY_DATALAKE_PATH.</summary>
    public static string LakePath(string? root, string filterId)
    {
        root ??= Environment.GetEnvironmentVariable("MOTELY_DATALAKE_PATH");
        return Path.Combine(string.IsNullOrWhiteSpace(root) ? "Seeds" : root, filterId + ".duckdb");
    }

    public SeedLakeSink(string? root, string filterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterId);
        _path = LakePath(root, filterId);
    }

    public void OnSeed(string seed) => Write(seed);

    public void OnScored(in MotelyScoredSeedResult tally) => Write(tally.Seed);

    private void Write(string seed)
    {
        if (string.IsNullOrEmpty(seed))
            return;

        lock (_gate)
        {
            if (_disposed)
                return;

            if (_connection is null)
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(_path));
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                _connection = new DuckDBConnection($"Data Source={_path}");
                _connection.Open();
                using var create = _connection.CreateCommand();
                create.CommandText = "CREATE TABLE IF NOT EXISTS seeds (seed VARCHAR PRIMARY KEY)";
                create.ExecuteNonQuery();
            }

            using var insert = _connection.CreateCommand();
            insert.CommandText = "INSERT OR IGNORE INTO seeds VALUES (?)";
            insert.Parameters.Add(new DuckDBParameter { Value = seed });
            insert.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _connection?.Dispose();
            _connection = null;
        }
    }
}
