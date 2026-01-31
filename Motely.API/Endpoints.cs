using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Motely;
using Motely.API;
using Motely.API.Models;
using Motely.API.Services;
using Motely.Executors;

namespace Motely.API;

public static class Endpoints
{
    public static IResult GetFilters()
    {
        try
        {
            var filters = FilterService.LoadFiltersFromDisk(
                MotelyPaths.JamlFiltersDir,
                cfg => false
            );
            return Results.Ok(filters);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error loading filters: {ex.Message}");
        }
    }

    public static IResult GetSeedSources()
    {
        var results = new List<object>
        {
            new
            {
                key = "all",
                label = "All Seeds",
                kind = "builtin",
            },
        };

        var seedSourcesDir = MotelyPaths.SeedSourcesDir;
        if (Directory.Exists(seedSourcesDir))
        {
            foreach (
                var file in Directory
                    .GetFiles(seedSourcesDir, "*.*")
                    .Where(f => f.EndsWith(".db") || f.EndsWith(".txt") || f.EndsWith(".csv"))
                    .Select(Path.GetFileName)
                    .Where(f => f != null)
                    .Cast<string>()
            )
            {
                var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                var (category, displayName) = SeedSourceHelper.ParseCategoryFromFileName(file);
                results.Add(
                    new
                    {
                        key = $"{ext}:{file}",
                        label = displayName,
                        kind = ext,
                        fileName = file,
                    }
                );
            }
        }

        return Results.Ok(new { sources = results });
    }

    public static IResult GetSearches()
    {
        var allSearches = MultiSearchManager.Instance.GetAllStatuses();
        var searches = allSearches
            .Select(s => new
            {
                id = s.SearchId,
                searchId = s.SearchId,
                filterName = s.FilterName,
                deck = s.Deck,
                stake = s.Stake,
                seedsSearched = s.SeedsSearched,
                seedsPerSecond = s.SeedsPerSecond,
                resultsFound = s.TotalMatches,
                isRunning = s.IsRunning,
            })
            .ToList();

        return Results.Ok(new { searches });
    }

    public static async Task<IResult> StartSearch(HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<SearchStartRequest>();
        if (request?.FilterId == null)
            return Results.BadRequest();

        var filterJaml = FilterService.GetFilterJaml(request.FilterId);
        if (string.IsNullOrEmpty(filterJaml))
            return Results.BadRequest("Filter not found");

        var (_, searchId) = await MultiSearchManager.Instance.StartSearchAsync(
            filterJaml,
            request.Deck,
            request.Stake,
            threads: 1
        );

        return Results.Ok(new { searchId });
    }

    public static IResult GetSearch(string id)
    {
        var (results, progress) = MultiSearchManager.Instance.GetSearchStatusWithResults(id);
        return Results.Ok(new { results, progress });
    }

    public static IResult StopSearch(string id)
    {
        MultiSearchManager.Instance.Stop(id);
        return Results.Ok();
    }

    public static async Task<IResult> SaveFilter(string id, HttpRequest req)
    {
        var request = await req.ReadFromJsonAsync<FilterSaveRequest>();
        if (request?.FilterJaml == null)
            return Results.BadRequest();

        var jamlFiltersDir = MotelyPaths.JamlFiltersDir;
        Directory.CreateDirectory(jamlFiltersDir);

        // Use id from route, or extract name from JAML
        string? name = id;
        if (
            JamlConfigLoader.TryLoadFromJamlString(request.FilterJaml, out var cfg, out _)
            && cfg != null
        )
        {
            name = cfg.Name ?? id;
        }

        var fileName = $"{name}.jaml";
        var fullPath = Path.Combine(jamlFiltersDir, fileName);
        await File.WriteAllTextAsync(fullPath, request.FilterJaml);

        return Results.Ok(new { filePath = fileName });
    }

    public static IResult DeleteFilter(string id)
    {
        var safeName = Path.GetFileName(id);
        var fullPath = Path.Combine(MotelyPaths.JamlFiltersDir, safeName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Results.Ok();
        }

        return Results.NotFound();
    }
}