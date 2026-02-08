using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Motely.API.Models;
using Motely.DB;
using Motely.Filters;

namespace Motely.API.Services;

public class SearchQueueHostedService : BackgroundService
{
    private readonly SearchQueueService _queue;
    private readonly SearchService _searchService;
    private readonly ILogger<SearchQueueHostedService> _logger;
    private readonly ConcurrentDictionary<string, Task> _runningTasks = new();
    private readonly int _maxThreads;
    private readonly int _burstReserved = 1;

    public SearchQueueHostedService(
        SearchQueueService queue,
        SearchService searchService,
        ILogger<SearchQueueHostedService> logger,
        int maxThreads = 15
    )
    {
        _queue = queue;
        _searchService = searchService;
        _logger = logger;
        _maxThreads = maxThreads;
        _searchService.SearchCompleted += OnSearchCompleted;
        _searchService.SearchError += OnSearchError;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Cleanup stale entries and resume running on startup
        _queue.CleanupStale(TimeSpan.FromDays(3.14));
        ResumeRunning();

        while (!stoppingToken.IsCancellationRequested)
        {
            // Prune completed tasks and observe faults (no fire-and-forget; we observe when we remove)
            var completed = _runningTasks.Where(kvp => kvp.Value.IsCompleted).ToList();
            foreach (var kvp in completed)
            {
                _runningTasks.TryRemove(kvp.Key, out var t);
                if (t != null && t.IsFaulted)
                    _logger.LogError(t.Exception, "Search {SearchId} failed", kvp.Key);
                _logger.LogDebug("Removed completed task for search {SearchId}", kvp.Key);
            }

            var activeCount = _runningTasks.Count;
            var available = _maxThreads - _burstReserved - activeCount;

            if (available > 0)
            {
                var entries = new List<SearchQueueEntry>();
                for (int i = 0; i < available; i++)
                {
                    var entry = _queue.DequeueNext();
                    if (entry == null)
                        break;
                    entries.Add(entry);
                }

                foreach (var entry in entries)
                {
                    StartSearch(entry, stoppingToken);
                }
            }

            // Check cancellation before delay
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
        }

        _searchService.SearchCompleted -= OnSearchCompleted;
        _searchService.SearchError -= OnSearchError;
    }

    private void ResumeRunning()
    {
        var all = _queue.GetAll();
        var running = all.Where(e => e.Status == "running").ToList();
        foreach (var entry in running)
        {
            _logger.LogInformation(
                "Resuming search {SearchId} from batch {BatchMarker}",
                entry.SearchId,
                entry.BatchMarker
            );
            StartSearch(entry, CancellationToken.None);
        }
    }

    private void StartSearch(SearchQueueEntry entry, CancellationToken ct)
    {
        // Parse JAML filter (assume valid - validated at enqueue time)
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var config = JsonSerializer.Deserialize<MotelyJsonConfig>(entry.JamlFilter, options);

        // Create criteria with batch limit (100 batches max)
        var criteria = new SearchCriteriaDto
        {
            ThreadCount = entry.ThreadCount,
            StartBatch = (ulong)entry.BatchMarker,
            EndBatch = (ulong)Math.Min(entry.BatchMarker + 100, long.MaxValue),
        };

        // Start search; task is stored and observed when we prune completed tasks in ExecuteAsync
        var task = _searchService.RunQueuedSearchAsync(config!, entry, ct);
        _runningTasks.TryAdd(entry.SearchId, task);
        _logger.LogInformation("Started queued search {SearchId}", entry.SearchId);
    }

    private void OnSearchCompleted(string searchId)
    {
        _queue.MarkCompleted(searchId, 0, 0); // TODO: Get actual counts from search state
        _logger.LogInformation("Search {SearchId} completed", searchId);
    }

    private void OnSearchError(string searchId, string error)
    {
        _queue.MarkError(searchId, error);
        _logger.LogError("Search {SearchId} failed: {Error}", searchId, error);
    }

    public void NotifyCompleted(string searchId)
    {
        var removed = _runningTasks.TryRemove(searchId, out _);
        if (removed)
        {
            _logger.LogInformation("Notified completion for search {SearchId}", searchId);
        }
    }
}
