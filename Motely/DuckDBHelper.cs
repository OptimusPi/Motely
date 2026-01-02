#if !BROWSER
using DuckDB.NET.Data;
#endif

namespace Motely;

/// <summary>
/// Platform-agnostic DuckDB helper for CSV conversion
/// Platform-specific implementations can be provided via partial class in BalatroSeedOracle.Browser/
/// </summary>
public static partial class DuckDBHelper
{
    /// <summary>
    /// Convert CSV file to DuckDB database
    /// </summary>
    public static void ConvertCsvToDuckDB(string csvPath, string dbPath)
    {
#if !BROWSER
        using (var conn = new DuckDBConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            
            // Create table from CSV - DuckDB can read CSV directly
            cmd.CommandText = $@"
                CREATE TABLE seeds AS 
                SELECT * FROM read_csv_auto('{csvPath.Replace("'", "''")}');
            ";
            cmd.ExecuteNonQuery();
            
            // If the CSV doesn't have a 'seed' column, try to rename the first column
            cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name='seeds' ORDER BY ordinal_position LIMIT 1";
            var firstColumn = cmd.ExecuteScalar()?.ToString();
            
            if (!string.IsNullOrEmpty(firstColumn) && !firstColumn.Equals("seed", StringComparison.OrdinalIgnoreCase))
            {
                // Rename first column to 'seed' if it's not already
                cmd.CommandText = $"ALTER TABLE seeds RENAME COLUMN \"{firstColumn}\" TO seed";
                cmd.ExecuteNonQuery();
            }
        }
#else
        // Browser implementation should be provided via partial class in BalatroSeedOracle.Browser/
        // This will use DuckDB WASM via JavaScript interop
        throw new NotImplementedException("CSV to DuckDB conversion not implemented for browser. Provide implementation in BalatroSeedOracle.Browser/");
#endif
    }
}
