using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Motely.API;

/// <summary>
/// Centralized path resolver for Motely directories.
/// Uses ASP.NET Core ContentRootPath as the base, with optional config overrides.
/// </summary>
public static class MotelyPaths
{
    private static string _contentRoot = Directory.GetCurrentDirectory();
    private static string? _jamlFiltersOverride;
    private static string? _seedSourcesOverride;
    private static string? _searchResultsOverride;

    /// <summary>
    /// Gets the content root path (typically the repo root).
    /// </summary>
    public static string ContentRoot => _contentRoot;

    /// <summary>
    /// Gets the directory for JAML filter files.
    /// Defaults to ContentRoot/JamlFilters, can be overridden via config.
    /// </summary>
    public static string JamlFiltersDir => ResolvePath(_jamlFiltersOverride, "JamlFilters");

    /// <summary>
    /// Gets the directory for seed source files (txt, csv, db).
    /// Defaults to ContentRoot/SeedSources, can be overridden via config.
    /// </summary>
    public static string SeedSourcesDir => ResolvePath(_seedSourcesOverride, "SeedSources");

    /// <summary>
    /// Gets the directory for search result databases and metadata.
    /// Defaults to ContentRoot/SearchResults, can be overridden via config.
    /// </summary>
    public static string SearchResultsDir => ResolvePath(_searchResultsOverride, "SearchResults");

    /// <summary>
    /// Initializes MotelyPaths with the web host environment and configuration.
    /// Should be called once at application startup.
    /// </summary>
    public static void Initialize(IWebHostEnvironment env, IConfiguration? config = null)
    {
        _contentRoot = env.ContentRootPath;

        if (config != null)
        {
            _jamlFiltersOverride = config["Motely:Paths:JamlFiltersDir"];
            _seedSourcesOverride = config["Motely:Paths:SeedSourcesDir"];
            _searchResultsOverride = config["Motely:Paths:SearchResultsDir"];
        }

        // Ensure directories exist
        Directory.CreateDirectory(JamlFiltersDir);
        Directory.CreateDirectory(SeedSourcesDir);
        Directory.CreateDirectory(SearchResultsDir);
    }

    /// <summary>
    /// Resolves a path: if override is provided and is absolute, use it;
    /// if override is relative, combine with ContentRoot;
    /// otherwise use default relative to ContentRoot.
    /// </summary>
    private static string ResolvePath(string? overridePath, string defaultSubDir)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            // If override is an absolute path, use it as-is
            if (Path.IsPathRooted(overridePath))
            {
                return overridePath;
            }
            // If override is relative, combine with ContentRoot
            return Path.Combine(_contentRoot, overridePath);
        }

        // Default: combine ContentRoot with default subdirectory
        return Path.Combine(_contentRoot, defaultSubDir);
    }
}
