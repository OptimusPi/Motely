using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using Motely.Filters;

namespace Motely.API;

public record CreateFilterRequest(string Name, string Content);
public record UpdateFilterRequest(string Content);
public record FilterInfo(string Name, DateTime LastModified);

public record StartSearchRequest(
    string FilterName,
    string? Seed = null,
    string? Keyword = null,
    string? Keywords = null,
    string? Padding = null,
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
public record ServerStatusResponse(string Hostname, int ProcessorCount, string OS, string Runtime, string MotelyVersion, string Uptime, IEnumerable<ActiveSearchInfo> ActiveSearches, int FilterCount, WorkerStatusResponse? Worker);
public record WorkerStatusResponse(bool Running, int? Pid, string? PoolUrl, int Threads, string? WorkerId, string? Error);
public record StartWorkerRequest(string PoolUrl, int Threads = -1, string? WorkerId = null);

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
[JsonSerializable(typeof(ServerStatusResponse))]
[JsonSerializable(typeof(WorkerStatusResponse))]
[JsonSerializable(typeof(StartWorkerRequest))]
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

        // PoolWorkerOptions removed - standalone AOT worker doesn't need API config

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseCors();
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

        // Worker process management
        var workerProcess = new ConcurrentBag<Process>();
        var workerConfig = new ConcurrentBag<(string PoolUrl, int Threads, string WorkerId)>();

        app.Lifetime.ApplicationStopping.Register(() =>
        {
            foreach (var handle in searches.Values)
            {
                try { handle.Cancellation.Cancel(); } catch { }
                try { handle.Search.Cancel(); } catch { }
            }
            // Kill worker on shutdown
            foreach (var proc in workerProcess)
            {
                try { proc.Kill(); } catch { }
            }
        });

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

                var searchOpts = new SearchOptionsDto
                {
                    SpecificSeed = string.IsNullOrWhiteSpace(req.Seed) ? null : req.Seed,
                    Keyword = string.IsNullOrWhiteSpace(req.Keyword) ? null : req.Keyword,
                    Keywords = string.IsNullOrWhiteSpace(req.Keywords) ? null
                        : req.Keywords.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
                    Padding = string.IsNullOrWhiteSpace(req.Padding) ? null : req.Padding,
                    Palindrome = req.Palindrome ? true : null,
                };
                var (_, modeError) = settings.ApplySearchMode(searchOpts);
                if (modeError != null)
                    return Results.BadRequest(modeError);

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

        var serverStart = DateTime.UtcNow;

        app.MapGet("/api/status", () =>
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

            var motelyVer = typeof(MotelyCore).Assembly
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "unknown";

            WorkerStatusResponse? workerStatus = null;
            if (workerProcess.TryTake(out var proc) && !proc.HasExited)
            {
                workerProcess.Add(proc); // Put it back
                var cfg = workerConfig.ToArray().FirstOrDefault();
                workerStatus = new WorkerStatusResponse(true, proc.Id, cfg.PoolUrl, cfg.Threads, cfg.WorkerId, null);
            }
            else
            {
                // Clean up dead process
                workerProcess.Clear();
                var cfg = workerConfig.ToArray().FirstOrDefault();
                if (!string.IsNullOrEmpty(cfg.PoolUrl))
                {
                    workerStatus = new WorkerStatusResponse(false, null, cfg.PoolUrl, cfg.Threads, cfg.WorkerId, "Worker stopped");
                }
            }

            return Results.Ok(new ServerStatusResponse(
                Environment.MachineName,
                Environment.ProcessorCount,
                $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}",
                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                motelyVer,
                (DateTime.UtcNow - serverStart).ToString(@"d\.hh\:mm\:ss"),
                activeSearches,
                Directory.GetFiles(jamlDir, "*.jaml").Length,
                workerStatus
            ));
        }).WithName("GetStatus");

        // Worker management endpoints
        app.MapPost("/api/worker/start", (StartWorkerRequest req) =>
        {
            // Check if already running
            if (workerProcess.TryTake(out var existing) && !existing.HasExited)
            {
                workerProcess.Add(existing);
                return Results.Conflict("Worker already running");
            }
            workerProcess.Clear();

            // Find MotelyWorker executable
            var exeName = OperatingSystem.IsWindows() ? "MotelyWorker.exe" : "MotelyWorker";
            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, exeName),
                Path.Combine(AppContext.BaseDirectory, "..", exeName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", exeName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Motely.DistributedWorker", "bin", "Release", exeName),
            };

            string? workerPath = null;
            foreach (var p in searchPaths)
            {
                var full = Path.GetFullPath(p);
                if (File.Exists(full))
                {
                    workerPath = full;
                    break;
                }
            }

            if (workerPath == null)
                return Results.NotFound("MotelyWorker executable not found. Build with: dotnet publish Motely.DistributedWorker -c Release");

            var threads = req.Threads < 1 ? Environment.ProcessorCount : Math.Clamp(req.Threads, 1, Environment.ProcessorCount);
            var workerId = string.IsNullOrWhiteSpace(req.WorkerId) ? $"{Environment.MachineName}-{Environment.ProcessId}" : req.WorkerId;

            var startInfo = new ProcessStartInfo
            {
                FileName = workerPath,
                Arguments = $"--pool \"{req.PoolUrl}\" --threads {threads} --worker-id \"{workerId}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            try
            {
                var proc = Process.Start(startInfo);
                if (proc == null)
                    return Results.Problem("Failed to start worker process");

                workerProcess.Add(proc);
                workerConfig.Clear();
                workerConfig.Add((req.PoolUrl, threads, workerId));

                // Log output
                _ = Task.Run(async () =>
                {
                    while (!proc.HasExited)
                    {
                        var line = await proc.StandardOutput.ReadLineAsync();
                        if (line != null) Console.WriteLine($"[Worker] {line}");
                    }
                });

                return Results.Ok(new WorkerStatusResponse(true, proc.Id, req.PoolUrl, threads, workerId, null));
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to start worker: {ex.Message}");
            }
        }).WithName("StartWorker");

        app.MapPost("/api/worker/stop", () =>
        {
            if (!workerProcess.TryTake(out var proc) || proc.HasExited)
            {
                workerProcess.Clear();
                return Results.Ok(new WorkerStatusResponse(false, null, null, 0, null, "Worker not running"));
            }

            try
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
            catch { }

            var cfg = workerConfig.ToArray().FirstOrDefault();
            workerProcess.Clear();
            workerConfig.Clear();

            return Results.Ok(new WorkerStatusResponse(false, proc.Id, cfg.PoolUrl, cfg.Threads, cfg.WorkerId, "Stopped"));
        }).WithName("StopWorker");

        app.MapGet("/api/worker/status", () =>
        {
            if (workerProcess.TryTake(out var proc))
            {
                if (!proc.HasExited)
                {
                    workerProcess.Add(proc);
                    var cfg = workerConfig.ToArray().FirstOrDefault();
                    return Results.Ok(new WorkerStatusResponse(true, proc.Id, cfg.PoolUrl, cfg.Threads, cfg.WorkerId, null));
                }
                workerProcess.Clear();
            }

            return Results.Ok(new WorkerStatusResponse(false, null, null, 0, null, null));
        }).WithName("GetWorkerStatus");

        app.MapFallbackToFile("/index.html");

        return app;
    }
}

