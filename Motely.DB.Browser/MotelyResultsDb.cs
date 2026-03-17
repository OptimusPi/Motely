using System.Text.Json;

namespace Motely.DB;

/// <summary>
/// Browser-compatible DuckDB WASM result store.
/// Uses [JSImport] to call DuckDB WASM running in JavaScript for remote Parquet lake queries.
/// Writes are stored in-memory (IndexedDB planned); reads can query remote Parquet on R2/S3.
/// </summary>
public sealed class MotelyResultsDb : IDisposable
{
    private static bool _initialized;
    private readonly int _tallyCount;
    private readonly List<ResultRow> _memoryResults = new();
    private readonly object _lock = new();

    /// <summary>Number of tally columns (one per should-clause).</summary>
    public int TallyCount => _tallyCount;

    public MotelyResultsDb(string dbPath, int tallyCount)
    {
        _tallyCount = Math.Max(0, tallyCount);

        // Fire-and-forget initialization of DuckDB WASM
        // (will be ready by the time user queries remote data)
        if (!_initialized)
        {
            _ = InitializeWasmAsync();
        }
    }

    private static async Task InitializeWasmAsync()
    {
        try
        {
            _initialized = await DuckDbWasmInterop.InitAsync();
        }
        catch
        {
            // DuckDB WASM unavailable — degrade gracefully to in-memory only
            _initialized = false;
        }
    }

    /// <summary>
    /// Configure S3/R2 credentials for querying remote Parquet lakes.
    /// Call this once before using QueryRemoteLakeAsync.
    /// </summary>
    public static async Task<bool> ConfigureRemoteLakeAsync(
        string region = "auto",
        string endpoint = "",
        string accessKeyId = "",
        string secretAccessKey = "")
    {
        if (!_initialized)
            await InitializeWasmAsync();

        return await DuckDbWasmInterop.ConfigureS3Async(region, endpoint, accessKeyId, secretAccessKey);
    }

    /// <summary>
    /// Query a remote Parquet file (e.g. on Cloudflare R2) and return parsed results.
    /// Uses DuckDB WASM httpfs to make HTTP range requests — only downloads needed data.
    /// </summary>
    public static async Task<List<ResultRow>> QueryRemoteLakeAsync(
        string parquetUrl,
        string? whereClause = null,
        string orderBy = "",
        int limit = 1000)
    {
        var json = await DuckDbWasmInterop.QueryParquetAsync(
            parquetUrl,
            whereClause ?? "",
            orderBy,
            limit);

        return ParseQueryResults(json);
    }

    /// <summary>
    /// Fetch seed strings from an Ice Lake Parquet on R2.
    /// Ice Lake files contain only seed values (pre-sorted into rank/suit buckets).
    /// </summary>
    public static async Task<List<string>> QueryIceLakeSeedsAsync(
        string parquetUrl,
        int limit = 1000)
    {
        var json = await DuckDbWasmInterop.QueryParquetAsync(parquetUrl, "", "", limit);
        return ParseSeedColumn(json);
    }

    /// <summary>
    /// Execute raw SQL against DuckDB WASM (for advanced queries).
    /// </summary>
    public static async Task<List<ResultRow>> QueryAsync(string sql)
    {
        var json = await DuckDbWasmInterop.QueryAsync(sql);
        return ParseQueryResults(json);
    }

    /// <summary>
    /// Count rows in a remote Parquet file without downloading all data.
    /// </summary>
    public static async Task<int> CountRemoteAsync(string parquetUrl)
    {
        return await DuckDbWasmInterop.CountParquetAsync(parquetUrl);
    }

    // --- Local in-memory write support (used during browser search sessions) ---

    public void AppendResults(ReadOnlySpan<ResultRow> rows)
    {
        lock (_lock)
        {
            foreach (ref readonly var row in rows)
                _memoryResults.Add(row);
        }
    }

    public void AppendResult(string seed, int score, ReadOnlySpan<int> tallies)
    {
        lock (_lock)
        {
            _memoryResults.Add(new ResultRow(seed, score, tallies.ToArray()));
        }
    }

    public List<ResultRow> GetTopResults(int limit = 1000)
    {
        lock (_lock)
        {
            return _memoryResults
                .OrderByDescending(r => r.Score)
                .Take(limit)
                .ToList();
        }
    }

    public long Count
    {
        get
        {
            lock (_lock)
            {
                return _memoryResults.Count;
            }
        }
    }

    public void ExportParquet(string parquetPath) { }

    public void ExportParquet(string parquetPath, int? limit) { }

    public void Clear()
    {
        lock (_lock)
        {
            _memoryResults.Clear();
        }
    }

    public void Dispose() { }

    // --- Helpers ---

    private static List<ResultRow> ParseQueryResults(string json)
    {
        var results = new List<ResultRow>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
            {
                // Query failed — return empty
                return results;
            }

            if (!root.TryGetProperty("rows", out var rows))
                return results;

            foreach (var row in rows.EnumerateArray())
            {
                var cells = new List<JsonElement>();
                foreach (var cell in row.EnumerateArray())
                    cells.Add(cell);

                if (cells.Count < 2) continue;

                var seed = cells[0].GetString() ?? "";
                var score = cells[1].GetInt32();
                var tallies = new int[Math.Max(0, cells.Count - 2)];
                for (int i = 2; i < cells.Count; i++)
                    tallies[i - 2] = cells[i].GetInt32();

                results.Add(new ResultRow(seed, score, tallies));
            }
        }
        catch
        {
            // Parse failure — degrade gracefully
        }

        return results;
    }

    private static List<string> ParseSeedColumn(string json)
    {
        var seeds = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return seeds;

            if (!root.TryGetProperty("rows", out var rows))
                return seeds;

            foreach (var row in rows.EnumerateArray())
            {
                var enumerator = row.EnumerateArray();
                if (enumerator.MoveNext())
                {
                    var seed = enumerator.Current.GetString();
                    if (!string.IsNullOrEmpty(seed))
                        seeds.Add(seed);
                }
            }
        }
        catch { }

        return seeds;
    }
}

/// <summary>
/// One result row: seed, score, and per-should-clause tallies.
/// </summary>
public readonly record struct ResultRow(string Seed, int Score, int[] Tallies);
