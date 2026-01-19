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
    /// <exception cref="InvalidOperationException">Thrown when a configured path points to a sensitive system directory</exception>
    public static void Initialize(IWebHostEnvironment env, IConfiguration? config = null)
    {
        _contentRoot = env.ContentRootPath;

        if (config != null)
        {
            _jamlFiltersOverride = ValidateConfiguredPath(config["Motely:Paths:JamlFiltersDir"], "JamlFiltersDir");
            _seedSourcesOverride = ValidateConfiguredPath(config["Motely:Paths:SeedSourcesDir"], "SeedSourcesDir");
            _searchResultsOverride = ValidateConfiguredPath(config["Motely:Paths:SearchResultsDir"], "SearchResultsDir");
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
    /// Validates a configured path to ensure it doesn't point to sensitive system directories.
    /// </summary>
    /// <param name="configuredPath">The path from configuration</param>
    /// <param name="pathName">The name of the path configuration (for error messages)</param>
    /// <returns>The validated path, or null if the path is null/empty</returns>
    /// <exception cref="InvalidOperationException">Thrown when the path points to a sensitive system directory</exception>
    private static string? ValidateConfiguredPath(string? configuredPath, string pathName)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return null;

        // For absolute paths, ensure they don't point to sensitive system directories
        if (Path.IsPathRooted(configuredPath))
        {
            var normalizedPath = Path.GetFullPath(configuredPath);
            
            if (IsSensitiveSystemPath(normalizedPath))
            {
                throw new InvalidOperationException(
                    $"Security: Configured path '{pathName}' points to a sensitive system directory: {normalizedPath}. " +
                    "Configured paths must not point to system directories like /etc, /sys, /proc, C:\\Windows, etc.");
            }
        }

        return configuredPath;
    }

    /// <summary>
    /// Checks if a path points to a sensitive system directory.
    /// </summary>
    private static bool IsSensitiveSystemPath(string fullPath)
    {
        // Normalize path separators for consistent comparison
        var normalizedPath = fullPath.Replace('\\', '/').TrimEnd('/');
        
        // Common sensitive Unix/Linux directories
        string[] unixSensitivePaths = new[]
        {
            "/etc", "/sys", "/proc", "/dev", "/boot", "/root",
            "/bin", "/sbin", "/usr/bin", "/usr/sbin", "/lib", "/lib64"
        };
        
        // Common sensitive Windows directories
        string[] windowsSensitivePaths = new[]
        {
            "C:/Windows", "C:/Windows/System32", "C:/Program Files",
            "C:/Program Files (x86)", "C:/ProgramData"
        };
        
        // Check Unix paths (case-sensitive)
        foreach (var sensitivePath in unixSensitivePaths)
        {
            if (normalizedPath.Equals(sensitivePath, StringComparison.Ordinal) ||
                normalizedPath.StartsWith(sensitivePath + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }
        
        // Check Windows paths (case-insensitive)
        foreach (var sensitivePath in windowsSensitivePaths)
        {
            if (normalizedPath.Equals(sensitivePath, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(sensitivePath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
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
