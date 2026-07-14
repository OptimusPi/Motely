using Motely;
using Motely.Filters;

namespace Motely.DataLake;

/// <summary>
/// The seed lake: bare seeds, one per line, appended live to Seeds/&lt;filterId&gt;.csv.
/// Every find hits the disk the moment it happens — tail it mid-run, grep it, drown it.
/// Seeds are the data; scores live in the console output and the JAML seeds: save-back.
/// </summary>
public sealed class SeedLakeSink : IMotelyResultSink
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private StreamWriter? _writer;
    private bool _disposed;

    /// <summary>Seeds/&lt;filterId&gt;.csv — root via --results-path or MOTELY_DATALAKE_PATH.</summary>
    public static string LakePath(string? root, string filterId)
    {
        root ??= Environment.GetEnvironmentVariable("MOTELY_DATALAKE_PATH");
        return Path.Combine(string.IsNullOrWhiteSpace(root) ? "Seeds" : root, filterId + ".csv");
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
            if (_disposed || !_seen.Add(seed))
                return;

            if (_writer is null)
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(_path));
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                _writer = new StreamWriter(_path, append: true) { AutoFlush = true };
            }

            _writer.WriteLine(seed);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }
}
