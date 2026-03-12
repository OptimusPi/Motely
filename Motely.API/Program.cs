using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Motely.DistributedWorker;
using Motely.Filters;

namespace Motely.API;

public record CreateFilterRequest(string Name, string Content);
public record UpdateFilterRequest(string Content);
public record FilterInfo(string Name, DateTime LastModified);

public record StartSearchRequest(
    string FilterName,
    string? Seed = null,
    string? Keyword = null,
    int ThreadCount = -1,
    int BatchCharCount = 4,
    bool Palindrome = false
);

public record SearchInfo(
    string FilterId,
    string FilterName,
    bool IsCompleted,
    long TotalSeedsSearched,
    long MatchingSeeds,
    TimeSpan ElapsedTime
);

public record ActiveSearchInfo(string FilterId, bool IsCompleted, long SeedsSearched, long Matches, string Elapsed);
public record WorkerStatusResponse(bool Enabled, bool Configured, string PoolUrl, string WorkerId, int Threads, string State);
public record ServerStatusResponse(string Hostname, int ProcessorCount, string OS, string Runtime, string Uptime, IEnumerable<ActiveSearchInfo> ActiveSearches, int FilterCount, string PoolUrl, WorkerStatusResponse Worker);

internal sealed class SearchHandle(IMotelySearch search, CancellationTokenSource cancellation, Task runTask)
{
    public IMotelySearch Search { get; } = search;
    public CancellationTokenSource Cancellation { get; } = cancellation;
    public Task RunTask { get; } = runTask;
}

[JsonSerializable(typeof(CreateFilterRequest))]
[JsonSerializable(typeof(UpdateFilterRequest))]
[JsonSerializable(typeof(FilterInfo))]
[JsonSerializable(typeof(FilterInfo[]))]
[JsonSerializable(typeof(IEnumerable<FilterInfo>))]
[JsonSerializable(typeof(StartSearchRequest))]
[JsonSerializable(typeof(SearchInfo))]
[JsonSerializable(typeof(ActiveSearchInfo))]
[JsonSerializable(typeof(ActiveSearchInfo[]))]
[JsonSerializable(typeof(WorkerStatusResponse))]
[JsonSerializable(typeof(ServerStatusResponse))]
internal partial class ApiJsonSerializerContext : JsonSerializerContext
{
}

public class Program
{
    public static void Main(string[] args) => CreateHost(args).Run();

    private static string ResolveJamlDirectory(IConfiguration configuration)
    {
        var configured = configuration["Jaml:Directory"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Directory.CreateDirectory(configured);
            return configured;
        }

        var sharedJamlDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "JamlFilters"));
        Directory.CreateDirectory(sharedJamlDir);
        return sharedJamlDir;
    }

    /// <summary>
    /// Builds the WebApplication without starting it.
    /// Called by TUI ApiServerWindow to host in-process.
    /// </summary>
    public static WebApplication CreateHost(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiJsonSerializerContext.Default);
        });

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
        var jammyIndexPath = Path.Combine(
            app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"),
            "jammy-seed-finder",
            "index.html");

        app.MapGet("/jammy-seed-finder", () => Results.Redirect("/jammy-seed-finder/", permanent: false));
        app.MapGet("/jammy-seed-finder/", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(jammyIndexPath);
        });
        app.MapFallbackToFile("/jammy-seed-finder/{*path:nonfile}", "jammy-seed-finder/index.html");

        var jamlDir = ResolveJamlDirectory(builder.Configuration);

        var searches = new ConcurrentDictionary<string, SearchHandle>();

        app.Lifetime.ApplicationStopping.Register(() =>
        {
            foreach (var handle in searches.Values)
            {
                try { handle.Cancellation.Cancel(); } catch { }
                try { handle.Search.Cancel(); } catch { }
            }
        });

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

                if (!string.IsNullOrWhiteSpace(req.Seed))
                    settings = settings.WithListSearch([req.Seed.Trim().ToUpperInvariant()]);
                else if (!string.IsNullOrWhiteSpace(req.Keyword))
                {
                    string kw = req.Keyword.Trim().ToUpperInvariant();
                    int padLen = MotelyCore.MaxSeedLength - kw.Length;
                    if (padLen < 0)
                        return Results.BadRequest($"Keyword '{kw}' is too long (max {MotelyCore.MaxSeedLength} chars).");
                    settings = settings.WithListSearch(MotelyCore.GeneratePaddedSeeds(kw, padLen));
                }
                else
                    settings = settings.WithPalindromeSearch();

                var search = settings.CreateSearch();
                var filterId = MotelyRuntimeIds.GenerateFilterId(config);
                if (searches.TryRemove(filterId, out var existingSearch))
                {
                    existingSearch.Cancellation.Cancel();
                    existingSearch.Search.Cancel();
                    existingSearch.Search.Dispose();
                    existingSearch.Cancellation.Dispose();
                }

                var searchCts = CancellationTokenSource.CreateLinkedTokenSource(app.Lifetime.ApplicationStopping);
                var runTask = Task.Run(() => search.Start(searchCts.Token), searchCts.Token);
                var handle = new SearchHandle(search, searchCts, runTask);
                searches[filterId] = handle;

                _ = runTask.ContinueWith(_ =>
                {
                    if (searches.TryRemove(filterId, out var completedHandle))
                    {
                        try { completedHandle.Search.Dispose(); } catch { }
                        completedHandle.Cancellation.Dispose();
                    }
                }, TaskScheduler.Default);

                return Results.Ok(new SearchInfo(filterId, req.FilterName, false, 0, 0, TimeSpan.Zero));
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to start search: {ex.Message}");
            }
        }).WithName("StartSearch");

        app.MapGet("/api/search/{filterId}", (string filterId) =>
        {
            if (!searches.TryGetValue(filterId, out var handle))
                return Results.NotFound();
            return Results.Ok(new SearchInfo(
                filterId, "Unknown", handle.Search.IsCompleted,
                handle.Search.TotalSeedsSearched, handle.Search.MatchingSeeds, handle.Search.ElapsedTime));
        }).WithName("GetSearch");

        app.MapPost("/api/search/{filterId}/stop", (string filterId) =>
        {
            if (!searches.TryRemove(filterId, out var handle))
                return Results.NotFound();
            handle.Cancellation.Cancel();
            handle.Search.Cancel();
            handle.Search.Dispose();
            handle.Cancellation.Dispose();
            return Results.Ok();
        }).WithName("StopSearch");

        // ── Server Status (Dashboard) ──

        var serverStart = DateTime.UtcNow;

        app.MapGet("/api/status", (IOptions<PoolWorkerOptions> poolOptionsAccessor) =>
        {
            var activeSearches = searches.Select(kvp =>
            {
                var s = kvp.Value.Search;
                return new ActiveSearchInfo(
                    kvp.Key,
                    s.IsCompleted,
                    s.TotalSeedsSearched,
                    s.MatchingSeeds,
                    s.ElapsedTime.ToString(@"hh\:mm\:ss")
                );
            }).ToArray();

            var poolOptions = poolOptionsAccessor.Value;
            var configuredPoolUrl = poolOptions.Url?.Trim() ?? string.Empty;
            var configuredWorkerId = poolOptions.WorkerId?.Trim();
            var workerConfigured = !string.IsNullOrWhiteSpace(configuredPoolUrl);
            var workerEnabled = workerConfigured;
            var workerState = workerConfigured ? "running" : "disabled";
            var workerStatus = new WorkerStatusResponse(
                workerEnabled,
                workerConfigured,
                configuredPoolUrl,
                string.IsNullOrWhiteSpace(configuredWorkerId) ? $"{Environment.MachineName}-{Environment.ProcessId}" : configuredWorkerId,
                Math.Clamp(poolOptions.Threads, 1, Environment.ProcessorCount),
                workerState
            );

            return Results.Ok(new ServerStatusResponse(
                Environment.MachineName,
                Environment.ProcessorCount,
                $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                (DateTime.UtcNow - serverStart).ToString(@"d\.hh\:mm\:ss"),
                activeSearches,
                Directory.GetFiles(jamlDir, "*.jaml").Length,
                string.IsNullOrWhiteSpace(configuredPoolUrl) ? "https://www.seedfinder.app" : configuredPoolUrl,
                workerStatus
            ));
        }).WithName("GetStatus");

        app.MapFallbackToFile("/index.html");

        return app;
    }
}
