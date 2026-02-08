using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Motely;
using Motely.API.Hubs;
using Motely.API.Models;
using Motely.DB;
using Motely.Executors;
using Motely.Filters;
using Motely.Reporting;
using Motely.Utils;

namespace Motely.API.Services;

/// <summary>
/// Runs JAML seed searches (burst or queued) and notifies the queue and SignalR hub on completion or error.
/// </summary>
public class SearchService
{
    private readonly ConcurrentDictionary<string, SearchState> _searches = new();
    private readonly ILogger<SearchService> _logger;
    private readonly IHubContext<SearchHub> _hubContext;
    private readonly SearchQueueService _queue;

    /// <summary>Raised when a search finishes successfully. Argument is the search ID.</summary>
    public event Action<string>? SearchCompleted;

    /// <summary>Raised when a search fails. Arguments are search ID and error message.</summary>
    public event Action<string, string>? SearchError;

    /// <summary>
    /// Creates a new SearchService.
    /// </summary>
    /// <param name="logger">Logger for search lifecycle.</param>
    /// <param name="queue">Queue service for enqueueing and completion updates.</param>
    /// <param name="hubContext">SignalR hub context for real-time client updates.</param>
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

    /// <summary>
    /// Starts a search: runs immediately for burst (single/seed-sources) mode, or enqueues for batch mode.
    /// </summary>
    /// <param name="config">JAML filter configuration.</param>
    /// <param name="criteria">Seed range, thread count, cutoff, etc.</param>
    /// <returns>The assigned search ID.</returns>
    public async Task<string> StartSearchAsync(MotelyJsonConfig config, SearchCriteriaDto criteria)
    {
        var searchId = Guid.NewGuid().ToString();
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var jamlJson = JsonSerializer.Serialize(config, options);

        // Validate criteria to prevent unlimited writes
        if (criteria == null)
        {
            throw new ArgumentNullException(
                nameof(criteria),
                "Search criteria must be provided to prevent unlimited seed generation."
            );
        }

        // Detect burst mode: single-seed/seedsources sources
        var isBurst =
            criteria.SourceType == SearchSourceType.Single
            || criteria.SourceType == SearchSourceType.SeedSources;

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
        if (state.Config == null)
            throw new InvalidOperationException("Search configuration is missing.");

        _logger.LogInformation("Starting Motely search: {SearchId}", state.SearchId);

        var ct = state.CancellationTokenSource.Token;
        var parameters = new JsonSearchParams
        {
            Threads = criteria.ThreadCount > 0 ? criteria.ThreadCount : Environment.ProcessorCount,
            BatchSize = criteria.BatchSize > 0 ? criteria.BatchSize : 4,
            StartBatch = criteria.StartBatch,
            EndBatch = criteria.EndBatch,
            Cutoff = criteria.MinScore,
            CutoffMode = criteria.MinScore > 0 ? ScoreCutoffMode.Manual : ScoreCutoffMode.None,
            Quiet = true,
            OutputDbPath = state.SearchId,
            CancellationToken = ct,
        };

        using var context = MotelySearchOrchestrator.LaunchWithContext(
            state.Config,
            parameters,
            useInMemoryStorage: false
        );
        context.Start(ct);

        await context.WaitForCompletionAsync(ct).ConfigureAwait(false);

        state.Status = "completed";
        state.ResultsFound = context.ResultCount;
        state.SeedsSearched = context.TotalSeedsSearched;
        state.Results = context
            .GetTopResults(1000)
            .Select(r => new SeedResult { Seed = r.Seed, Score = r.Score })
            .ToList();
        SearchCompleted?.Invoke(state.SearchId);
    }

    /// <summary>
    /// Runs one queued search (called by the queue hosted service). Updates queue and raises events on completion or error.
    /// </summary>
    /// <param name="config">JAML filter configuration.</param>
    /// <param name="entry">Queue entry with search ID, batch marker, thread count.</param>
    /// <param name="ct">Cancellation token.</param>
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

        var state = new SearchState
        {
            SearchId = entry.SearchId,
            Config = config,
            Status = "running",
            FilterName = config.Name ?? "Queued Filter",
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct),
        };
        _searches[entry.SearchId] = state;

        try
        {
            _logger.LogInformation(
                "Starting queued search {SearchId} from batch {BatchMarker}",
                entry.SearchId,
                entry.BatchMarker
            );

            ulong startBatch = (ulong)Math.Max(0, entry.BatchMarker);
            ulong endBatch = startBatch + 100;

            var parameters = new JsonSearchParams
            {
                Threads = entry.ThreadCount > 0 ? entry.ThreadCount : Environment.ProcessorCount,
                BatchSize = 4,
                StartBatch = startBatch,
                EndBatch = endBatch,
                Cutoff = 0,
                CutoffMode = ScoreCutoffMode.None,
                Quiet = true,
                OutputDbPath = entry.SearchId,
                CancellationToken = ct,
            };

            using var context = MotelySearchOrchestrator.LaunchWithContext(
                config,
                parameters,
                useInMemoryStorage: false
            );
            context.Start(ct);

            await context.WaitForCompletionAsync(ct).ConfigureAwait(false);

            entry.BatchMarker = (long)endBatch;
            _queue.Update(entry);
            _queue.MarkCompleted(entry.SearchId, context.TotalSeedsSearched, context.ResultCount);

            state.Status = "completed";
            _logger.LogInformation(
                "Search {SearchId} completed. Seeds: {Seeds}, Results: {Results}",
                entry.SearchId,
                context.TotalSeedsSearched,
                context.ResultCount
            );
            SearchCompleted?.Invoke(entry.SearchId);
        }
        catch (OperationCanceledException)
        {
            state.Status = "cancelled";
            _logger.LogInformation("Search {SearchId} was cancelled.", entry.SearchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during queued search {SearchId}", entry.SearchId);
            state.Status = "error";
            state.ErrorMessage = ex.Message;
            _queue.MarkError(entry.SearchId, ex.Message);
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
