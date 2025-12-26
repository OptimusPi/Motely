// Browser-specific DuckDB implementation using DuckDB-WASM via JavaScript interop
#if BROWSER
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace Motely;

/// <summary>
/// Browser implementation of DuckDBSeeds using DuckDB-WASM
/// Uses window.DuckDB JavaScript interop from duckdb-interop.js
/// </summary>
public static partial class DuckDBSeeds
{
    [JSImport("window.DuckDB.initialize", "js/duckdb-interop.js")]
    private static partial Task<bool> InitializeDuckDB();

    [JSImport("window.DuckDB.openConnection", "js/duckdb-interop.js")]
    private static partial Task<int> OpenConnection();

    [JSImport("window.DuckDB.query", "js/duckdb-interop.js")]
    private static partial Task<string> QueryDuckDB(int connId, string sql);

    [JSImport("window.DuckDB.closeConnection", "js/duckdb-interop.js")]
    private static partial Task CloseConnection(int connId);

    public static IEnumerable<string> Stream(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        // Browser requires async, but IEnumerable is synchronous
        // Use Task.Run to bridge async/sync gap (blocking but necessary for interface)
        var task = StreamAsync(dbPath, tableName, columnName).ToListAsync();
        return task.GetAwaiter().GetResult();
    }

    private static async IAsyncEnumerable<string> StreamAsync(string dbPath, string tableName = "seeds", string columnName = "seed")
    {
        // Initialize DuckDB-WASM if not already initialized
        await InitializeDuckDB();

        // Open connection
        int connId = await OpenConnection();

        try
        {
            // Load database file into DuckDB-WASM if needed
            // Note: dbPath on browser might be a URL, IndexedDB key, or OPFS path
            // For now, assume database is already loaded or accessible via OPFS
            
            // Query seeds sorted by length
            string sql = $"SELECT {columnName} FROM {tableName} ORDER BY LENGTH({columnName})";
            string jsonResult = await QueryDuckDB(connId, sql);
            
            // Parse JSON array
            var seeds = JsonSerializer.Deserialize<string[]>(jsonResult) ?? Array.Empty<string>();
            
            foreach (var seed in seeds)
            {
                yield return seed;
            }
        }
        finally
        {
            await CloseConnection(connId);
        }
    }
}

// DuckDBSeedProvider removed - use DuckDBSeeds.Stream() with WithListSearch() instead
// WithListSearch() wraps the stream in MotelySeedListProvider which handles multi-threading
#endif

