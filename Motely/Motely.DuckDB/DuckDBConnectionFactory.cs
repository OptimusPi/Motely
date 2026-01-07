#if !BROWSER
using DuckDB.NET.Data;

namespace Motely.DuckDB;

/// <summary>
/// Centralized DuckDB connection factory.
/// This is the SINGLE SOURCE OF TRUTH for DuckDB connection creation.
/// Ensures consistent connection string formatting and configuration.
/// </summary>
public static class DuckDBConnectionFactory
{
    /// <summary>
    /// Create a standard DuckDB connection with default configuration.
    /// </summary>
    /// <param name="dbPath">Path to DuckDB database file</param>
    /// <returns>Opened DuckDB connection</returns>
    public static DuckDBConnection CreateConnection(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("Database path cannot be empty", nameof(dbPath));

        var connectionString = $"Data Source={dbPath}";
        var connection = new DuckDBConnection(connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Create a DuckDB connection with custom configuration.
    /// </summary>
    /// <param name="dbPath">Path to DuckDB database file</param>
    /// <param name="configure">Action to configure the connection after opening</param>
    /// <returns>Opened and configured DuckDB connection</returns>
    public static DuckDBConnection CreateConnectionWithConfig(string dbPath, Action<DuckDBConnection> configure)
    {
        var connection = CreateConnection(dbPath);
        configure?.Invoke(connection);
        return connection;
    }

    /// <summary>
    /// Create a DuckDB connection with DuckLake attached.
    /// This enables concurrent access to the same dataset from multiple processes.
    /// Supports both local paths and remote URLs (R2, S3, HTTPS).
    /// </summary>
    /// <param name="catalogPath">Path to DuckLake catalog file (.ducklake) - can be local or HTTPS URL</param>
    /// <param name="dataPath">Path to DuckLake data directory - can be local or R2/S3 URL</param>
    /// <param name="schemaName">Schema name to attach as (default: "seed_source")</param>
    /// <param name="overrideDataPath">If true, override persisted data path (for remote data)</param>
    /// <param name="r2AccessKeyId">Optional R2 access key ID for remote paths</param>
    /// <param name="r2SecretAccessKey">Optional R2 secret access key for remote paths</param>
    /// <param name="r2Endpoint">Optional R2 endpoint URL for remote paths</param>
    /// <returns>Opened DuckDB connection with DuckLake attached</returns>
    public static DuckDBConnection CreateConnectionWithDuckLake(
        string catalogPath, 
        string dataPath, 
        string schemaName = "seed_source", 
        bool overrideDataPath = false,
        string? r2AccessKeyId = null,
        string? r2SecretAccessKey = null,
        string? r2Endpoint = null)
    {
        // Use in-memory connection and attach DuckLake
        var connection = CreateConnection(":memory:");
        
        // Auto-configure R2 if using remote paths and credentials are provided
        if (!string.IsNullOrWhiteSpace(r2AccessKeyId) && 
            !string.IsNullOrWhiteSpace(r2SecretAccessKey) && 
            !string.IsNullOrWhiteSpace(r2Endpoint) &&
            CloudStorageHelper.IsS3CompatiblePath(dataPath))
        {
            try
            {
                CloudStorageHelper.ConfigureR2Secret(connection, r2AccessKeyId, r2SecretAccessKey, r2Endpoint);
            }
            catch (Exception ex)
            {
                // Log but don't fail - R2 might not be configured, which is OK for local paths
                System.Diagnostics.Debug.WriteLine($"[DuckDBConnectionFactory] R2 configuration failed (non-fatal): {ex.Message}");
            }
        }
        
        DuckLakeHelper.EnsureDuckLakeExtension(connection);
        DuckLakeHelper.AttachDuckLake(connection, catalogPath, dataPath, schemaName, overrideDataPath);
        return connection;
    }
}
#endif
