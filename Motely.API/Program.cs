using System.Collections.Concurrent;
using System.IO;
using Motely.DistributedWorker;
using Motely.Filters;

namespace Motely.API;

public record CreateFilterRequest(string Name, string Content);
public record UpdateFilterRequest(string Content);
public record FilterInfo(string Name, DateTime LastModified);

public record StartSearchRequest(
    string FilterName,
    string? Seed = null,
    int ThreadCount = -1,
    int BatchCharCount = 4,
    bool Palindrome = false
);

public record SearchInfo(
    string Id,
    string FilterName,
    bool IsCompleted,
    long TotalSeedsSearched,
    long MatchingSeeds,
    TimeSpan ElapsedTime
);

public class Program
{
    public static void Main(string[] args) => CreateHost(args).Run();

    /// <summary>
    /// Builds the WebApplication without starting it.
    /// Called by TUI ApiServerWindow to host in-process.
    /// </summary>
    public static WebApplication CreateHost(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
            c.SwaggerDoc("v1", new() { Title = "Motely API", Version = "v1" }));

        builder.Services.AddCors(options =>
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

        builder.Services.Configure<PoolWorkerOptions>(builder.Configuration.GetSection(PoolWorkerOptions.SectionName));
        builder.Services.AddHostedService<PoolWorkerHostedService>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // No UseHttpsRedirection — breaks plain HTTP behind Cloudflare Tunnel / reverse proxy
        app.UseCors();

        // Required for WASM threads (SharedArrayBuffer). Browser blocks it without COOP/COEP.
        app.Use(async (ctx, next) =>
        {
            ctx.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
            ctx.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
            await next();
        });

        app.UseStaticFiles();
        app.UseDefaultFiles();
        app.MapGet("/BSO", () => Results.Redirect("/BSO/", permanent: false));
        app.MapGet("/jammy-seed-searcher", () => Results.Redirect("/jammy-seed-searcher/", permanent: false));

        var jamlDir = Path.Combine(Directory.GetCurrentDirectory(), "jaml-filters");
        Directory.CreateDirectory(jamlDir);

        var searches = new ConcurrentDictionary<string, IMotelySearch>();

        // ── Filters ──

        app.MapGet("/api/filters", () =>
        {
            var filters = Directory.GetFiles(jamlDir, "*.jaml")
                .Select(f => new FilterInfo(
                    Path.GetFileNameWithoutExtension(f),
                    File.GetLastWriteTime(f)))
                .OrderBy(f => f.Name);
            return Results.Ok(filters);
        }).WithName("GetFilters");

        app.MapGet("/api/filters/{name}", (string name) =>
        {
            var path = Path.Combine(jamlDir, $"{name}.jaml");
            return !File.Exists(path)
                ? Results.NotFound()
                : Results.Text(File.ReadAllText(path), "text/yaml");
        }).WithName("GetFilter");

        app.MapPost("/api/filters", (CreateFilterRequest req) =>
        {
            var path = Path.Combine(jamlDir, $"{req.Name}.jaml");
            if (File.Exists(path))
                return Results.Conflict($"Filter '{req.Name}' already exists");
            File.WriteAllText(path, req.Content);
            return Results.Created($"/api/filters/{req.Name}", new FilterInfo(req.Name, DateTime.Now));
        }).WithName("CreateFilter");

        app.MapPut("/api/filters/{name}", (string name, UpdateFilterRequest req) =>
        {
            var path = Path.Combine(jamlDir, $"{name}.jaml");
            if (!File.Exists(path)) return Results.NotFound();
            File.WriteAllText(path, req.Content);
            return Results.Ok(new FilterInfo(name, DateTime.Now));
        }).WithName("UpdateFilter");

        app.MapDelete("/api/filters/{name}", (string name) =>
        {
            var path = Path.Combine(jamlDir, $"{name}.jaml");
            if (!File.Exists(path)) return Results.NotFound();
            File.Delete(path);
            return Results.NoContent();
        }).WithName("DeleteFilter");

        // ── Local Search (single-machine) ──

        app.MapPost("/api/search/start", (StartSearchRequest req) =>
        {
            var filterPath = Path.Combine(jamlDir, $"{req.FilterName}.jaml");
            if (!File.Exists(filterPath))
                return Results.NotFound($"Filter '{req.FilterName}' not found");

            try
            {
                var jaml = File.ReadAllText(filterPath);
                if (!JamlConfigLoader.TryLoad(jaml, out var config, out var error) || config is null)
                    return Results.BadRequest($"Invalid JAML: {error}");

                var threads = req.ThreadCount < 1
                    ? Environment.ProcessorCount
                    : Math.Clamp(req.ThreadCount, 1, Environment.ProcessorCount);
                var batchCharCount = Math.Clamp(req.BatchCharCount, 1, 7);

                var settings = JamlSearchBuilder
                    .CreateSettings(config)
                    .WithDeck(config.Deck)
                    .WithStake(config.Stake)
                    .WithThreadCount(threads)
                    .WithBatchCharacterCount(batchCharCount);

                if (!string.IsNullOrEmpty(req.Seed))
                    settings = settings.WithListSearch([req.Seed.ToUpperInvariant()]);
                else if (req.Palindrome)
                    settings = settings.WithPalindromeSearch();
                else
                    settings = settings.WithSequentialSearch();

                var search = settings.Start();
                var id = Guid.NewGuid().ToString("N");
                searches[id] = search;
                return Results.Ok(new SearchInfo(id, req.FilterName, false, 0, 0, TimeSpan.Zero));
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to start search: {ex.Message}");
            }
        }).WithName("StartSearch");

        app.MapGet("/api/search/{id}", (string id) =>
        {
            if (!searches.TryGetValue(id, out var search))
                return Results.NotFound();
            return Results.Ok(new SearchInfo(
                id, "Unknown", search.IsCompleted,
                search.TotalSeedsSearched, search.MatchingSeeds, search.ElapsedTime));
        }).WithName("GetSearch");

        app.MapPost("/api/search/{id}/stop", (string id) =>
        {
            if (!searches.TryRemove(id, out var search))
                return Results.NotFound();
            search.Cancel();
            search.Dispose();
            return Results.Ok();
        }).WithName("StopSearch");

        // ── Server Status (Dashboard) ──

        var serverStart = DateTime.UtcNow;

        app.MapGet("/api/status", () =>
        {
            var activeSearches = searches.Select(kvp =>
            {
                var s = kvp.Value;
                return new
                {
                    Id = kvp.Key,
                    IsCompleted = s.IsCompleted,
                    SeedsSearched = s.TotalSeedsSearched,
                    Matches = s.MatchingSeeds,
                    Elapsed = s.ElapsedTime.ToString(@"hh\:mm\:ss"),
                };
            });

            return Results.Ok(new
            {
                Hostname = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                OS = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Uptime = (DateTime.UtcNow - serverStart).ToString(@"d\.hh\:mm\:ss"),
                ActiveSearches = activeSearches,
                FilterCount = Directory.GetFiles(jamlDir, "*.jaml").Length,
                PoolUrl = "https://www.seedfinder.app",
            });
        }).WithName("GetStatus");

        app.MapFallbackToFile("/index.html");

        return app;
    }
}
