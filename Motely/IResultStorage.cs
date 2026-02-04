namespace Motely;

/// <summary>
/// Abstraction for search result storage (e.g. DuckDB on desktop, in-memory on browser).
/// Implemented by Motely.DB.MotelySearchDatabase. Used by Orchestration so browser build
/// can reference this interface without referencing Motely.DB (no DuckDB in WASM/NPM package).
/// </summary>
public interface IResultStorage : IDisposable
{
    int GetResultCount();
    List<Dictionary<string, object?>> GetResultsPage(int offset, int limit);
    void InsertRow(string seed, int score, List<int> tallies, List<string?>? columnValues = null);
}
