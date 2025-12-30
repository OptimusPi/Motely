using System.Text.Json;
using Motely;

namespace Motely.API.Services;

public static class FilterService
{
    public static string GetFilterJaml(string? filterId)
    {
        if (string.IsNullOrEmpty(filterId))
            return string.Empty;
            
        var filterPath = ResolveFilterPath($"{filterId}.jaml");
        if (string.IsNullOrEmpty(filterPath) || !File.Exists(filterPath))
            return string.Empty;
            
        return File.ReadAllText(filterPath);
    }

    private static string? ResolveFilterPath(string fileName)
    {
        // Try AppDomain.BaseDirectory first (for deployed scenarios)
        var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Filters", fileName);
        if (File.Exists(basePath))
            return basePath;

        // Search upward from current directory (for development)
        foreach (var directory in EnumerateDirectoriesUpwards(Directory.GetCurrentDirectory()))
        {
            var candidate = Path.Combine(directory, "Filters", fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        // Try relative to current directory
        var relativePath = Path.Combine("Filters", fileName);
        if (File.Exists(relativePath))
            return relativePath;

        return null;
    }

    private static IEnumerable<string> EnumerateDirectoriesUpwards(string startDirectory)
    {
        var current = startDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            yield return current;

            var parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }
    }

    public static List<object> LoadFiltersFromDisk(string filtersPath, Func<global::Motely.Filters.MotelyJsonConfig?, bool> hasErraticFilters)
    {
        var filters = new List<object>();
        if (!Directory.Exists(filtersPath)) return filters;
        
        var allFiles = Directory.GetFiles(filtersPath, "*.jaml");
        
        var filterFiles = allFiles
            .GroupBy(f => Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (var file in filterFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            string filterJaml;
            try
            {
                filterJaml = File.ReadAllText(file);
            }
            catch
            {
                continue;
            }

            var deck = "Red";
            var stake = "White";
            var columns = new List<string> { "seed", "score" };
            string? displayName = name;
            string? author = null;
            string? jamlErr;
            if (JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out jamlErr) && cfg != null)
            {
                if (!string.IsNullOrWhiteSpace(cfg.Name))
                    displayName = cfg.Name;
                if (!string.IsNullOrWhiteSpace(cfg.Author))
                    author = cfg.Author;
                if (!string.IsNullOrWhiteSpace(cfg.Deck)) deck = cfg.Deck;
                if (!string.IsNullOrWhiteSpace(cfg.Stake)) stake = cfg.Stake;
                
                if (string.IsNullOrWhiteSpace(cfg.Deck) && hasErraticFilters(cfg))
                    deck = "Erratic";

                try
                {
                    columns = cfg.GetColumnNames();
                }
                catch
                {
                    columns = new List<string> { "seed", "score" };
                }
            }

            var filterName = displayName ?? "UnknownFilter";
            var searchId = $"{SearchManager.SanitizeFilterFileStem(filterName)}_{deck}_{stake}";
            var fileName = Path.GetFileName(file);
            var filterId = Path.GetFileNameWithoutExtension(fileName);

            filters.Add(new
            {
                id = filterId,
                name = displayName,
                author = author ?? "Default",
                filterId,
                filterJaml,
                filePath = fileName,
                searchId,
                columns
            });
        }
        
        return filters
            .OrderBy(f =>
            {
                var nameProp = f.GetType().GetProperty("name");
                return nameProp?.GetValue(f) as string ?? "";
            }, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

