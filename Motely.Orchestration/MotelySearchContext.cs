using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Motely.DuckDB;
using Motely.Filters;

namespace Motely.Executors;

/// <summary>
/// Implementation of IMotelySearchContext that wraps a search instance
/// and its result storage. Provides unified access to search control
/// and result queries.
/// 
/// For Desktop: Uses MotelySearchDatabase (native DuckDB)
/// For Browser: Uses in-memory storage (no native DuckDB available)
/// 
/// Consumers just call methods - Motely handles storage internally.
/// </summary>
public sealed class MotelySearchContext : IMotelySearchContext
{
    private readonly IMotelySearch _search;
    private readonly MotelySearchDatabase? _database;
    private readonly MotelyRunConfig _runConfig;
    private readonly string _searchId;
    private readonly string _filterId;
    
    // In-memory result storage for browser/WASM builds
    private readonly ConcurrentBag<MotelySearchResultRow>? _inMemoryResults;
    private readonly bool _useInMemoryStorage;
    
    /// <summary>
    /// Create a search context with database storage (Desktop/native)
    /// </summary>
    public MotelySearchContext(
        IMotelySearch search,
        MotelySearchDatabase database,
        MotelyRunConfig runConfig,
        string searchId,
        string filterId)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _runConfig = runConfig ?? throw new ArgumentNullException(nameof(runConfig));
        _searchId = searchId ?? throw new ArgumentNullException(nameof(searchId));
        _filterId = filterId ?? throw new ArgumentNullException(nameof(filterId));
        _useInMemoryStorage = false;
        _inMemoryResults = null;
    }
    
    /// <summary>
    /// Create a search context with in-memory storage (Browser/WASM)
    /// </summary>
    public MotelySearchContext(
        IMotelySearch search,
        MotelyRunConfig runConfig,
        string searchId,
        string filterId)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _database = null;
        _runConfig = runConfig ?? throw new ArgumentNullException(nameof(runConfig));
        _searchId = searchId ?? throw new ArgumentNullException(nameof(searchId));
        _filterId = filterId ?? throw new ArgumentNullException(nameof(filterId));
        _useInMemoryStorage = true;
        _inMemoryResults = new ConcurrentBag<MotelySearchResultRow>();
    }
    
    // === IMotelySearchContext implementation ===
    
    public string SearchId => _searchId;
    
    public string FilterId => _filterId;
    
    public int ResultCount
    {
        get
        {
            if (_useInMemoryStorage)
                return _inMemoryResults?.Count ?? 0;
            
            return _database?.GetResultCount() ?? 0;
        }
    }
    
    public IReadOnlyList<string> ColumnNames
    {
        get
        {
            var columns = new List<string> { "seed", "score" };
            columns.AddRange(_runConfig.Columns.Select(c => c.Name));
            return columns;
        }
    }
    
    public List<MotelySearchResultRow> GetResults(int offset, int limit)
    {
        if (_useInMemoryStorage)
        {
            // In-memory: return slice of results
            return _inMemoryResults?
                .OrderByDescending(r => r.Score)
                .Skip(offset)
                .Take(limit)
                .ToList() ?? new List<MotelySearchResultRow>();
        }
        
        // Database: query via MotelySearchDatabase
        if (_database == null)
            return new List<MotelySearchResultRow>();
            
        var rows = _database.GetResultsPage(offset, limit);
        return rows.Select(r => new MotelySearchResultRow
        {
            Seed = r.TryGetValue("seed", out var s) ? s?.ToString() ?? "" : "",
            Score = r.TryGetValue("score", out var sc) ? Convert.ToInt32(sc) : 0,
            Tallies = ExtractTallies(r)
        }).ToList();
    }
    
    public List<MotelySearchResultRow> GetTopResults(int limit = 1000)
    {
        return GetResults(0, limit);
    }
    
    /// <summary>
    /// Add a result to in-memory storage (used by browser builds)
    /// </summary>
    internal void AddResult(string seed, int score, List<int>? tallies)
    {
        if (_useInMemoryStorage && _inMemoryResults != null)
        {
            _inMemoryResults.Add(new MotelySearchResultRow
            {
                Seed = seed,
                Score = score,
                Tallies = tallies
            });
        }
    }
    
    private List<int>? ExtractTallies(Dictionary<string, object?> row)
    {
        var tallies = new List<int>();
        foreach (var col in _runConfig.Columns)
        {
            if (col.Type == Reporting.ColumnType.ScoreTally)
            {
                if (row.TryGetValue(col.Name, out var val) && val != null)
                    tallies.Add(Convert.ToInt32(val));
                else
                    tallies.Add(0);
            }
        }
        return tallies.Count > 0 ? tallies : null;
    }
    
    // === IMotelySearch delegation ===
    
    public MotelySearchStatus Status => _search.Status;
    public long BatchIndex => _search.BatchIndex;
    public long CompletedBatchCount => _search.CompletedBatchCount;
    public TimeSpan ElapsedTime => _search.ElapsedTime;
    public long TotalSeedsSearched => _search.TotalSeedsSearched;
    public long MatchingSeeds => _search.MatchingSeeds;
    public long FilteredSeeds => _search.FilteredSeeds;
    
    public void Start(CancellationToken cancellationToken = default) => _search.Start(cancellationToken);
    public void AwaitCompletion() => _search.AwaitCompletion();
    public Task WaitForCompletionAsync(CancellationToken cancellationToken = default) 
        => _search.WaitForCompletionAsync(cancellationToken);
    public void Pause() => _search.Pause();
    public void Cancel() => _search.Cancel();
    public void ForceProgressReport() => _search.ForceProgressReport();
    
    public void Dispose()
    {
        _search.Dispose();
        _database?.Dispose();
    }
}
