using System.Collections.Generic;
using Motely;
using Motely.DB;

namespace Motely.Executors;

/// <summary>
/// Wraps MotelySearchDatabase so API only sees IResultsDatabaseWriter (Orchestration gate to Motely.DB).
/// </summary>
internal sealed class ResultsDatabaseWriterAdapter : IResultsDatabaseWriter
{
    private readonly MotelySearchDatabase _db;

    public ResultsDatabaseWriterAdapter(MotelySearchDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public void InsertRow(string seed, int score, List<int>? tallies, List<string?>? columnValues) =>
        _db.InsertRow(seed, score, tallies ?? new List<int>(), columnValues);

    public void SaveBatchPosition(long batch, int batchSize) => _db.SaveBatchPosition(batch, batchSize);
    public void Checkpoint() => _db.Checkpoint();

    public List<MotelySearchResultRow> GetTopResults(int limit = 1000)
    {
        var rows = _db.GetTopResults(limit);
        return rows.ConvertAll(r => new MotelySearchResultRow
        {
            Seed = r.Seed,
            Score = r.Score,
            Tallies = r.Tallies
        });
    }

    public void Dispose() => _db.Dispose();
}
