namespace Motely;

/// <summary>
/// Read-only abstraction for a search result storage (e.g. DuckDB on desktop).
/// Implemented by Motely.DB.MotelySearchDatabase. Used by Orchestration so browser build
/// can reference this interface without referencing Motely.DB (no DuckDB in WASM/NPM package).
/// </summary>
public interface IResultStorage : IDisposable
{
    int GetResultCount();
    List<Dictionary<string, object?>> GetResultsPage(int offset, int limit);
}
