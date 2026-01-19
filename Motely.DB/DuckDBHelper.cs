using System.IO;
using System.Linq;
using DuckDB.NET.Data;

namespace Motely.DuckDB;

/// <summary>
/// Helper for converting CSV and text files to DuckDB databases
/// Cross-platform compatible (Desktop, Browser, CLI, TUI, Avalonia)
/// </summary>
public static class DuckDBHelper
{
    /// <summary>
    /// Convert CSV file to DuckDB database
    /// </summary>
    public static void ConvertCsvToDuckDB(string csvPath, string dbPath)
    {
        if (!File.Exists(csvPath))
            throw new FileNotFoundException($"CSV file not found: {csvPath}");

        // Ensure directory exists
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            Directory.CreateDirectory(dbDir);

        using var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
        using var cmd = conn.CreateCommand();

        // Properly escape single quotes for SQL string literal
        var escapedPath = csvPath.Replace("\\", "/").Replace("'", "''");

        // Create seeds table
        cmd.CommandText =
            $@"
            CREATE TABLE IF NOT EXISTS seeds (
                seed VARCHAR PRIMARY KEY
            );
            
            -- Import CSV data - disable header detection for consistency
            -- Workaround for DuckDB limitation: Load into temp table (no constraints, fast),
            -- then INSERT OR IGNORE from temp (DuckDB handles duplicates naturally during insert)
            CREATE TEMP TABLE temp_seeds AS
            SELECT TRIM(column0) as seed
            FROM read_csv('{escapedPath}', header=false)
            WHERE TRIM(column0) IS NOT NULL AND TRIM(column0) != '';
            
            INSERT OR IGNORE INTO seeds (seed)
            SELECT seed FROM temp_seeds;
            
            DROP TABLE temp_seeds;
        ";

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Convert text or CSV file to DuckDB database
    /// Disables header detection to ensure column0 is always available
    /// </summary>
    public static void ConvertTextToDuckDB(string textPath, string dbPath)
    {
        if (!File.Exists(textPath))
            throw new FileNotFoundException($"Text file not found: {textPath}");

        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
            Directory.CreateDirectory(dbDir);

        using var conn = DuckDBConnectionFactory.CreateConnection(dbPath);
        using var cmd = conn.CreateCommand();

        // Properly escape single quotes for SQL string literal
        var escapedPath = textPath.Replace("\\", "/").Replace("'", "''");

        // Create table with PK
        cmd.CommandText = "CREATE TABLE seeds (seed VARCHAR PRIMARY KEY)";
        cmd.ExecuteNonQuery();

        // Disable header detection explicitly - seed files are just lists of seeds, one per line
        // This ensures column0 is always available regardless of first line content
        // Workaround for DuckDB limitation: Load into temp table (no constraints, fast),
        // then INSERT OR IGNORE from temp (DuckDB handles duplicates naturally during insert)
        cmd.CommandText =
            $@"
            CREATE TEMP TABLE temp_seeds AS
            SELECT TRIM(column0) as seed
            FROM read_csv('{escapedPath}', header=false)
            WHERE TRIM(column0) IS NOT NULL AND TRIM(column0) != '';
            
            INSERT OR IGNORE INTO seeds (seed)
            SELECT seed FROM temp_seeds;
            
            DROP TABLE temp_seeds;";
        cmd.ExecuteNonQuery();
    }
}
