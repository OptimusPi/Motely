using Motely.Filters;

namespace Motely.Repository;

/// <summary>
/// Repository abstraction for seed sources and result storage.
/// Desktop: DuckDB-based implementation. Browser/WASM: In-memory only.
/// </summary>
public interface IMotelyRepository
{
    /// <summary>Get a seed provider by moniker (file path or identifier).</summary>
    IMotelySeedProvider GetSource(string moniker);

    /// <summary>Get result storage by moniker (file path or identifier).</summary>
    IResultStorage GetSink(string moniker, MotelyRunConfig runConfig);
}
