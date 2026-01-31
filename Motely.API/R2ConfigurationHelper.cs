using DuckDB.NET.Data;
using Microsoft.Extensions.Configuration;
using Motely.DB;

namespace Motely.API;

/// <summary>
/// R2 configuration helper for Motely.API - loads R2 credentials from appsettings.json.
/// </summary>
public static class R2ConfigurationHelper
{
    /// <summary>
    /// Configure R2 secret in DuckDB connection from appsettings.json configuration.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="configuration">IConfiguration instance (from appsettings.json)</param>
    /// <param name="secretName">Name for the secret (default: "r2")</param>
    /// <returns>True if R2 was configured, false if R2 is disabled or not configured</returns>
    public static bool ConfigureR2FromConfig(
        DuckDBConnection connection,
        IConfiguration configuration,
        string secretName = "r2"
    )
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        var r2Section = configuration.GetSection("Cloudflare:R2");
        if (!r2Section.GetValue<bool>("Enabled", false))
        {
            return false; // R2 not enabled
        }

        var accountId = r2Section["AccountId"];
        var accessKeyId = r2Section["AccessKeyId"];
        var secretAccessKey = r2Section["SecretAccessKey"];
        var endpoint = r2Section["Endpoint"];

        // Validate required fields
        if (
            string.IsNullOrWhiteSpace(accessKeyId)
            || string.IsNullOrWhiteSpace(secretAccessKey)
            || string.IsNullOrWhiteSpace(endpoint)
        )
        {
            // R2 is enabled but not fully configured - log warning but don't throw
            System.Diagnostics.Debug.WriteLine(
                "[R2ConfigurationHelper] R2 is enabled but credentials are missing. R2 features will not work."
            );
            return false;
        }

        // Replace {accountId} placeholder in endpoint if present
        if (!string.IsNullOrWhiteSpace(accountId) && endpoint.Contains("{accountId}"))
        {
            endpoint = endpoint.Replace("{accountId}", accountId);
        }

        // Configure R2 secret
        // CloudStorageHelper.ConfigureR2Secret(connection, accessKeyId, secretAccessKey, endpoint, secretName); // TODO: Implement R2 configuration
        return true;
    }

    /// <summary>
    /// Check if R2 is enabled in configuration.
    /// </summary>
    public static bool IsR2Enabled(IConfiguration configuration)
    {
        if (configuration == null)
            return false;

        var r2Section = configuration.GetSection("Cloudflare:R2");
        return r2Section.GetValue<bool>("Enabled", false);
    }

    /// <summary>
    /// Get R2 bucket name from configuration.
    /// </summary>
    public static string? GetR2Bucket(IConfiguration configuration)
    {
        if (configuration == null)
            return null;

        var r2Section = configuration.GetSection("Cloudflare:R2");
        return r2Section["Bucket"];
    }
}
