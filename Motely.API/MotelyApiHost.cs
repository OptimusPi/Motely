using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Motely;
using Motely.API.Services;
using Motely.API;

// Request records
public record SearchStartRequest(string? FilterId, string? Deck, string? Stake, long? SeedCount, long? StartBatch, int? Cutoff, string? SeedSource);
public record SearchStopRequest(string? SearchId);

public static class MotelyApiHost
{
    public static WebApplication CreateHost(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Configure logging to use simple formatter (no ANSI colors)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = "Simple";
        });

        var app = builder.Build();

        // Configure middleware
        app.UseCors("AllowAll");
        
        // Add static files
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Basic endpoints
        app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

        // Search endpoints
        app.MapPost("/search", async (HttpRequest request) => 
        {
            try
            {
                var req = await request.ReadFromJsonAsync<SearchStartRequest>();
                if (req == null)
                    return Results.BadRequest(new { error = "Missing request body" });

                var seedCount = req.SeedCount.HasValue ? (int)Math.Min(req.SeedCount.Value, int.MaxValue) : 0;
                
                var filterJaml = FilterService.GetFilterJaml(req.FilterId);
                if (string.IsNullOrEmpty(filterJaml))
                    return Results.BadRequest(new { error = "Filter not found" });
                
                var (immediateResults, searchId) = await SearchManager.Instance.StartSearchAsync(
                    filterJaml,
                    req.Deck ?? "Red",
                    req.Stake ?? "White",  
                    seedCount,
                    req.StartBatch,
                    req.Cutoff,
                    req.SeedSource);
                
                return Results.Ok(new { 
                    searchId = searchId, 
                    status = "running",
                    columns = SearchManager.Instance.GetColumnNames(searchId)
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapGet("/search/{id}", (string id) => 
        {
            try
            {
                var (results, progressPercent) = SearchManager.Instance.GetSearchStatus(id);
                var isRunning = SearchManager.Instance.IsSearchRunning(id);
                
                return Results.Ok(new { 
                    searchId = id, 
                    status = isRunning ? "running" : "stopped",
                    results = results,
                    progressPercent = progressPercent,
                    columns = SearchManager.Instance.GetColumnNames(id)
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
                var results = await SearchManager.Instance.StopSearchAsync(req?.SearchId ?? "");
                return Results.Ok(new { 
                    message = "Search stopped",
                    results = results,
                    isBackgroundRunning = false
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
