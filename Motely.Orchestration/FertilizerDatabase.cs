using DuckDB.NET.Data;
using Motely.DB;

namespace Motely.Executors;

/// <summary>
/// Singleton DuckDB-based fertilizer pile for ranked seed storage.
/// Lives in Orchestration so only Orchestration (gate) touches Motely.DB; API calls Orchestrator.GetFertilizerDatabase().
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

            var fertilizerSchema = DuckDBSchema.FertilizerTableSchema();
            DuckDBTableManager.EnsureTableExists(_connection, fertilizerSchema);

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
            var count = DuckDBOperations.GetRowCount(_connection, "seeds");

            if (count > 0)
                return;

            Console.WriteLine($"Migrating fertilizer.txt to DuckDB...");

            DuckDBFertilizerOperations.MigrateSeedsFromTextFile(_connection, _txtPath);

            var finalCount = DuckDBOperations.GetRowCount(_connection, "seeds");
            Console.WriteLine($"Migrated {finalCount} seeds to DuckDB");

            File.Delete(_txtPath);
            Console.WriteLine("fertilizer.txt deleted after successful migration");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Migration failed: {ex.Message}");
        }
    }

    public Task AddSeedsAsync(IEnumerable<string> seeds)
    {
        if (_connection == null)
            return Task.CompletedTask;

        try
        {
            DuckDBFertilizerOperations.AddSeeds(_connection, seeds);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to add seeds to fertilizer pile: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public long GetSeedCount()
    {
        if (_connection == null)
            return 0;

        try
        {
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
