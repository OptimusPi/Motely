using System.Collections.Generic;

namespace Motely.Orchestration.Browser;

/// <summary>
/// Browser/WASM result storage. In-memory or DuckDB WASM (via JS interop).
/// </summary>
public interface IWasmResultStorage
{
    void InsertRow(string seed, int score, IReadOnlyList<int>? tallies);
    IReadOnlyList<(string Seed, int Score, int[]? Tallies)> GetTopResults(int limit);
    void Dispose();
}
