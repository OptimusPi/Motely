using System.Collections.Generic;
using Motely;

namespace Motely.Executors;

/// <summary>
/// Write/read abstraction for a search result database.
/// Only Orchestration implements this (via Motely.DB); API never touches DuckDB.
/// </summary>
public interface IResultsDatabaseWriter : IDisposable
{
    void InsertRow(string seed, int score, List<int>? tallies, List<string?>? columnValues);
    void SaveBatchPosition(long batch, int batchSize);
    void Checkpoint();
    List<MotelySearchResultRow> GetTopResults(int limit = 1000);
}
