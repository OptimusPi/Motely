using DuckDB.NET.Data;

namespace Motely.DuckDB;

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
        var connection = new DuckDBConnection(dbPath);
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
        var connection = new DuckDBConnection(dbPath);
        connection.Open();
        configure?.Invoke(connection);
        return connection;
    }

    /// <summary>
    /// Create a DuckDB connection with DuckLake attached for concurrent access
    /// </summary>
    public static DuckDBConnection CreateConnectionWithDuckLake(
        string catalogPath,
        string dataPath,
        string schemaName = "seed_source"
    )
    {
        var connection = new DuckDBConnection(":memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();

        // Attach DuckLake catalog
        cmd.CommandText =
            $"ATTACH '{catalogPath}' AS {schemaName} (TYPE DUCKLAKE, CATALOG_PATH '{catalogPath}', DATA_PATH '{dataPath}')";
        cmd.ExecuteNonQuery();

        return connection;
    }
}
