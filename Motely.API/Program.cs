using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.IO;
using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Linq;
using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;
using System.Net;

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

        // Use the assembly location (bin folder) to find wwwroot, since we copy it there now
        var assemblyPath = Path.GetDirectoryName(typeof(MotelyApiFactory).Assembly.Location) 
                           ?? AppContext.BaseDirectory;
        
        // Try to find the real Motely root (where JamlFilters/WordLists are)
        var cwd = Directory.GetCurrentDirectory();
        
        // When running via 'dotnet run', the static files are often in the project source folder, not bin
        // But for published builds, they are in bin.
        // Let's check typical source locations first if debugging
        var possibleWebRoots = new[]
        {
            Path.Combine(assemblyPath, "wwwroot"),
            Path.Combine(cwd, "wwwroot"),
            Path.Combine(cwd, "external", "Motely", "Motely.API", "wwwroot"),
            Path.Combine(cwd, "Motely.API", "wwwroot")
        };

        var webRoot = possibleWebRoots.FirstOrDefault(Directory.Exists) ?? Path.Combine(assemblyPath, "wwwroot");

        // Look up to 4 levels up for Motely root
        var candidates = new List<string> { cwd, assemblyPath };
        var check = cwd;
        for (int i = 0; i < 4; i++)
        {
            candidates.Add(Path.Combine(check, "external", "Motely"));
            candidates.Add(Path.Combine(check, "Motely"));
            check = Directory.GetParent(check)?.FullName;
            if (check == null) break;
            candidates.Add(check);
        }

        var motelyRoot = candidates.FirstOrDefault(d => 
            Directory.Exists(Path.Combine(d, "JamlFilters")) || 
            Directory.Exists(Path.Combine(d, "WordLists"))) 
            ?? cwd;

        Console.WriteLine($"[Init] Resolved MotelyRoot: {motelyRoot}");
        Console.WriteLine($"[Init] WebRoot: {webRoot}");
        builder.Environment.WebRootPath = webRoot;
        
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

        // Add Swagger/OpenAPI
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Add SignalR
        builder.Services.AddSignalR();

        var app = builder.Build();

        // Set up SignalR broadcaster after app is built
        var hubContext = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<SearchHub>>();
        var broadcaster = new SignalRSearchBroadcaster(hubContext);
        SearchManager.Instance.SetBroadcaster(broadcaster);
        // SearchManager still needs project root for JamlFilters/WordLists if they aren't copied
        // But for now, let's fallback to current directory for those if not found relative to bin
        var projectRoot = Directory.GetCurrentDirectory();
        SearchManager.Instance.SetMotelyRoot(projectRoot);
        
        // Configure middleware
        app.UseCors("AllowAll");
        // Static files first (explicit file provider so it works when launched from Motely.TUI)
        if (Directory.Exists(webRoot))
        {
            // Configure MIME types for WebAssembly files (required for Avalonia Browser/BSO)
            var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            contentTypeProvider.Mappings[".wasm"] = "application/wasm";
            contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
            contentTypeProvider.Mappings[".blat"] = "application/octet-stream";
            contentTypeProvider.Mappings[".dll"] = "application/octet-stream";
            contentTypeProvider.Mappings[".pdb"] = "application/octet-stream";
            
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot),
                RequestPath = "",
                ContentTypeProvider = contentTypeProvider,
                OnPrepareResponse = ctx =>
                {
                    var path = ctx.File.Name.ToLowerInvariant();
                    // Don't cache HTML, JS, or CSS files - force fresh load
                    if (path.EndsWith(".html") || path.EndsWith(".js") || path.EndsWith(".css"))
                    {
                        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                        ctx.Context.Response.Headers.Append("Expires", "0");
                    }
                    // Allow caching for other assets (images, fonts, etc.) but with revalidation
                    else
                    {
                        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, must-revalidate");
                    }
                }
            });
        }
        else
        {
            // Fallback for dev environment if not copied yet? 
            // Or just log a warning. For now, let's assume the csproj fix works.
            Console.WriteLine($"[Warning] wwwroot not found at {webRoot}");
        }
        
        // Swagger/OpenAPI
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapGet("/openapi/v1.json", () => Results.Redirect("/swagger/v1/swagger.json"));

        app.UseRouting();
        
        // Map SignalR hub
        app.MapHub<SearchHub>("/searchHub");
        
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

        // WebSocket endpoint removed - using SignalR instead

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
                    
                    // Skip unsaved/temp files
                    if (name.StartsWith("_UNSAVED_", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("__TEMP_", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("{unsaved}", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
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
                    string? displayName = name; // Default to filename if we can't parse JAML
                    string? jamlErr;
                    if (global::Motely.JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out jamlErr) && cfg != null)
                    {
                        // Use actual filter name from JAML config (not normalized filename)
                        if (!string.IsNullOrWhiteSpace(cfg.Name))
                        {
                            displayName = cfg.Name;
                        }
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
                        name = displayName,
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

                broadcaster.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));
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

            // Replace spaces with underscores (as per user requirement)
            trimmed = trimmed.Replace(' ', '_');
            
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

                broadcaster.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

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

                broadcaster.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

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

                if (string.IsNullOrWhiteSpace(filterJaml))
                    return Results.BadRequest(new { error = "Missing filterJaml" });

                var filtersPath = Path.Combine(motelyRoot, "JamlFilters");
                Directory.CreateDirectory(filtersPath); // Ensure directory exists

                // Extract name from JAML config
                string? normalizedName = null;
                if (global::Motely.JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out var jamlErr) && cfg != null)
                {
                    if (!string.IsNullOrWhiteSpace(cfg.Name))
                    {
                        normalizedName = SanitizeFilterFileStem(cfg.Name);
                    }
                }

                // If we couldn't extract a name from JAML, fall back to filterId or generate one
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    if (!string.IsNullOrWhiteSpace(filterId))
                    {
                        normalizedName = Path.GetFileNameWithoutExtension(filterId);
                    }
                    else
                    {
                        normalizedName = "NewFilter";
                    }
                }

                var ext = ".jaml";
                var newFileName = normalizedName + ext;
                var newFullPath = Path.Combine(filtersPath, newFileName);

                // If filterId was provided and it's different from the new name, delete the old file
                if (!string.IsNullOrWhiteSpace(filterId))
                {
                    var oldSafeName = Path.GetFileName(filterId);
                    if (!string.IsNullOrWhiteSpace(oldSafeName) && !string.Equals(oldSafeName, newFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        var oldFullPath = Path.Combine(filtersPath, oldSafeName);
                        if (File.Exists(oldFullPath) && string.Equals(Path.GetExtension(oldSafeName), ext, StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                File.Delete(oldFullPath);
                            }
                            catch
                            {
                                // Ignore deletion errors - we'll overwrite anyway if it exists
                            }
                        }
                    }
                }

                // Write the filter with the normalized name
                File.WriteAllText(newFullPath, filterJaml);

                broadcaster.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

                return Results.Ok(new
                {
                    filePath = newFileName,
                    filterJaml = filterJaml
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // BSO browser app route - serve index.html directly (static files middleware handles /BSO/index.html automatically)
        app.MapGet("/BSO", () => Results.File(Path.Combine(webRoot, "BSO", "index.html"), "text/html"));
        app.MapGet("/BSO/", () => Results.File(Path.Combine(webRoot, "BSO", "index.html"), "text/html"));
        
        // JAML WebUI route - serve index.html directly
        app.MapGet("/JAML/", () => Results.File(Path.Combine(webRoot, "JAML", "index.html"), "text/html"));
        
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
