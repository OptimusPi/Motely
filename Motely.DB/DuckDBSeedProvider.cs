using System;
using System.Collections.Generic;
using DuckDB.NET.Data;
using Motely;

namespace Motely.DuckDB;

/// <summary>
/// Streams seeds from a DuckDB database table (in-memory database, backed by file).
/// Queries DuckDB directly - no pre-loading into arrays!
/// ONE TRUE IMPLEMENTATION using Motely.DuckDB
/// </summary>
public sealed class DuckDBSeedProvider : IMotelySeedProvider, IDisposable
{
    private IEnumerator<string>? _seedEnumerator;
    private bool _disposed = false;
    private readonly object _lock = new();
    private int _seedCount;

    public DuckDBSeedProvider(string dbPath)
    {
        var conn = DuckDBConnectionFactory.CreateConnection(dbPath);

        // Get seed count for interface compliance (minimal overhead)
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM seeds";
        _seedCount = (int)Convert.ToInt64(countCmd.ExecuteScalar() ?? 0);

        // Simple direct streaming - no counting, no over-engineering!
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT seed FROM seeds";

        var reader = cmd.ExecuteReader();
        _seedEnumerator = GetSeedsFromReader(reader, conn, cmd);
    }

    public int SeedCount => _seedCount;

    private IEnumerator<string> GetSeedsFromReader(
        DuckDBDataReader reader,
        DuckDBConnection conn,
        DuckDBCommand cmd
    )
    {
        try
        {
            while (reader.Read())
            {
                yield return reader.GetString(0);
            }
        }
        finally
        {
            reader.Dispose();
            cmd.Dispose();
            conn.Close();
            conn.Dispose();
        }
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_lock)
        {
            if (_disposed || _seedEnumerator == null)
                return ReadOnlySpan<char>.Empty;

            if (_seedEnumerator.MoveNext())
                return _seedEnumerator.Current.AsSpan();

            return ReadOnlySpan<char>.Empty;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _seedEnumerator?.Dispose();
        }
    }
}
