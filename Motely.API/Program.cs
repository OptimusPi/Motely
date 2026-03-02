using System.Collections.Concurrent;
using System.IO;

namespace Motely.API;

public record CreateFilterRequest(string Name, string Content);
public record UpdateFilterRequest(string Content);
public record FilterInfo(string Name, DateTime LastModified);

public record StartSearchRequest(
    string FilterName,
    string? Seed = null,
    int ThreadCount = 4,
    int BatchSize = 3
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

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors();
        app.UseStaticFiles();

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

        // ── Search ──

        app.MapPost("/api/search/start", (StartSearchRequest req) =>
        {
            var filterPath = Path.Combine(jamlDir, $"{req.FilterName}.jaml");
            if (!File.Exists(filterPath))
                return Results.NotFound($"Filter '{req.FilterName}' not found");

            try
            {
                var jaml = File.ReadAllText(filterPath);
                if (!Filters.JamlConfigLoader.TryLoad(jaml, out var config, out var error))
                    return Results.BadRequest($"Invalid JAML: {error}");

                var search = Filters.JamlSearchBuilder.CreateSettings(config).Start();
                var id = Guid.NewGuid().ToString();
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

        app.MapFallbackToFile("/index.html");

        return app;
    }
}
