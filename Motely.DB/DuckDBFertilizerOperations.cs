using DuckDB.NET.Data;

namespace Motely.DB;

/// <summary>
/// Helper for fertilizer database operations
/// Abstracts SQL operations related to fertilizer/seed storage
/// </summary>
public static class DuckDBFertilizerOperations
{
    /// <summary>
    /// Migrate seeds from CSV text file using DuckDB COPY command
    /// Efficiently bulk-imports seeds without individual INSERT statements
    /// </summary>
    public static void MigrateSeedsFromTextFile(DuckDBConnection connection, string txtPath)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        if (!File.Exists(txtPath))
            throw new FileNotFoundException($"Fertilizer text file not found: {txtPath}");

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"COPY seeds(seed) FROM '{txtPath.Replace("\\", "\\\\")}' (FORMAT CSV, HEADER false, DELIMITER '\n')";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Add a single seed to the fertilizer pile (ignore duplicates)
    /// </summary>
    public static void AddSeed(DuckDBConnection connection, string seed)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        if (string.IsNullOrWhiteSpace(seed))
            return;

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            @"
            INSERT INTO seeds (seed)
            VALUES (?)
            ON CONFLICT (seed) DO NOTHING";

        cmd.Parameters.Add(new DuckDBParameter(seed));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Add multiple seeds to the fertilizer pile efficiently
    /// </summary>
    public static void AddSeeds(DuckDBConnection connection, IEnumerable<string> seeds)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        using var cmd = connection.CreateCommand();
        cmd.CommandText =
            @"
            INSERT INTO seeds (seed)
            VALUES (?)
            ON CONFLICT (seed) DO NOTHING";

        foreach (var seed in seeds)
        {
            if (string.IsNullOrWhiteSpace(seed))
                continue;

            cmd.Parameters.Clear();
            cmd.Parameters.Add(new DuckDBParameter(seed));
            cmd.ExecuteNonQuery();
        }
    }
}
