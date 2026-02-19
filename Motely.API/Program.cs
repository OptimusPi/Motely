using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Motely;
using Motely.Filters;
using Motely.Executors;

namespace Motely.API;

// DTOs
public record CreateFilterRequest(string Name, string Content);
public record UpdateFilterRequest(string Content);
public record FilterInfo(string Name, DateTime LastModified);
public record StartSearchRequest(string FilterName, string? Seed = null, int ThreadCount = 4, int BatchSize = 3);
public record SearchInfo(string Id, string FilterName, bool IsCompleted, long TotalSeedsSearched, long MatchingSeeds, TimeSpan ElapsedTime);
public record SearchResult(string Seed, int Score);

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddOpenApi();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Motely API", Version = "v1" });
        });

        // Add CORS for Blueprint frontend
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors();

        // Serve static files (Blueprint frontend)
        app.UseStaticFiles();

        // JAML storage directory
        var jamlDir = Path.Combine(Directory.GetCurrentDirectory(), "jaml-filters");
        Directory.CreateDirectory(jamlDir);

        // In-memory search storage
        var searches = new ConcurrentDictionary<string, IMotelySearch>();

        // JAML Filter Management
        app.MapGet("/api/filters", () =>
        {
            var filters = Directory.GetFiles(jamlDir, "*.jaml")
                .Select(file => new FilterInfo(
                    Path.GetFileNameWithoutExtension(file),
                    File.GetLastWriteTime(file)
                ))
                .OrderBy(f => f.Name);
            
            return Results.Ok(filters);
        })
        .WithName("GetFilters")
        .WithOpenApi();

        app.MapGet("/api/filters/{name}", (string name) =>
        {
            var filePath = Path.Combine(jamlDir, $"{name}.jaml");
            if (!File.Exists(filePath))
                return Results.NotFound();
            
            var content = File.ReadAllText(filePath);
            return Results.Text(content, "text/yaml");
        })
        .WithName("GetFilter")
        .WithOpenApi();

        app.MapPost("/api/filters", (CreateFilterRequest request) =>
        {
            var filePath = Path.Combine(jamlDir, $"{request.Name}.jaml");
            if (File.Exists(filePath))
                return Results.Conflict($"Filter '{request.Name}' already exists");
            
            File.WriteAllText(filePath, request.Content);
            return Results.Created($"/api/filters/{request.Name}", new FilterInfo(request.Name, DateTime.Now));
        })
        .WithName("CreateFilter")
        .WithOpenApi();

        app.MapPut("/api/filters/{name}", (string name, UpdateFilterRequest request) =>
        {
            var filePath = Path.Combine(jamlDir, $"{name}.jaml");
            if (!File.Exists(filePath))
                return Results.NotFound();
            
            File.WriteAllText(filePath, request.Content);
            return Results.Ok(new FilterInfo(name, DateTime.Now));
        })
        .WithName("UpdateFilter")
        .WithOpenApi();

        app.MapDelete("/api/filters/{name}", (string name) =>
        {
            var filePath = Path.Combine(jamlDir, $"{name}.jaml");
            if (!File.Exists(filePath))
                return Results.NotFound();
            
            File.Delete(filePath);
            return Results.NoContent();
        })
        .WithName("DeleteFilter")
        .WithOpenApi();

        // Search Management
        app.MapPost("/api/search/start", (StartSearchRequest request) =>
        {
            var filterPath = Path.Combine(jamlDir, $"{request.FilterName}.jaml");
            if (!File.Exists(filterPath))
                return Results.NotFound($"Filter '{request.FilterName}' not found");
            
            var searchId = Guid.NewGuid().ToString();
            
            try
            {
                var jamlContent = File.ReadAllText(filterPath);
                if (!Filters.JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
                    return Results.BadRequest($"Invalid JAML configuration: {error}");
                
                var settings = Filters.JamlSearchBuilder.CreateSettings(config);
                var search = settings.Start();
                
                searches[searchId] = search;
                
                return Results.Ok(new SearchInfo(searchId, request.FilterName, false, 0, 0, TimeSpan.Zero));
            }
            catch (Exception ex)
            {
                return Results.BadRequest($"Failed to start search: {ex.Message}");
            }
        })
        .WithName("StartSearch")
        .WithOpenApi();

        app.MapGet("/api/search/{id}", (string id) =>
        {
            if (!searches.TryGetValue(id, out var search))
                return Results.NotFound();
            
            var context = new MotelySearchContext(search, id, id);
            
            return Results.Ok(new SearchInfo(
                id, 
                "Unknown", 
                context.IsCompleted, 
                context.TotalSeedsSearched, 
                context.MatchingSeeds, 
                context.ElapsedTime
            ));
        })
        .WithName("GetSearch")
        .WithOpenApi();

        app.MapGet("/api/search/{id}/results", (string id) =>
        {
            if (!searches.TryGetValue(id, out var search))
                return Results.NotFound();
            
            var context = new MotelySearchContext(search, id, id);
            var results = context.GetTopResults(1067); // The meme number!
            
            var searchResults = results.Select(r => new SearchResult(r.Seed, r.Score)).ToList();
            
            return Results.Ok(searchResults);
        })
        .WithName("GetSearchResults")
        .WithOpenApi();

        app.MapPost("/api/search/{id}/stop", (string id) =>
        {
            if (!searches.TryGetValue(id, out var search))
                return Results.NotFound();
            
            // Note: We'd need to add cancellation support to MotelySearch
            // For now, just mark as completed
            searches.TryRemove(id, out _);
            
            return Results.Ok();
        })
        .WithName("StopSearch")
        .WithOpenApi();

        // Fallback to Blueprint frontend
        app.MapFallbackToFile("/index.html");

        app.Run();
    }
}
