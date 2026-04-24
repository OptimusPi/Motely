namespace Motely.DB.SeedSource;

public sealed record SeedStoragePath(string Input, string ResolvedPath, bool IsExplicitPath)
{
    public string Extension => Path.GetExtension(ResolvedPath).ToLowerInvariant();
}

public static class SeedStoragePaths
{
    public static string StandardRoot => Path.Combine(Directory.GetCurrentDirectory(), "Seeds");

    public static string StandardLakeDirectory => Path.Combine(StandardRoot, "ducklake");

    public static string StandardSourceDirectory => StandardLakeDirectory;

    public static string StandardSinkDirectory => StandardLakeDirectory;

    public static SeedStoragePath ResolveSource(string value)
    {
        // Check if input is an HTTP/HTTPS URL
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // Return URL as-is for remote sources (DuckDB supports reading Parquet from URLs)
            return new SeedStoragePath(value, value, IsExplicitPath: true);
        }

        var resolved = Resolve(value, StandardSourceDirectory, "source", ensureParentDirectory: false);
        if (!File.Exists(resolved.ResolvedPath) && !Directory.Exists(resolved.ResolvedPath))
            throw new FileNotFoundException(
                $"Source not found. Looked for '{resolved.ResolvedPath}'.",
                resolved.ResolvedPath
            );
        return resolved;
    }

    public static SeedStoragePath ResolveSink(string value) =>
        Resolve(value, StandardSinkDirectory, "sink", ensureParentDirectory: true);

    private static SeedStoragePath Resolve(
        string value,
        string standardDirectory,
        string label,
        bool ensureParentDirectory
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} path is required.", nameof(value));

        var isExplicitPath = Path.IsPathRooted(value);
        var resolvedPath = isExplicitPath
            ? Path.GetFullPath(value)
            : Path.GetFullPath(Path.Combine(standardDirectory, value));

        if (ensureParentDirectory)
        {
            var parentDirectory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
                Directory.CreateDirectory(parentDirectory);
        }

        return new SeedStoragePath(value, resolvedPath, isExplicitPath);
    }
}

public static class SeedReader
{
    public static IReadOnlyList<string> ParseInlineSeeds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSeedToken)
            .Where(static seed => !string.IsNullOrWhiteSpace(seed))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<string> ReadSeeds(string value)
    {
        if (!Path.HasExtension(value) &&
            !value.Contains(Path.DirectorySeparatorChar) &&
            !value.Contains(Path.AltDirectorySeparatorChar))
        {
            return ReadSeedsByFilterId(value);
        }

        var source = SeedStoragePaths.ResolveSource(value);
        return ReadSeeds(source);
    }

    public static IReadOnlyList<string> ReadSeeds(SeedStoragePath source) =>
        source.Extension switch
        {
            ".txt" or ".csv" or ".list" => ReadSeedLines(source.ResolvedPath),
            ".db" => ReadDbSeeds(source.ResolvedPath),
            ".parquet" => ReadParquetSeeds(source.ResolvedPath),
            _ => throw new NotSupportedException(
                $"Unsupported source format '{source.Extension}'. Supported source formats: .txt, .csv, .list, .db, .parquet"
            ),
        };

    public static string NormalizeSeedToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var span = value.AsSpan().Trim();
        var commaIndex = span.IndexOf(',');
        if (commaIndex >= 0)
            span = span[..commaIndex];

        if (span.Length >= 2 && span[0] == '"' && span[^1] == '"')
            span = span[1..^1];

        return span.ToString().Trim().ToUpperInvariant().Replace('0', 'O');
    }

    private static IReadOnlyList<string> ReadSeedLines(string path) =>
        File.ReadLines(path)
            .Select(NormalizeSeedToken)
            .Where(static seed => !string.IsNullOrWhiteSpace(seed))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ReadDbSeeds(string path)
    {
        using var db = new MotelyResultsDb(path, 0);
        return db.GetSeeds().Select(NormalizeSeedToken).Where(static seed => !string.IsNullOrWhiteSpace(seed)).ToArray();
    }

    public static IReadOnlyList<string> ReadSeedsByFilterId(string filterId)
    {
        if (string.IsNullOrWhiteSpace(filterId))
            return [];

        using var db = new MotelyResultsDb(SeedStoragePaths.StandardLakeDirectory, 0);
        return db.GetSeeds(filterId)
            .Select(NormalizeSeedToken)
            .Where(static seed => !string.IsNullOrWhiteSpace(seed))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadParquetSeeds(string path)
    {
        var escapedPath = path.Replace("'", "''");
        using var conn = new DuckDB.NET.Data.DuckDBConnection("Data Source=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT seed FROM read_parquet('{escapedPath}') WHERE seed IS NOT NULL";
        using var reader = cmd.ExecuteReader();
        var seeds = new List<string>();
        while (reader.Read())
        {
            var raw = reader.IsDBNull(0) ? null : reader.GetString(0);
            var normalized = NormalizeSeedToken(raw ?? "");
            if (!string.IsNullOrWhiteSpace(normalized))
                seeds.Add(normalized);
        }
        return seeds
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

public interface ISeedResultSink : IDisposable
{
    string OutputPath { get; }

    void AppendSeed(string seed);

    void AppendScoredResult(string seed, int score, ReadOnlySpan<int> tallies);
}

public static class SeedResultSinkFactory
{
    public static ISeedResultSink Create(string value, int tallyCount)
    {
        var sink = SeedStoragePaths.ResolveSink(value);
        return sink.Extension switch
        {
            ".db" => new DuckLakeSeedResultSink(sink.ResolvedPath, tallyCount),
            ".parquet" => new ParquetSeedResultSink(sink.ResolvedPath, tallyCount),
            _ => throw new NotSupportedException(
                $"Unsupported sink format '{sink.Extension}'. Supported sink formats: .db, .parquet"
            ),
        };
    }
}

public sealed class SeedResultSinkDirectory : IDisposable
{
    private readonly string _directoryPath;
    private readonly MotelyResultsDb _db;
    private readonly Dictionary<string, ISeedResultSink> _sinks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public SeedResultSinkDirectory(string directoryPath, int tallyCount = 0, string extension = ".db")
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Sink directory path is required.", nameof(directoryPath));

        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Sink file extension is required.", nameof(extension));

        _directoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(_directoryPath);
        _db = new MotelyResultsDb(_directoryPath, tallyCount);
    }

    public ISeedResultSink GetOrOpen(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Sink key is required.", nameof(key));

        lock (_lock)
        {
            if (_sinks.TryGetValue(key, out var existing))
                return existing;

            var sink = new FilterScopedSeedResultSink(_db, key, Path.Combine(_directoryPath, $"{key}.parquet"));
            _sinks[key] = sink;
            return sink;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var sink in _sinks.Values)
                sink.Dispose();
            _sinks.Clear();
        }

        _db.Dispose();
    }
}

internal sealed class FilterScopedSeedResultSink : ISeedResultSink
{
    private readonly MotelyResultsDb _db;
    private readonly string _filterId;

    public FilterScopedSeedResultSink(MotelyResultsDb db, string filterId, string outputPath)
    {
        _db = db;
        _filterId = filterId;
        OutputPath = outputPath;
    }

    public string OutputPath { get; }

    public void AppendSeed(string seed)
    {
        var normalizedSeed = SeedReader.NormalizeSeedToken(seed);
        if (string.IsNullOrWhiteSpace(normalizedSeed))
            return;

        _db.AppendResult(_filterId, normalizedSeed, 0, ReadOnlySpan<int>.Empty);
    }

    public void AppendScoredResult(string seed, int score, ReadOnlySpan<int> tallies)
    {
        var normalizedSeed = SeedReader.NormalizeSeedToken(seed);
        if (string.IsNullOrWhiteSpace(normalizedSeed))
            return;

        _db.AppendResult(_filterId, normalizedSeed, score, tallies);
    }

    public void Dispose()
    {
        // Shared DuckLake lifetime belongs to SeedResultSinkDirectory.
    }
}

internal sealed class DuckLakeSeedResultSink : ISeedResultSink
{
    private readonly MotelyResultsDb _db;

    public DuckLakeSeedResultSink(string outputPath, int tallyCount)
    {
        OutputPath = outputPath;
        _db = new MotelyResultsDb(outputPath, tallyCount);
    }

    public string OutputPath { get; }

    public void AppendSeed(string seed)
    {
        var normalizedSeed = SeedReader.NormalizeSeedToken(seed);
        if (string.IsNullOrWhiteSpace(normalizedSeed))
            return;

        _db.AppendResult(normalizedSeed, 0, ReadOnlySpan<int>.Empty);
    }

    public void AppendScoredResult(string seed, int score, ReadOnlySpan<int> tallies)
    {
        var normalizedSeed = SeedReader.NormalizeSeedToken(seed);
        if (string.IsNullOrWhiteSpace(normalizedSeed))
            return;

        _db.AppendResult(normalizedSeed, score, tallies);
    }

    public void Dispose() => _db.Dispose();
}

internal sealed class ParquetSeedResultSink : ISeedResultSink
{
    private readonly MotelyResultsDb _db;

    public ParquetSeedResultSink(string outputPath, int tallyCount)
    {
        OutputPath = outputPath;
        _db = new MotelyResultsDb(":memory:", tallyCount);
    }

    public string OutputPath { get; }

    public void AppendSeed(string seed)
    {
        var normalizedSeed = SeedReader.NormalizeSeedToken(seed);
        if (string.IsNullOrWhiteSpace(normalizedSeed))
            return;

        _db.AppendResult(normalizedSeed, 0, ReadOnlySpan<int>.Empty);
    }

    public void AppendScoredResult(string seed, int score, ReadOnlySpan<int> tallies)
    {
        var normalizedSeed = SeedReader.NormalizeSeedToken(seed);
        if (string.IsNullOrWhiteSpace(normalizedSeed))
            return;

        _db.AppendResult(normalizedSeed, score, tallies);
    }

    public void Dispose()
    {
        _db.ExportParquet(OutputPath);
        _db.Dispose();
    }
}
