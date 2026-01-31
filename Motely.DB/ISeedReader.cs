namespace Motely.DB;

/// <summary>
/// Stateful seed reader: one connection, one streaming DuckDBDataReader.
/// Fill buffers in batches; caller disposes when done.
/// </summary>
public interface ISeedReader : IDisposable
{
    /// <summary>
    /// Read up to buffer.Length seeds into buffer. Returns count read; 0 when exhausted.
    /// </summary>
    int ReadSeeds(string[] buffer);
}
