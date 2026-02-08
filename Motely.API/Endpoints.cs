using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Motely;
using Motely.API;
using Motely.API.Models;
using Motely.Executors;
using Motely.Repository;

namespace Motely.API;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public static class Endpoints
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static IResult GetFilters(ILibraryMetadata library)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static IResult GetSeedSources()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static IResult GetSearches()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<IResult> StartSearch(HttpRequest req, ILibraryMetadata library)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static IResult GetSearch(string id)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var (results, progress) = MultiSearchManager.Instance.GetSearchStatusWithResults(id);
        return Results.Ok(new { results, progress });
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static IResult StopSearch(string id)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        MultiSearchManager.Instance.Stop(id);
        return Results.Ok();
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static async Task<IResult> SaveFilter(string id, HttpRequest req)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static IResult DeleteFilter(string id)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
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
