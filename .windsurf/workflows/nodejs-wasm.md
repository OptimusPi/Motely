# Using WASM in Node.js

Run the .NET WASM build directly in Node.js without any adapter nonsense.

## Quick Start

```javascript
import { dotnet } from './Motely.NodeWasm/bin/Release/net10.0-browser/publish/wwwroot/_framework/dotnet.js';

const { getAssemblyExports, getConfig } = await dotnet.create();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

// Call [JSExport] methods directly
const version = await exports.Motely.BrowserWasm.MotelyWasmExports.GetVersionAsync();
console.log(JSON.parse(version));
```

## Available [JSExport] Methods

- `GetVersionAsync()` → JSON string with version info
- `GetCapabilitiesAsync()` → JSON with SIMD/threads status  
- `AnalyzeSeedAsync(seed, deck, stake)` → JSON analysis result
- `ValidateJamlAsync(jamlContent)` → JSON validation result
- `StartJamlSearch(jamlContent, optionsJson)` → JSON search result
- `StopSearch()` → void
- `DisposeSearch()` → void

## Setting up Callbacks

The WASM expects these global callbacks for search progress:

```javascript
globalThis.__motelyOnProgress = (searched, matches, elapsed, count) => {
  console.log(`Progress: ${searched} seeds searched, ${matches} matches`);
};

globalThis.__motelyOnResult = (seed, score) => {
  console.log(`Found: ${seed} (score: ${score})`);
};
```

Set these BEFORE calling `dotnet.create()`.

## Node.js Flags for Threading

```bash
node --experimental-wasm-threads --experimental-wasm-bulk-memory your-script.js
```

Or use `NODE_OPTIONS`:
```bash
set NODE_OPTIONS=--experimental-wasm-threads --experimental-wasm-bulk-memory
node your-script.js
```

## Full Example

See `test-node.mjs` in project root for complete working example.
