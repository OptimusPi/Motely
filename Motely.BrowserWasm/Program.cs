using Motely.BrowserWasm;
using Motely.Executors;
using Motely.Repository;

// Register browser repository (no DuckDB, no filesystem)
MotelySearchOrchestrator.SetRepository(BrowserRepository.Instance);

// Keep alive - required for WASM host to stay resident for JSExport calls
await Task.Delay(Timeout.Infinite);
