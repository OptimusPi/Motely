#if !BROWSER
using DuckDB.NET.Data;
using System;

namespace Motely.DuckDB;

/// <summary>
/// Helper for cloud storage integration (R2, S3, etc.)
/// Provides utilities for working with remote DuckLake data paths.
/// </summary>
public static class CloudStorageHelper
{
    /// <summary>
    /// Configure R2 secret in DuckDB connection.
    /// This must be called before using R2 paths in queries.
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="accessKeyId">R2 access key ID</param>
    /// <param name="secretAccessKey">R2 secret access key</param>
    /// <param name="endpoint">R2 endpoint URL (default: https://{accountId}.r2.cloudflarestorage.com)</param>
    /// <param name="secretName">Name for the secret (default: "r2")</param>
    public static void ConfigureR2Secret(DuckDBConnection connection, string accessKeyId, string secretAccessKey, string endpoint, string secretName = "r2")
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(accessKeyId))
            throw new ArgumentException("Access key ID cannot be empty", nameof(accessKeyId));
        if (string.IsNullOrWhiteSpace(secretAccessKey))
            throw new ArgumentException("Secret access key cannot be empty", nameof(secretAccessKey));
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));

        // Ensure httpfs extension is loaded
        InstallHttpfsExtension(connection);

        // Create or replace R2 secret
        // DuckDB syntax: CREATE SECRET (TYPE s3, KEY_ID '...', SECRET '...', ENDPOINT '...')
        // R2 is S3-compatible, so we use TYPE s3
        var sql = $@"
            CREATE SECRET IF NOT EXISTS {secretName} (
                TYPE S3,
                KEY_ID '{accessKeyId.Replace("'", "''")}',
                SECRET '{secretAccessKey.Replace("'", "''")}',
                ENDPOINT '{endpoint.Replace("'", "''")}'
            )";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Install and load httpfs extension (required for S3/R2 access).
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    public static void InstallHttpfsExtension(DuckDBConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        using var cmd = connection.CreateCommand();
        
        // Install extension if not already installed
        cmd.CommandText = "INSTALL httpfs;";
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Extension might already be installed, ignore
        }

        // Load extension
        cmd.CommandText = "LOAD httpfs;";
        try
        {
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Extension might already be loaded, ignore
        }
    }
    /// <summary>
    /// Check if a path is a remote URL (R2, S3, HTTPS).
    /// </summary>
    public static bool IsRemotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("r2://", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if a path is Cloudflare R2.
    /// </summary>
    public static bool IsR2Path(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.StartsWith("r2://", StringComparison.OrdinalIgnoreCase) ||
               (path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && 
                path.Contains(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if a path is S3-compatible (S3, R2, etc.).
    /// </summary>
    public static bool IsS3CompatiblePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ||
               IsR2Path(path);
    }

    /// <summary>
    /// Convert R2 path to S3-compatible format for DuckDB.
    /// DuckDB uses S3 syntax, but R2 is S3-compatible.
    /// </summary>
    /// <param name="r2Path">R2 path (r2://bucket/path or https://account.r2.cloudflarestorage.com/bucket/path)</param>
    /// <returns>S3-compatible path</returns>
    public static string ConvertR2ToS3Path(string r2Path)
    {
        if (string.IsNullOrWhiteSpace(r2Path))
            throw new ArgumentException("R2 path cannot be empty", nameof(r2Path));

        // If already S3 format, return as-is
        if (r2Path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            return r2Path;

        // Convert r2:// to s3://
        if (r2Path.StartsWith("r2://", StringComparison.OrdinalIgnoreCase))
            return r2Path.Replace("r2://", "s3://", StringComparison.OrdinalIgnoreCase);

        // Convert HTTPS R2 URL to S3 format
        // Format: https://account.r2.cloudflarestorage.com/bucket/path
        // To: s3://bucket/path
        if (r2Path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            r2Path.Contains(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(r2Path);
            var pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 2);
            if (pathParts.Length >= 1)
            {
                var bucket = pathParts[0];
                var key = pathParts.Length > 1 ? pathParts[1] : "";
                return $"s3://{bucket}/{key}";
            }
        }

        return r2Path; // Return as-is if we can't convert
    }

    /// <summary>
    /// Build R2 URL from bucket and path.
    /// </summary>
    /// <param name="accountId">Cloudflare account ID</param>
    /// <param name="bucket">R2 bucket name</param>
    /// <param name="path">Path within bucket</param>
    /// <returns>R2 HTTPS URL</returns>
    public static string BuildR2Url(string accountId, string bucket, string path = "")
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("Account ID cannot be empty", nameof(accountId));
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Bucket cannot be empty", nameof(bucket));

        var cleanPath = path?.TrimStart('/') ?? "";
        return $"https://{accountId}.r2.cloudflarestorage.com/{bucket}/{cleanPath}";
    }

    /// <summary>
    /// Build R2 S3-compatible path for DuckDB.
    /// </summary>
    /// <param name="bucket">R2 bucket name</param>
    /// <param name="path">Path within bucket</param>
    /// <returns>S3-compatible path (s3://bucket/path)</returns>
    public static string BuildR2S3Path(string bucket, string path = "")
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Bucket cannot be empty", nameof(bucket));

        var cleanPath = path?.TrimStart('/') ?? "";
        return $"s3://{bucket}/{cleanPath}";
    }
}
#endif
