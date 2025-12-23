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
    private readonly ConcurrentDictionary<string, string> _lastErrors = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string _searchResultsDir = "SearchResults";
    
    public string GetSearchResultsDir() => _searchResultsDir;

    private string? _motelyRoot;

    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private int _configuredThreadBudget = Environment.ProcessorCount;
    private const int ReservedThreads = 1;

    private ISearchBroadcaster? _broadcaster;

    internal void SetBroadcaster(ISearchBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    internal void SetMotelyRoot(string motelyRoot)
    {
        _motelyRoot = motelyRoot;
    }

    public bool TryGetLastError(string searchId, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(searchId))
            return false;

        if (!_lastErrors.TryGetValue(searchId, out var e))
            return false;

        error = e;
        return !string.IsNullOrWhiteSpace(error);
    }

    public class ActiveSearch
    {
        public string SearchId { get; set; } = "";
        public string FilterJaml { get; set; } = "";
        public string Deck { get; set; } = "";
        public string Stake { get; set; } = "";
        public string? SeedSource { get; set; }
        public List<string>? SeedList { get; set; }
        public JsonSearchExecutor? Executor { get; set; }
        public CancellationTokenSource? CancellationToken { get; set; }
        public MotelySearchDatabase? Database { get; set; }
        public Task? SearchTask { get; set; }
        public int AssignedThreads { get; set; } = 1;
        public int BatchSize { get; set; } = 3;
        public long ResumeStartBatch { get; set; } = 0;
        public int? CutoffOverride { get; set; }
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
        string filterJaml,
        string deck,
        string stake,
        int seedCount,
        long? startBatchOverride = null,
        int? cutoffOverride = null,
        string? seedSource = null,
        List<string>? seedList = null)
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

            _lastErrors.TryRemove(searchId, out _);

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
                SeedSource = seedSource,
                SeedList = seedList,
                CancellationToken = new CancellationTokenSource(),
                ColumnNames = columnNames,
                BatchSize = 3,
                CutoffOverride = cutoffOverride
            };

            search.StopReason = null;

            ReadResumeCursor(dbPath, columnNames, out var startBatch, out var batchSize);
            if (startBatchOverride.HasValue)
                search.ResumeStartBatch = Math.Max(0, startBatchOverride.Value);
            else
                search.ResumeStartBatch = startBatch;
            if (batchSize > 0)
                search.BatchSize = batchSize;

            // (Re)open DB for active writes
            search.Database = new MotelySearchDatabase(dbPath, columnNames);

            // Save JAML metadata file so it can be retrieved even after search stops
            var jamlPath = Path.Combine(_searchResultsDir, $"{searchId}.jaml");
            File.WriteAllText(jamlPath, filterJaml);

            // Automatically save filter to JamlFilters ecosystem
            SaveFilterToEcosystem(filterJaml);

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
        var results = new List<SearchResult>();
        
        var progressPercent = 0;
        if (_activeSearches.TryGetValue(searchId, out var search))
        {
            try
            {
                if (search.Database != null)
                {
                    results = search.Database.GetTopResults(1000);
                }
            }
            catch
            {
                results = new List<SearchResult>();
            }

            if (search.TotalBatches > 0)
            {
                progressPercent = (int)Math.Min(100, (search.CompletedBatches * 100) / search.TotalBatches);
            }
        }

        if (results.Count == 0)
        {
            results = GetTopResultsFromDb(dbPath, 1000);
        }
        
        return (results, progressPercent);
    }
    
    public bool IsSearchRunning(string searchId)
    {
        return _activeSearches.ContainsKey(searchId);
    }

    public bool TryGetSearchProgress(string searchId, out long currentBatch, out long totalBatches)
    {
        currentBatch = 0;
        totalBatches = 0;

        if (!_activeSearches.TryGetValue(searchId, out var search))
            return false;

        currentBatch = search.CompletedBatches;
        totalBatches = search.TotalBatches;
        return true;
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
        
        // First check active searches
        if (_activeSearches.TryGetValue(searchId, out var search))
        {
            filterJaml = search.FilterJaml;
            if (!string.IsNullOrWhiteSpace(filterJaml))
                return true;
        }
        
        // If not in active searches, try to load from saved metadata file
        var jamlPath = Path.Combine(_searchResultsDir, $"{searchId}.jaml");
        if (File.Exists(jamlPath))
        {
            try
            {
                filterJaml = File.ReadAllText(jamlPath);
                return !string.IsNullOrWhiteSpace(filterJaml);
            }
            catch
            {
                // File read failed, return false
            }
        }
        
        return false;
    }

    public bool TryGetSearchOverrides(string searchId, out long? startBatchOverride, out int? cutoffOverride)
    {
        startBatchOverride = null;
        cutoffOverride = null;
        if (!_activeSearches.TryGetValue(searchId, out var search))
            return false;

        cutoffOverride = search.CutoffOverride;
        return true;
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

    public async Task StopAllSearchesAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            var ids = _activeSearches.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var id in ids)
            {
                if (_activeSearches.TryRemove(id, out var search))
                {
                    // For stop_all, we don't re-add even if stop times out - force clear
                    await StopSearchInternalAsync(search, reason: "stop_all");
                }
            }

            _broadcaster?.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ClearAllSearchesAsync()
    {
        await _lifecycleGate.WaitAsync();
        try
        {
            // Clear all active searches
            var ids = _activeSearches.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var id in ids)
            {
                if (_activeSearches.TryRemove(id, out var search))
                {
                    await StopSearchInternalAsync(search, reason: "clear_all");
                }
            }

            // Clear all stored results by deleting all database files
            if (Directory.Exists(_searchResultsDir))
            {
                var dbFiles = Directory.GetFiles(_searchResultsDir, "*.db");
                foreach (var file in dbFiles)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Ignore file deletion errors
                    }
                }
            }
            
            // Broadcast clear event
            _broadcaster?.Broadcast(JsonSerializer.Serialize(new { type = "results_cleared" }));
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task<bool> StopSearchInternalAsync(ActiveSearch search, string reason)
    {
        _broadcaster?.BroadcastToSearch(search.SearchId, JsonSerializer.Serialize(new { type = "search_halted", searchId = search.SearchId, reason }));

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

        var timeout = TimeSpan.FromSeconds(1);
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

    private void ApplySeedSource(JsonSearchParams searchParams, string? seedSource)
    {
        if (string.IsNullOrWhiteSpace(seedSource))
            return;

        var s = seedSource.Trim();
        if (string.Equals(s, "all", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(s, "new", StringComparison.OrdinalIgnoreCase))
            return;

        if (s.StartsWith("random:", StringComparison.OrdinalIgnoreCase))
        {
            var raw = s.Substring("random:".Length);
            if (int.TryParse(raw, out var n) && n > 0)
            {
                searchParams.RandomSeeds = n;
            }
            return;
        }

        if (s.StartsWith("txt:", StringComparison.OrdinalIgnoreCase))
        {
            var file = s.Substring("txt:".Length).Trim();
            if (file.Length == 0) return;
            var stem = Path.GetFileNameWithoutExtension(file);
            if (stem.Length == 0) return;
            searchParams.Wordlist = stem;
            return;
        }

        if (s.StartsWith("csv:", StringComparison.OrdinalIgnoreCase))
        {
            var file = s.Substring("csv:".Length).Trim();
            if (file.Length == 0) return;
            
            var safeName = Path.GetFileName(file);
            string? csvPath = null;
            
            if (!string.IsNullOrWhiteSpace(_motelyRoot))
            {
                var p1 = Path.Combine(_motelyRoot, "WordLists", safeName);
                if (File.Exists(p1))
                {
                    csvPath = p1;
                }
                else
                {
                    var p2 = Path.Combine(_motelyRoot, "wordlists", safeName);
                    if (File.Exists(p2))
                    {
                        csvPath = p2;
                    }
                }
            }
            
            if (csvPath == null && File.Exists(file))
            {
                csvPath = file;
            }
            
            if (csvPath != null && File.Exists(csvPath))
            {
                // Parse CSV and validate seeds
                var csvContent = File.ReadAllText(csvPath);
                var seeds = SeedSourceHelper.ParseCsvSeeds(csvContent);
                if (seeds.Count > 0)
                {
                    searchParams.SeedList = seeds;
                }
            }
            return;
        }

        if (s.StartsWith("db:", StringComparison.OrdinalIgnoreCase))
        {
            var file = s.Substring("db:".Length).Trim();
            if (file.Length == 0) return;

            var safeName = Path.GetFileName(file);

            if (!string.IsNullOrWhiteSpace(_motelyRoot))
            {
                var p1 = Path.Combine(_motelyRoot, "WordLists", safeName);
                if (File.Exists(p1))
                {
                    searchParams.DbList = p1;
                    return;
                }

                var p2 = Path.Combine(_motelyRoot, "wordlists", safeName);
                if (File.Exists(p2))
                {
                    searchParams.DbList = p2;
                    return;
                }
            }

            searchParams.DbList = file;
            return;
        }
    }

    private async Task RunSequentialSearch(Guid runId, ActiveSearch search, string filterJaml, string deck, string stake, int seedCount, long startBatchOverride)
    {
        try
        {
            if (!JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var config, out var parseError) || config == null)
            {
                var errText = parseError ?? "Invalid filter";
                _lastErrors[search.SearchId] = errText;
                _broadcaster?.BroadcastToSearch(search.SearchId, JsonSerializer.Serialize(new
                {
                    type = "search_failed",
                    searchId = search.SearchId,
                    error = errText
                }));
                return;
            }

            var searchParams = new JsonSearchParams
            {
                Threads = Math.Max(1, search.AssignedThreads),
                BatchSize = search.BatchSize,
                StartBatch = (ulong)Math.Max(0, startBatchOverride),
                Cutoff = search.CutoffOverride ?? 0,
                AutoCutoff = !search.CutoffOverride.HasValue || search.CutoffOverride.Value <= 0,
                EnableDebug = false,
                NoFancy = true,
                Quiet = false
            };

            // If SeedList is provided, use it directly; otherwise apply seed source
            if (search.SeedList != null && search.SeedList.Count > 0)
            {
                searchParams.SeedList = search.SeedList;
            }
            else
            {
                ApplySeedSource(searchParams, search.SeedSource);
            }

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

                // Check if search is complete (all batches done)
                bool isComplete = totalBatches > 0 && completedBatches >= totalBatches;

                _broadcaster?.BroadcastToSearch(search.SearchId, JsonSerializer.Serialize(new
                {
                    type = isComplete ? "search_completed" : "progress",
                    searchId = search.SearchId,
                    currentBatch = search.CompletedBatches,
                    totalBatches = search.TotalBatches,
                    seedsSearched = search.SeedsSearched,
                    seedsPerSecond = search.SeedsPerSecond,
                    seedsFound = search.TotalResults,
                    columns = search.ColumnNames,
                    threadsInUse = search.AssignedThreads,
                    completed = isComplete
                }));
            };

            search.Executor = new JsonSearchExecutor(
                config,
                searchParams,
                result =>
                {
                    search.Database?.InsertRow(result.Seed, result.Score, result.TallyColumns);
                    search.TotalResults++;

                    _broadcaster?.BroadcastToSearch(search.SearchId, JsonSerializer.Serialize(new
                    {
                        type = "result",
                        searchId = search.SearchId,
                        result = new { seed = result.Seed, score = result.Score, tallies = result.TallyColumns },
                        columns = search.ColumnNames
                    }));
                });

            await Task.Run(() =>
            {
                search.Executor.Execute();
            }, search.CancellationToken?.Token ?? CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            var err = ex.ToString();
            _lastErrors[search.SearchId] = err;
            _broadcaster?.BroadcastToSearch(search.SearchId, JsonSerializer.Serialize(new
            {
                type = "search_failed",
                searchId = search.SearchId,
                error = ex.Message
            }));

            Console.WriteLine($"Search {search.SearchId} failed: {ex.Message}");
        }
        finally
        {
            if (search.StopReason == null && search.RunInstanceId == runId)
            {
                // Broadcast search completion if it finished naturally (not cancelled)
                _broadcaster?.BroadcastToSearch(search.SearchId, JsonSerializer.Serialize(new
                {
                    type = "search_completed",
                    searchId = search.SearchId,
                    seedsFound = search.TotalResults,
                    seedsSearched = search.SeedsSearched,
                    columns = search.ColumnNames
                }));

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
                if (s.SearchTask == null)
                {
                    // Brand new (or already stopped) search: don't broadcast a rebalance halt.
                    // Just mark it as eligible to start below.
                    stoppable.Add(id);
                    continue;
                }

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

            _broadcaster?.BroadcastToSearch(search.SearchId, JsonSerializer.Serialize(new
            {
                type = "search_started",
                searchId = search.SearchId,
                deck = search.Deck,
                stake = search.Stake,
                threads = search.AssignedThreads,
                batchSize = search.BatchSize
            }));

            var runId = Guid.NewGuid();
            search.RunInstanceId = runId;
            search.SearchTask = RunSequentialSearch(runId, search, search.FilterJaml, search.Deck, search.Stake, seedCount: 0, startBatchOverride: search.ResumeStartBatch);
        }
    }

    private async Task ExportTopResultsToFertilizerAsync(string dbPath, int limit)
    {
        var topSeeds = GetTopSeedsOnlyFromDb(dbPath, limit);
        if (topSeeds.Count == 0)
            return;

        await FertilizerDatabase.Instance.AddSeedsAsync(topSeeds);
    }

    public List<string> GetTopSeedsOnlyFromDb(string dbPath, int limit)
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

    private void SaveFilterToEcosystem(string filterJaml)
    {
        if (string.IsNullOrWhiteSpace(_motelyRoot) || string.IsNullOrWhiteSpace(filterJaml))
            return;

        try
        {
            var filtersPath = Path.Combine(_motelyRoot, "JamlFilters");
            Directory.CreateDirectory(filtersPath);

            // Extract name from JAML config
            string? normalizedName = null;
            if (JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out _) && cfg != null)
            {
                if (!string.IsNullOrWhiteSpace(cfg.Name))
                {
                    normalizedName = SanitizeFilterFileStem(cfg.Name);
                }
            }

            // If we couldn't extract a name, generate one from the filter content
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = GetFilterName(filterJaml);
                if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName == "UnknownFilter")
                {
                    normalizedName = $"Filter_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                }
                normalizedName = SanitizeFilterFileStem(normalizedName);
            }

            var fileName = normalizedName + ".jaml";
            var fullPath = Path.Combine(filtersPath, fileName);

            // Only save if it doesn't already exist (don't overwrite user's saved filters)
            if (!File.Exists(fullPath))
            {
                File.WriteAllText(fullPath, filterJaml);
                _broadcaster?.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));
            }
        }
        catch
        {
            // Silently fail - saving to ecosystem is nice-to-have, not critical
        }
    }

    private static string SanitizeFilterFileStem(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        // Replace spaces with underscores
        trimmed = trimmed.Replace(' ', '_');
        
        var invalid = Path.GetInvalidFileNameChars();
        var chars = trimmed.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        var safe = new string(chars).Trim();
        safe = safe.Replace(Path.DirectorySeparatorChar, '-').Replace(Path.AltDirectorySeparatorChar, '-');
        return safe;
    }
}

// SearchResult class already exists in MotelyApiServer.cs
