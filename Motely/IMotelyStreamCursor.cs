namespace Motely;

/// <summary>
/// Stateful cursor over one Balatro PRNG stream for a single seed (shop items, tags, vouchers, …).
/// </summary>
public interface IMotelyStreamCursor : IDisposable
{
    int GetNext();

    /// <summary>Advance <paramref name="count"/> steps in one call (preferred for WASM/UI batching).</summary>
    int[] GetNextChunk(int count);
}
