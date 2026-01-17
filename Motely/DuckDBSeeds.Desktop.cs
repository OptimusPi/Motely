using Motely.DuckDB;

namespace Motely;

/// <summary>
/// Desktop implementation of DuckDBSeeds - streams seeds from DuckDB database
/// </summary>
public static partial class DuckDBSeeds
{
    /// <summary>
    /// Stream seeds from a DuckDB database, sorted by length for vectorization
    /// </summary>
    public static IEnumerable<string> Stream(string dbPath)
    {
        using var connection = DuckDBConnectionFactory.CreateConnection(dbPath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT seed FROM seeds ORDER BY seed_len, seed";
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return reader.GetString(0);
        }
    }
}
