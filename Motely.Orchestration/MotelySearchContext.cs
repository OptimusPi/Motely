using System;
using System.Collections.Generic;
using System.Linq;
using Motely;
using Motely.Filters;
using Motely.Reporting;

namespace Motely.Executors;

/// <summary>
/// Wraps a search instance and its result sink.
/// </summary>
public sealed class MotelySearchContext : IMotelySearchContext
{
    private readonly IMotelySearch _search;
    private readonly IResultStorage? _sink;
    private readonly MotelyRunConfig _runConfig;
    private readonly string _searchId;
    private readonly string _filterId;
    private readonly bool _useInMemoryStorage;
    
    private readonly List<MotelySearchResultRow>? _inMemoryResults;
    private readonly object _inMemoryResultsLock = new object();
    
    /// <summary>
    /// Create a search context with a result sink.
    /// </summary>
    public MotelySearchContext(
        IMotelySearch search,
        IResultStorage sink,
        MotelyRunConfig runConfig,
        string searchId,
        string filterId)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _runConfig = runConfig ?? throw new ArgumentNullException(nameof(runConfig));
        _searchId = searchId ?? throw new ArgumentNullException(nameof(searchId));
        _filterId = filterId ?? throw new ArgumentNullException(nameof(filterId));
        _useInMemoryStorage = false;
    }

    /// <summary>
    /// Create a search context with in-memory storage.
    /// </summary>
    public MotelySearchContext(
        IMotelySearch search,
        MotelyRunConfig runConfig,
        string searchId,
        string filterId)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _sink = null;
        _runConfig = runConfig ?? throw new ArgumentNullException(nameof(runConfig));
        _searchId = searchId ?? throw new ArgumentNullException(nameof(searchId));
        _filterId = filterId ?? throw new ArgumentNullException(nameof(filterId));
        _useInMemoryStorage = true;
        _inMemoryResults = new List<MotelySearchResultRow>();
    }
    
    /// <summary>
    /// Store a result in in-memory storage (browser/WASM mode).
    /// Called from the result callback during search execution.
    /// </summary>
    internal void StoreResult(MotelySeedScoreTally tally)
    {
        if (!_useInMemoryStorage || _inMemoryResults == null)
            return;
            
        var tallies = ExtractTalliesFromTally(tally);
        var row = new MotelySearchResultRow
        {
            Seed = tally.Seed,
            Score = tally.Score,
            Tallies = tallies
        };
        
        lock (_inMemoryResultsLock)
        {
            // Insert in sorted order (score descending) for efficient top-K queries
            int insertIndex = 0;
            for (int i = 0; i < _inMemoryResults.Count; i++)
            {
                if (_inMemoryResults[i].Score < row.Score)
                {
                    insertIndex = i;
                    break;
                }
                insertIndex = i + 1;
            }
            _inMemoryResults.Insert(insertIndex, row);
        }
    }
    
    private List<int>? ExtractTalliesFromTally(MotelySeedScoreTally tally)
    {
        if (tally.TallyValuesSpan.IsEmpty)
            return null;
            
        var tallies = new List<int>();
        foreach (var val in tally.TallyValuesSpan)
        {
            tallies.Add(val);
        }
        return tallies.Count > 0 ? tallies : null;
    }
    
    // === IMotelySearchContext implementation ===
    
    public string SearchId => _searchId;
    
    public string FilterId => _filterId;
    
    public int ResultCount
    {
        get
        {
            if (_useInMemoryStorage)
            {
                // Browser/WASM: Return actual stored result count
                if (_inMemoryResults == null)
                    return 0;
                lock (_inMemoryResultsLock)
                {
                    return _inMemoryResults.Count;
                }
            }
            return _sink?.GetResultCount() ?? 0;
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
            // Browser/WASM: Query in-memory storage
            if (_inMemoryResults == null)
                return new List<MotelySearchResultRow>();
                
            lock (_inMemoryResultsLock)
            {
                // Results are already sorted by score descending
                int count = Math.Min(limit, _inMemoryResults.Count - offset);
                if (count <= 0)
                    return new List<MotelySearchResultRow>();
                    
                var result = new List<MotelySearchResultRow>(count);
                for (int i = offset; i < offset + count; i++)
                {
                    result.Add(_inMemoryResults[i]);
                }
                return result;
            }
        }

        // Non-browser: query via IResultStorage
        if (_sink == null)
            return new List<MotelySearchResultRow>();

        var rows = _sink.GetResultsPage(offset, limit);
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
    public bool IsSequentialBatchSearch => _search.IsSequentialBatchSearch;
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
        _sink?.Dispose();
    }
}
