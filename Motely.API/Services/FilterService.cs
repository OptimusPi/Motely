using System.Text.Json;
using Motely;

namespace Motely.API.Services;

public static class FilterService
{
    public static string GetFilterJaml(string? filterId)
    {
        if (string.IsNullOrEmpty(filterId))
            return string.Empty;
            
        var filterPath = Path.Combine("Filters", $"{filterId}.jaml");
        if (!File.Exists(filterPath))
            return string.Empty;
            
        return File.ReadAllText(filterPath);
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

