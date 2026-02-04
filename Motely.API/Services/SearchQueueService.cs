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

    public SearchQueueService(ILogger<SearchQueueService> logger, string dbPath = "searchqueue.db")
    {
        _logger = logger;
        _database = new MotelySearchQueueDatabase(dbPath);
    }

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

    public SearchQueueEntry? DequeueNext()
    {
        return _database.DequeueNext();
    }

    public void MarkRunning(string searchId)
    {
        _database.MarkRunning(searchId);
    }

    public void MarkCompleted(string searchId, long seedsSearched, int resultsFound)
    {
        _database.MarkCompleted(searchId, seedsSearched, resultsFound);
    }

    public void MarkError(string searchId, string error)
    {
        _database.MarkError(searchId, error);
    }

    public void UpdateProgress(
        string searchId,
        long seedsSearched,
        int resultsFound,
        long batchMarker = 0
    )
    {
        _database.UpdateProgress(searchId, seedsSearched, resultsFound, batchMarker);
    }

    public void CleanupStale(TimeSpan olderThan)
    {
        _database.CleanupStale(olderThan);
        _logger.LogInformation("Cleaned up stale search queue entries");
    }

    public List<SearchQueueEntry> GetAll()
    {
        return _database.GetAll();
    }

    public void Update(SearchQueueEntry entry)
    {
        _database.Update(entry);
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
