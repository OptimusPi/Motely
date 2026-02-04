using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
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
using Motely.DB;
using Motely.Executors;

// Request records
public record SearchStartRequest(
    string? FilterId,
    long? SeedCount,
    long? StartBatch,
    int? Cutoff,
    string? SeedSource,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MotelyDeck Deck = MotelyDeck.Red,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] MotelyStake Stake = MotelyStake.White
);

public record SearchStopRequest(string? SearchId);

public static class MotelyApiHost
{
    public static WebApplication CreateHost(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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
        var app = builder.Build();

        // Initialize MotelyPaths with ContentRoot and configuration
        MotelyPaths.Initialize(app.Environment, app.Configuration);

        // Repository + library root for DuckLake storage
        MotelySearchOrchestrator.SetRepository(new MotelyRepository());
        ResultsSetReader.SetLibraryRoot(MotelyPaths.SearchResultsDir);

        // Register shutdown handler to close SignalR connections quickly
        {
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(() =>
            {
                try
                {
                    // Stop all searches gracefully
                    MultiSearchManager.Instance.StopAll("Server shutdown");

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

        // Middleware to ensure WASM threading headers are set on ALL responses
        // This must come early to apply to all requests including default files
        app.Use(
            async (context, next) =>
            {
                // Set headers BEFORE the response is sent
                context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
                context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                await next();
            }
        );

        // /BSO and /BSO/ -> /BSO/index.html (MUST be before UseStaticFiles)
        app.Use(
            (context, next) =>
            {
                var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
                if (path.Equals("/BSO", StringComparison.OrdinalIgnoreCase))
                {
                    context.Request.Path = "/BSO/index.html";
                }
                return next();
            }
        );

        // Static file hosting - check project folder first, then parent folder (for dotnet run vs publish)
        var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        if (!Directory.Exists(wwwrootPath))
        {
            // Fallback: wwwroot is in parent directory (when running dotnet run from Motely.API)
            wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "..", "wwwroot");
        }
        if (Directory.Exists(wwwrootPath))
        {
            var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                wwwrootPath
            );

            // Create a custom content type provider that includes .dat and .wasm files
            var contentTypeProvider = new FileExtensionContentTypeProvider();
            contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
            contentTypeProvider.Mappings[".wasm"] = "application/wasm";

            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            app.UseStaticFiles(
                new StaticFileOptions
                {
                    FileProvider = fileProvider,
                    ContentTypeProvider = contentTypeProvider,
                    OnPrepareResponse = ctx =>
                    {
                        try
                        {
                            // CRITICAL: Set COOP/COEP headers for WASM threading support
                            // These MUST be set on static file responses for SharedArrayBuffer to work
                            ctx.Context.Response.Headers["Cross-Origin-Opener-Policy"] =
                                "same-origin";
                            ctx.Context.Response.Headers["Cross-Origin-Embedder-Policy"] =
                                "require-corp";
                            ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";

                            // Let ASP.NET Core handle MIME types automatically via FileExtensionContentTypeProvider
                            // Only set content encoding headers for compressed files
                            if (ctx.File?.Name != null)
                            {
                                var path = ctx.File.Name.ToLowerInvariant();
                                if (path.EndsWith(".br"))
                                {
                                    ctx.Context.Response.Headers.Append("Content-Encoding", "br");
                                    // Tell CloudFlare this is already compressed
                                    ctx.Context.Response.Headers.Append("Vary", "Accept-Encoding");
                                    ctx.Context.Response.Headers.Append(
                                        "Cache-Control",
                                        "public, max-age=31536000, immutable"
                                    );
                                }
                                else if (path.EndsWith(".gz"))
                                {
                                    ctx.Context.Response.Headers.Append("Content-Encoding", "gzip");
                                    // Tell CloudFlare this is already compressed
                                    ctx.Context.Response.Headers.Append("Vary", "Accept-Encoding");
                                    ctx.Context.Response.Headers.Append(
                                        "Cache-Control",
                                        "public, max-age=31536000, immutable"
                                    );
                                }
                                else if (path.EndsWith(".wasm"))
                                {
                                    // WASM files - aggressive caching
                                    ctx.Context.Response.Headers.Append(
                                        "Cache-Control",
                                        "public, max-age=31536000, immutable"
                                    );
                                }
                            }
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
        var publicPath = Path.Combine(app.Environment.ContentRootPath, "public");
        if (!Directory.Exists(publicPath))
        {
            publicPath = Path.Combine(app.Environment.ContentRootPath, "..", "public");
        }
        if (Directory.Exists(publicPath))
        {
            app.UseStaticFiles(
                new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(publicPath),
                    RequestPath = "/public",
                    OnPrepareResponse = ctx =>
                    {
                        // Set COOP/COEP headers for WASM threading support
                        ctx.Context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
                        ctx.Context.Response.Headers["Cross-Origin-Embedder-Policy"] =
                            "require-corp";
                        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
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
        app.MapHub<SearchHub>("/searchHub");

        // Register MCP endpoints via reflection to avoid circular dependency
        try
        {
            var mcpEndpointsType = Type.GetType("Motely.MCP.McpEndpoints, Motely.MCP");
            if (mcpEndpointsType != null)
            {
                var mapMethod = mcpEndpointsType.GetMethod(
                    "MapMcpEndpoints",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                );
                mapMethod?.Invoke(null, new object[] { app });
            }
        }
        catch
        {
            // MCP assembly not available - silently skip
        }

        return app;
    }
}
