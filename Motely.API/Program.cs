using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.IO;
using Microsoft.Extensions.FileProviders;
using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Hosting;

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

        static string FindMotelyRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                var wwwroot = Path.Combine(dir.FullName, "wwwroot");
                if (Directory.Exists(wwwroot))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return startDir;
        }

        var motelyRoot = FindMotelyRoot(builder.Environment.ContentRootPath);

        // Keep Environment in sync for any direct path usage
        builder.Environment.WebRootPath = Path.Combine(motelyRoot, "wwwroot");
        
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

        var ws = new WebSocketBroadcaster();
        SearchManager.Instance.SetBroadcaster(ws);
        
        // Configure middleware
        app.UseCors("AllowAll");
        // Static files first (explicit file provider so it works when launched from Motely.TUI)
        var webRoot = Path.Combine(motelyRoot, "wwwroot");
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(webRoot),
            RequestPath = ""
        });
        app.UseRouting();
        app.UseWebSockets();
        
        // Health check
        app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

        // Close endpoint
        app.MapPost("/close", async (IHostApplicationLifetime lifetime) => 
        {
            try
            {
                await SearchManager.Instance.StopAllSearchesAsync();
            }
            catch
            {
            }

            lifetime.StopApplication();
            return Results.Ok(new { message = "Server shutting down..." });
        });

        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var id = ws.Add(socket);
            try
            {
                var buffer = new byte[1024];
                while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(buffer, context.RequestAborted);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
            }
            catch
            {
            }
            finally
            {
                ws.Remove(id);
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch { }
            }
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

                var deck = "Red";
                var stake = "White";
                string? jamlErr;
                if (global::Motely.JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out jamlErr) && cfg != null)
                {
                    if (!string.IsNullOrWhiteSpace(cfg.Deck)) deck = cfg.Deck;
                    if (!string.IsNullOrWhiteSpace(cfg.Stake)) stake = cfg.Stake;
                }
                
                (List<SearchResult> immediateResults, string searchId) = await SearchManager.Instance.StartSearchAsync(
                    filterJaml,
                    deck: deck,
                    stake: stake,
                    seedCount: seedCountInt);

                var columns = SearchManager.Instance.GetColumnNames(searchId);
                
                return Results.Ok(new { 
                    searchId = searchId, 
                    status = "running",
                    results = immediateResults,
                    columns = columns,
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

                SearchManager.Instance.TryGetSearchMetrics(
                    id,
                    out var currentBatch,
                    out var totalBatches,
                    out var seedsSearched,
                    out var seedsPerSecond);

                return Results.Ok(new { 
                    searchId = id, 
                    status = isRunning ? "running" : "stopped",
                    searchStatus = isRunning ? "running" : "stopped",
                    progressPercent = progressPercent,
                    results = results,
                    columns = SearchManager.Instance.GetColumnNames(id),
                    isBackgroundRunning = isRunning,
                    seedsFound = results?.Count ?? 0,
                    currentBatch = currentBatch,
                    totalBatches = totalBatches,
                    seedsSearched = seedsSearched,
                    seedsPerSecond = seedsPerSecond
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
                
                var results = await SearchManager.Instance.StopSearchAsync(searchId);
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

        // Filters endpoint - load JAML filters (what the UI expects)
        app.MapGet("/filters", () => 
        {
            var filtersPath = Path.Combine(motelyRoot, "JamlFilters");
            var filters = new List<object>();

            if (Directory.Exists(filtersPath))
            {
                var filterFiles = Directory.GetFiles(filtersPath, "*.jaml")
                    .Concat(Directory.GetFiles(filtersPath, "*.yaml"))
                    .Concat(Directory.GetFiles(filtersPath, "*.yml"));

                foreach (var file in filterFiles)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    string filterJaml;
                    try
                    {
                        filterJaml = File.ReadAllText(file);
                    }
                    catch
                    {
                        continue;
                    }

                    var deck = "Red";
                    var stake = "White";
                    string? jamlErr;
                    if (global::Motely.JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out jamlErr) && cfg != null)
                    {
                        if (!string.IsNullOrWhiteSpace(cfg.Deck)) deck = cfg.Deck;
                        if (!string.IsNullOrWhiteSpace(cfg.Stake)) stake = cfg.Stake;
                    }

                    var searchId = $"{SearchManager.Instance.GetFilterNameForId(filterJaml)}_{deck}_{stake}";

                    filters.Add(new
                    {
                        name,
                        filterJaml,
                        filePath = Path.GetFileName(file),
                        searchId
                    });
                }
            }

            var runningSearchIds = SearchManager.Instance.GetRunningSearchIds();
            var isSearchRunning = runningSearchIds.Count > 0;

            foreach (var activeId in runningSearchIds)
            {
                SearchManager.Instance.TryGetRunningSearchFilterJaml(activeId, out var activeFilterJaml);

                var alreadyListed = filters.Any(f =>
                {
                    var prop = f.GetType().GetProperty("searchId");
                    return (prop?.GetValue(f) as string) == activeId;
                });

                if (!alreadyListed)
                {
                    filters.Insert(0, new
                    {
                        name = $"(unsaved) {activeId}",
                        filterJaml = string.IsNullOrWhiteSpace(activeFilterJaml) ? (string?)null : activeFilterJaml,
                        filePath = (string?)null,
                        searchId = activeId
                    });
                }
            }

            return Results.Ok(new
            {
                filters,
                runningSearchIds,
                isSearchRunning
            });
        });

        app.MapDelete("/filters/{filterId}", (string filterId) => 
        {
            try
            {
                var filtersPath = Path.Combine(motelyRoot, "JamlFilters");

                if (string.IsNullOrWhiteSpace(filterId))
                    return Results.BadRequest(new { error = "Missing filterId" });

                var safeName = Path.GetFileName(filterId);
                if (!string.Equals(safeName, filterId, StringComparison.Ordinal))
                    return Results.BadRequest(new { error = "Invalid filterId" });

                var ext = Path.GetExtension(safeName);
                if (!string.Equals(ext, ".jaml", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(ext, ".yaml", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(ext, ".yml", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { error = "Invalid filter extension" });
                }

                var fullPath = Path.Combine(filtersPath, safeName);
                if (!File.Exists(fullPath))
                    return Results.NotFound(new { error = "Filter not found" });

                File.Delete(fullPath);
                return Results.Ok(new { message = $"Filter {safeName} deleted" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Convert endpoint (needs proper implementation)
        app.MapPost("/convert", (object request) => 
        {
            // TODO: Implement actual JSON to JAML conversion
            return Results.Ok(new { jaml = "converted jaml here" });
        });

        // Default route - serve the web UI
        app.MapGet("/", () => Results.File(Path.Combine(webRoot, "index.html"), "text/html"));
        
        return app;
    }
}

internal sealed record SearchStartRequest(string? FilterJaml, long? SeedCount, long? StartBatch, int? Cutoff);
internal sealed record SearchStopRequest(string? SearchId);

public sealed class WebSocketBroadcaster
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _sockets = new();

    public Guid Add(WebSocket socket)
    {
        var id = Guid.NewGuid();
        _sockets[id] = socket;
        return id;
    }

    public void Remove(Guid id)
    {
        _sockets.TryRemove(id, out _);
    }

    public void Broadcast(string json)
    {
        _ = BroadcastAsync(json);
    }

    private async Task BroadcastAsync(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var seg = new ArraySegment<byte>(payload);

        foreach (var kvp in _sockets)
        {
            var socket = kvp.Value;
            if (socket.State != WebSocketState.Open)
                continue;

            try
            {
                await socket.SendAsync(seg, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
            }
        }
    }
}
