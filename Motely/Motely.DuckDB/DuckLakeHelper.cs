#if !BROWSER
using DuckDB.NET.Data;
using System;
using System.IO;

namespace Motely.DuckDB;

/// <summary>
/// Helper class for DuckLake operations.
/// DuckLake enables "multiplayer DuckDB" - multiple DuckDB instances can read/write the same dataset.
/// This solves the file locking problem with traditional DuckDB databases.
/// </summary>
public static class DuckLakeHelper
{
    /// <summary>
    /// Check if a path points to a DuckLake (has .ducklake catalog file).
    /// </summary>
    public static bool IsDuckLake(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // DuckLake uses a catalog file with .ducklake extension
        // Format: "metadata.ducklake" in the same directory as data files
        var directory = Path.GetDirectoryName(path);
        var baseName = Path.GetFileNameWithoutExtension(path);
        
        if (string.IsNullOrEmpty(directory))
            directory = ".";

        var catalogPath = Path.Combine(directory, $"{baseName}.ducklake");
        return File.Exists(catalogPath) || Directory.Exists(catalogPath);
    }

    /// <summary>
    /// Check if a path points to a legacy DuckDB database (.db file).
    /// </summary>
    public static bool IsLegacyDuckDB(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return Path.GetExtension(path).Equals(".db", StringComparison.OrdinalIgnoreCase) &&
               File.Exists(path);
    }

    /// <summary>
    /// Get the DuckLake catalog path for a given seed source name.
    /// </summary>
    public static string GetDuckLakeCatalogPath(string seedSourceName, string seedSourcesDir = "SeedSources")
    {
        var directory = Path.IsPathRooted(seedSourceName) 
            ? Path.GetDirectoryName(seedSourceName) ?? seedSourcesDir
            : seedSourcesDir;

        var baseName = Path.GetFileNameWithoutExtension(seedSourceName);
        return Path.Combine(directory, $"{baseName}.ducklake");
    }

    /// <summary>
    /// Get the DuckLake data directory path (where Parquet files are stored).
    /// </summary>
    public static string GetDuckLakeDataPath(string seedSourceName, string seedSourcesDir = "SeedSources")
    {
        var directory = Path.IsPathRooted(seedSourceName) 
            ? Path.GetDirectoryName(seedSourceName) ?? seedSourcesDir
            : seedSourcesDir;

        var baseName = Path.GetFileNameWithoutExtension(seedSourceName);
        return Path.Combine(directory, $"{baseName}_data");
    }

    /// <summary>
    /// Ensure the ducklake extension is installed in the connection.
    /// </summary>
    public static void EnsureDuckLakeExtension(DuckDBConnection connection)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        try
        {
            using var cmd = connection.CreateCommand();
            // Try to load the extension - if it fails, install it
            cmd.CommandText = "LOAD ducklake;";
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // Extension not loaded, try to install it
                cmd.CommandText = "INSTALL ducklake;";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "LOAD ducklake;";
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to install/load DuckLake extension. " +
                "Make sure you're using DuckDB 0.10.0+ with DuckLake support. " +
                $"Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attach a DuckLake to the connection.
    /// Supports both local paths and remote URLs (R2, S3, HTTPS).
    /// </summary>
    /// <param name="connection">DuckDB connection</param>
    /// <param name="catalogPath">Path to DuckLake catalog file (.ducklake) - can be local or HTTPS URL</param>
    /// <param name="dataPath">Path to DuckLake data directory (Parquet files) - can be local or R2/S3 URL</param>
    /// <param name="schemaName">Schema name to attach as (default: "seed_source")</param>
    /// <param name="overrideDataPath">If true, override the persisted data path in catalog (for remote data)</param>
    public static void AttachDuckLake(DuckDBConnection connection, string catalogPath, string dataPath, string schemaName = "seed_source", bool overrideDataPath = false)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(catalogPath))
            throw new ArgumentException("Catalog path cannot be empty", nameof(catalogPath));
        if (string.IsNullOrWhiteSpace(dataPath))
            throw new ArgumentException("Data path cannot be empty", nameof(dataPath));

        EnsureDuckLakeExtension(connection);

        // Normalize paths for DuckDB (use forward slashes, escape single quotes)
        var normalizedCatalog = catalogPath.Replace('\\', '/').Replace("'", "''");
        var normalizedData = dataPath.Replace('\\', '/').Replace("'", "''");
        var normalizedSchema = schemaName.Replace("'", "''");

        // Create data directory if it doesn't exist
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        using var cmd = connection.CreateCommand();
        
        // Determine if we're using a remote URL (R2, S3, HTTPS)
        bool isRemoteCatalog = catalogPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                               catalogPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        bool isRemoteData = dataPath.StartsWith("s3://", StringComparison.OrdinalIgnoreCase) ||
                           dataPath.StartsWith("r2://", StringComparison.OrdinalIgnoreCase) ||
                           dataPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                           dataPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase);

        // For remote data paths, we need to use OVERRIDE_DATA_PATH
        // See: https://ducklake.select/docs/stable/duckdb/guides/using_a_remote_data_path
        string attachSql;
        if (isRemoteCatalog)
        {
            // Remote catalog (HTTPS URL) - attach directly
            attachSql = $@"
                ATTACH 'ducklake:{normalizedCatalog}' AS {normalizedSchema} (TYPE ducklake)";
        }
        else if (isRemoteData && overrideDataPath)
        {
            // Local catalog, remote data - override data path
            attachSql = $@"
                ATTACH 'ducklake:{normalizedCatalog}' AS {normalizedSchema} (
                    TYPE ducklake,
                    DATA_PATH '{normalizedData}',
                    OVERRIDE_DATA_PATH true
                )";
        }
        else if (isRemoteData)
        {
            // Local catalog, remote data - use remote path directly
            attachSql = $@"
                ATTACH 'ducklake:{normalizedCatalog}' AS {normalizedSchema} (
                    TYPE ducklake,
                    DATA_PATH '{normalizedData}'
                )";
        }
        else
        {
            // Local catalog and data (standard case)
            // Create data directory if it doesn't exist
            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }
            attachSql = $@"
                ATTACH 'ducklake:{normalizedCatalog}' AS {normalizedSchema} (
                    TYPE ducklake,
                    DATA_PATH '{normalizedData}/'
                )";
        }

        cmd.CommandText = attachSql;

        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to attach DuckLake. Catalog: {catalogPath}, Data: {dataPath}, Remote: {isRemoteData}. " +
                $"Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convert a legacy DuckDB database to DuckLake format.
    /// This enables concurrent access to the seed source.
    /// </summary>
    /// <param name="legacyDbPath">Path to legacy .db file</param>
    /// <param name="catalogPath">Output path for DuckLake catalog</param>
    /// <param name="dataPath">Output path for DuckLake data directory</param>
    public static void ConvertLegacyToDuckLake(string legacyDbPath, string catalogPath, string dataPath)
    {
        if (string.IsNullOrWhiteSpace(legacyDbPath))
            throw new ArgumentException("Legacy database path cannot be empty", nameof(legacyDbPath));
        if (!File.Exists(legacyDbPath))
            throw new FileNotFoundException("Legacy database file not found", legacyDbPath);

        // Create data directory
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        // Create a temporary connection to the legacy database
        using var sourceConn = DuckDBConnectionFactory.CreateConnection(legacyDbPath);
        
        // Create a new connection for DuckLake
        // We'll use an in-memory connection to create the DuckLake, then export
        using var tempConn = DuckDBConnectionFactory.CreateConnection(":memory:");
        EnsureDuckLakeExtension(tempConn);

        // Create DuckLake catalog
        var normalizedCatalog = catalogPath.Replace('\\', '/').Replace("'", "''");
        var normalizedData = dataPath.Replace('\\', '/').Replace("'", "''");

        using (var cmd = tempConn.CreateCommand())
        {
            // Create DuckLake and attach it
            cmd.CommandText = $@"
                ATTACH 'ducklake:{normalizedCatalog}' AS ducklake_temp (DATA_PATH '{normalizedData}/');
            ";
            cmd.ExecuteNonQuery();

            // Create seeds table in DuckLake (copy schema from legacy)
            cmd.CommandText = @"
                CREATE TABLE ducklake_temp.seeds AS
                SELECT id, seed FROM read_duckdb(?) AS seeds;
            ";
            cmd.Parameters.Add(new DuckDBParameter(legacyDbPath));
            cmd.ExecuteNonQuery();

            // Create index
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_seeds_id ON ducklake_temp.seeds(id);";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Create a new DuckLake from a CSV/TXT file (replaces ConvertSeedFileToDuckDB).
    /// </summary>
    public static void CreateDuckLakeFromSeedFile(string sourcePath, string catalogPath, string dataPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be empty", nameof(sourcePath));
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Source file not found", sourcePath);

        // Create data directory
        if (!Directory.Exists(dataPath))
        {
            Directory.CreateDirectory(dataPath);
        }

        // Delete existing DuckLake if it exists
        if (File.Exists(catalogPath))
        {
            File.Delete(catalogPath);
        }
        if (Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, true);
        }
        Directory.CreateDirectory(dataPath);

        // Create temporary connection to build DuckLake
        using var conn = DuckDBConnectionFactory.CreateConnection(":memory:");
        EnsureDuckLakeExtension(conn);

        var normalizedCatalog = catalogPath.Replace('\\', '/').Replace("'", "''");
        var normalizedData = dataPath.Replace('\\', '/').Replace("'", "''");

        using (var cmd = conn.CreateCommand())
        {
            // Attach DuckLake
            cmd.CommandText = $@"
                ATTACH 'ducklake:{normalizedCatalog}' AS ducklake_new (DATA_PATH '{normalizedData}/');
            ";
            cmd.ExecuteNonQuery();

            // Create seeds table from CSV/TXT (same logic as ConvertSeedFileToDuckDB)
            cmd.CommandText = @"
                CREATE TABLE ducklake_new.seeds AS
                WITH lines AS (
                    SELECT column0 AS raw_line
                    FROM read_csv(?, delim=E'\n', header=false)
                ),
                cleaned AS (
                    SELECT
                        UPPER(TRIM(SUBSTRING(
                            TRIM(BOTH '""' FROM
                                CASE
                                    WHEN INSTR(raw_line, ',') > 0 THEN SUBSTRING(raw_line, 1, INSTR(raw_line, ',') - 1)
                                    WHEN INSTR(raw_line, ' ') > 0 THEN SUBSTRING(raw_line, 1, INSTR(raw_line, ' ') - 1)
                                    WHEN INSTR(raw_line, '\t') > 0 THEN SUBSTRING(raw_line, 1, INSTR(raw_line, '\t') - 1)
                                    ELSE raw_line
                                END
                            ),
                            1,
                            8
                        ))) AS seed
                    FROM lines
                    WHERE raw_line IS NOT NULL AND TRIM(raw_line) != ''
                )
                SELECT
                    ROW_NUMBER() OVER (ORDER BY LENGTH(seed), seed) - 1 AS id,
                    seed
                FROM cleaned
                WHERE seed != ''
                  AND seed NOT LIKE '%0%'
                  AND regexp_matches(seed, '^[1-9A-Z]+$');
            ";
            cmd.Parameters.Add(new DuckDBParameter(sourcePath));
            cmd.ExecuteNonQuery();

            // Create index
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_seeds_id ON ducklake_new.seeds(id);";
            cmd.ExecuteNonQuery();
        }
    }
}
#endif
