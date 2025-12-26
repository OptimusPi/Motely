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
using Microsoft.Extensions.Caching.Memory;
using Motely;
using Motely.Analysis;

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
    /// Checks if a config contains erraticRank or erraticSuit filters that require Erratic deck
    /// </summary>
    private static bool HasErraticFilters(global::Motely.Filters.MotelyJsonConfig? cfg)
    {
        if (cfg == null) return false;

        static bool CheckClauses(System.Collections.Generic.List<global::Motely.Filters.MotelyJsonConfig.MotleyJsonFilterClause>? clauses)
        {
            if (clauses == null) return false;
            foreach (var clause in clauses)
            {
                var type = clause.Type ?? "";
                if (string.Equals(type, "erraticRank", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(type, "erraticSuit", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                // Check nested clauses (for and/or groups)
                if (clause.Clauses != null && CheckClauses(clause.Clauses))
                {
                    return true;
                }
            }
            return false;
        }

        return CheckClauses(cfg.Must) || CheckClauses(cfg.Should) || CheckClauses(cfg.MustNot);
    }

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
        
        // Add appsettings.json from assembly directory if it exists (for TUI builds)
        // WebApplication.CreateBuilder looks in content root, but TUI copies appsettings.json to bin
        var appsettingsPath = Path.Combine(assemblyPath, "appsettings.json");
        if (File.Exists(appsettingsPath))
        {
            builder.Configuration.AddJsonFile(appsettingsPath, optional: true, reloadOnChange: true);
            Console.Write($"[Init] Loaded appsettings.json from: {appsettingsPath} | ");
        }
        else
        {
            // Also try content root (for standalone API runs)
            var contentRootPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (File.Exists(contentRootPath))
            {
                builder.Configuration.AddJsonFile(contentRootPath, optional: true, reloadOnChange: true);
                Console.Write($"[Init] Loaded appsettings.json from: {contentRootPath} | ");
            }
            else
            {
                Console.Write($"[Init] WARNING: appsettings.json not found | ");
            }
        }
        
        // Simple: use current directory
        var cwd = Directory.GetCurrentDirectory();
        var motelyRoot = cwd;
        
        // Find wwwroot - fail fast if not found (no silent fallbacks)
        var possibleWebRoots = new[]
        {
            Path.Combine(assemblyPath, "wwwroot"),
            Path.Combine(cwd, "wwwroot"),
            Path.Combine(cwd, "external", "Motely", "Motely.API", "wwwroot"),
            Path.Combine(cwd, "Motely.API", "wwwroot")
        };
        var webRoot = possibleWebRoots.FirstOrDefault(Directory.Exists);
        
        if (string.IsNullOrEmpty(webRoot) || !Directory.Exists(webRoot))
        {
            var errorMsg = $"[Init] ERROR: wwwroot directory not found in any expected location:\n" +
                          string.Join("\n", possibleWebRoots.Select(p => $"  - {p}"));
            Console.WriteLine(errorMsg);
            throw new DirectoryNotFoundException(errorMsg);
        }

        Console.WriteLine($"[Init] MotelyRoot: {motelyRoot} | WebRoot: {webRoot}");
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
        
        // Add MemoryCache for seed sources caching
        builder.Services.AddMemoryCache();

        // Add HttpClient for MCP server
        builder.Services.AddHttpClient();

        // Register GenieFeedbackService, McpServer, and MCP Protocol Server
        builder.Services.AddSingleton<GenieFeedbackService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<GenieFeedbackService>>();
            return new GenieFeedbackService(logger, motelyRoot);
        });
        
        builder.Services.AddScoped<McpServer>(sp =>
        {
            try
            {
                var logger = sp.GetRequiredService<ILogger<McpServer>>();
                var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                var config = sp.GetRequiredService<IConfiguration>();
                var feedbackService = sp.GetService<GenieFeedbackService>();
                
                // Log the worker URL for debugging
                var workerUrl = config.GetSection("Cloudflare:WorkersAI")["WorkerUrl"];
                if (string.IsNullOrEmpty(workerUrl))
                {
                    Console.Write("[Init] WARNING: Cloudflare Worker URL not configured | ");
                }
                
                return new McpServer(logger, httpClient, config, feedbackService);
            }
            catch (Exception ex)
            {
                // Log but don't fail app startup - endpoint will handle missing config gracefully
                var logger = sp.GetRequiredService<ILogger<McpServer>>();
                logger.LogWarning(ex, "McpServer initialization failed - JamlGenie will not work until configured");
                throw; // Re-throw so endpoint can handle it
            }
        });

        // Register MCP Protocol Server (real MCP implementation for Claude Desktop)
        builder.Services.AddScoped<McpProtocol.McpProtocolServer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<McpProtocol.McpProtocolServer>>();
            var jamlGenieService = sp.GetRequiredService<McpServer>();
            var searchManager = SearchManager.Instance;
            return new McpProtocol.McpProtocolServer(logger, jamlGenieService, searchManager);
        });

        var app = builder.Build();

        // Set up SignalR broadcaster after app is built
        var hubContext = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<SearchHub>>();
        var broadcaster = new SignalRSearchBroadcaster(hubContext);
        SearchManager.Instance.SetBroadcaster(broadcaster);
        // SearchManager still needs project root for JamlFilters/WordLists if they aren't copied
        // But for now, let's fallback to current directory for those if not found relative to bin
        var projectRoot = Directory.GetCurrentDirectory();
        SearchManager.Instance.SetMotelyRoot(projectRoot);

        // Filter loading function - called on every /filters request to ensure fresh data
        var filtersPath = "JamlFilters";
        
        List<object> LoadFiltersFromDisk()
        {
            var filters = new List<object>();
            if (!Directory.Exists(filtersPath)) return filters;
            
            // Get all filter files, deduplicate by base name (prefer .jaml > .yaml > .yml)
            var allFiles = Directory.GetFiles(filtersPath, "*.jaml")
                .Concat(Directory.GetFiles(filtersPath, "*.yaml"))
                .Concat(Directory.GetFiles(filtersPath, "*.yml"));
            
            // Group by base name and take the first (jaml comes before yaml alphabetically anyway)
            var filterFiles = allFiles
                .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());

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
                string? displayName = name;
                string? author = null;
                string? jamlErr;
                if (global::Motely.JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out jamlErr) && cfg != null)
                {
                    if (!string.IsNullOrWhiteSpace(cfg.Name))
                    {
                        displayName = cfg.Name;
                    }
                    if (!string.IsNullOrWhiteSpace(cfg.Author))
                    {
                        author = cfg.Author;
                    }
                    if (!string.IsNullOrWhiteSpace(cfg.Deck)) deck = cfg.Deck;
                    if (!string.IsNullOrWhiteSpace(cfg.Stake)) stake = cfg.Stake;
                    
                    if (string.IsNullOrWhiteSpace(cfg.Deck) && HasErraticFilters(cfg))
                    {
                        deck = "Erratic";
                    }

                    try
                    {
                        columns = cfg.GetColumnNames();
                    }
                    catch
                    {
                        columns = new List<string> { "seed", "score" };
                    }
                }

                // Sanitize filter name for searchId (spaces -> underscores, remove invalid chars)
                var filterName = SearchManager.Instance.GetFilterNameForId(filterJaml);
                var sanitizedName = SearchManager.SanitizeFilterFileStem(filterName);
                var searchId = $"{sanitizedName}_{deck}_{stake}";
                var fileName = Path.GetFileName(file);
                var filterId = SearchManager.GetFilterIdFromFileName(fileName);

                filters.Add(new
                {
                    name = displayName,
                    author = author ?? "Default",
                    filterId,
                    filterJaml,
                    filePath = fileName,
                    searchId,
                    columns
                });
            }
            
            // Order filters alphabetically by name
            return filters
                .OrderBy(f =>
                {
                    var nameProp = f.GetType().GetProperty("name");
                    return nameProp?.GetValue(f) as string ?? "";
                }, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Load once at startup just for logging
        var startupFilters = LoadFiltersFromDisk();
        Console.WriteLine($"");
        Console.WriteLine($"[Startup] ========================================");
        Console.WriteLine($"[Startup] FILTERS: Found {startupFilters.Count} filters in {filtersPath}");

        // Load seed sources once at startup and cache
        var seedSourcesCache = new List<object>
        {
            new { key = "all", label = "All Seeds (Start from beginning)", kind = "builtin", icon = "⭐", category = (string?)null, displayName = "All Seeds (Start from beginning)", fileName = (string?)null }
        };

        seedSourcesCache.Add(new { key = "random:1000000", label = "Random 1M", kind = "builtin", icon = "⭐", category = (string?)null, displayName = "Random 1M", fileName = (string?)null });

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

        var wordListsDir = "WordLists";
        var dbFiles = SafeListFiles(wordListsDir, "*.db");
        var txtFiles = SafeListFiles(wordListsDir, "*.txt");
        var csvFiles = SafeListFiles(wordListsDir, "*.csv");

        var categorySet = new HashSet<string>();
        var fileSources = new List<(string category, string fileName, string kind, string key, string label, string icon, string displayName)>();

        foreach (var f in dbFiles)
        {
            var (category, displayName) = SeedSourceHelper.ParseCategoryFromFileName(f);
            var categoryName = category ?? "Uncategorized";
            categorySet.Add(categoryName);
            var icon = SeedSourceHelper.GetIconForFileType("db");
            fileSources.Add((categoryName, f, "db", $"db:{f}", displayName, icon, displayName));
        }

        foreach (var f in txtFiles)
        {
            var (category, displayName) = SeedSourceHelper.ParseCategoryFromFileName(f);
            var categoryName = category ?? "Uncategorized";
            categorySet.Add(categoryName);
            var icon = SeedSourceHelper.GetIconForFileType("txt");
            fileSources.Add((categoryName, f, "txt", $"txt:{f}", displayName, icon, displayName));
        }

        foreach (var f in csvFiles)
        {
            var (category, displayName) = SeedSourceHelper.ParseCategoryFromFileName(f);
            var categoryName = category ?? "Uncategorized";
            categorySet.Add(categoryName);
            var icon = SeedSourceHelper.GetIconForFileType("csv");
            fileSources.Add((categoryName, f, "csv", $"csv:{f}", displayName, icon, displayName));
        }

        foreach (var category in categorySet.OrderBy(c => c == "Uncategorized" ? "ZZZ" : c, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in fileSources.Where(f => f.category == category).OrderBy(f => f.displayName, StringComparer.OrdinalIgnoreCase))
            {
                seedSourcesCache.Add(new
                {
                    key = file.key,
                    label = file.label,
                    kind = file.kind,
                    icon = file.icon,
                    category = category,
                    displayName = file.displayName,
                    fileName = file.fileName
                });
            }
        }

        seedSourcesCache.Add(new { key = "new", label = "New word list…", kind = "action", icon = "➕", category = (string?)null, displayName = "New word list…", fileName = (string?)null });

        var builtinCount = seedSourcesCache.Count(r => r.GetType().GetProperty("kind")?.GetValue(r)?.ToString() == "builtin");
        var fileCount = seedSourcesCache.Count - builtinCount - 1; // -1 for "new" action
        Console.WriteLine($"[Startup] SEED SOURCES: Loaded {seedSourcesCache.Count} seed sources ({builtinCount} builtin, {fileCount} files)");
        Console.WriteLine($"[Startup] ========================================");
        Console.WriteLine($"");
        
        // Configure middleware
        app.UseCors("AllowAll");
        // Static files first (explicit file provider so it works when launched from Motely.TUI)
        if (Directory.Exists(webRoot))
        {
            // Configure MIME types for WebAssembly files (required for Avalonia Browser/BSO)
            var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
            // Ensure HTML files have correct MIME type (fixes iPhone download issue)
            contentTypeProvider.Mappings[".html"] = "text/html; charset=utf-8";
            contentTypeProvider.Mappings[".htm"] = "text/html; charset=utf-8";
            contentTypeProvider.Mappings[".wasm"] = "application/wasm";
            contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
            contentTypeProvider.Mappings[".blat"] = "application/octet-stream";
            contentTypeProvider.Mappings[".dll"] = "application/octet-stream";
            contentTypeProvider.Mappings[".webcil"] = "application/octet-stream";
            contentTypeProvider.Mappings[".pdb"] = "application/octet-stream";
            contentTypeProvider.Mappings[".br"] = "application/brotli";
            contentTypeProvider.Mappings[".gz"] = "application/gzip";
            contentTypeProvider.Mappings[".json"] = "application/json";
            contentTypeProvider.Mappings[".woff"] = "font/woff";
            contentTypeProvider.Mappings[".woff2"] = "font/woff2";
            contentTypeProvider.Mappings[".css"] = "text/css; charset=utf-8";
            contentTypeProvider.Mappings[".js"] = "text/javascript; charset=utf-8";
            contentTypeProvider.Mappings[".mjs"] = "text/javascript; charset=utf-8";
            contentTypeProvider.Mappings[".txt"] = "text/plain; charset=utf-8";
            contentTypeProvider.Mappings[".ogg"] = "audio/ogg";
            contentTypeProvider.Mappings[".png"] = "image/png";
            contentTypeProvider.Mappings[".ico"] = "image/x-icon";
            
            // Enable default files (index.html) for directory requests
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot),
                RequestPath = ""
            });
            
            // BSO-specific static file serving - serve from wwwroot/BSO/
            var bsoPath = Path.Combine(webRoot, "BSO");
            if (Directory.Exists(bsoPath))
            {
                app.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = new PhysicalFileProvider(bsoPath),
                    RequestPath = "/BSO"
                });
                
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(bsoPath),
                    RequestPath = "/BSO",
                    ContentTypeProvider = contentTypeProvider,
                    OnPrepareResponse = ctx =>
                    {
                        var path = ctx.File.Name.ToLowerInvariant();
                        
                        // Ensure HTML files have correct Content-Type
                        if (path.EndsWith(".html") || path.EndsWith(".htm"))
                        {
                            ctx.Context.Response.ContentType = "text/html; charset=utf-8";
                            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                        }
                        // Don't cache JS or CSS files - force fresh load
                        else if (path.EndsWith(".js") || path.EndsWith(".mjs"))
                        {
                            ctx.Context.Response.ContentType = "text/javascript; charset=utf-8";
                            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                        }
                        else if (path.EndsWith(".css"))
                        {
                            ctx.Context.Response.ContentType = "text/css; charset=utf-8";
                            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                        }
                        // Allow caching for other assets
                        else
                        {
                            ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, must-revalidate");
                        }
                    }
                });
            }
            else
            {
                // BSO directory doesn't exist - this is normal if BSO isn't included in this build
                // Requests to /BSO/ will return 404, which is expected
            }
            
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot),
                RequestPath = "",
                ContentTypeProvider = contentTypeProvider,
                OnPrepareResponse = ctx =>
                {
                    var path = ctx.File.Name.ToLowerInvariant();
                    var fileInfo = ctx.File;
                    
                    // Ensure HTML files have correct Content-Type (fixes iPhone download issue)
                    if (path.EndsWith(".html") || path.EndsWith(".htm"))
                    {
                        ctx.Context.Response.ContentType = "text/html; charset=utf-8";
                        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                        ctx.Context.Response.Headers.Append("Expires", "0");
                    }
                    // Don't cache JS or CSS files - force fresh load with ETag for cache validation
                    else if (path.EndsWith(".js"))
                    {
                        ctx.Context.Response.ContentType = "text/javascript; charset=utf-8";
                        // Use ETag based on file modification time for cache validation
                        var lastModified = fileInfo.LastModified;
                        var etag = $"\"{lastModified.Ticks:X}\"";
                        ctx.Context.Response.Headers.Append("ETag", etag);
                        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, must-revalidate");
                        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                        // Check if client has matching ETag (304 Not Modified)
                        if (ctx.Context.Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) && 
                            ifNoneMatch.ToString() == etag)
                        {
                            ctx.Context.Response.StatusCode = 304;
                            return;
                        }
                    }
                    else if (path.EndsWith(".css"))
                    {
                        ctx.Context.Response.ContentType = "text/css; charset=utf-8";
                        // Use ETag based on file modification time for cache validation
                        var lastModified = fileInfo.LastModified;
                        var etag = $"\"{lastModified.Ticks:X}\"";
                        ctx.Context.Response.Headers.Append("ETag", etag);
                        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, must-revalidate");
                        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                        // Check if client has matching ETag (304 Not Modified)
                        if (ctx.Context.Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) && 
                            ifNoneMatch.ToString() == etag)
                        {
                            ctx.Context.Response.StatusCode = 304;
                            return;
                        }
                    }
                    // Allow caching for other assets (images, fonts, etc.) but with revalidation
                    else
                    {
                        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, must-revalidate");
                    }
                    
                    // Add ETag header for cache validation (Cloudflare-friendly)
                    if (fileInfo.Exists)
                    {
                        var etag = $"\"{fileInfo.LastModified.Ticks}-{fileInfo.Length}\"";
                        ctx.Context.Response.Headers.Append("ETag", etag);
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

        // Active searches status (for UI display)
        app.MapGet("/searches/active", () =>
        {
            var searches = SearchManager.Instance.GetActiveSearchesStatus();
            return Results.Ok(new
            {
                searches = searches,
                schedulerRunning = searches.Any(s => s.InQueue),
                count = searches.Count
            });
        });
        
        // Panic stop a specific search
        app.MapPost("/search/{id}/panic-stop", async (string id) =>
        {
            try
            {
                var results = await SearchManager.Instance.StopSearchAsync(id);
                return Results.Ok(new
                {
                    message = $"Search {id} panic stopped",
                    resultsCount = results?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

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
        // Multi-source hydrate search endpoint
        app.MapPost("/search/hydrate", async (HttpRequest request) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<MultiSourceHydrateRequest>();
                if (req == null)
                    return Results.BadRequest(new { error = "Missing request body" });

                var filterJaml = req.FilterJaml ?? string.Empty;
                var seedSources = req.SeedSources ?? Array.Empty<string>();
                
                // Combine seeds from multiple sources
                var allSeeds = new List<string>();
                foreach (var sourceKey in seedSources)
                {
                    if (string.IsNullOrWhiteSpace(sourceKey))
                        continue;

                    var s = sourceKey.Trim();
                    
                    // Skip built-in sources (handled by search executor)
                    if (s == "all" || s == "all:continue" || s.StartsWith("random:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Handle CSV files
                    if (s.StartsWith("csv:", StringComparison.OrdinalIgnoreCase))
                    {
                        var file = s.Substring("csv:".Length).Trim();
                        if (file.Length > 0)
                        {
                            var safeName = Path.GetFileName(file);
                            var wordListsDir = "WordLists";
                            var csvPath = Path.Combine(wordListsDir, safeName);
                            if (!File.Exists(csvPath)) csvPath = null;
                            
                            if (csvPath != null && File.Exists(csvPath))
                            {
                                var csvContent = File.ReadAllText(csvPath);
                                var seeds = SeedSourceHelper.ParseCsvSeeds(csvContent);
                                allSeeds.AddRange(seeds);
                            }
                        }
                    }
                    // Handle TXT files
                    else if (s.StartsWith("txt:", StringComparison.OrdinalIgnoreCase))
                    {
                        var file = s.Substring("txt:".Length).Trim();
                        if (file.Length > 0)
                        {
                            var stem = Path.GetFileNameWithoutExtension(file);
                            var wordListsDir = "WordLists";
                            var txtPath = Path.Combine(wordListsDir, stem + ".txt");
                            if (!File.Exists(txtPath)) txtPath = null;
                            
                            if (txtPath != null && File.Exists(txtPath))
                            {
                                var lines = File.ReadAllLines(txtPath)
                                    .Where(line => !string.IsNullOrWhiteSpace(line))
                                    .Select(line => SeedSourceHelper.ValidateAndNormalizeSeed(line.Trim()))
                                    .Where(seed => seed != null)
                                    .Cast<string>();
                                allSeeds.AddRange(lines);
                            }
                        }
                    }
                    // Handle DB files - these are streamed, not loaded into memory
                    // For multi-source, we'd need to combine them differently
                    // For now, just use the first DB source
                }

                // Remove duplicates and sort
                allSeeds = allSeeds.Distinct().ToList();
                
                // If we have combined seeds, use them; otherwise fall back to single source behavior
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
                    
                    if (string.IsNullOrWhiteSpace(cfg.Deck) && HasErraticFilters(cfg))
                    {
                        deck = "Erratic";
                    }
                }
                
                // If we have combined seeds, pass them as SeedList
                // Otherwise, use the first source (or "all" if none)
                string? seedSource = null;
                if (allSeeds.Count > 0)
                {
                    // Pass seeds directly to search executor via SeedList parameter
                    // This requires modifying StartSearchAsync to accept SeedList
                    // For now, create a temporary wordlist or use SeedList if available
                    seedSource = seedSources.FirstOrDefault() ?? "all";
                }
                else
                {
                    seedSource = seedSources.FirstOrDefault() ?? "all";
                }

                (List<SearchResult> immediateResults, string searchId) = await SearchManager.Instance.StartSearchAsync(
                    filterJaml,
                    deck: deck,
                    stake: stake,
                    seedCount: seedCountInt,
                    startBatchOverride: req.StartBatch,
                    cutoffOverride: req.Cutoff,
                    seedSource: seedSource,
                    seedList: allSeeds.Count > 0 ? allSeeds : null);

                var columns = SearchManager.Instance.GetColumnNames(searchId);
                
                return Results.Ok(new { 
                    searchId = searchId, 
                    status = "running",
                    results = immediateResults,
                    columns = columns,
                    isBackgroundRunning = true,
                    seedsLoaded = allSeeds.Count
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

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
                    
                    // Auto-detect Erratic deck if filter uses erraticRank or erraticSuit
                    if (string.IsNullOrWhiteSpace(cfg.Deck) && HasErraticFilters(cfg))
                    {
                        deck = "Erratic";
                    }
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

        app.MapGet("/search/all", () =>
        {
            try
            {
                var runningIds = SearchManager.Instance.GetRunningSearchIds();
                return Results.Ok(new { runningSearchIds = runningIds });
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

        app.MapDelete("/search/{id}/results", (string id) =>
        {
            try
            {
                var dbPath = Path.Combine(SearchManager.Instance.GetSearchResultsDir(), $"{id}.db");
                if (File.Exists(dbPath))
                {
                    File.Delete(dbPath);
                    return Results.Ok(new { message = "Results cleared" });
                }
                return Results.Ok(new { message = "No results to clear" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/search/{id}/export-to-fertilizer", async (string id) =>
        {
            try
            {
                // Step 1: Stop search if running (synchronous/blocking)
                await SearchManager.Instance.StopSearchAsync(id);
                
                // Step 2: Get database path
                var dbPath = Path.Combine(SearchManager.Instance.GetSearchResultsDir(), $"{id}.db");
                
                // Step 3: Verify database exists
                if (!File.Exists(dbPath))
                {
                    return Results.Ok(new { message = "No results to export", exported = 0 });
                }
                
                // Step 4: Get TOP 1000 seeds (synchronous/blocking)
                var topSeeds = SearchManager.Instance.GetTopSeedsOnlyFromDb(dbPath, 1000);
                
                if (topSeeds.Count == 0)
                {
                    return Results.Ok(new { message = "No seeds to export", exported = 0 });
                }
                
                // Step 5: Export to Fertilizer (synchronous/blocking - wait for completion)
                await FertilizerDatabase.Instance.AddSeedsAsync(topSeeds);
                
                // Step 6: Return success only if all steps completed
                return Results.Ok(new { 
                    message = "Seeds exported to Fertilizer", 
                    exported = topSeeds.Count 
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // MCP Protocol endpoint (JSON-RPC 2.0) - for Claude Desktop and other MCP clients
        app.MapPost("/mcp", async (HttpRequest request, IServiceProvider services) =>
        {
            try
            {
                var jsonRpcRequest = await JsonSerializer.DeserializeAsync<McpProtocol.JsonRpcRequest>(
                    request.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (jsonRpcRequest == null)
                {
                    return Results.BadRequest(new { error = "Invalid JSON-RPC request" });
                }

                var mcpServer = services.GetRequiredService<McpProtocol.McpProtocolServer>();
                var response = await mcpServer.HandleRequestAsync(jsonRpcRequest);

                // Return JSON-RPC 2.0 response (preserve property names as-is for MCP protocol)
                return Results.Json(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new
                {
                    jsonrpc = "2.0",
                    error = new { code = -32603, message = $"Internal error: {ex.Message}" }
                });
            }
        });

        // Analyze seed endpoint (REST) - for frontend seed verification
        app.MapGet("/analyze", (string seed, string? deck, string? stake) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(seed))
                {
                    return Results.BadRequest(new { error = "Missing seed parameter" });
                }

                var deckValue = deck ?? "Red";
                var stakeValue = stake ?? "White";

                if (!Enum.TryParse<global::Motely.MotelyDeck>(deckValue, true, out var deckEnum))
                    deckEnum = global::Motely.MotelyDeck.Red;
                if (!Enum.TryParse<global::Motely.MotelyStake>(stakeValue, true, out var stakeEnum))
                    stakeEnum = global::Motely.MotelyStake.White;

                var analysis = global::Motely.Analysis.MotelySeedAnalyzer.Analyze(
                    new global::Motely.Analysis.MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum)
                );

                if (!string.IsNullOrEmpty(analysis.Error))
                {
                    return Results.BadRequest(new { error = analysis.Error });
                }

                return Results.Text(analysis.ToString(), "text/plain");
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Legacy REST endpoint for JamlGenie frontend - natural language to JAML translation
        app.MapPost("/mcp/prompt", async (HttpRequest request, IServiceProvider services) => 
        {
            try
            {
                var req = await request.ReadFromJsonAsync<McpPromptRequest>();
                if (req == null || string.IsNullOrWhiteSpace(req.Prompt))
                {
                    return Results.BadRequest(new { 
                        success = false,
                        error = "Missing prompt" 
                    });
                }

                McpServer? mcpServer;
                try
                {
                    mcpServer = services.GetRequiredService<McpServer>();
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { 
                        success = false,
                        error = $"JamlGenie is not configured: {ex.Message}. Please configure Cloudflare Worker URL in appsettings.json"
                    });
                }

                var response = await mcpServer.ProcessPromptAsync(req.Prompt);
                
                if (!response.Success)
                {
                    return Results.BadRequest(new { 
                        success = false,
                        error = response.Message ?? "Unknown error occurred"
                    });
                }

                return Results.Ok(new { 
                    success = true,
                    searchId = response.SearchId,
                    jamlFilter = response.JamlFilter,
                    reasoning = response.Reasoning,
                    results = response.Results ?? new List<SearchResult>(),
                    columns = response.Columns ?? new List<string>(),
                    message = response.Message,
                    searchUrl = response.SearchUrl // URL to view full search in JAML UI
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { 
                    success = false,
                    error = ex.Message ?? "An unexpected error occurred"
                });
            }
        });

        // Filters endpoint - load JAML filters (what the UI expects)
        app.MapPost("/filters/columns", async (HttpRequest request) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<FilterColumnsRequest>();
                if (req == null || string.IsNullOrWhiteSpace(req.FilterJaml))
                {
                    return Results.Ok(new { columns = new List<string> { "seed", "score" } });
                }

                var columns = new List<string> { "seed", "score" };
                if (global::Motely.JamlConfigLoader.TryLoadFromJamlString(req.FilterJaml, out var cfg, out var jamlErr) && cfg != null)
                {
                    try
                    {
                        columns = cfg.GetColumnNames();
                    }
                    catch
                    {
                        columns = new List<string> { "seed", "score" };
                    }
                }

                return Results.Ok(new { columns });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        app.MapPost("/filters/update-column-label", async (HttpRequest request) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<FilterUpdateColumnLabelRequest>();
                if (req == null || string.IsNullOrWhiteSpace(req.FilterJaml))
                {
                    return Results.BadRequest(new { error = "Missing filter JAML" });
                }

                if (!global::Motely.JamlConfigLoader.TryLoadFromJamlString(req.FilterJaml, out var cfg, out var jamlErr) || cfg == null)
                {
                    return Results.BadRequest(new { error = jamlErr ?? "Failed to parse JAML" });
                }

                // Update label for the specified should clause
                if (cfg.Should != null && req.ColumnIndex >= 0 && req.ColumnIndex < cfg.Should.Count)
                {
                    cfg.Should[req.ColumnIndex].Label = req.NewLabel;
                    
                    // Convert back to JAML using centralized formatter
                    var updatedJaml = cfg.SaveAsJaml();
                    return Results.Ok(new { filterJaml = updatedJaml });
                }

                return Results.BadRequest(new { error = "Invalid column index" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Filters endpoint - reload from disk each time to pick up new/changed filters
        app.MapGet("/filters", () => 
        {
            var runningSearchIds = SearchManager.Instance.GetRunningSearchIds();
            var isSearchRunning = runningSearchIds.Count > 0;

            // Load fresh from disk every time
            var filters = LoadFiltersFromDisk();

            // Add unsaved/running filters
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

                    var filterIdFromSearchId = activeId.Split('_').FirstOrDefault() ?? activeId;
                    
                    filters.Insert(0, new
                    {
                        name = $"(unsaved) {activeId}",
                        filterId = filterIdFromSearchId,
                        filterJaml = string.IsNullOrWhiteSpace(activeFilterJaml) ? (string?)null : activeFilterJaml,
                        filePath = (string?)null,
                        searchId = activeId,
                        columns
                    });
                }
            }

            // Order filters: unsaved first, then alphabetical
            var orderedFilters = filters
                .OrderBy(f =>
                {
                    var nameProp = f.GetType().GetProperty("name");
                    var name = nameProp?.GetValue(f) as string ?? "";
                    return name.StartsWith("(unsaved)", StringComparison.OrdinalIgnoreCase) ? $"0_{name}" : $"1_{name}";
                })
                .ThenBy(f =>
                {
                    var nameProp = f.GetType().GetProperty("name");
                    return nameProp?.GetValue(f) as string ?? "";
                }, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Results.Ok(new
            {
                filters = orderedFilters,
                runningSearchIds,
                isSearchRunning
            });
        });

        // Export system prompt for Worker (admin endpoint - shows what should be hardcoded in Worker)
        app.MapGet("/admin/system-prompt", (IServiceProvider services) =>
        {
            try
            {
                var mcpServer = services.GetRequiredService<McpServer>();
                var systemPrompt = mcpServer.GetSystemPrompt();
                return Results.Ok(new
                {
                    message = "System prompt to hardcode in Cloudflare Worker",
                    systemPrompt = systemPrompt,
                    length = systemPrompt.Length,
                    instruction = "Copy this systemPrompt value and hardcode it as a constant in your Worker code"
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Seed sources endpoint - serve from memory cache only (no disk I/O for security)
        app.MapGet("/seed-sources", (HttpContext context) =>
        {
            // Start with cached seed sources
            var results = new List<object>(seedSourcesCache);

            // Check for resumable sequential search (dynamic - can't cache this)
            var runningSearchIds = SearchManager.Instance.GetRunningSearchIds();
            foreach (var searchId in runningSearchIds)
            {
                if (SearchManager.Instance.TryGetSearchProgress(searchId, out var currentBatch, out var totalBatches))
                {
                    if (totalBatches > 0 && currentBatch > 0)
                    {
                        var progress = (currentBatch * 100.0 / totalBatches);
                        var progressStr = progress.ToString("F4");
                        // Insert after "all" but before "random"
                        var allIndex = results.FindIndex(r => r.GetType().GetProperty("key")?.GetValue(r)?.ToString() == "all");
                        if (allIndex >= 0)
                        {
                            results.Insert(allIndex + 1, new 
                            { 
                                key = "all:continue", 
                                label = $"All Seeds (Continue Saved Search - {progressStr}%)", 
                                kind = "builtin", 
                                icon = "⭐", 
                                category = (string?)null, 
                                displayName = $"All Seeds (Continue Saved Search - {progressStr}%)",
                                fileName = (string?)null
                            });
                        }
                        break;
                    }
                }
            }

            // Set Cloudflare-friendly headers
            context.Response.Headers.CacheControl = "public, max-age=1800";
            
            var resultsJson = System.Text.Json.JsonSerializer.Serialize(results);
            var etagBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(resultsJson));
            var etag = Convert.ToHexString(etagBytes).Substring(0, 16);
            context.Response.Headers.ETag = $"\"{etag}\"";

            return Results.Ok(new { sources = results, categories = BuildCategories(results) });
        });

        static List<object> BuildCategories(List<object> sources)
        {
            var categoryMap = new Dictionary<string, List<object>>();
            
            foreach (var source in sources)
            {
                var categoryProp = source.GetType().GetProperty("category");
                var category = categoryProp?.GetValue(source) as string ?? "Uncategorized";
                
                if (!categoryMap.ContainsKey(category))
                {
                    categoryMap[category] = new List<object>();
                }
                categoryMap[category].Add(source);
            }

            var categories = categoryMap
                .OrderBy(kvp => kvp.Key == "Uncategorized" ? "ZZZ" : kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => new { name = kvp.Key, sources = kvp.Value })
                .ToList();

            return categories.Cast<object>().ToList();
        }

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

                var wordListsDir = "WordLists";
                var path = Path.Combine(wordListsDir, safeName);
                if (!File.Exists(path)) path = null;
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

                var wordListsDir = "WordLists";
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

        app.MapPost("/filters/save", async (HttpRequest request) =>
        {
            try
            {
                var req = await request.ReadFromJsonAsync<FilterSaveRequest>();
                var filterId = req?.FilterId ?? string.Empty;
                var filterJaml = req?.FilterJaml ?? string.Empty;
                var createNew = req?.CreateNew ?? false;

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
                var baseFileName = normalizedName + ext;
                var newFullPath = Path.Combine(filtersPath, baseFileName);

                // If creating new or name changed, ensure unique filename
                if (createNew || (File.Exists(newFullPath) && !string.IsNullOrWhiteSpace(filterId)))
                {
                    // Add timestamp to make it unique
                    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
                    baseFileName = $"{nameWithoutExt}_{timestamp}{ext}";
                    newFullPath = Path.Combine(filtersPath, baseFileName);
                }

                // Write the filter
                File.WriteAllText(newFullPath, filterJaml);

                broadcaster.Broadcast(JsonSerializer.Serialize(new { type = "filters_changed" }));

                return Results.Ok(new
                {
                    filePath = baseFileName,
                    filterJaml = filterJaml,
                    createdNew = createNew
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

        // Static files middleware with UseDefaultFiles will automatically serve index.html for:
        // - / (root index.html)
        // - /JAML/ (JAML/index.html)
        // - /BSO/ (BSO/index.html)
        // - /JamlGenie/ (JamlGenie/index.html)
        // No explicit routes needed!
        
        return app;
    }
}

internal sealed record SearchStartRequest(string? FilterJaml, long? SeedCount, long? StartBatch, int? Cutoff, string? SeedSource);
internal sealed record SearchStopRequest(string? SearchId);
internal sealed record FilterCloneRequest(string? FilterId, string? NewName);
internal sealed record FilterRenameRequest(string? FilterId, string? NewName);
internal sealed record FilterUpdateRequest(string? FilterId, string? FilterJaml);
internal sealed record FilterSaveRequest(string? FilterId, string? FilterJaml, bool CreateNew = false);
internal sealed record WordListUpsertRequest(string? Text);
internal sealed record McpPromptRequest(string? Prompt);
internal sealed record FilterColumnsRequest(string? FilterJaml);
internal sealed record FilterUpdateColumnLabelRequest(string? FilterJaml, int ColumnIndex, string? NewLabel);
internal sealed record MultiSourceHydrateRequest(string? FilterJaml, string[]? SeedSources, long? SeedCount, long? StartBatch, int? Cutoff);
