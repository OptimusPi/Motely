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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public SearchQueueService(ILogger<SearchQueueService> logger, string dbPath = "searchqueue.db")
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        _logger = logger;
        _database = new MotelySearchQueueDatabase(dbPath);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public void Enqueue(
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        string searchId,
        string jamlFilter,
        int threadCount = 1,
        bool isBurst = false
    )
    {
        _database.Enqueue(searchId, jamlFilter, threadCount, isBurst);
        _logger.LogInformation("Enqueued search {SearchId}", searchId);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public SearchQueueEntry? DequeueNext()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        return _database.DequeueNext();
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public void MarkRunning(string searchId)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        _database.MarkRunning(searchId);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public void MarkCompleted(string searchId, long seedsSearched, int resultsFound)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        _database.MarkCompleted(searchId, seedsSearched, resultsFound);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public void MarkError(string searchId, string error)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        _database.MarkError(searchId, error);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public void UpdateProgress(
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        string searchId,
        long seedsSearched,
        int resultsFound,
        long batchMarker = 0
    )
    {
        _database.UpdateProgress(searchId, seedsSearched, resultsFound, batchMarker);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public void CleanupStale(TimeSpan olderThan)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        _database.CleanupStale(olderThan);
        _logger.LogInformation("Cleaned up stale search queue entries");
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public List<SearchQueueEntry> GetAll()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        return _database.GetAll();
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public void Update(SearchQueueEntry entry)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        _database.Update(entry);
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        _database?.Dispose();
    }
}
