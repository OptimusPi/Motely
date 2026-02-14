using Motely;
using Motely.Executors;
using Motely.Repository;

namespace Motely.API.Services;

/// <summary>
/// File-based implementation of filter library metadata (JamlFilters directory).
/// API-only: uses Orchestration for JAML parsing. DB-backed impl can live in Motely.DB.
/// </summary>
public sealed class FilterFileLibrary : ILibraryMetadata
{
    private readonly string _jamlFiltersDir;
    private readonly Func<global::Motely.Filters.MotelyJsonConfig?, bool> _hasErraticFilters;

    /// <summary>Initializes a new filter file library</summary>
    /// <param name="jamlFiltersDir">Directory containing JAML filter files</param>
    /// <param name="hasErraticFilters">Optional function to check for Erratic deck filters</param>
    public FilterFileLibrary(string jamlFiltersDir, Func<global::Motely.Filters.MotelyJsonConfig?, bool>? hasErraticFilters = null)
    {
        _jamlFiltersDir = jamlFiltersDir ?? throw new ArgumentNullException(nameof(jamlFiltersDir));
        _hasErraticFilters = hasErraticFilters ?? (_ => false);
    }

    /// <inheritdoc />
    public IReadOnlyList<FilterMetadata> GetLibraryMetadata()
    {
        var list = new List<FilterMetadata>();
        if (!Directory.Exists(_jamlFiltersDir))
            return list;

        var files = Directory
            .GetFiles(_jamlFiltersDir, "*.jaml")
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        foreach (var file in files)
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

            if (JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var cfg, out _) && cfg != null)
            {
                if (!string.IsNullOrWhiteSpace(cfg.Name))
                    displayName = cfg.Name;
                if (!string.IsNullOrWhiteSpace(cfg.Author))
                    author = cfg.Author;
                if (!string.IsNullOrWhiteSpace(cfg.Deck))
                    deck = cfg.Deck;
                if (!string.IsNullOrWhiteSpace(cfg.Stake))
                    stake = cfg.Stake;
                if (string.IsNullOrWhiteSpace(cfg.Deck) && _hasErraticFilters(cfg))
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
            var searchId = $"{MultiSearchManager.SanitizeFilterFileStem(filterName)}_{deck}_{stake}";
            var fileName = Path.GetFileName(file);

            list.Add(new FilterMetadata(
                Id: name,
                Name: displayName ?? name,
                Author: author ?? "Default",
                FilePath: fileName,
                SearchId: searchId,
                Columns: columns
            ));
        }

        return list.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <inheritdoc />
    public string? GetFilterJaml(string filterId)
    {
        if (string.IsNullOrEmpty(filterId))
            return null;

        var safeName = Path.GetFileNameWithoutExtension(filterId);
        if (string.IsNullOrWhiteSpace(safeName))
            return null;

        foreach (var c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c, '_');

        var filterPath = Path.Combine(_jamlFiltersDir, $"{safeName}.jaml");
        if (!File.Exists(filterPath))
            return null;

        try
        {
            return File.ReadAllText(filterPath);
        }
        catch
        {
            return null;
        }
    }
}
