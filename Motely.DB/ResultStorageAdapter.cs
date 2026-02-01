using System.Collections.Generic;
using Motely;

namespace Motely.DB;

/// <summary>
/// Wraps MotelySearchDatabase so callers see only IResultStorage.
/// Desktop implementation; browser/WASM uses a different implementation behind the same interface.
/// </summary>
public sealed class ResultStorageAdapter : IResultStorage
{
    private readonly MotelySearchDatabase _db;

    public ResultStorageAdapter(MotelySearchDatabase db) => _db = db ?? throw new ArgumentNullException(nameof(db));

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

    public int GetResultCount() => _db.GetResultCount();
    public List<Dictionary<string, object?>> GetResultsPage(int offset, int limit) => _db.GetResultsPage(offset, limit, "score", false);

    public void Dispose() => _db.Dispose();
}
