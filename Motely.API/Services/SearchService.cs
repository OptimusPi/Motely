using Motely.API.Models;
using Motely.Filters;
using Motely.Utils;
using System.Collections.Concurrent;
using Motely;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Motely.API.Hubs;

namespace Motely.API.Services;

public class SearchService
{
    private readonly ConcurrentDictionary<string, SearchState> _searches = new();
    private readonly ILogger<SearchService> _logger;
    private readonly IHubContext<SearchHub>? _hubContext;

    public SearchService(ILogger<SearchService> logger, IHubContext<SearchHub>? hubContext = null)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task<string> StartSearchAsync(MotelyJsonConfig config, SearchCriteriaDto? criteria)
    {
        var searchId = Guid.NewGuid().ToString();
        var state = new SearchState
        {
            SearchId = searchId,
            Config = config,
            Status = "running",
            FilterName = config.Name ?? "Unnamed Filter",
            CancellationTokenSource = new CancellationTokenSource()
        };

        _searches[searchId] = state;

        // Start search in background
        _ = Task.Run(async () => await RunSearchAsync(state, criteria ?? new SearchCriteriaDto()));

        return searchId;
    }

    private async Task RunSearchAsync(SearchState state, SearchCriteriaDto criteria)
    {
        try
        {
            _logger.LogInformation("Starting Motely search: {SearchId}", state.SearchId);

            // Validate config
            MotelyJsonConfigValidator.ValidateConfig(state.Config);

            // Create search using JsonSearchExecutor pattern
            var search = CreateSearch(state.Config, criteria, state);

            if (search == null)
            {
                state.Status = "error";
                state.ErrorMessage = "Failed to create search";
                return;
            }

            // Start search
            search.Start();

            // Wait for completion or cancellation
            while (search.Status != MotelySearchStatus.Completed && 
                   search.Status != MotelySearchStatus.Disposed &&
                   !state.CancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(100);
                
                // Update progress from search object
                state.SeedsSearched = search.TotalSeedsSearched;
                state.ResultsFound = (int)search.MatchingSeeds;
            }

            // Cleanup
            if (state.CancellationTokenSource.Token.IsCancellationRequested)
            {
                search.Dispose();
                state.Status = "cancelled";
            }
            else if (search.Status == MotelySearchStatus.Completed)
            {
                state.Status = "completed";
                state.ResultsFound = (int)search.MatchingSeeds;
                state.SeedsSearched = search.TotalSeedsSearched;
            }
            else
            {
                state.Status = "error";
                state.ErrorMessage = "Search ended unexpectedly";
            }

            // Send completion WebSocket update
            if (_hubContext != null)
            {
                await _hubContext.Clients.Group($"search_{state.SearchId}").SendAsync("SearchCompleted", new
                {
                    searchId = state.SearchId,
                    status = state.Status,
                    resultsFound = state.ResultsFound,
                    errorMessage = state.ErrorMessage
                });
            }

            search.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed: {SearchId}", state.SearchId);
            state.Status = "error";
            state.ErrorMessage = ex.Message;
        }
    }

    private IMotelySearch? CreateSearch(MotelyJsonConfig config, SearchCriteriaDto criteria, SearchState state)
    {
        try
        {
            // Initialize parsed enums for all clauses
            var mustClauses = config.Must?.ToList() ?? new List<MotelyJsonConfig.MotleyJsonFilterClause>();
            foreach (var clause in mustClauses)
            {
                clause.InitializeParsedEnums();
            }

            if (config.Should != null)
            {
                foreach (var clause in config.Should)
                {
                    if (!mustClauses.Contains(clause))
                    {
                        clause.InitializeParsedEnums();
                    }
                }
            }

            if (config.MustNot != null)
            {
                foreach (var clause in config.MustNot)
                {
                    clause.InitializeParsedEnums();
                }
            }

            // Post-process config for scoring
            config.PostProcess();

            // Build composite filter from all clauses
            var allRequiredClauses = new List<MotelyJsonConfig.MotleyJsonFilterClause>();
            
            if (config.Must != null)
            {
                allRequiredClauses.AddRange(config.Must);
            }
            
            if (config.MustNot != null)
            {
                foreach (var clause in config.MustNot)
                {
                    clause.IsInverted = true;
                    allRequiredClauses.Add(clause);
                }
            }

            // Create scoring config if needed
            MotelyJsonSeedScoreDesc? scoreDesc = null;
            if (config.Should?.Count > 0)
            {
                var voucherMustClauses = config.Must?.Where(c => c.ItemTypeEnum == MotelyFilterItemType.Voucher).ToList() ?? [];
                var scoringConfig = new MotelyJsonConfig
                {
                    Name = config.Name,
                    Must = voucherMustClauses,
                    Should = config.Should,
                    MustNot = []
                };
                scoringConfig.PostProcess();

                // Create score callback to collect results and send via WebSocket
                Action<MotelySeedScoreTally> scoreCallback = (result) =>
                {
                    if (state.Results == null)
                    {
                        state.Results = new List<SeedResult>();
                    }
                    var seedResult = new SeedResult
                    {
                        Seed = result.Seed,
                        Score = result.Score
                    };
                    state.Results.Add(seedResult);

                    // Send via SignalR for JAML UI
                    _ = Task.Run(async () =>
                    {
                        if (_hubContext != null)
                        {
                            var tallies = result.TallyColumns?.ToArray() ?? Array.Empty<int>();
                            // Build columns array: seed, score, then tally labels if available
                            var columns = new List<string> { "seed", "score" };
                            if (result.TallyColumns != null && result.TallyColumns.Count > 0)
                            {
                                // Use actual tally labels if available, otherwise generic names
                                for (int i = 0; i < result.TallyColumns.Count; i++)
                                {
                                    columns.Add($"tally{i + 1}");
                                }
                            }
                            
                            await _hubContext.Clients.Group($"search_{state.SearchId}").SendAsync("Result", new
                            {
                                seed = result.Seed,
                                score = result.Score,
                                tallies = tallies
                            }, columns.ToArray());
                        }
                    });
                };

                scoreDesc = new MotelyJsonSeedScoreDesc(scoringConfig, criteria.MinScore, ScoreCutoffMode.Manual, scoreCallback);
            }

            var compositeFilter = new MotelyCompositeFilterDesc(allRequiredClauses);
            var compositeSettings = new MotelySearchSettings<MotelyCompositeFilterDesc.MotelyCompositeFilter>(compositeFilter);

            // Apply search settings
            compositeSettings = compositeSettings
                .WithThreadCount(criteria.ThreadCount)
                .WithBatchCharacterCount(criteria.BatchSize)
                .WithStartBatchIndex((long)criteria.StartBatch);

            if (criteria.EndBatch > 0 && criteria.EndBatch < ulong.MaxValue)
            {
                compositeSettings = compositeSettings.WithEndBatchIndex((long)criteria.EndBatch);
            }

            // Apply deck/stake
            if (!string.IsNullOrEmpty(criteria.Deck) && Enum.TryParse<MotelyDeck>(criteria.Deck, true, out var deck))
            {
                compositeSettings = compositeSettings.WithDeck(deck);
            }

            if (!string.IsNullOrEmpty(criteria.Stake) && Enum.TryParse<MotelyStake>(criteria.Stake, true, out var stake))
            {
                compositeSettings = compositeSettings.WithStake(stake);
            }

            // Apply scoring if needed
            if (scoreDesc != null)
            {
                compositeSettings = compositeSettings.WithSeedScoreProvider(scoreDesc);
                compositeSettings = compositeSettings.WithCsvOutput(true);
            }

            // Add progress callback
            compositeSettings = compositeSettings.WithProgressCallback((seedsSearched, resultsFound, totalSeeds, progress) =>
            {
                state.SeedsSearched = seedsSearched;
                state.ResultsFound = (int)resultsFound;
                state.ProgressPercent = progress;

                // Send WebSocket update
                _ = Task.Run(async () =>
                {
                    if (_hubContext != null)
                    {
                        await _hubContext.Clients.Group($"search_{state.SearchId}").SendAsync("ProgressUpdate", new
                        {
                            searchId = state.SearchId,
                            seedsSearched = seedsSearched,
                            resultsFound = resultsFound,
                            progressPercent = progress
                        });
                    }
                });
            });

            return compositeSettings.WithSequentialSearch().Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create search: {Error}", ex.Message);
            return null;
        }
    }

    public SearchStatusResponse? GetSearchStatus(string searchId)
    {
        if (!_searches.TryGetValue(searchId, out var state))
            return null;

        return new SearchStatusResponse
        {
            SearchId = state.SearchId,
            Status = state.Status,
            FilterName = state.FilterName,
            ResultsFound = state.ResultsFound,
            SeedsSearched = state.SeedsSearched,
            ProgressPercent = state.ProgressPercent,
            ErrorMessage = state.ErrorMessage,
            Results = state.Results
        };
    }

    public bool CancelSearch(string searchId)
    {
        if (!_searches.TryGetValue(searchId, out var state))
            return false;

        state.CancellationTokenSource?.Cancel();
        return true;
    }

    public IEnumerable<SearchStatusResponse> ListSearches()
    {
        return _searches.Values.Select(s => new SearchStatusResponse
        {
            SearchId = s.SearchId,
            Status = s.Status,
            FilterName = s.FilterName,
            ResultsFound = s.ResultsFound,
            SeedsSearched = s.SeedsSearched,
            ProgressPercent = s.ProgressPercent
        });
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
