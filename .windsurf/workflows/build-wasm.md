# Build WASM (browser)

Build **`Motely.Orchestration`** with **`WasmBuild=true`** (Bootsharp + NativeAOT-LLVM for `browser-wasm`), then stage into **`motely-wasm/dist/`**.

**Do not** publish `Motely/Motely.csproj` with `-f net10.0-browser` — the engine project is **`net10.0` only**; the browser entry is **Orchestration**. See repo-root **`AGENTS.md`**.

## Quick build (recommended)

From repo root:

```bash
node build.mjs wasm
```

This restores/builds/publishes Orchestration with `-p:WasmBuild=true` and copies Bootsharp output into `motely-wasm/dist/`.

## Manual equivalent

```bash
dotnet publish Motely.Orchestration/Motely.Orchestration.csproj -c Release -p:WasmBuild=true
node Motely/build/stage-wasm.mjs
```

(`stage-wasm.mjs` reads **`Motely.Orchestration/bin/bootsharp/`** and writes **`motely-wasm/dist/index.mjs`**, `dist/types/`, `dist/jaml.schema.json`.)

## What this produces

- Publish output: under `Motely.Orchestration/bin/Release/net10.0/browser-wasm/` (and Bootsharp bundle under `Motely.Orchestration/bin/bootsharp/` as produced by the toolchain).
- npm payload: **`motely-wasm/dist/index.mjs`** (+ `types/`, `jaml.schema.json` when present).

## Requirements

- .NET 10 SDK
- Experimental LLVM / wasm workload chain as required by your SDK (see Bootsharp / ILCompiler docs for this repo’s versions).

## Troubleshooting

### wasm-opt crashes

If applicable, disable in the relevant project: `<WasmRunWasmOpt>false</WasmRunWasmOpt>` (only if your target supports it).

### Missing SharedArrayBuffer

Threads + WASM may require COOP/COEP headers on the host page.

### Copy / PDB errors after a failed or overlapping build

Stop other `dotnet` processes, clean `bin`/`obj` for the projects involved, then **one** full publish. See **`AGENTS.md`**.
