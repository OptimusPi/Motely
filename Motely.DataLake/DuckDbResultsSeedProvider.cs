using System.Data.Common;
using DuckDB.NET.Data;
using Motely.SeedProviders;

namespace Motely.DataLake;

public sealed class DuckDbResultsSeedProvider : IMotelySeedProvider, IDisposable
{
    public const int DefaultChunkSize = 1225;
    private const string CsvReadOptions = "auto_detect = false, header = true, delim = ',', strict_mode = false, null_padding = true, ignore_errors = true";

    private readonly object _gate = new();
    private readonly DuckDBConnection _connection;
    private readonly DbCommand _readerCommand;
    private readonly DbDataReader _reader;
    private readonly Queue<string> _buffer;
    private readonly int _chunkSize;
    private string? _currentSeed;
    private bool _exhausted;
    private bool _disposed;

    public long SeedCount { get; }
    public string ResultsCsvPath { get; }

    private DuckDbResultsSeedProvider(string resultsCsvPath, int chunkSize)
    {
        ResultsCsvPath = Path.GetFullPath(resultsCsvPath);
        _chunkSize = Math.Max(MotelyGlobals.MaxVectorWidth, chunkSize);
        _buffer = new Queue<string>(_chunkSize);
        _connection = new DuckDBConnection("DataSource=:memory:");
        _connection.Open();

        string csvSqlPath = ToDuckDbPathLiteral(ResultsCsvPath);
        string columnsSql = BuildColumnsSql(ResultsCsvPath);
        SeedCount = ReadSeedCount(_connection, csvSqlPath, columnsSql);

        _readerCommand = _connection.CreateCommand();
        _readerCommand.CommandText = BuildSeedSelectSql(csvSqlPath, columnsSql);
        _reader = _readerCommand.ExecuteReader();
    }

    public static bool TryCreate(
        string filterId,
        string? resultsRootPath,
        out DuckDbResultsSeedProvider? provider,
        out string? error,
        int chunkSize = DefaultChunkSize)
    {
        provider = null;
        error = null;

        if (string.IsNullOrWhiteSpace(filterId))
        {
            error = "Error: --drown requires a JAML filter id.";
            return false;
        }

        string resultsCsvPath = ResolveResultsCsvPath(resultsRootPath, filterId);
        if (!File.Exists(resultsCsvPath))
        {
            error = $"Error: drown source not found at '{resultsCsvPath}'. Expected a results.csv file for filterId '{filterId}'.";
            return false;
        }

        try
        {
            provider = new DuckDbResultsSeedProvider(resultsCsvPath, chunkSize);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Error: could not open DuckDB results source '{resultsCsvPath}': {ex.Message}";
            provider?.Dispose();
            provider = null;
            return false;
        }
    }

    public static string ResolveResultsCsvPath(string? resultsRootPath, string filterId)
    {
        string root = string.IsNullOrWhiteSpace(resultsRootPath)
            ? Path.GetFullPath("results")
            : Path.GetFullPath(resultsRootPath);
        return Path.Combine(root, filterId, "results.csv");
    }

    public string NextSeed()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!EnsureBuffered())
                return string.Empty;

            _currentSeed = _buffer.Dequeue();
            return _currentSeed;
        }
    }

    public int NextSeeds(string[] seeds)
    {
        if (seeds is not { Length: > 0 })
            return 0;

        lock (_gate)
        {
            ThrowIfDisposed();

            int filled = 0;
            while (filled < seeds.Length)
            {
                if (!EnsureBuffered())
                    break;

                seeds[filled++] = _buffer.Dequeue();
            }

            return filled;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _reader.Dispose();
            _readerCommand.Dispose();
            _connection.Dispose();
            _buffer.Clear();
            _currentSeed = null;
        }
    }

    private bool EnsureBuffered()
    {
        if (_buffer.Count > 0)
            return true;

        if (_exhausted)
            return false;

        while (_buffer.Count < _chunkSize && TryReadNextSeed(out string? seed))
        {
            if (seed != null)
                _buffer.Enqueue(seed);
        }

        if (_buffer.Count == 0)
            _exhausted = true;

        return _buffer.Count > 0;
    }

    private bool TryReadNextSeed(out string? seed)
    {
        while (_reader.Read())
        {
            if (_reader.IsDBNull(0))
                continue;

            seed = NormalizeSeed(_reader.GetString(0));
            if (seed != null)
                return true;
        }

        seed = null;
        return false;
    }

    private static string BuildSeedSelectSql(string csvSqlPath, string columnsSql)
    {
        return $"SELECT Seed FROM read_csv('{csvSqlPath}', columns = {{{columnsSql}}}, {CsvReadOptions}) WHERE Seed IS NOT NULL";
    }

    private static long ReadSeedCount(DuckDBConnection connection, string csvSqlPath, string columnsSql)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = $"SELECT COUNT(Seed) FROM read_csv('{csvSqlPath}', columns = {{{columnsSql}}}, {CsvReadOptions}) WHERE Seed IS NOT NULL";
        object? scalar = countCommand.ExecuteScalar();
        return scalar is null || scalar is DBNull ? -1 : Convert.ToInt64(scalar);
    }

    private static string BuildColumnsSql(string resultsCsvPath)
    {
        string? header = File.ReadLines(resultsCsvPath).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
            throw new InvalidOperationException($"Results CSV '{resultsCsvPath}' is empty.");

        string[] columnNames = header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (columnNames.Length == 0 || !string.Equals(columnNames[0], "Seed", StringComparison.Ordinal))
            throw new InvalidOperationException($"Results CSV '{resultsCsvPath}' must begin with a Seed column.");

        return string.Join(
            ", ",
            columnNames.Select(static name => $"'{name.Replace("'", "''", StringComparison.Ordinal)}':'VARCHAR'")
        );
    }

    private static string ToDuckDbPathLiteral(string path)
    {
        return path.Replace("\\", "/", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);
    }

    private static string? NormalizeSeed(string value)
    {
        string seed = value.Trim().ToUpperInvariant().Replace('0', 'O');
        if (seed.Length == 0 || seed.Length > MotelyGlobals.MaxSeedLength)
            return null;

        foreach (char c in seed)
        {
            if (!MotelyGlobals.SeedDigits.Contains(c))
                return null;
        }

        return seed;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
