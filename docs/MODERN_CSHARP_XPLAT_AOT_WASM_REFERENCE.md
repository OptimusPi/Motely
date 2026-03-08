# Modern C# Cross-Platform AOT WASM Reference

This reference explains the real Motely architecture for modern C# cross-platform apps using native .NET AOT WebAssembly, desktop .NET, and Node/browser package distribution without Blazor-specific assumptions.

## Goals

- Keep core search and analysis logic in C#
- Run the same engine across desktop, browser, and Node entry points
- Use AOT WebAssembly for browser performance
- Keep runtime asset hosting explicit instead of pretending bundler magic exists
- Treat DuckDB as the results/query layer where it actually helps

## Project map

- `Motely/`
  - Core search engine, JAML parsing, filter descriptors, analyzers, SIMD-heavy search path
- `Motely.Orchestration/`
  - Search launch/config wiring around the core engine
- `Motely.DB/`
  - Native DuckDB-backed result storage and export helpers
- `Motely.DB.Browser/`
  - Browser-safe stub matching the native DB surface
- `Motely.BrowserWasm/`
  - Browser-targeted .NET WASM build (`net10.0-browser`, AOT, SIMD, threads)
- `Motely.SingleThread/`
  - Browser-targeted single-thread runtime build for non-isolated hosting
- `Motely.node/`
  - `motely-node` npm package for Node/V8 usage
- `Motely.npm/`
  - `motely-wasm` npm package for browser usage
- `Motely.npm.singlethread/`
  - `motely-wasm-singlethread` npm package for forced single-thread browser usage

## Runtime model

## Core engine first

The C# engine is the product. Browser and Node packages are delivery shells around that engine.

The important split is:

- core search/analyzer logic stays in `Motely`
- orchestration/persistence belongs around the engine, not jammed through UI-only JS callbacks
- package loaders are thin wrappers around staged .NET runtime assets

## Browser model

The browser build is **not Blazor UI**. It is .NET AOT WASM hosted as runtime assets and called through exported JS interop surfaces.

Key properties:

- `net10.0-browser`
- AOT compilation enabled
- SIMD enabled
- threaded runtime available when `crossOriginIsolated` is available
- single-thread fallback runtime available for preview/non-isolated hosts

## Node model

`motely-node` ships the single-thread .NET runtime assets plus JS entrypoints for Node/V8. The package must include `_framework/` in the tarball or it is broken.

## DuckDB role

DuckDB matters when it is used as a **results store and query engine**, not as a fantasy cross-WASM shared-memory bridge.

### What DuckDB is good for here

- storing scored search rows efficiently
- paging top results without holding everything in UI memory
- exporting CSV / Parquet for downstream querying
- desktop/native result persistence
- remote Parquet consumption by DuckDB-WASM later

### What the old BalatroSeedOracle shape got right

The old BalatroSeedOracle flow treated DuckDB as the backing results database that the UI queried incrementally.

Important pattern:

- search execution owned result production
- UI consumed paged results from a search context / results store
- DuckDB-backed queries were used for loading existing results and fetching additional pages
- this avoids turning JS interop into the primary bulk-results transport

That is the architectural reason DuckDB still matters.

### What not to pretend

Do not describe the architecture as direct zero-copy buffer sharing from .NET WASM into DuckDB-WASM. Separate WASM runtimes do not share linear memory that way.

The practical high-performance path is:

- C# engine computes results
- native/Desktop path writes DuckDB / Parquet
- browser analytics path lets DuckDB-WASM read Parquet/Arrow files it can access directly

## Package/runtime asset rules

## `motely-wasm`

Ships two runtime folders:

- `_framework/` for threaded runtime hosting
- `_framework_st/` for bundled single-thread hosting

Consumers must serve those assets explicitly and set COOP/COEP for threaded mode.

## `motely-wasm-singlethread`

Ships a single-thread `_framework/` only.

## `motely-node`

Ships `_framework/` plus JS/CJS entrypoints and JAML schema exports.

## Documentation rules

Keep package docs factual:

- no claims about nonexistent Vite/Next plugins
- no claim that BrowserWasm publish automatically fixes consumer hosting
- no claim that threaded hosting works without COOP/COEP
- no claim that DuckDB-WASM receives .NET buffers zero-copy

## Release checklist for runtime packages

For each release:

1. publish the browser and single-thread .NET runtime outputs
2. stage runtime assets into package folders
3. run `npm pack --dry-run` for all packages
4. inspect tarball contents for `_framework`, `_framework_st`, and `jaml.schema.json`
5. install tarballs into clean test apps
6. verify runtime boot in Node and browser consumers before `npm publish`

## Recommended documentation structure

Good Motely docs should read like a modern cross-platform product reference:

- overview
- architecture
- runtime asset hosting
- browser threading requirements
- package-specific usage
- DuckDB/result-storage model
- release verification checklist
- troubleshooting

That structure fits the current Motely reality better than framework-specific plugin marketing.
