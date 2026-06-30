#if !BROWSER

using System.Data.Common;
using DuckDB.NET.Data;
using Motely.SeedProviders;

namespace Motely.Data;

/// <summary>
/// Streams seeds straight off an open DuckDB cursor — a local <c>.parquet</c>/<c>.csv</c>/<c>.txt</c>,
/// a glob, a public http(s) URL (Cloudflare R2 via <c>httpfs</c>), or a DuckLake table. Never
/// materializes its own array: worker threads drain batches under one lock (the same pattern as
/// <see cref="MotelySeedListProvider"/>), and the seed is read by ordinal 0.
/// </summary>
public sealed class MotelyDuckSeedProvider : IMotelySeedProvider, IDisposable
{
    private readonly DuckDBConnection _connection;
    private readonly DuckDBCommand _command;
    private readonly DbDataReader _reader;
    private readonly object _lock = new();
    private bool _exhausted;

    public long SeedCount { get; }

    /// <param name="source">Lake table name, file path, glob, or http(s) url.</param>
    /// <param name="seedCount">Known count, or -1 when unknown (the search treats it as a stream).</param>
    public MotelyDuckSeedProvider(string source, long seedCount = -1)
    {
        // A lake-table read needs the catalog attached; a pure file/url read does not.
        _connection = MotelyDuckLake.Open(attachLake: MotelyDuckLake.IsLakeTable(source));
        _command = _connection.CreateCommand();
        _command.CommandText = MotelyDuckLake.SeedQuery(source);
        _reader = _command.ExecuteReader();
        SeedCount = seedCount;
    }

    public ReadOnlySpan<char> NextSeed()
    {
        lock (_lock)
        {
            if (_exhausted)
                return ReadOnlySpan<char>.Empty;
            while (_reader.Read())
            {
                if (_reader.IsDBNull(0))
                    continue;
                var seed = _reader.GetString(0);
                if (!string.IsNullOrEmpty(seed))
                    return seed;
            }
            _exhausted = true;
            return ReadOnlySpan<char>.Empty;
        }
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds is not { Length: > 0 })
            return 0;

        lock (_lock)
        {
            if (_exhausted)
                return 0;

            int count = 0;
            while (count < seeds.Length)
            {
                if (!_reader.Read())
                {
                    _exhausted = true;
                    break;
                }
                seeds[count++] = _reader.GetString(0);
            }
            return count;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _reader.Dispose();
            _command.Dispose();
            _connection.Dispose();
        }
    }
}

#endif
