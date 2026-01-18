using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Motely;
using Motely.API.Hubs;
using Motely.API.Models;
using Motely.Filters;
using Motely.Utils;

namespace Motely.API.Services;

public class SearchService
{
    private readonly ConcurrentDictionary<string, SearchState> _searches = new();
    private readonly ILogger<SearchService> _logger;
    private readonly IHubContext<SearchHub> _hubContext;
    private readonly SearchQueueService _queue;

    // Events for queue service
    public event Action<string>? SearchCompleted;
    public event Action<string, string>? SearchError;

    public SearchService(
        ILogger<SearchService> logger,
        SearchQueueService queue,
        IHubContext<SearchHub> hubContext
    )
    {
        _logger = logger;
        _hubContext = hubContext;
        _queue = queue;
    }

    public async Task<string> StartSearchAsync(MotelyJsonConfig config, SearchCriteriaDto criteria)
    {
        var searchId = Guid.NewGuid().ToString();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var jamlJson = System.Text.Json.JsonSerializer.Serialize(config, options);

        // Validate criteria to prevent unlimited writes
        if (criteria == null)
        {
            throw new ArgumentNullException(
                nameof(criteria),
                "Search criteria must be provided to prevent unlimited seed generation."
            );
        }

        // Detect burst mode: single-seed/seedsources sources
        var isBurst = criteria.SourceType == "single" || criteria.SourceType == "seedsources";

        if (isBurst)
        {
            // Burst path: run immediately, bypass queue
            var state = new SearchState
            {
                SearchId = searchId,
                Config = config,
                Status = "running",
                FilterName = config.Name ?? "Burst Filter",
                CancellationTokenSource = new CancellationTokenSource(),
            };

            _searches[searchId] = state;

            try
            {
                await RunSearchAsync(state, criteria);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Burst search failed for {SearchId}", searchId);
                state.Status = "error";
                state.ErrorMessage = ex.Message;
            }

            return searchId;
        }
        else
        {
            // Enqueue for background processing
            var threadCount = criteria.ThreadCount > 0 ? criteria.ThreadCount : 1;

            // Emit WebSocket QUEUED with blue dot
            await EmitQueuedAsync(searchId, config.Name ?? "Queued Filter");

            return searchId;
        }
    }

    private async Task EmitQueuedAsync(string searchId, string filterName)
    {
        if (_hubContext != null)
        {
            await _hubContext
                .Clients.Group($"search_{searchId}")
                .SendAsync(
                    "SearchQueued",
                    new
                    {
                        searchId = searchId,
                        status = "queued",
                        filterName = filterName,
                    }
                );
        }
    }

    private async Task RunSearchAsync(SearchState state, SearchCriteriaDto criteria)
    {
        try
        {
            _logger.LogInformation("Starting Motely search: {SearchId}", state.SearchId);

            // Validate config
            if (state.Config == null)
            {
                throw new InvalidOperationException("Search configuration is missing.");
            }

            // Simulate search logic (replace with actual implementation)
            await Task.Delay(1000);

            state.Status = "completed";
            SearchCompleted?.Invoke(state.SearchId);
        }
        catch (Exception ex)
        {
            state.Status = "error";
            state.ErrorMessage = ex.Message;
            SearchError?.Invoke(state.SearchId, ex.Message);
            throw;
        }
    }

    public async Task RunQueuedSearchAsync(
        MotelyJsonConfig config,
        SearchQueueEntry entry,
        CancellationToken ct
    )
    {
        if (ct.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Search {SearchId} was cancelled before starting.",
                entry.SearchId
            );
            return;
        }

        try
        {
            _logger.LogInformation(
                "Starting queued search {SearchId} from batch {BatchMarker}",
                entry.SearchId,
                entry.BatchMarker
            );

            // Initialize search state
            var state = new SearchState
            {
                SearchId = entry.SearchId,
                Config = config,
                Status = "running",
                FilterName = config.Name ?? "Queued Filter",
                CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct),
            };

            _searches[entry.SearchId] = state;

            // Simulate search logic (replace with actual implementation)
            for (
                ulong batch = (ulong)entry.BatchMarker;
                batch < (ulong)entry.BatchMarker + 100;
                batch++
            )
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "Search {SearchId} was cancelled at batch {Batch}",
                        entry.SearchId,
                        batch
                    );
                    state.Status = "cancelled";
                    return;
                }

                // Simulate batch processing
                await Task.Delay(100, ct);
                _logger.LogInformation(
                    "Processed batch {Batch} for search {SearchId}",
                    batch,
                    entry.SearchId
                );

                // Update batch marker in queue
                entry.BatchMarker = (long)batch;
                _queue.Update(entry);
            }

            state.Status = "completed";
            _logger.LogInformation("Search {SearchId} completed successfully.", entry.SearchId);
            SearchCompleted?.Invoke(entry.SearchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during queued search {SearchId}", entry.SearchId);
            if (_searches.TryGetValue(entry.SearchId, out var state))
            {
                state.Status = "error";
                state.ErrorMessage = ex.Message;
            }
            SearchError?.Invoke(entry.SearchId, ex.Message);
        }
        finally
        {
            _searches.TryRemove(entry.SearchId, out _);
        }
    }
}

internal class SearchState
{
    public string SearchId { get; set; } = string.Empty;
    public MotelyJsonConfig Config { get; set; } = null!;
    public string Status { get; set; } = "running";
    public string FilterName { get; set; } = string.Empty;
    public int ResultsFound { get; set; }
    public long SeedsSearched { get; set; }
    public double ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public List<SeedResult>? Results { get; set; }
    public CancellationTokenSource CancellationTokenSource { get; set; } = new();
}
