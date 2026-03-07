using System;

namespace Motely.DB;

/// <summary>
/// A WASM-compatible dummy wrapper for MotelyResultsDb.
/// DuckDB does not support WASM via native bindings out of the box, 
/// so we simply ignore saves in the browser version natively.
/// </summary>
public sealed class MotelyResultsDb : IDisposable
{
    public int TallyCount { get; }

    public MotelyResultsDb(string dbPath, int tallyCount)
    {
        TallyCount = tallyCount;
    }

    public void AppendResults(ReadOnlySpan<ResultRow> rows) { }

    public void AppendResult(string seed, int score, ReadOnlySpan<int> tallies) { }

    public void Dispose() { }
}

public readonly record struct ResultRow(string Seed, int Score, int[] Tallies);
