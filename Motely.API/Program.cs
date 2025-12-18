using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Motely.API;

/// <summary>
/// Factory for creating and configuring the Motely WebApplication API.
/// </summary>
public static class MotelyApiFactory
{
    /// <summary>
    /// Creates and configures a new Motely WebApplication instance.
    /// </summary>
    /// <param name="args">Command line arguments (optional)</param>
    /// <returns>Configured WebApplication instance</returns>
    public static WebApplication CreateApi(string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? new string[0]);
        
        // Configure WebRootPath to find wwwroot correctly
        builder.Environment.WebRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
        
        // Configure logging to redirect to console (which will be captured by TUI)
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Logging.AddSimpleConsole(options =>
        {
            options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled; // Disable ANSI color codes
            options.SingleLine = true; // Make logs more compact
        });
        
        // Add CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        var app = builder.Build();
        
        // Configure middleware
        app.UseCors("AllowAll");
        app.UseStaticFiles(); // Static files first
        app.UseRouting();
        
        // Health check
        app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

        // Close endpoint
        app.MapPost("/close", () => 
        {
            return Results.Ok(new { message = "Server shutting down..." });
        });

        // Search endpoints - use existing SearchManager
        app.MapPost("/search", async (HttpRequest request) => 
        {
            try
            {
                var req = await request.ReadFromJsonAsync<SearchStartRequest>();
                if (req == null)
                    return Results.BadRequest(new { error = "Missing request body" });

                var filterJaml = req.FilterJaml ?? string.Empty;
                var seedCount = req.SeedCount;
                var seedCountInt = seedCount.HasValue
                    ? (int)Math.Min(seedCount.Value, int.MaxValue)
                    : 0;
                
                (List<SearchResult> immediateResults, string searchId) = await SearchManager.Instance.StartSearchAsync(
                    filterJaml,
                    deck: "RedDeck",
                    stake: "White",
                    seedCount: seedCountInt);
                
                return Results.Ok(new { 
                    searchId = searchId, 
                    status = "running",
                    results = immediateResults,
                    columns = new[] { "seed", "score" },
                    isBackgroundRunning = true,
                    progressPercent = 0
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/search", (string id) => 
        {
            try
            {
                var (results, progressPercent) = SearchManager.Instance.GetSearchStatus(id);
                var isRunning = SearchManager.Instance.IsSearchRunning(id);
                return Results.Ok(new { 
                    searchId = id, 
                    status = isRunning ? "running" : "stopped",
                    searchStatus = isRunning ? "running" : "stopped",
                    progressPercent = progressPercent,
                    results = results,
                    columns = new[] { "seed", "score" },
                    isBackgroundRunning = isRunning,
                    seedsFound = results?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/search/stop", async (HttpRequest request) => 
        {
            try
            {
                var req = await request.ReadFromJsonAsync<SearchStopRequest>();
                var searchId = req?.SearchId ?? "";
                
                var results = SearchManager.Instance.StopSearch(searchId);
                return Results.Ok(new { 
                    message = "Search stopped",
                    results = results,
                    isBackgroundRunning = false,
                    progressPercent = 100
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Filters endpoint - load actual filter files
        app.MapGet("/filters", () => 
        {
            var filtersPath = Path.Combine(builder.Environment.ContentRootPath, "..", "JsonFilters");
            var filters = new List<object>();
            
            if (Directory.Exists(filtersPath))
            {
                var filterFiles = Directory.GetFiles(filtersPath, "*.json");
                foreach (var file in filterFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    filters.Add(new { name = fileName, filePath = fileName, searchId = (string?)null });
                }
            }
            
            return Results.Ok(new { 
                filters = filters,
                runningSearchId = (string?)null,
                isSearchRunning = false
            });
        });

        app.MapDelete("/filters/{name}", (string name) => 
        {
            // TODO: Implement actual filter deletion
            return Results.Ok(new { message = $"Filter {name} deleted" });
        });

        // Convert endpoint (needs proper implementation)
        app.MapPost("/convert", (object request) => 
        {
            // TODO: Implement actual JSON to JAML conversion
            return Results.Ok(new { jaml = "converted jaml here" });
        });

        // Default route - serve the web UI
        app.MapGet("/", () => Results.File(Path.Combine(builder.Environment.WebRootPath, "index.html"), "text/html"));
        
        return app;
    }
}

internal sealed record SearchStartRequest(string? FilterJaml, long? SeedCount, long? StartBatch, int? Cutoff);
internal sealed record SearchStopRequest(string? SearchId);
