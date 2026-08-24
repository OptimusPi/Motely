using DuckDB.NET.Data;
using Motely.Filters;

namespace Motely.DataLake;

/// <summary>
/// One filter's writer into the seed lake (<see cref="SeedLake"/>): every find — bare or scored, with
/// its tallies — lands in the shared <c>results</c> table under this filter's id, and the filter's
/// tally labels are registered so the rows stay readable. Thread-safe; result callbacks fire on every
/// engine thread. Finds are durable within <see cref="SeedLake.FlushInterval"/> of being found, and on
/// <see cref="Dispose"/>.
///
/// When DuckLake cannot be attached at all (first run offline — the extensions never downloaded — or
/// an unusable catalog path) the sink says so once and writes bare seeds to the legacy per-filter
/// <c>&lt;root&gt;/&lt;filterId&gt;.duckdb</c> instead, which <c>--drown</c> still pours. A search never
/// loses finds because of storage.
/// </summary>
public sealed class SeedLakeSink : IMotelyResultSink
{
    private readonly string? _root;
    private readonly string? _catalogPath;
    private readonly string _filterId;
    private readonly IReadOnlyList<string>? _tallyLabels;
    private readonly object _gate = new();
    private SeedLake? _lake;
    private LegacySeedFile? _legacy;
    private bool _opened;
    private bool _disposed;

    /// <summary>The data root as the CLI prints it: --results-path, else MOTELY_DATALAKE_PATH, else "Seeds".</summary>
    public static string LakeRoot(string? root)
    {
        root ??= Environment.GetEnvironmentVariable(SeedLake.DataRootEnv);
        return string.IsNullOrWhiteSpace(root) ? "Seeds" : root;
    }

    /// <summary>The legacy per-filter file, <c>&lt;root&gt;/&lt;filterId&gt;.duckdb</c> — the fallback
    /// target, and what older lakes on disk look like.</summary>
    public static string LakePath(string? root, string filterId) =>
        Path.Combine(LakeRoot(root), filterId + ".duckdb");

    /// <param name="root">Data root; see <see cref="LakeRoot"/>.</param>
    /// <param name="filterId">The JAML filter id every row is tagged with.</param>
    /// <param name="tallyLabels">The filter's tally column names, registered once per open (scored filters).</param>
    /// <param name="catalogPath">An explicit catalog; default: beside the data root (see <see cref="SeedLake.CatalogPathFor"/>).</param>
    public SeedLakeSink(string? root, string filterId, IReadOnlyList<string>? tallyLabels = null, string? catalogPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterId);
        _root = root;
        _filterId = filterId;
        _tallyLabels = tallyLabels;
        _catalogPath = catalogPath;
    }

    /// <summary>True once the lake (not the legacy file) is taking this sink's writes. Null before the first write.</summary>
    public bool? UsingLake
    {
        get { lock (_gate) return _opened ? _lake is not null : null; }
    }

    public void OnSeed(string seed) => Write(seed, null, ReadOnlySpan<int>.Empty);

    public void OnScored(in MotelyScoredSeedResult tally) => Write(tally.Seed, tally.Score, tally.TallyValuesSpan);

    /// <summary>Write one find directly — the worker's path, where results arrive as (seed, score).</summary>
    public void Write(string seed, int? score, ReadOnlySpan<int> tallies = default)
    {
        if (string.IsNullOrEmpty(seed))
            return;

        lock (_gate)
        {
            if (_disposed)
                return;
            EnsureOpen();
            if (_lake is not null)
                _lake.Write(_filterId, seed, score, tallies);
            else
                _legacy!.Write(seed);
        }
    }

    private void EnsureOpen()
    {
        if (_opened)
            return;
        _opened = true;
        try
        {
            _lake = SeedLake.Open(_root, _catalogPath);
            if (_tallyLabels is { Count: > 0 })
                _lake.RegisterFilter(_filterId, _tallyLabels);
        }
        catch (Exception ex)
        {
            _lake?.Dispose();
            _lake = null;
            var legacyPath = LakePath(_root, _filterId);
            var reason = ex.Message;
            int nl = reason.IndexOf('\n');
            if (nl >= 0) reason = reason[..nl];
            Console.Error.WriteLine(
                $"[SeedLake] DuckLake unavailable ({reason}); writing bare seeds to {legacyPath} instead — scores and tallies for this run are in the CSV only.");
            _legacy = new LegacySeedFile(legacyPath);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _lake?.Dispose();
            _legacy?.Dispose();
            _lake = null;
            _legacy = null;
        }
    }

    /// <summary>The pre-lake format, kept as the fallback: a per-filter DuckDB file with one table,
    /// <c>seeds(seed VARCHAR PRIMARY KEY)</c>, one INSERT OR IGNORE per find.</summary>
    private sealed class LegacySeedFile : IDisposable
    {
        private readonly string _path;
        private DuckDBConnection? _connection;

        public LegacySeedFile(string path) => _path = path;

        public void Write(string seed)
        {
            if (_connection is null)
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(_path));
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                // Publish the connection only once it is fully open with the table in place.
                var connection = new DuckDBConnection($"Data Source={_path}");
                try
                {
                    connection.Open();
                    using var create = connection.CreateCommand();
                    create.CommandText = "CREATE TABLE IF NOT EXISTS seeds (seed VARCHAR PRIMARY KEY)";
                    create.ExecuteNonQuery();
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
                _connection = connection;
            }

            using var insert = _connection.CreateCommand();
            insert.CommandText = "INSERT OR IGNORE INTO seeds VALUES (?)";
            insert.Parameters.Add(new DuckDBParameter { Value = seed });
            insert.ExecuteNonQuery();
        }

        public void Dispose()
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
