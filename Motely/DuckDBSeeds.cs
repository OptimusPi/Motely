using Motely.DuckDB;

namespace Motely;

/// <summary>
/// Streams seeds from DuckDB database
/// </summary>
public static class DuckDBSeeds
{
    /// <summary>
    /// Stream seeds from a DuckDB database
    /// </summary>
    public static IEnumerable<string> Stream(string dbPath)
    {
        using var connection = DuckDBConnectionFactory.CreateConnection(dbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT seed FROM seeds";
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return reader.GetString(0);
        }
    }
}
