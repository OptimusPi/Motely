using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Motely;
using Motely.DB;
using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Manages multiple concurrent Motely searches with thread allocation.
/// Thin wrapper around IMotelySearchContext - doesn't duplicate state.
/// </summary>
public sealed class MultiSearchManager
{
    private static readonly Lazy<MultiSearchManager> _instance = new(() => new MultiSearchManager());
    public static MultiSearchManager Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, ActiveSearch> _activeSearches = new();
    private int _totalThreads;
    private int _allocatedThreads;

    private MultiSearchManager()
    {
        _totalThreads = Environment.ProcessorCount;
    }

    #region Thread Management

    public void SetTotalThreads(int count) => _totalThreads = Math.Max(1, count);
    public int TotalThreads => _totalThreads;
    public int AllocatedThreads => _allocatedThreads;
    public int AvailableThreads => Math.Max(0, _totalThreads - _allocatedThreads);

    #endregion

    #region Launch/Stop

    /// <summary>
    /// Launch a new search.
    /// </summary>
    /// <param name="requestStartBatch">If set, overrides resume-from-meta; API/CLI explicit start position.</param>
    /// <param name="requestEndBatch">If set, end batch (e.g. from API/CLI).</param>
    /// <param name="requestCutoff">If set, result cutoff (e.g. from API request).</param>
    public ActiveSearch? Launch(
        MotelyJsonConfig config,
        string searchId,
        int threadCount,
        string? seedSource = null,
        bool isSequential = true,
        ulong? requestStartBatch = null,
        ulong? requestEndBatch = null,
        int? requestCutoff = null)
    {
        if (string.IsNullOrWhiteSpace(searchId))
            throw new ArgumentException("searchId is required", nameof(searchId));

        // Check if already running
        if (_activeSearches.TryGetValue(searchId, out var existing) && existing.IsRunning)
            return existing;

        // Check thread budget
        if (Interlocked.Add(ref _allocatedThreads, threadCount) > _totalThreads)
        {
            Interlocked.Add(ref _allocatedThreads, -threadCount);
            return null;
        }

        var parameters = new JsonSearchParams
        {
            Threads = threadCount,
            BatchSize = 3,
            AutoSave = true,
            SeedSources = seedSource,
        };

        if (requestCutoff.HasValue)
            parameters.Cutoff = requestCutoff.Value;
        if (requestEndBatch.HasValue)
            parameters.EndBatch = requestEndBatch.Value;

        // Explicit start from request takes priority; else resume from last seed if sequential
        if (requestStartBatch.HasValue)
            parameters.StartBatch = requestStartBatch.Value;
        else if (isSequential)
        {
            try
            {
                var meta = SequentialLibrary.Instance.GetSearchMeta(searchId);
                if (meta?.LastSeed != null)
                    parameters.StartBatch = (ulong)SeedMath.SeedToBatchIndex(meta.LastSeed, parameters.BatchSize) + 1;
            }
            catch { /* Library not initialized */ }
        }

        var context = MotelySearchOrchestrator.LaunchWithContext(config, parameters, useInMemoryStorage: false);

        var activeSearch = new ActiveSearch(searchId, config, context, threadCount, isSequential, seedSource);
        _activeSearches[searchId] = activeSearch;

        // Mark active in DB
        if (isSequential)
        {
            try
            {
                SequentialLibrary.Instance.UpsertSearchMeta(new SearchMeta
                {
                    SearchId = searchId,
                    TableName = searchId,
                    JamlFilter = config.Name,
                    Deck = config.Deck,
                    Stake = config.Stake,
                    SeedSource = seedSource,
                    IsActive = true,
                    LastAccessed = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            catch { /* Library not initialized */ }
        }

        return activeSearch;
    }

    /// <summary>
    /// Stop a search and persist its position.
    /// </summary>
    public void Stop(string searchId, string reason = "User stopped")
    {
        if (!_activeSearches.TryGetValue(searchId, out var search))
            return;

        search.Context?.Cancel();

        // Persist last position
        if (search.IsSequential && search.Context != null)
        {
            try
            {
                var prefix = SeedMath.BatchIndexToSeedPrefix(search.Context.BatchIndex, 3);
                var lastSeed = prefix.PadRight(8, '1');
                SequentialLibrary.Instance.UpdateLastSeed(
                    searchId, lastSeed,
                    search.Context.TotalSeedsSearched,
                    search.Context.MatchingSeeds);
                SequentialLibrary.Instance.SetSearchActive(searchId, false);
            }
            catch { /* Library not initialized */ }
        }

        Interlocked.Add(ref _allocatedThreads, -search.AllocatedThreads);
    }

    /// <summary>
    /// Stop all searches.
    /// </summary>
    public void StopAll(string reason = "Shutdown")
    {
        foreach (var id in _activeSearches.Keys.ToList())
            Stop(id, reason);
    }

    #endregion

    #region Status (read directly from Context)

    public SearchStatus? GetStatus(string searchId)
    {
        return _activeSearches.TryGetValue(searchId, out var search) ? search.GetStatus() : null;
    }

    public List<SearchStatus> GetAllStatuses()
    {
        return _activeSearches.Values.Select(s => s.GetStatus()).ToList();
    }

    public List<string> GetActiveSearchIds()
    {
        return _activeSearches.Values.Where(s => s.IsRunning).Select(s => s.SearchId).ToList();
    }

    public bool IsSearchRunning(string searchId)
    {
        return _activeSearches.TryGetValue(searchId, out var search) && search.IsRunning;
    }

    #endregion

    #region Restore

    public Task<List<string>> RestoreActiveSearchesAsync()
    {
        try
        {
            return Task.FromResult(SequentialLibrary.Instance.GetAllActiveSearchIds());
        }
        catch
        {
            return Task.FromResult(new List<string>());
        }
    }

    public SearchMeta? GetPersistedMeta(string searchId)
    {
        try { return SequentialLibrary.Instance.GetSearchMeta(searchId); }
        catch { return null; }
    }

    #endregion

    #region API-Friendly Methods

    public Task<(List<MotelySearchResultRow> results, string searchId)> StartSearchAsync(
        string filterJaml, MotelyDeck deck, MotelyStake stake, int threads = 1)
    {
        return StartSearchAsync(filterJaml, deck, stake, threads, seedCount: null, startBatch: null, cutoff: null, seedSource: null);
    }

    /// <summary>
    /// Start a search (deck/stake as strings; parses to enum and calls enum overload).
    /// </summary>
    public Task<(List<MotelySearchResultRow> results, string searchId)> StartSearchAsync(
        string filterJaml,
        string deck,
        string stake,
        int threads = 1,
        long? seedCount = null,
        long? startBatch = null,
        int? cutoff = null,
        string? seedSource = null)
    {
        var deckEnum = Enum.TryParse<MotelyDeck>(deck, true, out var d) ? d : MotelyDeck.Red;
        var stakeEnum = Enum.TryParse<MotelyStake>(stake, true, out var s) ? s : MotelyStake.White;
        return StartSearchAsync(filterJaml, deckEnum, stakeEnum, threads, seedCount, startBatch, cutoff, seedSource);
    }

    /// <summary>
    /// Start a search with optional request parameters (start position, cutoff, seed source).
    /// Used by API so clients can specify StartBatch, Cutoff, SeedSource and SeedCount.
    /// </summary>
    public Task<(List<MotelySearchResultRow> results, string searchId)> StartSearchAsync(
        string filterJaml,
        MotelyDeck deck,
        MotelyStake stake,
        int threads = 1,
        long? seedCount = null,
        long? startBatch = null,
        int? cutoff = null,
        string? seedSource = null)
    {
        if (!JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var config, out var error) || config == null)
            throw new InvalidOperationException($"Failed to parse filter: {error}");

        config.Deck = deck.ToString();
        config.Stake = stake.ToString();

        var searchId = GenerateSearchId(config);
        var startBatchVal = startBatch.HasValue ? (ulong)Math.Max(0, startBatch.Value) : (ulong?)null;
        var search = Launch(
            config,
            searchId,
            threads,
            seedSource,
            isSequential: true,
            requestStartBatch: startBatchVal,
            requestEndBatch: null,
            requestCutoff: cutoff);

        if (search == null)
            throw new InvalidOperationException("Not enough threads available");

        return Task.FromResult((new List<MotelySearchResultRow>(), searchId));
    }

    public (List<MotelySearchResultRow> results, int progressPercent) GetSearchStatusWithResults(string searchId)
    {
        if (!_activeSearches.TryGetValue(searchId, out var search))
            return (new List<MotelySearchResultRow>(), 0);

        var results = search.Context?.GetResults(0, 100) ?? new List<MotelySearchResultRow>();
        var progressPercent = search.GetProgressPercent();
        return (results, progressPercent);
    }

    /// <summary>Alias for API: returns (results, progressPercent).</summary>
    public (List<MotelySearchResultRow> results, int progressPercent) GetSearchStatus(string searchId) =>
        GetSearchStatusWithResults(searchId);

    /// <summary>Stop a search and return current results (for API).</summary>
    public Task<List<MotelySearchResultRow>> StopSearchAsync(string searchId)
    {
        var (results, _) = GetSearchStatusWithResults(searchId);
        Stop(searchId);
        return Task.FromResult(results);
    }

    public List<string> GetColumnNames(string searchId)
    {
        if (_activeSearches.TryGetValue(searchId, out var search) && search.Context != null)
            return search.Context.ColumnNames.ToList();
        return new List<string> { "seed", "score" };
    }

    public static string GenerateSearchId(MotelyJsonConfig config)
    {
        return $"{MotelySearchOrchestrator.GenerateFilterId(config)}_{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    public static string SanitizeFilterFileStem(string name) => MotelySearchOrchestrator.SanitizeForId(name);

    #endregion
}

/// <summary>
/// Active search - thin wrapper around IMotelySearchContext.
/// </summary>
public sealed class ActiveSearch
{
    public string SearchId { get; }
    public MotelyJsonConfig Config { get; }
    public IMotelySearchContext? Context { get; }
    public int AllocatedThreads { get; }
    public bool IsSequential { get; }
    public string? SeedSource { get; }
    public DateTime StartedAt { get; }

    public ActiveSearch(string searchId, MotelyJsonConfig config, IMotelySearchContext? context,
        int allocatedThreads, bool isSequential, string? seedSource)
    {
        SearchId = searchId;
        Config = config;
        Context = context;
        AllocatedThreads = allocatedThreads;
        IsSequential = isSequential;
        SeedSource = seedSource;
        StartedAt = DateTime.UtcNow;
    }

    public bool IsRunning => Context?.Status == MotelySearchStatus.Running;

    public SearchStatus GetStatus()
    {
        var ctx = Context;
        return new SearchStatus
        {
            SearchId = SearchId,
            FilterId = MotelySearchOrchestrator.GenerateFilterId(Config),
            FilterName = Config.Name ?? "Unknown",
            Deck = Config.Deck ?? "Red",
            Stake = Config.Stake ?? "White",
            IsRunning = IsRunning,
            AllocatedThreads = AllocatedThreads,
            SeedsSearched = ctx?.TotalSeedsSearched ?? 0,
            TotalMatches = ctx?.MatchingSeeds ?? 0,
            SeedsPerSecond = ctx != null && ctx.ElapsedTime.TotalSeconds > 0
                ? ctx.TotalSeedsSearched / ctx.ElapsedTime.TotalSeconds : 0,
            StartedAt = StartedAt,
        };
    }

    public int GetProgressPercent()
    {
        var ctx = Context;
        if (ctx == null || !ctx.IsSequentialBatchSearch || ctx.CompletedBatchCount == 0)
            return 0;
        var totalBatches = (long)Math.Pow(35, 5); // 35^(8-3) for batchSize=3
        return (int)Math.Min(100, (ctx.CompletedBatchCount * 100) / totalBatches);
    }
}

public sealed class SearchStatus
{
    public string SearchId { get; set; } = "";
    public string FilterId { get; set; } = "";
    public string FilterName { get; set; } = "";
    public string Deck { get; set; } = "";
    public string Stake { get; set; } = "";
    public bool IsRunning { get; set; }
    public int AllocatedThreads { get; set; }
    public long SeedsSearched { get; set; }
    public long TotalMatches { get; set; }
    public double SeedsPerSecond { get; set; }
    public DateTime StartedAt { get; set; }
}

public sealed class SearchProgress
{
    public long SeedsSearched { get; set; }
    public double SeedsPerSecond { get; set; }
    public long TotalMatches { get; set; }
}

public enum SearchCompletionReason { Completed, Stopped, Error }
