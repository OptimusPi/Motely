using Microsoft.Extensions.Logging;
using Motely.DB;

namespace Motely.API.Services;

/// <summary>
/// Service wrapper around MotelySearchQueueDatabase
/// All SQL and connection management abstracted to Motely.DB
/// </summary>
public class SearchQueueService : IDisposable
{
    private readonly MotelySearchQueueDatabase _database;
    private readonly ILogger<SearchQueueService> _logger;

    /// <summary>Initializes a new search queue service</summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="dbPath">Path to the queue database file</param>
    public SearchQueueService(ILogger<SearchQueueService> logger, string dbPath = "searchqueue.db")
    {
        _logger = logger;
        _database = new MotelySearchQueueDatabase(dbPath);
    }

    /// <summary>Enqueues a search request to the queue</summary>
    /// <param name="searchId">Unique search identifier</param>
    /// <param name="jamlFilter">JAML filter configuration</param>
    /// <param name="threadCount">Number of threads to use</param>
    /// <param name="isBurst">Whether this is a burst-mode search</param>
    public void Enqueue(
        string searchId,
        string jamlFilter,
        int threadCount = 1,
        bool isBurst = false
    )
    {
        _database.Enqueue(searchId, jamlFilter, threadCount, isBurst);
        _logger.LogInformation("Enqueued search {SearchId}", searchId);
    }

    /// <summary>Dequeues the next pending search entry</summary>
    /// <returns>The next search entry, or null if queue is empty</returns>
    public SearchQueueEntry? DequeueNext()
    {
        return _database.DequeueNext();
    }

    /// <summary>Marks a search as currently running</summary>
    /// <param name="searchId">Unique search identifier</param>
    public void MarkRunning(string searchId)
    {
        _database.MarkRunning(searchId);
    }

    /// <summary>Marks a search as completed with results</summary>
    /// <param name="searchId">Unique search identifier</param>
    /// <param name="seedsSearched">Total seeds searched</param>
    /// <param name="resultsFound">Number of matching seeds found</param>
    public void MarkCompleted(string searchId, long seedsSearched, int resultsFound)
    {
        _database.MarkCompleted(searchId, seedsSearched, resultsFound);
    }

    /// <summary>Marks a search as failed with an error</summary>
    /// <param name="searchId">Unique search identifier</param>
    /// <param name="error">Error message</param>
    public void MarkError(string searchId, string error)
    {
        _database.MarkError(searchId, error);
    }

    /// <summary>Updates progress for a running search</summary>
    /// <param name="searchId">Unique search identifier</param>
    /// <param name="seedsSearched">Total seeds searched so far</param>
    /// <param name="resultsFound">Number of matching seeds found so far</param>
    /// <param name="batchMarker">Current batch position</param>
    public void UpdateProgress(
        string searchId,
        long seedsSearched,
        int resultsFound,
        long batchMarker = 0
    )
    {
        _database.UpdateProgress(searchId, seedsSearched, resultsFound, batchMarker);
    }

    /// <summary>Removes stale queue entries older than the specified time</summary>
    /// <param name="olderThan">Time threshold for stale entries</param>
    public void CleanupStale(TimeSpan olderThan)
    {
        _database.CleanupStale(olderThan);
        _logger.LogInformation("Cleaned up stale search queue entries");
    }

    /// <summary>Gets all queue entries</summary>
    /// <returns>List of all search queue entries</returns>
    public List<SearchQueueEntry> GetAll()
    {
        return _database.GetAll();
    }

    /// <summary>Updates a queue entry in the database</summary>
    /// <param name="entry">The entry to update</param>
    public void Update(SearchQueueEntry entry)
    {
        _database.Update(entry);
    }

    /// <summary>Disposes the database connection</summary>
    public void Dispose()
    {
        _database?.Dispose();
    }
}
