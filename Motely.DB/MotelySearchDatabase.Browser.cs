using System;
using System.Collections.Generic;
using Motely.Filters;

namespace Motely.DB;

public sealed class MotelySearchDatabase : IDisposable, IResultStorage
{
    public string DatabasePath => throw new PlatformNotSupportedException("Database storage not available in browser.");
    
    public MotelySearchDatabase(string dbPath, MotelyRunConfig runConfig, Action<string>? logCallback = null)
    {
        throw new PlatformNotSupportedException("Database storage not available in browser.");
    }
    
    public static bool IsSchemaCompatible(string dbPath, MotelyRunConfig runConfig, out string? error)
    {
        error = "Database storage not available in browser.";
        return false;
    }
    
    public int GetResultCount() => throw new PlatformNotSupportedException("Database storage not available in browser.");
    
    public List<Dictionary<string, object?>> GetResultsPage(int offset, int limit) => throw new PlatformNotSupportedException("Database storage not available in browser.");
    
    public void InsertRow(string seed, int score, List<int> tallies, List<string?>? columnValues = null) => throw new PlatformNotSupportedException("Database storage not available in browser.");
    
    public void Dispose() { }
}
