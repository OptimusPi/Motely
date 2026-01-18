using DuckDB.NET.Data;
using Motely.DuckDB;

namespace Motely.API;

/// <summary>
/// Singleton DuckDB-based fertilizer pile for ranked seed storage
/// Handles migration from fertilizer.txt and provides top-K queries
/// </summary>
public sealed class FertilizerDatabase : IDisposable
{
    private static readonly Lazy<FertilizerDatabase> _lazyInstance = new(() =>
        new FertilizerDatabase()
    );

    public static FertilizerDatabase Instance => _lazyInstance.Value;

    private readonly string _dbPath;
    private readonly string _txtPath;
    private DuckDBConnection? _connection;
    private bool _disposed;

    private FertilizerDatabase()
    {
        var dataDir = Environment.CurrentDirectory;
        _dbPath = Path.Combine(dataDir, "fertilizer.db");
        _txtPath = Path.Combine(dataDir, "fertilizer.txt");

        Initialize();
    }

    private void Initialize()
    {
        try
        {
            _connection = DuckDBConnectionFactory.CreateConnection(_dbPath);

            // Use centralized schema for fertilizer table
            var fertilizerSchema = DuckDBSchema.FertilizerTableSchema();
            DuckDBTableManager.EnsureTableExists(_connection, fertilizerSchema);

            // Migrate from fertilizer.txt if it exists
            MigrateFromTxtIfNeeded();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize fertilizer database: {ex.Message}");
        }
    }

    private void MigrateFromTxtIfNeeded()
    {
        if (!File.Exists(_txtPath) || _connection == null)
            return;

        try
        {
            // Check if DB already has data - use centralized operation
            var count = DuckDBOperations.GetRowCount(_connection, "seeds");

            if (count > 0)
                return; // DB already populated

            Console.WriteLine($"Migrating fertilizer.txt to DuckDB...");

            // Use DuckDB's COPY command for efficient bulk import
            using var cmd = _connection.CreateCommand();
            cmd.CommandText =
                $"COPY seeds(seed) FROM '{_txtPath.Replace("\\", "\\\\")}' (FORMAT CSV, HEADER false, DELIMITER '\n')";
            cmd.ExecuteNonQuery();

            // Get final count - use centralized operation
            var finalCount = DuckDBOperations.GetRowCount(_connection, "seeds");
            Console.WriteLine($"Migrated {finalCount} seeds to DuckDB");

            // Delete the txt file after successful migration
            File.Delete(_txtPath);
            Console.WriteLine("fertilizer.txt deleted after successful migration");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Migration failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Add seeds to the fertilizer pile (ignore duplicates)
    /// </summary>
    public Task AddSeedsAsync(IEnumerable<string> seeds)
    {
        if (_connection == null)
            return Task.CompletedTask;

        try
        {
            using var cmd = _connection.CreateCommand();
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
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to add seeds to fertilizer pile: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get total seed count
    /// </summary>
    public long GetSeedCount()
    {
        if (_connection == null)
            return 0;

        try
        {
            // Use centralized operation for getting row count
            return DuckDBOperations.GetRowCount(_connection, "seeds");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FertilizerDatabase] Failed to get seed count: {ex.Message}");
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during fertilizer database disposal: {ex.Message}");
        }

        _disposed = true;
    }
}
