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
    /// Handles both header and no-header CSVs - auto-detects and uses first column as seed
    /// </summary>
    public static void ConvertCsvToDuckDB(string csvPath, string dbPath)
    {
#if !BROWSER
        using (var conn = new DuckDBConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            
            string escapedPath = csvPath.Replace("'", "''");
            
            // Read CSV, get first column, rename to seed, filter empty - done
            cmd.CommandText = $@"
                CREATE TABLE seeds AS 
                SELECT * FROM read_csv_auto('{escapedPath}');
            ";
            cmd.ExecuteNonQuery();
            
            // Get first column and rename to 'seed'
            cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name='seeds' ORDER BY ordinal_position LIMIT 1";
            var firstColumn = cmd.ExecuteScalar()?.ToString() ?? throw new Exception("CSV has no columns");
            
            if (!firstColumn.Equals("seed", StringComparison.OrdinalIgnoreCase))
            {
                cmd.CommandText = $@"ALTER TABLE seeds RENAME COLUMN ""{firstColumn.Replace("\"", "\"\"")}"" TO seed";
                cmd.ExecuteNonQuery();
            }
            
            // Filter empty
            cmd.CommandText = "DELETE FROM seeds WHERE seed IS NULL OR trim(seed) = ''";
            cmd.ExecuteNonQuery();
        }
#else
        // Browser implementation provided in DuckDBHelper.Browser.cs
        throw new NotImplementedException("Browser implementation should be in DuckDBHelper.Browser.cs");
#endif
    }

    /// <summary>
    /// Convert text file (one seed per line, or CSV with seed as first column) to DuckDB database
    /// </summary>
    public static void ConvertTextToDuckDB(string textPath, string dbPath)
    {
#if !BROWSER
        using (var conn = new DuckDBConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            
            string escapedPath = textPath.Replace("'", "''");
            
            // Read text file, first column is seed, filter empty - done
            cmd.CommandText = $@"
                CREATE TABLE seeds AS 
                SELECT column0 AS seed 
                FROM read_csv_auto('{escapedPath}', header=false)
                WHERE column0 IS NOT NULL AND trim(column0) != '';
            ";
            cmd.ExecuteNonQuery();
        }
#else
        // Browser implementation provided in DuckDBHelper.Browser.cs
        throw new NotImplementedException("Browser implementation should be in DuckDBHelper.Browser.cs");
#endif
    }
}
