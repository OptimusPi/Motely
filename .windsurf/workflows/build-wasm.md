# Build WASM Browser Projects

Build the current Motely browser WASM projects and stage their publish outputs for npm packaging.

## Quick Build

```bash
dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
dotnet publish Motely.SingleThread/Motely.SingleThread.csproj -c Release
node stage-packages.mjs all
```

## What This Produces

- `Motely.BrowserWasm/bin/Release/net10.0-browser/publish/wwwroot/_framework/`
- `Motely.SingleThread/bin/Release/net10.0-browser/publish/wwwroot/_framework/`
- staged package assets in:
  - `Motely.npm/_framework/`
  - `Motely.npm/_framework_st/`
  - `Motely.npm.singlethread/_framework/`
  - `Motely.node/_framework/`

## Staging to NPM Packages

The root `stage-packages.mjs` script copies filtered publish assets into the npm package folders.

## Requirements

- .NET 10 SDK
- wasm-tools workload: `dotnet workload install wasm-tools`
- wasm-experimental workload: `dotnet workload install wasm-experimental`

## Troubleshooting

### wasm-opt crashes
Already disabled in csproj: `<WasmRunWasmOpt>false</WasmRunWasmOpt>`

### Missing SharedArrayBuffer
For Browser: Needs COOP/COEP headers when using `Motely.BrowserWasm`
For Node.js: use the staged `Motely.SingleThread` runtime via `motely-node`
