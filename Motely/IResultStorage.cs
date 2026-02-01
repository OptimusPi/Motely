using System.Collections.Generic;

namespace Motely;

/// <summary>
/// Write/read abstraction for search result storage. Platform-agnostic: DuckLake, in-memory, DuckDB WASM, etc.
/// Callers depend on this interface only; they never reference a specific implementation.
/// </summary>
public interface IResultStorage : IDisposable
{
    void InsertRow(string seed, int score, List<int>? tallies, List<string?>? columnValues);
    void SaveBatchPosition(long batch, int batchSize);
    void Checkpoint();
    List<MotelySearchResultRow> GetTopResults(int limit = 1000);
    int GetResultCount();
    List<Dictionary<string, object?>> GetResultsPage(int offset, int limit);
}
