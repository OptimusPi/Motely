using Motely.Filters;
using Motely.Repository;

namespace Motely.BrowserWasm;

/// <summary>
/// Minimal IMotelyRepository for browser WASM.
/// No DuckDB. No filesystem. In-memory search uses MotelySearchContext's in-memory storage.
/// GetSource throws (no seed source files in browser). GetSink throws (use in-memory mode).
/// </summary>
public sealed class BrowserRepository : IMotelyRepository
{
    public static readonly BrowserRepository Instance = new();

    public IMotelySeedProvider GetSource(string moniker) =>
        throw new PlatformNotSupportedException(
            "Seed source files are not available in browser WASM. Use seed lists or random seeds.");

    public IResultStorage GetSink(string moniker, MotelyRunConfig runConfig) =>
        throw new PlatformNotSupportedException(
            "Database storage is not available in browser WASM. Use in-memory mode (useInMemoryStorage: true).");
}
