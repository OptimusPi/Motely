#if !BROWSER
using DuckDB.NET.Data;
using System;
using System.IO;
using System.Text;
#endif

namespace Motely;

public static partial class DuckDBHelper
{
#if !BROWSER
    private static void ConvertSeedFileToDuckDB(string sourcePath, string dbPath)
    {
        if (File.Exists(dbPath))
        {
            // If this throws, the DB is locked by another process and we should fail loudly.
            File.Delete(dbPath);
        }

        using var conn = new DuckDBConnection($"Data Source={dbPath}");
        conn.Open();

        // One statement: read lines, extract the first token, take first 8 chars, validate, assign id.
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE seeds AS
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
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX idx_seeds_id ON seeds(id)";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM seeds";
            long count = Convert.ToInt64(cmd.ExecuteScalar());
            if (count == 0)
            {
                throw new InvalidDataException(
                    $"No valid seeds found in {sourcePath}. Seeds must be 1-8 chars, only 1-9/A-Z (no 0).");
            }
        }
    }

    private static void ConvertSeedFilesToDuckDB(string[] sourcePaths, string dbPath)
    {
        if (sourcePaths is null || sourcePaths.Length == 0)
            throw new ArgumentException("At least one source file is required.", nameof(sourcePaths));

        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }

        using var conn = new DuckDBConnection($"Data Source={dbPath}");
        conn.Open();

        var union = new StringBuilder();
        for (int i = 0; i < sourcePaths.Length; i++)
        {
            if (i > 0) union.AppendLine("UNION ALL");
            union.AppendLine("SELECT column0 AS raw_line FROM read_csv(?, delim=E'\\n', header=false)");
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $@"
                CREATE TABLE seeds AS
                WITH lines AS (
                    {union}
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

            foreach (var p in sourcePaths)
            {
                cmd.Parameters.Add(new DuckDBParameter(p));
            }

            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX idx_seeds_id ON seeds(id)";
            cmd.ExecuteNonQuery();
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM seeds";
            long count = Convert.ToInt64(cmd.ExecuteScalar());
            if (count == 0)
            {
                throw new InvalidDataException(
                    "No valid seeds found in provided files. Seeds must be 1-8 chars, only 1-9/A-Z (no 0).");
            }
        }
    }
#endif

    public static void ConvertCsvToDuckDB(string csvPath, string dbPath)
    {
#if !BROWSER
        ConvertSeedFileToDuckDB(csvPath, dbPath);
#else
        throw new NotImplementedException("CSV to DuckDB conversion not implemented for browser.");
#endif
    }

    public static void ConvertMultipleCsvToDuckDB(string[] csvPaths, string dbPath)
    {
#if !BROWSER
        ConvertSeedFilesToDuckDB(csvPaths, dbPath);
#else
        throw new NotImplementedException("Multiple CSV to DuckDB conversion not implemented for browser.");
#endif
    }

    public static void ConvertTextToDuckDB(string textPath, string dbPath)
    {
#if !BROWSER
        ConvertSeedFileToDuckDB(textPath, dbPath);
#else
        throw new NotImplementedException("Text to DuckDB conversion not implemented for browser.");
#endif
    }
}