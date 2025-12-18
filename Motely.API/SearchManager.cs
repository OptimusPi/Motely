using System.Collections.Concurrent;
using DuckDB.NET.Data;
using Motely;
using Motely.Executors;
using Motely.Filters;
using System.Text.Json;
using System.Linq;

namespace Motely.API;

/// <summary>
/// Manages search instances with DuckDB persistence and proper sequential searching
/// Follows BalatroSeedOracle patterns for search management
/// </summary>
public class SearchManager
{
    private static SearchManager? _instance;
    private static readonly object _lock = new();
    
    public static SearchManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SearchManager();
                }
            }
            return _instance;
        }
    }

    private readonly ConcurrentDictionary<string, ActiveSearch> _activeSearches = new();
    private static readonly string _searchResultsDir = "SearchResults";

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private int _configuredThreadBudget = Environment.ProcessorCount;
    private const int ReservedThreads = 1;

    private WebSocketBroadcaster? _broadcaster;

    internal void SetBroadcaster(WebSocketBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    public class ActiveSearch
    {
        public string SearchId { get; set; } = "";
        public string FilterJaml { get; set; } = "";
        public string Deck { get; set; } = "";
        public string Stake { get; set; } = "";
        public JsonSearchExecutor? Executor { get; set; }
        public CancellationTokenSource? CancellationToken { get; set; }
        public MotelySearchDatabase? Database { get; set; }
        public Task? SearchTask { get; set; }
        public int AssignedThreads { get; set; } = 1;
        public int BatchSize { get; set; } = 3;
        public long ResumeStartBatch { get; set; } = 0;
        public Guid RunInstanceId { get; set; } = Guid.Empty;
        public string? StopReason { get; set; }
        public List<string> ColumnNames { get; set; } = new();
        public long CompletedBatches { get; set; } = 0;
        public long TotalBatches { get; set; } = 0;
        public long SeedsSearched { get; set; } = 0;
        public double SeedsPerSecond { get; set; } = 0;
        public int TotalResults { get; set; } = 0;
    }
    
    /// <summary>
    /// Start a new search.
    /// Returns immediate results from existing DB if available
    /// </summary>
    public async Task<(List<SearchResult> immediateResults, string searchId)> StartSearchAsync(
        string filterJaml, string deck, string stake, int seedCount)
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            Directory.CreateDirectory(_searchResultsDir);

            if (!JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var config, out var error) || config == null)
                throw new ArgumentException(error ?? "Invalid filter");

            var columnNames = config.GetColumnNames();

            var searchId = $"{GetFilterName(filterJaml)}_{deck}_{stake}";
            var dbPath = Path.Combine(_searchResultsDir, $"{searchId}.db");

            // If this searchId already exists, stop it first (restart semantics)
            if (_activeSearches.TryGetValue(searchId, out var existing))
            {
                var stopped = await StopSearchInternalAsync(existing, reason: "restart");
                if (!stopped)
                    throw new InvalidOperationException($"Failed to stop existing search {searchId} (timeout)");

                _activeSearches.TryRemove(searchId, out _);
            }

            // Immediate results from existing DB (if present)
            var immediateResults = new List<SearchResult>();
            if (File.Exists(dbPath))
            {
                immediateResults = GetTopResultsFromDb(dbPath, 1000);
            }

            var search = new ActiveSearch
            {
                SearchId = searchId,
                FilterJaml = filterJaml,
                Deck = deck,
                Stake = stake,
                CancellationToken = new CancellationTokenSource(),
                ColumnNames = columnNames,
                BatchSize = 3
            };

            search.StopReason = null;

            ReadResumeCursor(dbPath, columnNames, out var startBatch, out var batchSize);
            search.ResumeStartBatch = startBatch;
            if (batchSize > 0)
                search.BatchSize = batchSize;

            // (Re)open DB for active writes
            search.Database = new MotelySearchDatabase(dbPath, columnNames);

            _activeSearches[searchId] = search;

            await RebalanceAndRestartAllSearchesAsync();

            _broadcaster?.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

            return (immediateResults, searchId);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
    
    /// <summary>
    /// Get current top results and progress for a search
    /// </summary>
    public (List<SearchResult> results, int progressPercent) GetSearchStatus(string searchId)
    {
        var dbPath = Path.Combine(_searchResultsDir, $"{searchId}.db");
        var results = GetTopResultsFromDb(dbPath, 1000);
        
        var progressPercent = 0;
        if (_activeSearches.TryGetValue(searchId, out var search))
        {
            if (search.TotalBatches > 0)
            {
                progressPercent = (int)Math.Min(100, (search.CompletedBatches * 100) / search.TotalBatches);
            }
        }
        
        return (results, progressPercent);
    }
    
    public bool IsSearchRunning(string searchId)
    {
        return _activeSearches.ContainsKey(searchId);
    }

    public bool TryGetSearchMetrics(
        string searchId,
        out long currentBatch,
        out long totalBatches,
        out long seedsSearched,
        out double seedsPerSecond)
    {
        currentBatch = 0;
        totalBatches = 0;
        seedsSearched = 0;
        seedsPerSecond = 0;

        if (!_activeSearches.TryGetValue(searchId, out var search))
            return false;

        currentBatch = search.CompletedBatches;
        totalBatches = search.TotalBatches;
        seedsSearched = search.SeedsSearched;
        seedsPerSecond = search.SeedsPerSecond;
        return true;
    }

    public List<string> GetColumnNames(string searchId)
    {
        if (_activeSearches.TryGetValue(searchId, out var search) && search.ColumnNames.Count > 0)
            return new List<string>(search.ColumnNames);

        var dbPath = Path.Combine(_searchResultsDir, $"{searchId}.db");
        return GetColumnNamesFromDb(dbPath);
    }

    public bool TryGetRunningSearch(out string searchId, out string filterJaml)
    {
        searchId = "";
        filterJaml = "";

        var first = _activeSearches.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first.Key) || first.Value == null)
            return false;

        searchId = first.Key;
        filterJaml = first.Value.FilterJaml;
        return true;
    }

    public bool TryGetRunningSearchFilterJaml(string searchId, out string filterJaml)
    {
        filterJaml = "";
        if (!_activeSearches.TryGetValue(searchId, out var search))
            return false;
        filterJaml = search.FilterJaml;
        return !string.IsNullOrWhiteSpace(filterJaml);
    }

    public List<string> GetRunningSearchIds()
    {
        return _activeSearches.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public void SetThreadBudget(int threadCount)
    {
        _configuredThreadBudget = Math.Max(1, threadCount);
    }

    private List<string> GetColumnNamesFromDb(string dbPath)
    {
        if (!File.Exists(dbPath)) return new List<string> { "seed", "score" };

        try
        {
            using var conn = new DuckDBConnection($"Data Source={dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name='results' ORDER BY ordinal_position";
            using var reader = cmd.ExecuteReader();

            var cols = new List<string>();
            while (reader.Read())
                cols.Add(reader.GetString(0));

            return cols.Count > 0 ? cols : new List<string> { "seed", "score" };
        }
        catch
        {
            return new List<string> { "seed", "score" };
        }
    }

    /// <summary>
    /// Stop a search and return final results 
    /// </summary>
    public async Task<List<SearchResult>> StopSearchAsync(string searchId)
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            if (_activeSearches.TryRemove(searchId, out var search))
            {
                var stopped = await StopSearchInternalAsync(search, reason: "user_stop");
                if (!stopped)
                {
                    // Put it back so we don't orphan a still-running search
                    _activeSearches[searchId] = search;
                    throw new InvalidOperationException($"Failed to stop search {searchId} (timeout)");
                }
            }

            await RebalanceAndRestartAllSearchesAsync();

            _broadcaster?.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

            var dbPath = Path.Combine(_searchResultsDir, $"{searchId}.db");
            return GetTopResultsFromDb(dbPath, 1000);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task<bool> StopSearchInternalAsync(ActiveSearch search, string reason)
    {
        _broadcaster?.Broadcast(JsonSerializer.Serialize(new { type = "search_halted", searchId = search.SearchId, reason }));

        search.StopReason = reason;

        try
        {
            search.Executor?.Cancel();
        }
        catch { }

        try
        {
            search.CancellationToken?.Cancel();
        }
        catch { }

        var timeout = TimeSpan.FromSeconds(5);
        var completed = true;
        if (search.SearchTask != null)
        {
            try
            {
                var finished = await Task.WhenAny(search.SearchTask, Task.Delay(timeout));
                completed = ReferenceEquals(finished, search.SearchTask);
            }
            catch
            {
                completed = false;
            }
        }

        if (!completed)
            return false;

        if (reason is "user_stop" or "stop_all")
        {
            try
            {
                var dbPath = search.Database?.DatabasePath
                             ?? Path.Combine(_searchResultsDir, $"{search.SearchId}.db");
                await ExportTopResultsToFertilizerAsync(dbPath, limit: 1000);
            }
            catch
            {
            }
        }

        search.Executor = null;
        search.SearchTask = null;

        try
        {
            search.Database?.Checkpoint();
        }
        catch { }

        try
        {
            search.Database?.Dispose();
        }
        catch { }

        return true;
    }

    private int GetWorkerThreadBudget()
    {
        return Math.Max(1, _configuredThreadBudget - ReservedThreads);
    }

    private Dictionary<string, int> ComputeThreadAllocations(IReadOnlyList<string> searchIds)
    {
        var allocations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (searchIds.Count == 0)
            return allocations;

        var budget = GetWorkerThreadBudget();
        var baseThreads = Math.Max(1, budget / searchIds.Count);
        var remainder = Math.Max(0, budget - (baseThreads * searchIds.Count));

        for (var i = 0; i < searchIds.Count; i++)
        {
            allocations[searchIds[i]] = baseThreads + (i < remainder ? 1 : 0);
        }

        return allocations;
    }

    private async Task RebalanceAndRestartAllSearchesAsync()
    {
        var ids = _activeSearches.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
            return;

        var allocations = ComputeThreadAllocations(ids);

        var stoppable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Stop all running tasks first (so we can change thread counts)
        foreach (var id in ids)
        {
            if (_activeSearches.TryGetValue(id, out var s))
            {
                var stopped = await StopSearchInternalAsync(s, reason: "rebalance");
                if (stopped)
                    stoppable.Add(id);
            }
        }

        // Restart each search from its saved cursor
        foreach (var id in ids)
        {
            if (!stoppable.Contains(id))
                continue;

            if (!_activeSearches.TryGetValue(id, out var search))
                continue;

            search.CancellationToken = new CancellationTokenSource();
            search.StopReason = null;
            search.AssignedThreads = allocations.TryGetValue(id, out var t) ? Math.Max(1, t) : 1;

            var dbPath = Path.Combine(_searchResultsDir, $"{search.SearchId}.db");
            ReadResumeCursor(dbPath, search.ColumnNames, out var startBatch, out var batchSize);
            search.ResumeStartBatch = startBatch;
            if (batchSize > 0)
                search.BatchSize = batchSize;

            search.Database = new MotelySearchDatabase(dbPath, search.ColumnNames);

            _broadcaster?.Broadcast(JsonSerializer.Serialize(new
            {
                type = "search_started",
                searchId = search.SearchId,
                startBatch,
                threadsInUse = search.AssignedThreads
            }));

            var runId = Guid.NewGuid();
            search.RunInstanceId = runId;
            search.SearchTask = Task.Run(() => RunSequentialSearch(runId, search, search.FilterJaml, search.Deck, search.Stake, seedCount: 0, startBatchOverride: startBatch));
        }
    }
    
    private void ReadResumeCursor(string dbPath, List<string> columnNames, out long startBatch, out int batchSize)
    {
        startBatch = 0;
        batchSize = 0;

        try
        {
            using var db = new MotelySearchDatabase(dbPath, columnNames);
            var (lastBatch, lastBatchSize) = db.GetLastBatchPosition();
            if (lastBatch.HasValue)
                startBatch = lastBatch.Value;
            if (lastBatchSize.HasValue)
                batchSize = lastBatchSize.Value;
        }
        catch
        {
            startBatch = 0;
            batchSize = 0;
        }
    }

    private async Task RunSequentialSearch(Guid runId, ActiveSearch search, string filterJaml, string deck, string stake, int seedCount, long startBatchOverride)
    {
        try
        {
            // Save JAML to temp file for JsonSearchExecutor
            var tempConfigPath = Path.Combine(_searchResultsDir, $"{search.SearchId}_temp.jaml");
            await File.WriteAllTextAsync(tempConfigPath, filterJaml);
            
            // Create JsonSearchParams (check actual properties)
            var searchParams = new JsonSearchParams
            {
                Threads = Math.Max(1, search.AssignedThreads),
                BatchSize = search.BatchSize,
                StartBatch = (ulong)Math.Max(0, startBatchOverride),
                EnableDebug = false,
                NoFancy = true,
                Quiet = false
            };

            searchParams.ProgressCallback = (completedBatches, totalBatches, seedsSearched, seedsPerMs) =>
            {
                search.CompletedBatches = completedBatches;
                search.TotalBatches = totalBatches;
                search.SeedsSearched = seedsSearched;
                search.SeedsPerSecond = seedsPerMs * 1000.0;

                try
                {
                    var absoluteBatch = (long)searchParams.StartBatch + completedBatches;
                    search.Database?.SaveBatchPosition(absoluteBatch, searchParams.BatchSize);
                }
                catch
                {
                }

                _broadcaster?.Broadcast(JsonSerializer.Serialize(new
                {
                    type = "progress",
                    searchId = search.SearchId,
                    currentBatch = search.CompletedBatches,
                    totalBatches = search.TotalBatches,
                    seedsSearched = search.SeedsSearched,
                    seedsPerSecond = search.SeedsPerSecond,
                    seedsFound = search.TotalResults,
                    columns = search.ColumnNames,
                    threadsInUse = search.AssignedThreads
                }));
            };

            search.Executor = new JsonSearchExecutor(
                tempConfigPath,
                searchParams,
                "jaml",
                result =>
                {
                    search.Database?.InsertRow(result.Seed, result.Score, result.TallyColumns);
                    search.TotalResults++;

                    _broadcaster?.Broadcast(JsonSerializer.Serialize(new
                    {
                        type = "result",
                        searchId = search.SearchId,
                        result = new { seed = result.Seed, score = result.Score, tallies = result.TallyColumns },
                        columns = search.ColumnNames
                    }));
                });
            
            // Run the actual search in background - JsonSearchExecutor handles everything
            await Task.Run(() => 
            {
                search.Executor.Execute();
                // Clean up temp file after search
                try { File.Delete(tempConfigPath); } catch { }
            }, search.CancellationToken?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled - normal
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Search {search.SearchId} failed: {ex.Message}");
        }
        finally
        {
            // Natural completion path: if nothing requested a stop, remove from active list and export to fertilizer.
            // If we were cancelled for rebalance/user_stop/etc, StopSearchInternalAsync handles cleanup.
            if (search.StopReason == null && search.RunInstanceId == runId)
            {
                try
                {
                    var dbPath = search.Database?.DatabasePath
                                 ?? Path.Combine(_searchResultsDir, $"{search.SearchId}.db");
                    await ExportTopResultsToFertilizerAsync(dbPath, limit: 1000);
                }
                catch
                {
                }

                try
                {
                    search.Database?.Checkpoint();
                }
                catch
                {
                }

                try
                {
                    search.Database?.Dispose();
                }
                catch
                {
                }

                _activeSearches.TryRemove(search.SearchId, out _);
                _broadcaster?.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));
            }
        }
    }

    private async Task ExportTopResultsToFertilizerAsync(string dbPath, int limit)
    {
        var topSeeds = GetTopSeedsOnlyFromDb(dbPath, limit);
        if (topSeeds.Count == 0)
            return;

        await FertilizerDatabase.Instance.AddSeedsAsync(topSeeds);
    }

    private List<string> GetTopSeedsOnlyFromDb(string dbPath, int limit)
    {
        if (!File.Exists(dbPath)) return new List<string>();

        try
        {
            using var conn = new DuckDBConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT seed FROM results ORDER BY score DESC LIMIT ?";
            cmd.Parameters.Add(new DuckDBParameter(limit));

            var results = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read seeds from {dbPath}: {ex.Message}");
            return new List<string>();
        }
    }
    
    /// <summary>
    /// Get top results from a search DB
    /// </summary>
    private List<SearchResult> GetTopResultsFromDb(string dbPath, int limit)
    {
        if (!File.Exists(dbPath)) return new List<SearchResult>();
        
        try
        {
            using var conn = new DuckDBConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM results ORDER BY score DESC LIMIT {limit}";
            
            var results = new List<SearchResult>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var tallies = new List<int>();
                for (int i = 2; i < reader.FieldCount; i++)
                {
                    tallies.Add(reader.IsDBNull(i) ? 0 : reader.GetInt32(i));
                }

                results.Add(new SearchResult 
                { 
                    Seed = reader.GetString(0), 
                    Score = reader.GetInt32(1),
                    Tallies = tallies
                });
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read from {dbPath}: {ex.Message}");
            return new List<SearchResult>();
        }
    }
    
    private void DumpToFertilizerAndDeleteDb(string dbPath)
    {
        // TODO: Get top 1000 from search DB and add to fertilizer pile
        // Then delete the search DB file
        try
        {
            var topResults = GetTopResultsFromDb(dbPath, 1000);
            // Add to fertilizer logic here
            File.Delete(dbPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to dump {dbPath} to fertilizer: {ex.Message}");
        }
    }
    
    private string GetFilterName(string filterJaml)
    {
        // Extract filter name from JAML content
        try
        {
            var lines = filterJaml.Split('\n');
            var nameLine = lines.FirstOrDefault(l => l.StartsWith("name:", StringComparison.OrdinalIgnoreCase));
            if (nameLine != null)
            {
                return nameLine.Substring(5).Trim().Trim('"');
            }
        }
        catch { }
        
        return "UnknownFilter";
    }

    internal string GetFilterNameForId(string filterJaml)
    {
        return GetFilterName(filterJaml);
    }
}

// SearchResult class already exists in MotelyApiServer.cs