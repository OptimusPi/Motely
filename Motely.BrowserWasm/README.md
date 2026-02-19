# Motely.BrowserWasm

Browser-only WebAssembly package for running Motely/JAML searches with SIMD + multi-threading.

## Overview

Pure C# AOT WebAssembly targeting `net10.0-browser` with:
- **AOT Compilation** - Native WASM for maximum performance
- **Threading** - Real multi-threading via SharedArrayBuffer
- **SIMD** - Vector512 operations for high-speed filtering
- **Bundler-Friendly** - Designed for Vite/Next.js, NOT standalone

## Building

```bash
# Build Release (generates _framework/ files)
dotnet publish -c Release Motely.BrowserWasm
```

This automatically copies WASM files to `../Motely.npm/_framework/` for packaging.

## Usage

Use the `motely-wasm` npm package:

```bash
npm install motely-wasm
```

### Vite
```js
// vite.config.js
import { defineConfig } from "vite";
import motelyWasm from "motely-wasm/vite-plugin";

export default defineConfig({
  plugins: [motelyWasm()],
});
```

### Next.js
```js
// next.config.mjs
import withMotelyWasm from "motely-wasm/next-plugin";

export default withMotelyWasm({
  // your Next.js config
});
```

## Exposed APIs

All `[JSExport]` methods for JavaScript interop:

- `GetVersionAsync()` - Package version and features
- `GetCapabilitiesAsync()` - SIMD/threads status
- `AnalyzeSeed(seed, deck, stake)` - Single seed analysis
- `StartJamlSearch(jaml, options)` - Async JAML search
- `GetSearchStatus(searchId)` - Current progress
- `StopSearch(searchId)` - Cancel running search

## Architecture

- **Motely** - Core JAML parser, filters, and search engine
- **BrowserWasm** - Thin JS bridge with `[JSExport]` methods
- **No Orchestration/Repository** - Browser WASM uses in-memory storage

## Performance

- **Threading**: Uses .NET's internal threading via SharedArrayBuffer
- **SIMD**: Vector512 operations for seed filtering
- **AOT**: No JIT compilation, pure native WASM
- **Streaming**: Progress/results pushed via callbacks, no polling

## Troubleshooting

### Threading Disabled
If searches run single-threaded:
- Missing COOP/COEP headers (use the provided plugins)
- Not using HTTPS (SharedArrayBuffer requires secure context)

### MIME Type Errors
Don't use `dotnet run` - this is designed for bundlers, not standalone.
