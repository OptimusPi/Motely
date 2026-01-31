using System.IO;
using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Cross-platform factory for creating DuckDB connections
/// Works on Desktop, Browser WASM, Android, and iOS
/// </summary>
public static class DuckDBConnectionFactory
{
    /// <summary>
    /// Create a standard DuckDB connection to a database file
    /// </summary>
    public static DuckDBConnection CreateConnection(string dbPath)
    {
        var connection = new DuckDBConnection($"Data Source={dbPath}");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Create a DuckDB connection with custom configuration
    /// </summary>
    public static DuckDBConnection CreateConnectionWithConfig(
        string dbPath,
        Action<DuckDBConnection>? configure = null
    )
    {
        var connection = new DuckDBConnection($"Data Source={dbPath}");
        connection.Open();
        configure?.Invoke(connection);
        return connection;
    }

    /// <summary>
    /// Create a DuckDB connection with DuckLake attached for concurrent read/write.
    /// Uses official DuckDB 1.4+ syntax: ATTACH 'ducklake:&lt;catalog&gt;' AS name (DATA_PATH '...').
    /// Extension autoloads on first ATTACH. For existing DuckLake, data path is read from catalog so dataPath can be null.
    /// </summary>
    /// <param name="catalogPath">Path to the .ducklake catalog file (or URL for R2/S3).</param>
    /// <param name="dataPath">Optional. Parquet data directory; omit for existing DuckLake (loaded from catalog).</param>
    /// <param name="schemaName">Attached catalog alias (default seed_source).</param>
    public static DuckDBConnection CreateConnectionWithDuckLake(
        string catalogPath,
        string? dataPath = null,
        string schemaName = "seed_source"
    )
    {
        // Use proper connection string format for in-memory database
        var connection = new DuckDBConnection("Data Source=:memory:");
        connection.Open();

        // Normalize path for SQL: single quotes escaped by doubling; use forward slashes
        var catalogSql = EscapePathForSql(catalogPath);

        string attachSql;
        if (string.IsNullOrEmpty(dataPath))
        {
            // Existing DuckLake: data path is loaded from catalog
            attachSql = $"ATTACH 'ducklake:{catalogSql}' AS {schemaName}";
        }
        else
        {
            var dataSql = EscapePathForSql(dataPath);
            attachSql = $"ATTACH 'ducklake:{catalogSql}' AS {schemaName} (DATA_PATH '{dataSql}')";
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = attachSql;
            cmd.ExecuteNonQuery();
        }

        return connection;
    }

    private static string EscapePathForSql(string path)
    {
        // Single quotes in path must be doubled for SQL; use forward slashes for portability
        var normalized = path.Replace('\\', '/');
        return normalized.Replace("'", "''");
    }
}
