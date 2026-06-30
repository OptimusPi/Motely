#if !BROWSER
using System.Data.Common;
using DuckDB.NET.Data;
using Motely.SeedProviders;

namespace Motely.Data;

public sealed class SeedSourceProvider : IMotelySeedProvider, IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly DbDataReader _reader;
    private readonly System.Threading.Lock _lock = new();
    private bool _disposed;

    public long SeedCount { get; }

    public SeedSourceProvider(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = path.Replace('\\', '/');

        _connection = new DuckDBConnection("Data Source=:memory:;threads=1");
        _connection.Open();

        if (IsRemotePath(path))
        {
            using var extCmd = _connection.CreateCommand();
            extCmd.CommandText = "INSTALL httpfs; LOAD httpfs;";
            extCmd.ExecuteNonQuery();
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var sql = ext switch
        {
            ".parquet" or ".pq" => $"SELECT seed FROM read_parquet('{EscapeSql(path)}')",
            // Seed = first field of each line. DuckDB positional reference (#1) never depends on a
            // generated column name (column0 vs column00 flips at 11+ columns; duckdb#19724).
            // null_padding keeps ragged short rows (e.g. "SEED,1") instead of erroring.
            _ =>
                $"SELECT #1 AS seed FROM read_csv('{EscapeSql(path)}', header = false, null_padding = true)",
        };
        var countSql = ext switch
        {
            ".parquet" or ".pq" => $"SELECT COUNT(*) FROM read_parquet('{EscapeSql(path)}')",
            _ =>
                $"SELECT COUNT(*) FROM read_csv('{EscapeSql(path)}', header = false, null_padding = true)",
        };

        using var countCmd = _connection.CreateCommand();
        countCmd.CommandText = countSql;
        SeedCount = Convert.ToInt64(countCmd.ExecuteScalar());

        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.UseStreamingMode = true;
        _reader = cmd.ExecuteReader();
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_lock)
        {
            if (_disposed)
                return ReadOnlySpan<char>.Empty;

            while (_reader.Read())
            {
                var seed = _reader.GetString(0);
                if (!string.IsNullOrEmpty(seed))
                    return seed;
            }
            return ReadOnlySpan<char>.Empty;
        }
    }

    public int NextSeeds(string[] buffer)
    {
        if (buffer is not { Length: > 0 })
            return 0;

        lock (_lock)
        {
            if (_disposed)
                return 0;

            int count = 0;
            while (count < buffer.Length && _reader.Read())
            {
                var seed = _reader.GetString(0);
                if (!string.IsNullOrEmpty(seed))
                    buffer[count++] = seed;
            }
            return count;
        }
    }

    private static string EscapeSql(string path) => path.Replace("'", "''");

    private static bool IsRemotePath(string path) =>
        path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _reader.Dispose();
        _connection.Dispose();
    }
}
#endif
