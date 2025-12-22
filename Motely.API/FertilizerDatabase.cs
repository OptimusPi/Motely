using DuckDB.NET.Data;

namespace Motely.API;

/// <summary>
/// Singleton DuckDB-based fertilizer pile for ranked seed storage
/// Handles migration from fertilizer.txt and provides top-K queries
/// </summary>
public sealed class FertilizerDatabase : IDisposable
{
    private static FertilizerDatabase? _instance;
    private static readonly object _lock = new();

    public static FertilizerDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new FertilizerDatabase();
                }
            }
            return _instance;
        }
    }

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
            _connection = new DuckDBConnection($"Data Source={_dbPath}");
            _connection.Open();

            // Create table if not exists
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS seeds (
                    seed VARCHAR PRIMARY KEY
                )";
            cmd.ExecuteNonQuery();

            using (var migrateCmd = _connection.CreateCommand())
            {
                migrateCmd.CommandText = @"
                    INSERT INTO seeds (seed)
                    SELECT seed FROM fertilizer_pile
                    ON CONFLICT (seed) DO NOTHING";
                try { migrateCmd.ExecuteNonQuery(); } catch { }
            }

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
        if (!File.Exists(_txtPath) || _connection == null) return;

        try
        {
            // Check if DB already has data
            using var countCmd = _connection.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM seeds";
            var count = (long)countCmd.ExecuteScalar()!;

            if (count > 0) return; // DB already populated

            Console.WriteLine($"Migrating fertilizer.txt to DuckDB...");

            // Use DuckDB's COPY command for efficient bulk import
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"COPY seeds(seed) FROM '{_txtPath.Replace("\\", "\\\\")}' (FORMAT CSV, HEADER false, DELIMITER '\n')";
            cmd.ExecuteNonQuery();

            // Get final count
            var finalCount = (long)countCmd.ExecuteScalar()!;
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
        if (_connection == null) return Task.CompletedTask;

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
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
    /// Get top N seeds (arbitrary order)
    /// </summary>
    public List<string> GetTopSeeds(int limit = 1000)
    {
        if (_connection == null) return new List<string>();

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = $"SELECT seed FROM seeds LIMIT {limit}";
            
            var results = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(reader.GetString(0));
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to query fertilizer pile: {ex.Message}");
            return new List<string>();
        }
    }

    /// <summary>
    /// Get total seed count
    /// </summary>
    public long GetSeedCount()
    {
        if (_connection == null) return 0;

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM seeds";
            return (long)cmd.ExecuteScalar()!;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

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
