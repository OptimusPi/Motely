# Build WASM (browser)

Build **`Motely`** for `net10.0-browser` (Bootsharp) and stage into **`motely-wasm/dist/`**.

## Quick build

```bash
dotnet publish Motely/Motely.csproj -c Release -f net10.0-browser
npm --prefix motely-wasm install
npm --prefix motely-wasm run build
```

(`motely-wasm`'s `build` script runs `Motely/build/stage-wasm.mjs`, which copies `publish/bootsharp` → `motely-wasm/dist/bootsharp` and writes `dist/index.mjs`.)

## What this produces

- `Motely/bin/Release/net10.0-browser/publish/bootsharp/` (publish output)
- `motely-wasm/dist/bootsharp/` + `motely-wasm/dist/index.mjs` (staged npm payload)

Legacy paths (`Motely.BrowserWasm`, `Motely.SingleThread`, `Motely.npm`, root `stage-packages.mjs`) are **removed** from the tree.

## Requirements

- .NET 10 SDK
- wasm-tools workload: `dotnet workload install wasm-tools`
- wasm-experimental workload: `dotnet workload install wasm-experimental` (if your SDK still expects it)

## Troubleshooting

### wasm-opt crashes

If applicable, disable in the browser target: `<WasmRunWasmOpt>false</WasmRunWasmOpt>`

### Missing SharedArrayBuffer

Threads + WASM may require COOP/COEP headers on the host page.
