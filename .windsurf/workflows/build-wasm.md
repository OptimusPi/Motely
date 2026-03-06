# Build WASM Browser Project

Build the Motely NodeWasm project for browser/Node.js usage.

## Quick Build

```bash
dotnet publish Motely.NodeWasm/Motely.BrowserWasm.csproj -c Release
```

## What This Produces

- `Motely.NodeWasm/bin/Release/net10.0-browser/publish/wwwroot/_framework/`
  - `dotnet.js` - Main entry point for JavaScript hosts
  - `dotnet.runtime.js` - .NET runtime
  - `dotnet.native.wasm` - Compiled AOT WASM module
  - `Motely.NodeWasm.wasm` - Your compiled app

## Auto-Copy to NPM Package

The build automatically copies files to `Motely.npm/_framework/` via the `PublishToNpmPackage` MSBuild target.

## Requirements

- .NET 10 SDK
- wasm-tools workload: `dotnet workload install wasm-tools`
- wasm-experimental workload: `dotnet workload install wasm-experimental`

## Troubleshooting

### wasm-opt crashes
Already disabled in csproj: `<WasmRunWasmOpt>false</WasmRunWasmOpt>`

### Missing SharedArrayBuffer
For Node.js: Use `--experimental-wasm-threads` or enable via flags
For Browser: Needs COOP/COEP headers (see vite-plugin in Motely.npm)
