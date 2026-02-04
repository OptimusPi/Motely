using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Motely;
using Motely.Analysis;
using Motely.API.Hubs;
using Motely.API.Models;
using Motely.API.Services;
using Motely.Executors;

namespace Motely.API;

/// <summary>
/// Modular endpoint registration for different deployment scenarios.
/// Allows enabling/disabling endpoints independently for testing and deployment.
/// </summary>
public static class EndpointRegistration
{
    /// <summary>
    /// Register core API endpoints (filters, analyze, health, etc.)
    /// </summary>
    public static IEndpointRouteBuilder MapCoreApiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Health and info endpoints
        endpoints.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });
        endpoints.MapGet(
            "/routes",
            () =>
                new
                {
                    homepage = "/",
                    health = "/health",
                    routes = "/routes",
                    analyze = "/analyze?seed=SEED[&deck=Red][&stake=White]",
                    filters = "/filters",
                    seed_sources = "/seed-sources",
                }
        );

        // Analyze endpoint (quick seed analyzer)
        endpoints.MapGet(
            "/analyze",
            (string seed, string? deck = "Red", string? stake = "White") =>
            {
                if (string.IsNullOrWhiteSpace(seed))
                    return Results.BadRequest(
                        new { error = "Missing required query parameter: seed" }
                    );

                if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
                    return Results.BadRequest(new { error = $"Invalid deck: {deck}" });

                if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                    return Results.BadRequest(new { error = $"Invalid stake: {stake}" });

                try
                {
                    var analysis = MotelySeedAnalyzer.Analyze(
                        new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum)
                    );
                    return Results.Text(analysis.ToString(), "text/plain; charset=utf-8");
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }
        );

        // Filter endpoints
        endpoints.MapGet("/filters", Endpoints.GetFilters);
        endpoints.MapPost(
            "/filters/update",
            async (FilterSaveRequest request) =>
            {
                if (request?.FilterJaml == null)
                    return Results.BadRequest(new { error = "Missing filterJaml in request body" });

                var jamlFiltersDir = MotelyPaths.JamlFiltersDir;
                Directory.CreateDirectory(jamlFiltersDir);

                string? name = null;
                if (
                    JamlConfigLoader.TryLoadFromJamlString(request.FilterJaml, out var cfg, out _)
                    && cfg != null
                )
                {
                    name = cfg.Name;
                }

                var fileName = $"{(name ?? "filter")}.jaml";
                var fullPath = Path.Combine(jamlFiltersDir, fileName);
                await File.WriteAllTextAsync(fullPath, request.FilterJaml);

                return Results.Ok(new { filePath = fileName });
            }
        );
        endpoints.MapDelete("/filters/{id}", Endpoints.DeleteFilter);

        // Seed sources endpoint
        endpoints.MapGet("/seed-sources", Endpoints.GetSeedSources);

        return endpoints;
    }

    /// <summary>
    /// Register search queue endpoints (for multiplayer seed searches)
    /// </summary>
    public static IEndpointRouteBuilder MapSearchQueueEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        // Searches endpoint
        endpoints.MapGet("/searches", Endpoints.GetSearches);

        // Search endpoints
        endpoints.MapPost(
            "/search",
            async (SearchStartRequest request) =>
            {
                try
                {
                    if (request == null)
                        return Results.BadRequest(new { error = "Missing request body" });

                    // Thread count is independent of SeedCount; cap at processor count to avoid allocating millions of threads.
                    var threads = Math.Min(Environment.ProcessorCount, 64);
                    if (threads < 1)
                        threads = 1;

                    var filterJaml = FilterService.GetFilterJaml(request.FilterId);
                    if (string.IsNullOrEmpty(filterJaml))
                        return Results.BadRequest(new { error = "Filter not found" });

                    var (immediateResults, searchId) =
                        await MultiSearchManager.Instance.StartSearchAsync(
                            filterJaml,
                            request.Deck,
                            request.Stake,
                            threads,
                            request.SeedCount,
                            request.StartBatch,
                            request.Cutoff,
                            request.SeedSource
                        );

                    return Results.Ok(
                        new
                        {
                            searchId = searchId,
                            status = "running",
                            columns = MultiSearchManager.Instance.GetColumnNames(searchId),
                        }
                    );
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }
        );

        endpoints.MapGet(
            "/search/{id}",
            (string id) =>
            {
                try
                {
                    var (results, progressPercent) = MultiSearchManager.Instance.GetSearchStatus(
                        id
                    );
                    var isRunning = MultiSearchManager.Instance.IsSearchRunning(id);

                    return Results.Ok(
                        new
                        {
                            searchId = id,
                            status = isRunning ? "running" : "stopped",
                            results = results,
                            progressPercent = progressPercent,
                            columns = MultiSearchManager.Instance.GetColumnNames(id),
                        }
                    );
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }
        );

        endpoints.MapPost(
            "/search/stop",
            async (SearchStopRequest? request) =>
            {
                try
                {
                    var results = await MultiSearchManager.Instance.StopSearchAsync(
                        request?.SearchId ?? ""
                    );
                    return Results.Ok(
                        new
                        {
                            message = "Search stopped",
                            results = results,
                            isBackgroundRunning = false,
                        }
                    );
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            }
        );

        return endpoints;
    }
}
