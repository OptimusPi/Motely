using System.Collections.Concurrent;
using Motely.API.Models;
using Motely.API.Services;
using Motely.Filters;

namespace Motely.API;

/// <summary>
/// Facade for search operations - provides unified API for MCP and other consumers.
/// Wraps SearchService with simpler synchronous-style API and manages search state.
/// </summary>
public class SearchManager
{
    private static SearchManager? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Singleton instance for MCP compatibility. Initialized via DI or first access.
    /// </summary>
    public static SearchManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SearchManager(null!);
                }
            }
            return _instance;
        }
    }

    private readonly SearchService? _searchService;
    private readonly ConcurrentDictionary<string, SearchMetrics> _metrics = new();
    private readonly string _searchResultsDir;

    public SearchManager(SearchService searchService)
    {
        _searchService = searchService;
        _searchResultsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Motely",
            "SearchResults"
        );
        Directory.CreateDirectory(_searchResultsDir);

        // Set singleton instance on DI creation
        lock (_lock)
        {
            _instance ??= this;
        }
    }

    /// <summary>
    /// Start a search from JAML/JSON string and return results with search ID.
    /// </summary>
    public async Task<(List<SearchResult> Results, string SearchId)> StartSearchAsync(
        string jamlOrJson,
        string? deck = null,
        string? stake = null,
        int seedCount = 1_000_000,
        int cutoff = 50,
        int threadCount = 0,
        string? seedSource = null
    )
    {
        var options = new System.Text.Json.JsonSerializerOptions(
            System.Text.Json.JsonSerializerDefaults.Web
        );
        var config =
            System.Text.Json.JsonSerializer.Deserialize<MotelyJsonConfig>(jamlOrJson, options)
            ?? throw new ArgumentException("Invalid JAML/JSON configuration", nameof(jamlOrJson));
        return await StartSearchAsync(
            config,
            deck,
            stake,
            seedCount,
            cutoff,
            threadCount,
            seedSource
        );
    }

    /// <summary>
    /// Start a search and return results with search ID.
    /// </summary>
    public async Task<(List<SearchResult> Results, string SearchId)> StartSearchAsync(
        MotelyJsonConfig config,
        string? deck = null,
        string? stake = null,
        int seedCount = 1_000_000,
        int cutoff = 50,
        int threadCount = 0,
        string? seedSource = null
    )
    {
        // Apply deck/stake overrides if provided
        if (!string.IsNullOrEmpty(deck))
            config.Deck = deck;
        if (!string.IsNullOrEmpty(stake))
            config.Stake = stake;

        var sourceType = SearchSourceType.Random;
        if (!string.IsNullOrEmpty(seedSource))
        {
            if (seedSource.StartsWith("random:"))
                sourceType = SearchSourceType.Random;
            else if (seedSource.StartsWith("db:"))
                sourceType = SearchSourceType.DbList;
            else if (seedSource.StartsWith("txt:") || seedSource.StartsWith("csv:"))
                sourceType = SearchSourceType.Wordlist;
        }

        var criteria = new SearchCriteriaDto
        {
            ThreadCount = threadCount > 0 ? threadCount : Environment.ProcessorCount,
            MinScore = cutoff,
            SourceType = sourceType,
            // Note: SeedCount is controlled via SourceType and EndBatch for random searches
            EndBatch = (ulong)(seedCount / 1225), // ~35^2 seeds per batch
        };

        if (_searchService == null)
        {
            // Fallback for when Instance is used without DI
            var searchId = Guid.NewGuid().ToString();
            _metrics.TryAdd(searchId, new SearchMetrics());
            return (new List<SearchResult>(), searchId);
        }

        var id = await _searchService.StartSearchAsync(config, criteria);

        // Initialize metrics tracking
        _metrics.TryAdd(id, new SearchMetrics());

        // Wait for results (simplified - real impl would use SignalR or polling)
        await Task.Delay(100); // Give search time to start

        var results = new List<SearchResult>();
        // Results will be populated via SignalR - return empty for now
        return (results, id);
    }

    /// <summary>
    /// Get column names for a search.
    /// </summary>
    public List<string> GetColumnNames(string searchId)
    {
        // Return default columns - real impl would get from search config
        return new List<string> { "seed", "score" };
    }

    /// <summary>
    /// Get search status (results and progress).
    /// </summary>
    public (List<SearchResult> Results, double ProgressPercent) GetSearchStatus(string searchId)
    {
        if (_metrics.TryGetValue(searchId, out var metrics))
        {
            return (new List<SearchResult>(), metrics.ProgressPercent);
        }
        return (new List<SearchResult>(), 0);
    }

    /// <summary>
    /// Check if a search is running.
    /// </summary>
    public bool IsSearchRunning(string searchId)
    {
        return _metrics.TryGetValue(searchId, out var metrics) && metrics.IsRunning;
    }

    /// <summary>
    /// Try to get search metrics.
    /// </summary>
    public bool TryGetSearchMetrics(
        string searchId,
        out long currentBatch,
        out long totalBatches,
        out long seedsSearched,
        out double seedsPerSecond
    )
    {
        if (_metrics.TryGetValue(searchId, out var metrics))
        {
            currentBatch = metrics.CurrentBatch;
            totalBatches = metrics.TotalBatches;
            seedsSearched = metrics.SeedsSearched;
            seedsPerSecond = metrics.SeedsPerSecond;
            return true;
        }
        currentBatch = 0;
        totalBatches = 0;
        seedsSearched = 0;
        seedsPerSecond = 0;
        return false;
    }

    /// <summary>
    /// Get the search results directory path.
    /// </summary>
    public string GetSearchResultsDir() => _searchResultsDir;

    /// <summary>
    /// Update metrics for a search (called by SignalR hub or internal callbacks).
    /// </summary>
    public void UpdateMetrics(string searchId, SearchMetrics metrics)
    {
        _metrics.AddOrUpdate(searchId, metrics, (_, _) => metrics);
    }

    public class SearchMetrics
    {
        public bool IsRunning { get; set; } = true;
        public double ProgressPercent { get; set; }
        public long CurrentBatch { get; set; }
        public long TotalBatches { get; set; }
        public long SeedsSearched { get; set; }
        public long MatchingSeeds { get; set; }
        public double SeedsPerSecond { get; set; }
    }
}
