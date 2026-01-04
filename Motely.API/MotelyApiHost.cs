using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.API.Services;
using Motely.API;
using Motely.API.Hubs;
using Motely.API.McpProtocol;
using Motely.API.Models;

// Request records
public record SearchStartRequest(string? FilterId, string? Deck, string? Stake, long? SeedCount, long? StartBatch, int? Cutoff, string? SeedSource);
public record SearchStopRequest(string? SearchId);

public static class MotelyApiHost
{
    private static string? FindMotelyRoot()
    {
        // Try to find the root by looking for JamlFilters directory
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        
        // Walk up the directory tree looking for JamlFilters
        while (dir != null)
        {
            var jamlFiltersPath = Path.Combine(dir.FullName, "JamlFilters");
            if (Directory.Exists(jamlFiltersPath))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        
        // Fallback: use current directory if we can't find it
        return currentDir;
    }

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

        // Register search queue services
        builder.Services.AddSingleton<SearchQueueService>();
        builder.Services.AddHostedService<SearchQueueHostedService>();
        builder.Services.AddSingleton<SearchService>();
        
        // Register MCP Server for JAML generation
        builder.Services.AddScoped<McpServer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<McpServer>>();
            var httpClient = new HttpClient();
            var config = sp.GetRequiredService<IConfiguration>();
            return new McpServer(logger, httpClient, config);
        });
        
        // Register MCP Protocol Server (JSON-RPC 2.0 handler)
        builder.Services.AddScoped<McpProtocolServer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<McpProtocolServer>>();
            var mcpServer = sp.GetRequiredService<McpServer>();
            var searchManager = SearchManager.Instance;
            return new McpProtocolServer(logger, mcpServer, searchManager);
        });
        
        // Add SignalR
        builder.Services.AddSignalR();
        
        // Register SearchBroadcaster
        builder.Services.AddSingleton<ISearchBroadcaster, SearchBroadcaster>();

        var app = builder.Build();

        // Initialize SearchManager with motely root path
        // Find the root directory by looking for JamlFilters folder
        var motelyRoot = FindMotelyRoot();
        if (!string.IsNullOrEmpty(motelyRoot))
        {
            SearchManager.Instance.SetMotelyRoot(motelyRoot);
        }
        
        // Wire up SearchBroadcaster to SearchManager
        var broadcaster = app.Services.GetRequiredService<ISearchBroadcaster>();
        SearchManager.Instance.SetBroadcaster(broadcaster);

        // Configure middleware
        app.UseCors("AllowAll");
        
        // Add SignalR
        app.MapHub<SearchHub>("/searchHub");
        
        // Add static files with custom caching for HTML files
        var staticFileOptions = new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                // Don't cache HTML files to prevent stale asset references
                if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                    ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                    ctx.Context.Response.Headers.Append("Expires", "0");
                }
            }
        };
        app.UseDefaultFiles();
        app.UseStaticFiles(staticFileOptions);

        // Basic endpoints
        app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });
        app.MapGet("/routes", () => new { 
            homepage = "/",
            health = "/health", 
            routes = "/routes",
            analyze = "/analyze?seed=SEED[&deck=Red][&stake=White]",
            filters = "/filters",
            seed_sources = "/seed-sources",
            searches = "/searches",
            search_start = "POST /search",
            search_status = "GET /search/{id}",
            search_stop = "POST /search/stop"
        });

        // Analyze endpoint (quick seed analyzer)
        // Example: GET /analyze?seed=MO4E11BR&deck=Ghost&stake=White
        app.MapGet("/analyze", (HttpRequest req) =>
        {
            var seed = req.Query["seed"].ToString();
            if (string.IsNullOrWhiteSpace(seed))
                return Results.BadRequest(new { error = "Missing required query parameter: seed" });

            var deckStr = req.Query["deck"].ToString();
            if (string.IsNullOrWhiteSpace(deckStr)) deckStr = "Red";

            var stakeStr = req.Query["stake"].ToString();
            if (string.IsNullOrWhiteSpace(stakeStr)) stakeStr = "White";

            if (!Enum.TryParse<MotelyDeck>(deckStr, true, out var deck))
                return Results.BadRequest(new { error = $"Invalid deck: {deckStr}" });

            if (!Enum.TryParse<MotelyStake>(stakeStr, true, out var stake))
                return Results.BadRequest(new { error = $"Invalid stake: {stakeStr}" });

            try
            {
                var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, deck, stake));
                // Return as text (fast + easy to view/copy). Frontends can call /analyze/json if desired later.
                return Results.Text(analysis.ToString(), "text/plain; charset=utf-8");
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Filter endpoints
        app.MapGet("/filters", Endpoints.GetFilters);
        app.MapPost("/filters/update", Endpoints.SaveFilter);
        app.MapDelete("/filters/{id}", Endpoints.DeleteFilter);

        // Seed sources endpoint
        app.MapGet("/seed-sources", Endpoints.GetSeedSources);

        // Searches endpoint
        app.MapGet("/searches", Endpoints.GetSearches);

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

        // MCP endpoints for JAML generation
        app.MapPost("/mcp/prompt", async (HttpRequest request, McpServer mcpServer) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<McpPromptRequest>();
                if (req?.Prompt == null)
                    return Results.BadRequest(new { error = "Missing prompt" });

                var response = await mcpServer.ProcessPromptAsync(req.Prompt);
                
                return Results.Ok(new
                {
                    success = response.Success,
                    jamlFilter = response.JamlFilter,
                    reasoning = response.Reasoning,
                    error = response.Success ? null : response.Message,
                    searchId = response.SearchId,
                    results = response.Results,
                    columns = response.Columns,
                    message = response.Message,
                    searchUrl = response.SearchUrl
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/mcp/generate", async (HttpRequest request, McpServer mcpServer) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<McpPromptRequest>();
                if (req?.Prompt == null)
                    return Results.BadRequest(new { error = "Missing prompt" });

                // Generate JAML only (no search)
                var (jaml, reasoning, error) = await mcpServer.GenerateJamlOnlyAsync(req.Prompt);
                
                return Results.Ok(new
                {
                    success = string.IsNullOrEmpty(error),
                    jaml = jaml,
                    reasoning = reasoning,
                    error = error
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // MCP Protocol endpoint (JSON-RPC 2.0) for AI assistants (Claude Desktop, Cline, etc.)
        app.MapPost("/mcp", async (HttpRequest request, McpProtocolServer mcpProtocolServer) =>
        {
            try
            {
                // Read request body
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync();
                
                if (string.IsNullOrWhiteSpace(body))
                {
                    return Results.BadRequest(new { error = "Request body is required" });
                }

                // Deserialize JSON-RPC request
                var jsonRpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (jsonRpcRequest == null)
                {
                    return Results.BadRequest(new { error = "Invalid JSON-RPC request" });
                }

                // Handle request via MCP Protocol Server
                var response = await mcpProtocolServer.HandleRequestAsync(jsonRpcRequest);
                
                // Return JSON-RPC response (respects JsonPropertyName attributes)
                return Results.Json(response);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = $"Invalid JSON: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
