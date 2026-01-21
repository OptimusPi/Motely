using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Motely;
using Motely.Analysis;
using Motely.API;
using Motely.API.Hubs;
using Motely.API.Models;
using Motely.API.Services;

// Request records
public record SearchStartRequest(
    string? FilterId,
    string? Deck,
    string? Stake,
    long? SeedCount,
    long? StartBatch,
    int? Cutoff,
    string? SeedSource
);

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

        // Set ContentRoot to repo root for consistent path resolution
        var motelyRoot = FindMotelyRoot();
        if (!string.IsNullOrEmpty(motelyRoot))
        {
            builder.Host.UseContentRoot(motelyRoot);
        }

        builder.Services.AddEndpointsApiExplorer();

        // Configure logging to use simple formatter (no ANSI colors)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options =>
        {
            options.FormatterName = "Simple";
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(
                "AllowAll",
                policy =>
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
            );
        });

        // Always enable Swagger
        builder.Services.AddSwaggerGen();

        builder.Services.AddMotelyServices(builder.Configuration);

        // Register MCP services from Motely.MCP project
        // Note: MCP is optional and only available when Motely.MCP project is referenced
        // For BSO library builds, MCP is disabled by default
#if !BSO_LIBRARY
        {
            try
            {
                // Use reflection to avoid compile-time dependency on Motely.MCP
                var mcpServerType = Type.GetType("Motely.MCP.McpServer, Motely.MCP");
                var mcpProtocolServerType = Type.GetType(
                    "Motely.MCP.McpProtocol.McpProtocolServer, Motely.MCP"
                );

                if (mcpServerType != null && mcpProtocolServerType != null)
                {
                    // Register HttpClientFactory if not already registered
                    if (!builder.Services.Any(s => s.ServiceType == typeof(IHttpClientFactory)))
                    {
                        builder.Services.AddHttpClient();
                    }

                    // Register MCP services via reflection
                    builder.Services.AddScoped(
                        mcpServerType,
                        sp =>
                        {
                            var logger = sp.GetRequiredService<ILoggerFactory>()
                                .CreateLogger(mcpServerType);
                            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                            var httpClient = httpClientFactory.CreateClient();
                            var config = sp.GetRequiredService<IConfiguration>();
                            return Activator.CreateInstance(
                                mcpServerType,
                                logger,
                                httpClient,
                                config
                            )!;
                        }
                    );

                    builder.Services.AddScoped(
                        mcpProtocolServerType,
                        sp =>
                        {
                            var logger = sp.GetRequiredService<ILoggerFactory>()
                                .CreateLogger(mcpProtocolServerType);
                            var mcpServer = sp.GetRequiredService(mcpServerType);
                            var searchManager = SearchManager.Instance;
                            return Activator.CreateInstance(
                                mcpProtocolServerType,
                                logger,
                                mcpServer,
                                searchManager
                            )!;
                        }
                    );
                }
            }
            catch
            {
                // MCP assembly not available - silently skip
            }
        }
#endif

        var app = builder.Build();

        // Initialize MotelyPaths with ContentRoot and configuration
        MotelyPaths.Initialize(app.Environment, app.Configuration);

        // Initialize SearchManager with motely root path (for SaveFilterToEcosystem compatibility)
        var motelyRootForSearchManager = FindMotelyRoot();
        if (!string.IsNullOrEmpty(motelyRootForSearchManager))
        {
            SearchManager.Instance.SetMotelyRoot(motelyRootForSearchManager);
        }

        // Wire up SearchBroadcaster to SearchManager (always enabled)
        var broadcaster = app.Services.GetRequiredService<ISearchBroadcaster>();
        SearchManager.Instance.SetBroadcaster(broadcaster);

        // Register shutdown handler to close SignalR connections quickly
        {
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(() =>
            {
                try
                {
                    // Force close all SignalR connections immediately
                    var hubContext = app.Services.GetService<IHubContext<SearchHub>>();
                    if (hubContext != null)
                    {
                        using var cts = new System.Threading.CancellationTokenSource(
                            TimeSpan.FromMilliseconds(100)
                        );
                        // Signal all clients to disconnect with very short timeout
                        _ = hubContext.Clients.All.SendAsync("ServerShuttingDown", cts.Token);
                    }
                }
                catch { }
            });
        }

        // Configure middleware - STATIC FILES MUST COME BEFORE ROUTING
        app.UseCors("AllowAll");

        // Static file hosting - wwwroot is at Motely.API/wwwroot (not ContentRoot/wwwroot)
        var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "Motely.API", "wwwroot");
        if (Directory.Exists(wwwrootPath))
        {
            var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                wwwrootPath
            );
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(
                new StaticFileOptions
                {
                    FileProvider = fileProvider,
                    OnPrepareResponse = ctx =>
                    {
                        try
                        {
                            // Let ASP.NET Core handle MIME types automatically via FileExtensionContentTypeProvider
                            // Only set content encoding headers for compressed files
                            if (ctx.File?.Name != null)
                            {
                                var path = ctx.File.Name.ToLowerInvariant();
                                if (path.EndsWith(".br"))
                                {
                                    ctx.Context.Response.Headers.Append("Content-Encoding", "br");
                                }
                                else if (path.EndsWith(".gz"))
                                {
                                    ctx.Context.Response.Headers.Append("Content-Encoding", "gzip");
                                }
                            }
                            // CORS headers for all static files
                            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                        }
                        catch { }
                    },
                }
            );
        }
        else
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        // Also serve static files from public folder (tracked by git, won't be wiped by build)
        // Files in Motely.API/public/ will be accessible at /public/* URLs
        var publicPath = Path.Combine(app.Environment.ContentRootPath, "Motely.API", "public");
        if (Directory.Exists(publicPath))
        {
            app.UseStaticFiles(
                new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(publicPath),
                    RequestPath = "/public",
                    OnPrepareResponse = ctx =>
                    {
                        try
                        {
                            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                        }
                        catch { }
                    },
                }
            );
        }

        // Add Swagger/OpenAPI (always enabled)
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Motely API v1");
            c.RoutePrefix = "swagger";
        });

        // Serve OpenAPI JSON at /openapi/v1.json
        app.MapGet(
            "/openapi/v1.json",
            () => Results.Redirect("/swagger/v1/swagger.json", permanent: false)
        );

        // Register all endpoints (always enabled)
        app.MapCoreApiEndpoints();
        app.MapSearchQueueEndpoints();
        app.MapSignalREndpoints();
        app.MapMcpEndpoints();

        return app;
    }
}
