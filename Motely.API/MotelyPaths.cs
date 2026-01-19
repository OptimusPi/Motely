using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Motely.API;

/// <summary>
/// Centralized path resolver for Motely directories.
/// Uses ASP.NET Core ContentRootPath as the base, with optional config overrides.
/// IMPORTANT: Must call Initialize(IWebHostEnvironment, IConfiguration?) at application startup
/// before accessing any path properties. Accessing paths before initialization will throw InvalidOperationException.
/// </summary>
public static class MotelyPaths
{
    private static volatile string _contentRoot = Directory.GetCurrentDirectory();
    private static volatile string? _jamlFiltersOverride;
    private static volatile string? _seedSourcesOverride;
    private static volatile string? _searchResultsOverride;
    private static volatile bool _isInitialized = false;

    /// <summary>
    /// Gets the content root path (typically the repo root).
    /// </summary>
    public static string ContentRoot
    {
        get
        {
            EnsureInitialized();
            return _contentRoot;
        }
    }

    /// <summary>
    /// Gets the directory for JAML filter files.
    /// Defaults to ContentRoot/JamlFilters, can be overridden via config.
    /// </summary>
    public static string JamlFiltersDir
    {
        get
        {
            EnsureInitialized();
            return ResolvePath(_jamlFiltersOverride, "JamlFilters");
        }
    }

    /// <summary>
    /// Gets the directory for seed source files (txt, csv, db).
    /// Defaults to ContentRoot/SeedSources, can be overridden via config.
    /// </summary>
    public static string SeedSourcesDir
    {
        get
        {
            EnsureInitialized();
            return ResolvePath(_seedSourcesOverride, "SeedSources");
        }
    }

    /// <summary>
    /// Gets the directory for search result databases and metadata.
    /// Defaults to ContentRoot/SearchResults, can be overridden via config.
    /// </summary>
    public static string SearchResultsDir
    {
        get
        {
            EnsureInitialized();
            return ResolvePath(_searchResultsOverride, "SearchResults");
        }
    }

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

        // Ensure directories exist (using ResolvePath directly to avoid EnsureInitialized check)
        Directory.CreateDirectory(ResolvePath(_jamlFiltersOverride, "JamlFilters"));
        Directory.CreateDirectory(ResolvePath(_seedSourcesOverride, "SeedSources"));
        Directory.CreateDirectory(ResolvePath(_searchResultsOverride, "SearchResults"));

        // Mark as initialized after all setup is complete
        _isInitialized = true;
    }

    /// <summary>
    /// Ensures that Initialize has been called before accessing path properties.
    /// Thread-safe: uses volatile _isInitialized field for proper memory ordering.
    /// Initialization should complete before any concurrent path access begins.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                "MotelyPaths.Initialize must be called before accessing path properties. " +
                "Call MotelyPaths.Initialize(IWebHostEnvironment, IConfiguration?) at application startup.");
        }
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
