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
using System.Text.RegularExpressions;

namespace Motely.API;

/// <summary>
/// Factory for creating and configuring the Motely WebApplication API.
/// </summary>
public static class MotelyApiFactory
{
    // Shared JSON options for camelCase serialization (JavaScript expects seed/score, not Seed/Score)
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    /// <summary>
    /// Creates and configures a new Motely WebApplication instance.
    /// </summary>
    /// <param name="args">Command line arguments (optional)</param>
    /// <returns>Configured WebApplication instance</returns>
    public static WebApplication CreateApi(string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? new string[0]);

        var motelyRoot = Directory.GetCurrentDirectory();

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
        
        // Configure JSON to use camelCase (JavaScript expects seed/score, not Seed/Score)
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
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
        SearchManager.Instance.SetMotelyRoot(motelyRoot);
        
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
                var buffer = new byte[4096];
                while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                        if (result.MessageType == WebSocketMessageType.Close)
                            break;

                        if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType != WebSocketMessageType.Text)
                        continue;

                    var payload = sb.ToString();
                    if (string.IsNullOrWhiteSpace(payload))
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        if (doc.RootElement.ValueKind != JsonValueKind.Object)
                            continue;

                        if (!doc.RootElement.TryGetProperty("type", out var typeEl))
                            continue;

                        var type = typeEl.GetString() ?? "";
                        if (string.Equals(type, "subscribe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!doc.RootElement.TryGetProperty("searchId", out var searchIdEl))
                                continue;
                            var searchId = (searchIdEl.GetString() ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(searchId))
                                continue;

                            ws.SetSubscription(id, searchId);

                            var (results, progressPercent) = SearchManager.Instance.GetSearchStatus(searchId);
                            var isRunning = SearchManager.Instance.IsSearchRunning(searchId);
                            SearchManager.Instance.TryGetRunningSearchFilterJaml(searchId, out var runningFilterJaml);
                            SearchManager.Instance.TryGetSearchOverrides(searchId, out var _, out var cutoffOverride);
                            SearchManager.Instance.TryGetLastError(searchId, out var lastError);
                            SearchManager.Instance.TryGetSearchMetrics(
                                searchId,
                                out var currentBatch,
                                out var totalBatches,
                                out var seedsSearched,
                                out var seedsPerSecond);

                            var snapshotJson = JsonSerializer.Serialize(new
                            {
                                type = "snapshot",
                                searchId = searchId,
                                status = isRunning ? "running" : "stopped",
                                searchStatus = isRunning ? "running" : "stopped",
                                progressPercent = progressPercent,
                                results = results,
                                filterJaml = string.IsNullOrWhiteSpace(runningFilterJaml) ? (string?)null : runningFilterJaml,
                                columns = SearchManager.Instance.GetColumnNames(searchId),
                                isBackgroundRunning = isRunning,
                                seedsFound = results?.Count ?? 0,
                                currentBatch = currentBatch,
                                totalBatches = totalBatches,
                                seedsSearched = seedsSearched,
                                seedsPerSecond = seedsPerSecond,
                                cutoff = cutoffOverride,
                                lastError = string.IsNullOrWhiteSpace(lastError) ? (string?)null : lastError
                            }, CamelCaseOptions);

                            await ws.SendToAsync(id, snapshotJson);
                        }
                        else if (string.Equals(type, "unsubscribe", StringComparison.OrdinalIgnoreCase))
                        {
                            ws.SetSubscription(id, null);
                        }
                    }
                    catch
                    {
                    }
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
                    seedCount: seedCountInt,
                    startBatchOverride: req.StartBatch,
                    cutoffOverride: req.Cutoff,
                    seedSource: req.SeedSource);

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

                SearchManager.Instance.TryGetRunningSearchFilterJaml(id, out var runningFilterJaml);
                SearchManager.Instance.TryGetSearchOverrides(id, out var _, out var cutoffOverride);
                SearchManager.Instance.TryGetLastError(id, out var lastError);

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
                    filterJaml = string.IsNullOrWhiteSpace(runningFilterJaml) ? (string?)null : runningFilterJaml,
                    columns = SearchManager.Instance.GetColumnNames(id),
                    isBackgroundRunning = isRunning,
                    seedsFound = results?.Count ?? 0,
                    currentBatch = currentBatch,
                    totalBatches = totalBatches,
                    seedsSearched = seedsSearched,
                    seedsPerSecond = seedsPerSecond,
                    cutoff = cutoffOverride,
                    lastError = string.IsNullOrWhiteSpace(lastError) ? (string?)null : lastError
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

        app.MapPost("/search/stop-all", async () => 
        {
            try
            {
                await SearchManager.Instance.StopAllSearchesAsync();
                return Results.Ok(new { message = "All searches stopped" });
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
                    var columns = new List<string> { "seed", "score" };
                    string? jamlErr;
                    if (global::Motely.JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out jamlErr) && cfg != null)
                    {
                        if (!string.IsNullOrWhiteSpace(cfg.Deck)) deck = cfg.Deck;
                        if (!string.IsNullOrWhiteSpace(cfg.Stake)) stake = cfg.Stake;

                        try
                        {
                            columns = cfg.GetColumnNames();
                        }
                        catch
                        {
                            columns = new List<string> { "seed", "score" };
                        }
                    }

                    var searchId = $"{SearchManager.Instance.GetFilterNameForId(filterJaml)}_{deck}_{stake}";

                    filters.Add(new
                    {
                        name,
                        filterJaml,
                        filePath = Path.GetFileName(file),
                        searchId,
                        columns
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
                    var columns = new List<string> { "seed", "score" };
                    if (!string.IsNullOrWhiteSpace(activeFilterJaml))
                    {
                        try
                        {
                            if (global::Motely.JamlConfigLoader.TryLoadFromJamlString(activeFilterJaml, out var cfg, out var jamlErr) && cfg != null)
                            {
                                columns = cfg.GetColumnNames();
                            }
                        }
                        catch
                        {
                            columns = new List<string> { "seed", "score" };
                        }
                    }

                    filters.Insert(0, new
                    {
                        name = $"(unsaved) {activeId}",
                        filterJaml = string.IsNullOrWhiteSpace(activeFilterJaml) ? (string?)null : activeFilterJaml,
                        filePath = (string?)null,
                        searchId = activeId,
                        columns
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

        app.MapGet("/seed-sources", () =>
        {
            var results = new List<object>
            {
                new { key = "all", label = "All Seeds (default)", kind = "builtin" },
                new { key = "random:1000000", label = "Random 1M", kind = "builtin" }
            };

            static IEnumerable<string> SafeListFiles(string dir, string pattern)
            {
                if (!Directory.Exists(dir))
                    return Enumerable.Empty<string>();
                try
                {
                    return Directory.GetFiles(dir, pattern)
                        .Select(p => Path.GetFileName(p) ?? string.Empty)
                        .Where(f => !string.IsNullOrWhiteSpace(f));
                }
                catch
                {
                    return Enumerable.Empty<string>();
                }
            }

            var wordListsDir = Path.Combine(motelyRoot, "WordLists");
            var legacyDir = Path.Combine(motelyRoot, "wordlists");

            var dbFiles = SafeListFiles(wordListsDir, "*.db").Concat(SafeListFiles(legacyDir, "*.db"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            foreach (var f in dbFiles)
            {
                results.Add(new { key = $"db:{f}", label = f, kind = "db", fileName = f });
            }

            var txtFiles = SafeListFiles(wordListsDir, "*.txt").Concat(SafeListFiles(legacyDir, "*.txt"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            foreach (var f in txtFiles)
            {
                results.Add(new { key = $"txt:{f}", label = f, kind = "txt", fileName = f });
            }

            results.Add(new { key = "new", label = "New word list…", kind = "action" });

            return Results.Ok(new { sources = results });
        });

        static string SanitizeWordListFileStem(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var chars = trimmed.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
            var safe = new string(chars).Trim();
            safe = safe.Replace(Path.DirectorySeparatorChar, '-').Replace(Path.AltDirectorySeparatorChar, '-');
            return safe;
        }

        app.MapGet("/wordlists/{name}", (string name) =>
        {
            try
            {
                var safeName = Path.GetFileName(name);
                if (string.IsNullOrWhiteSpace(safeName))
                    return Results.BadRequest(new { error = "Missing name" });

                var ext = Path.GetExtension(safeName);
                if (string.IsNullOrWhiteSpace(ext))
                    safeName += ".txt";
                else if (!string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest(new { error = "Invalid wordlist extension" });

                var wordListsDir = Path.Combine(motelyRoot, "WordLists");
                var legacyDir = Path.Combine(motelyRoot, "wordlists");

                var p1 = Path.Combine(wordListsDir, safeName);
                var p2 = Path.Combine(legacyDir, safeName);

                var path = File.Exists(p1) ? p1 : (File.Exists(p2) ? p2 : null);
                if (path == null)
                    return Results.NotFound(new { error = "Word list not found" });

                var content = File.ReadAllText(path);
                return Results.Ok(new
                {
                    name = Path.GetFileNameWithoutExtension(safeName),
                    fileName = safeName,
                    text = content
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPut("/wordlists/{name}", async (string name, HttpRequest request) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<WordListUpsertRequest>();
                var text = req?.Text ?? string.Empty;

                var safeName = Path.GetFileName(name);
                var stemRaw = Path.GetFileNameWithoutExtension(safeName);
                var stem = SanitizeWordListFileStem(stemRaw);
                if (string.IsNullOrWhiteSpace(stem))
                    return Results.BadRequest(new { error = "Missing name" });

                var wordListsDir = Path.Combine(motelyRoot, "WordLists");
                Directory.CreateDirectory(wordListsDir);

                var fileName = stem + ".txt";
                var fullPath = Path.Combine(wordListsDir, fileName);
                await File.WriteAllTextAsync(fullPath, text);

                return Results.Ok(new
                {
                    name = stem,
                    fileName,
                    key = $"txt:{fileName}"
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
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

                ws.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));
                return Results.Ok(new { message = $"Filter {safeName} deleted" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        static string SanitizeFilterFileStem(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars();
            var chars = trimmed.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
            var safe = new string(chars).Trim();
            safe = safe.Replace(Path.DirectorySeparatorChar, '-').Replace(Path.AltDirectorySeparatorChar, '-');
            return safe;
        }

        static string UpsertNameField(string content, string newName)
        {
            var updated = Regex.Replace(content ?? string.Empty, "(?m)^name:\\s*.*$", $"name: {newName}");
            if (!Regex.IsMatch(updated, "(?m)^name:\\s*.+$"))
            {
                updated = $"name: {newName}\n" + (content ?? string.Empty);
            }
            return updated;
        }

        app.MapPost("/filters/clone", async (HttpRequest request) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<FilterCloneRequest>();
                var filterId = req?.FilterId ?? string.Empty;
                var newNameRaw = req?.NewName ?? string.Empty;

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

                var filtersPath = Path.Combine(motelyRoot, "JamlFilters");
                var srcPath = Path.Combine(filtersPath, safeName);
                if (!File.Exists(srcPath))
                    return Results.NotFound(new { error = "Filter not found" });

                var newStem = SanitizeFilterFileStem(newNameRaw);
                if (string.IsNullOrWhiteSpace(newStem))
                    return Results.BadRequest(new { error = "Missing newName" });

                var baseDest = Path.Combine(filtersPath, newStem + ext);
                var destPath = baseDest;
                for (var i = 2; File.Exists(destPath); i++)
                {
                    destPath = Path.Combine(filtersPath, $"{newStem} {i}{ext}");
                }

                var content = File.ReadAllText(srcPath);
                var updated = UpsertNameField(content, Path.GetFileNameWithoutExtension(destPath));
                File.WriteAllText(destPath, updated);

                ws.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

                return Results.Ok(new
                {
                    name = Path.GetFileNameWithoutExtension(destPath),
                    filePath = Path.GetFileName(destPath),
                    filterJaml = updated
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/filters/rename", async (HttpRequest request) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<FilterRenameRequest>();
                var filterId = req?.FilterId ?? string.Empty;
                var newNameRaw = req?.NewName ?? string.Empty;

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

                var filtersPath = Path.Combine(motelyRoot, "JamlFilters");
                var srcPath = Path.Combine(filtersPath, safeName);
                if (!File.Exists(srcPath))
                    return Results.NotFound(new { error = "Filter not found" });

                var newStem = SanitizeFilterFileStem(newNameRaw);
                if (string.IsNullOrWhiteSpace(newStem))
                    return Results.BadRequest(new { error = "Missing newName" });

                var destName = newStem + ext;
                var destPath = Path.Combine(filtersPath, destName);
                if (!string.Equals(destPath, srcPath, StringComparison.OrdinalIgnoreCase) && File.Exists(destPath))
                    return Results.Conflict(new { error = "A filter with that name already exists" });

                var content = File.ReadAllText(srcPath);
                var updated = UpsertNameField(content, newStem);

                if (!string.Equals(destPath, srcPath, StringComparison.OrdinalIgnoreCase))
                    File.Move(srcPath, destPath);

                File.WriteAllText(destPath, updated);

                ws.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

                return Results.Ok(new
                {
                    name = newStem,
                    filePath = Path.GetFileName(destPath),
                    filterJaml = updated
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/filters/update", async (HttpRequest request) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<FilterUpdateRequest>();
                var filterId = req?.FilterId ?? string.Empty;
                var filterJaml = req?.FilterJaml ?? string.Empty;

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

                var filtersPath = Path.Combine(motelyRoot, "JamlFilters");
                Directory.CreateDirectory(filtersPath); // Ensure directory exists
                var fullPath = Path.Combine(filtersPath, safeName);

                // UPSERT: Create new or update existing filter
                File.WriteAllText(fullPath, filterJaml);

                ws.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

                return Results.Ok(new
                {
                    filePath = safeName,
                    filterJaml = filterJaml
                });
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

internal sealed record SearchStartRequest(string? FilterJaml, long? SeedCount, long? StartBatch, int? Cutoff, string? SeedSource);
internal sealed record SearchStopRequest(string? SearchId);
internal sealed record FilterCloneRequest(string? FilterId, string? NewName);
internal sealed record FilterRenameRequest(string? FilterId, string? NewName);
internal sealed record FilterUpdateRequest(string? FilterId, string? FilterJaml);
internal sealed record WordListUpsertRequest(string? Text);

public sealed class WebSocketBroadcaster
{
    private sealed class SocketInfo
    {
        public WebSocket Socket { get; }
        public string? SearchId;

        public SocketInfo(WebSocket socket)
        {
            Socket = socket;
        }
    }

    private readonly ConcurrentDictionary<Guid, SocketInfo> _sockets = new();

    public Guid Add(WebSocket socket)
    {
        var id = Guid.NewGuid();
        _sockets[id] = new SocketInfo(socket);
        return id;
    }

    public void Remove(Guid id)
    {
        _sockets.TryRemove(id, out _);
    }

    public void SetSubscription(Guid id, string? searchId)
    {
        if (_sockets.TryGetValue(id, out var info))
        {
            info.SearchId = string.IsNullOrWhiteSpace(searchId) ? null : searchId;
        }
    }

    public void BroadcastToSearch(string searchId, string json)
    {
        if (string.IsNullOrWhiteSpace(searchId))
            return;

        _ = BroadcastToSearchAsync(searchId, json);
    }

    public void Broadcast(string json)
    {
        _ = BroadcastAsync(json);
    }

    public async Task SendToAsync(Guid id, string json)
    {
        if (!_sockets.TryGetValue(id, out var info))
            return;

        var socket = info.Socket;
        if (socket.State != WebSocketState.Open)
            return;

        var payload = Encoding.UTF8.GetBytes(json);
        var seg = new ArraySegment<byte>(payload);

        try
        {
            await socket.SendAsync(seg, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch
        {
        }
    }

    private async Task BroadcastAsync(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var seg = new ArraySegment<byte>(payload);

        foreach (var kvp in _sockets)
        {
            var socket = kvp.Value.Socket;
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

    private async Task BroadcastToSearchAsync(string searchId, string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var seg = new ArraySegment<byte>(payload);

        foreach (var kvp in _sockets)
        {
            var info = kvp.Value;
            if (!string.Equals(info.SearchId, searchId, StringComparison.Ordinal))
                continue;

            var socket = info.Socket;
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
