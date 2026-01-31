using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DuckDB.NET.Data;
using Motely;

namespace Motely.DB;

/// <summary>
/// Streams seeds from various sources: DuckLake libraries, DuckDB files, CSV, TXT, Parquet.
/// This is the unified seed provider that can read from:
/// - SequentialLibrary tables (seq:{tableName})
/// - GenericLibrary tables (gen:{tableName})
/// - DuckDB files (.duckdb, .db)
/// - DuckLake catalogs (.ducklake) - uses thread-local connections for concurrent Parquet reads
/// - CSV files (.csv)
/// - Text files (.txt) - one seed per line
/// - Parquet files (.parquet)
/// </summary>
public sealed class DataLakeSeedProvider : IMotelySeedProvider, IDisposable
{
    private const string DuckLakeSchemaName = "seed_source";

    private ISeedReader? _seedReader;
    private IEnumerator<string>? _seedEnumerator;
    private bool _disposed = false;
    private readonly Lock _lock = new();
    private int _seedCount;

    /// <summary>
    /// Create a seed provider from a source path or library reference.
    /// 
    /// Supported formats:
    /// - "seq:{tableName}" - read from SequentialLibrary table
    /// - "gen:{tableName}" - read from GenericLibrary table
    /// - Path to .ducklake file
    /// - Path to .duckdb or .db file
    /// - Path to .csv file
    /// - Path to .txt file (one seed per line)
    /// - Path to .parquet file
    /// </summary>
    public DataLakeSeedProvider(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source is required", nameof(source));

        try
        {
            // Check for library prefixes
            if (source.StartsWith("seq:", StringComparison.OrdinalIgnoreCase))
            {
                var tableName = source.Substring(4);
                InitializeFromSequentialLibrary(tableName);
            }
            else if (source.StartsWith("gen:", StringComparison.OrdinalIgnoreCase))
            {
                var tableName = source.Substring(4);
                InitializeFromGenericLibrary(tableName);
            }
            else
            {
                // It's a file path
                InitializeFromFile(source);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to initialize DataLakeSeedProvider with source '{source}': {ex.Message}", ex);
        }
    }

    public int SeedCount => _seedCount;

    private void InitializeFromSequentialLibrary(string tableName)
    {
        var library = SequentialLibrary.Instance;
        _seedCount = (int)library.GetResultCount(tableName);
        _seedReader = library.OpenSeedReader(tableName);
    }

    private void InitializeFromGenericLibrary(string tableName)
    {
        var library = GenericLibrary.Instance;
        _seedCount = (int)library.GetRowCount(tableName);
        _seedReader = library.OpenSeedReader(tableName);
    }

    private void InitializeFromFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        switch (extension)
        {
            case ".ducklake":
                InitializeFromDuckLake(path);
                break;
            case ".duckdb":
            case ".db":
                InitializeFromDuckDb(path);
                break;
            case ".csv":
                InitializeFromCsv(path);
                break;
            case ".txt":
                InitializeFromTxt(path);
                break;
            case ".parquet":
                InitializeFromParquet(path);
                break;
            default:
                // Try to detect format
                if (DuckLakeHelper.IsDuckLake(path))
                    InitializeFromDuckLake(DuckLakeHelper.GetDuckLakeCatalogPath(path));
                else if (File.Exists(path))
                    InitializeFromDuckDb(path);
                else
                    throw new NotSupportedException($"Unknown file format: {extension}");
                break;
        }
    }

    private void InitializeFromDuckLake(string catalogPath)
    {
        // Create connection and read all seeds into enumerator
        // DuckDB/DuckLake handles concurrent Parquet reads internally
        var conn = DuckDBConnectionFactory.CreateConnectionWithDuckLake(catalogPath, dataPath: null, DuckLakeSchemaName);
        var countSql = $"SELECT COUNT(*) FROM {DuckLakeSchemaName}.main.seeds";
        var selectSql = $"SELECT seed FROM {DuckLakeSchemaName}.main.seeds";

        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = countSql;
            _seedCount = (int)Convert.ToInt64(countCmd.ExecuteScalar() ?? 0);
        }

        var cmd = conn.CreateCommand();
        cmd.CommandText = selectSql;
        var reader = cmd.ExecuteReader();
        _seedEnumerator = GetSeedsFromReader(reader, conn, cmd);
    }

    private void InitializeFromDuckDb(string dbPath)
    {
        var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
        var countSql = "SELECT COUNT(*) FROM seeds";
        var selectSql = "SELECT seed FROM seeds";

        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = countSql;
            _seedCount = (int)Convert.ToInt64(countCmd.ExecuteScalar() ?? 0);
        }

        var cmd = conn.CreateCommand();
        cmd.CommandText = selectSql;
        var reader = cmd.ExecuteReader();
        _seedEnumerator = GetSeedsFromReader(reader, conn, cmd);
    }

    private void InitializeFromCsv(string filePath)
    {
        // Use DuckDB to read CSV directly
        // Use proper connection string format: "Data Source=:memory:"
        var conn = new DuckDBConnection("Data Source=:memory:");
        conn.Open();

        var escapedPath = filePath.Replace("'", "''").Replace('\\', '/');
        var countSql = $"SELECT COUNT(*) FROM read_csv('{escapedPath}', header=true)";
        var selectSql = $"SELECT seed FROM read_csv('{escapedPath}', header=true)";

        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = countSql;
            _seedCount = (int)Convert.ToInt64(countCmd.ExecuteScalar() ?? 0);
        }

        var cmd = conn.CreateCommand();
        cmd.CommandText = selectSql;
        var reader = cmd.ExecuteReader();
        _seedEnumerator = GetSeedsFromReader(reader, conn, cmd);
    }

    private void InitializeFromTxt(string filePath)
    {
        // Validate file exists
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Text file not found: {filePath}");
        }

        // Use DuckDB's native read_csv to read text files directly (one seed per line)
        // This leverages DuckDB's optimized file reading without converting to .db first
        // Use proper connection string format: "Data Source=:memory:"
        var conn = new DuckDBConnection("Data Source=:memory:");
        conn.Open();

        // Ensure path is fully qualified and escape for SQL
        // DuckDB needs absolute paths for read_csv to work reliably
        var fullPath = Path.GetFullPath(filePath);
        var escapedPath = fullPath.Replace("'", "''").Replace('\\', '/');
        
        // read_csv with header=false treats each line as a row with one column (column0)
        // Filter out empty/whitespace-only lines
        var countSql = $"SELECT COUNT(*) FROM read_csv('{escapedPath}', header=false) WHERE TRIM(column0) != ''";
        var selectSql = $"SELECT TRIM(column0) AS seed FROM read_csv('{escapedPath}', header=false) WHERE TRIM(column0) != ''";

        try
        {
            using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = countSql;
                _seedCount = (int)Convert.ToInt64(countCmd.ExecuteScalar() ?? 0);
            }

            var cmd = conn.CreateCommand();
            cmd.CommandText = selectSql;
            var reader = cmd.ExecuteReader();
            _seedEnumerator = GetSeedsFromReader(reader, conn, cmd);
        }
        catch (Exception ex)
        {
            conn.Dispose();
            throw new Exception($"Failed to read text file '{filePath}' using DuckDB read_csv: {ex.Message}", ex);
        }
    }

    private void InitializeFromParquet(string filePath)
    {
        // Use DuckDB to read Parquet directly
        // Use proper connection string format: "Data Source=:memory:"
        var conn = new DuckDBConnection("Data Source=:memory:");
        conn.Open();

        var escapedPath = filePath.Replace("'", "''").Replace('\\', '/');
        var countSql = $"SELECT COUNT(*) FROM read_parquet('{escapedPath}')";
        var selectSql = $"SELECT seed FROM read_parquet('{escapedPath}')";

        using (var countCmd = conn.CreateCommand())
        {
            countCmd.CommandText = countSql;
            _seedCount = (int)Convert.ToInt64(countCmd.ExecuteScalar() ?? 0);
        }

        var cmd = conn.CreateCommand();
        cmd.CommandText = selectSql;
        var reader = cmd.ExecuteReader();
        _seedEnumerator = GetSeedsFromReader(reader, conn, cmd);
    }

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
            if (_disposed)
                return ReadOnlySpan<char>.Empty;

            if (_seedReader != null)
            {
                string[] one = new string[1];
                int n = _seedReader.ReadSeeds(one);
                return n > 0 ? one[0].AsSpan() : ReadOnlySpan<char>.Empty;
            }

            if (_seedEnumerator == null)
                return ReadOnlySpan<char>.Empty;
            if (_seedEnumerator.MoveNext())
                return _seedEnumerator.Current.AsSpan();
            return ReadOnlySpan<char>.Empty;
        }
    }

    /// <summary>
    /// Batch retrieve multiple seeds in one lock operation - much faster for multi-threaded access.
    /// Fills the provided array with seed strings, returns the actual count retrieved.
    /// </summary>
    public int NextSeeds(string[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return 0;

        lock (_lock)
        {
            if (_disposed)
                return 0;

            if (_seedReader != null)
                return _seedReader.ReadSeeds(seeds);

            if (_seedEnumerator == null)
                return 0;

            int count = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                if (!_seedEnumerator.MoveNext())
                    break;
                seeds[i] = _seedEnumerator.Current;
                count++;
            }
            return count;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _seedReader?.Dispose();
            _seedReader = null;
            _seedEnumerator?.Dispose();
            _seedEnumerator = null;
        }
    }
}

// Keep the old name as an alias for backward compatibility
[Obsolete("Use DataLakeSeedProvider instead")]
public sealed class DuckDBSeedProvider : IMotelySeedProvider, IDisposable
{
    private readonly DataLakeSeedProvider _inner;

    public DuckDBSeedProvider(string dbPath) => _inner = new DataLakeSeedProvider(dbPath);
    public int SeedCount => _inner.SeedCount;
    public ReadOnlySpan<char> NextSeed() => _inner.NextSeed();
    public int NextSeeds(string[] seeds) => _inner.NextSeeds(seeds);
    public void Dispose() => _inner.Dispose();
}
