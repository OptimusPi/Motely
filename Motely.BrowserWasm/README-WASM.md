# Motely browser WASM npm packages

## Default: `motely-wasm` (single-thread)

From repo root (`src/MotelyJAML`):

```bash
dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
```

Output: `Motely.BrowserWasm/motely-wasm/` (Bootsharp ES module + wasm + types). NPM metadata is overlaid from `Motely/package.json`.

Verify tarball:

```bash
cd Motely.BrowserWasm/motely-wasm
npm pack --dry-run
```

## Optional: `motely-wasm-mt` (pthread / multi-thread WASM)

**Status:** On some toolchains (e.g. current LLVM NativeAOT browser-wasm packages), linking fails with `wasm-ld: --shared-memory is disallowed ... 'atomics' or 'bulk-memory'` when `WasmEnableThreads` is true. The MSBuild switch is still supported for when a future SDK/runtime fixes this.

Some LLVM / SDK combinations fail to link threaded wasm; only ship `motely-wasm-mt` after a successful local publish.

```bash
dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release /p:MotelyWasmThreads=true
```

Output: `Motely.BrowserWasm/motely-wasm-mt/` with `package.json` name `motely-wasm-mt`.

**Browser hosting:** threaded WASM typically needs **cross-origin isolation**:

- `Cross-Origin-Opener-Policy: same-origin`
- `Cross-Origin-Embedder-Policy: require-corp` (or `credentialless` with compatible CORP on subresources)

Then `crossOriginIsolated === true` and SharedArrayBuffer-based threading can work. The default `motely-wasm` package does not require these headers.

## SeedSearcherWebsite

After publish, copy the folder into the static site (or `npm install` from `file:` / registry). See `Motely.SeedSearcherWebsite/scripts/copy-motely-wasm.mjs`.
