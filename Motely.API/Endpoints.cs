using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Motely;
using Motely.API;
using Motely.API.Models;
using Motely.Executors;
using Motely.Repository;

namespace Motely.API;

public static class Endpoints
{
    /// <summary>Gets all available filters</summary>
    /// <param name="library">Library metadata provider</param>
    /// <returns>List of filters with metadata</returns>
    public static IResult GetFilters(ILibraryMetadata library)
    {
        try
        {
            var list = library.GetLibraryMetadata();
            var filters = list.Select(f => new
            {
                id = f.Id,
                name = f.Name,
                author = f.Author,
                filterId = f.Id,
                filePath = f.FilePath,
                searchId = f.SearchId,
                columns = f.Columns,
            }).ToList();
            return Results.Ok(filters);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error loading filters: {ex.Message}");
        }
    }

    /// <summary>Gets all available seed sources</summary>
    /// <returns>List of seed sources</returns>
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

    /// <summary>Gets all active searches</summary>
    /// <returns>List of searches with status</returns>
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

    /// <summary>Starts a new search</summary>
    /// <param name="req">HTTP request containing search parameters</param>
    /// <param name="library">Library metadata provider</param>
    /// <returns>Search ID</returns>
    public static async Task<IResult> StartSearch(HttpRequest req, ILibraryMetadata library)
    {
        var request = await req.ReadFromJsonAsync<SearchStartRequest>();
        if (request?.FilterId == null)
            return Results.BadRequest();

        var filterJaml = library.GetFilterJaml(request.FilterId);
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

    /// <summary>Gets search status and results</summary>
    /// <param name="id">Search ID</param>
    /// <returns>Search results and progress</returns>
    public static IResult GetSearch(string id)
    {
        var (results, progress) = MultiSearchManager.Instance.GetSearchStatusWithResults(id);
        return Results.Ok(new { results, progress });
    }

    /// <summary>Stops a running search</summary>
    /// <param name="id">Search ID</param>
    /// <returns>Empty result</returns>
    public static IResult StopSearch(string id)
    {
        MultiSearchManager.Instance.Stop(id);
        return Results.Ok();
    }

    /// <summary>Saves a filter to disk</summary>
    /// <param name="id">Filter ID from route</param>
    /// <param name="req">HTTP request containing filter JAML</param>
    /// <returns>File path of saved filter</returns>
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

    /// <summary>Deletes a filter from disk</summary>
    /// <param name="id">Filter ID</param>
    /// <returns>Empty result or not found</returns>
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
