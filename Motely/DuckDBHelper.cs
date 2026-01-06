#if !BROWSER
using DuckDB.NET.Data;
using System.IO;
using System.Text;
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
            
            // CONFIGURE DUCKDB FOR LARGE DATA (per https://duckdb.org/docs/stable/guides/performance/how_to_tune_workloads)
            // - Increase temp directory size for out-of-core ORDER BY operations
            // - Disable preserve_insertion_order for better performance during large sorts
            // - Limit threads to avoid too many threads causing slowdowns
            cmd.CommandText = @"
                SET memory_limit='4GB';
                SET temp_directory='.duckdb_temp';
                SET max_temp_directory_size='20GB';
                SET preserve_insertion_order=false;
                SET threads=8;
            ";
            cmd.ExecuteNonQuery();

            // Drop table if it exists (in case of previous failed conversion)
            cmd.CommandText = "DROP TABLE IF EXISTS seeds";
            cmd.ExecuteNonQuery();
            
            // Create table from CSV - DuckDB can read CSV directly
            // Normalize path for DuckDB (avoid Windows backslash issues)
            string escapedPath = csvPath.Replace("'", "''").Replace("\\", "/");
            cmd.CommandText = $@"
                CREATE TABLE seeds_raw AS 
                SELECT * FROM read_csv_auto('{escapedPath}');
            ";
            cmd.ExecuteNonQuery();
            
            // If the CSV doesn't have a 'seed' column, try to rename the first column
            cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name='seeds_raw' ORDER BY ordinal_position LIMIT 1";
            var firstColumn = cmd.ExecuteScalar()?.ToString();
            
            if (!string.IsNullOrEmpty(firstColumn) && !firstColumn.Equals("seed", StringComparison.OrdinalIgnoreCase))
            {
                // Rename first column to 'seed' if it's not already
                cmd.CommandText = $"ALTER TABLE seeds_raw RENAME COLUMN \"{firstColumn}\" TO seed";
                cmd.ExecuteNonQuery();
            }
            
            // Create final table sorted by seed length (and seed for stability).
            // Provider-mode vectorizes ONLY when the 8 seeds in a batch share the same length.
            // If the table is not ordered by length, batches become heterogeneous and the search
            // falls back to per-seed scalar hashing (orders of magnitude slower).
            cmd.CommandText = @"
                CREATE TABLE seeds AS 
                SELECT ROW_NUMBER() OVER (ORDER BY LENGTH(seed), seed) - 1 AS id, seed 
                FROM seeds_raw
                WHERE seed IS NOT NULL AND trim(seed) != '';
                DROP TABLE seeds_raw;
                CREATE INDEX idx_seeds_id ON seeds(id);
            ";
            cmd.ExecuteNonQuery();
        }
#else
        // Browser implementation should be provided via partial class in BalatroSeedOracle.Browser/
        // This will use DuckDB WASM via JavaScript interop
        throw new NotImplementedException("CSV to DuckDB conversion not implemented for browser. Provide implementation in BalatroSeedOracle.Browser/");
#endif
    }

    /// <summary>
    /// Convert text file (one seed per line) to DuckDB database
    /// </summary>
    public static void ConvertTextToDuckDB(string textPath, string dbPath)
    {
#if !BROWSER
        using (var conn = new DuckDBConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            
            // CONFIGURE DUCKDB FOR LARGE DATA (per https://duckdb.org/docs/stable/guides/performance/how_to_tune_workloads)
            // - Increase temp directory size for out-of-core ORDER BY operations
            // - Disable preserve_insertion_order for better performance during large sorts
            // - Limit threads to avoid too many threads causing slowdowns
            cmd.CommandText = @"
                SET memory_limit='4GB';
                SET temp_directory='.duckdb_temp';
                SET max_temp_directory_size='20GB';
                SET preserve_insertion_order=false;
                SET threads=8;
            ";
            cmd.ExecuteNonQuery();

            cmd.CommandText = "DROP TABLE IF EXISTS seeds";
            cmd.ExecuteNonQuery();
            
            string escapedPath = textPath.Replace("'", "''").Replace("\\", "/");
            
            // STEP 1: IMPORT RAW
            cmd.CommandText = "CREATE TABLE seeds_raw (seed VARCHAR)";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"COPY seeds_raw FROM '{escapedPath}' (FORMAT CSV, HEADER false, DELIMITER E'\\n')";
            cmd.ExecuteNonQuery();

            // STEP 2: ADD SEQUENTIAL ID SORTED BY SEED LENGTH
            //
            // Provider-mode vectorizes ONLY when the 8 seeds in a batch share the same length.
            // If the table is not ordered by length, batches will be heterogeneous and the search
            // falls back to per-seed scalar hashing (orders of magnitude slower).
            cmd.CommandText = @"
                CREATE TABLE seeds AS 
                SELECT ROW_NUMBER() OVER (ORDER BY LENGTH(seed), seed) - 1 AS id, seed 
                FROM seeds_raw 
                WHERE seed IS NOT NULL AND trim(seed) != '';
                DROP TABLE seeds_raw;
                CREATE INDEX idx_seeds_id ON seeds(id);
            ";
            cmd.ExecuteNonQuery();
        }
#else
        throw new NotImplementedException("Text to DuckDB conversion not implemented for browser.");
#endif
    }
}
