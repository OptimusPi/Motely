# Motely.BrowserWasm

Browser-based WebAssembly for running Motely/JAML searches and analysis directly in the browser.

## Overview

This project compiles the Motely library to WebAssembly using Mono and the `net10.0-browser` target framework. It's configured with:

- **AOT Compilation** (`RunAOTCompilation=true`) for faster startup and better performance
- **Threading** (`WasmEnableThreads=true`) for parallel search support
- **SIMD** (`WasmEnableSIMD=true`) for vectorized operations
- **Bundler-Friendly Configuration** (`WasmBundlerFriendlyBootConfig=true`) for use with Vite, Webpack, etc.

## Building

```bash
# Build for development
dotnet build Motely.BrowserWasm

# Build Release with optimizations
dotnet build -c Release Motely.BrowserWasm

# Publish (generates _framework/ files)
dotnet publish -c Release Motely.BrowserWasm
```

## Testing

**Do NOT use `dotnet run`** on this project. The `WasmBundlerFriendlyBootConfig=true` setting means this WASM is designed to be bundled by external tools (Vite, Next.js), not served standalone.

### Recommended: Use via npm Package + Vite

1. Publish the project:
   ```bash
   dotnet publish -c Release Motely.BrowserWasm
   ```

2. This copies the `_framework` files to `Motely.npm/_framework/`

3. Use the `motely-wasm` npm package in your bundler:
   - **Vite**: See `vite-plugin-motely-wasm.js`
   - **Next.js**: See `next-plugin-motely-wasm.js`

4. Test via `vue-jaml-ui` or your own web app

## Exposed APIs

All public APIs are marked with `[JSExport]` for JavaScript interop:

- `GetVersion()` - Returns package version
- `GetCapabilities()` - Returns supported features
- `AnalyzeSeed(seed, deck, ...)` - Analyzes a single seed
- `StartJamlSearch(jaml, ...)` - Starts an async JAML search
- `GetSearchStatus(searchId)` - Gets current search progress
- `StopSearch(searchId)` - Stops a running search

## Architecture

- **Motely** (.csproj) - Core JAML parser and analyzer
- **Motely.Orchestration** (.csproj) - Search orchestration (supports both desktop and browser)
- **Motely.Repository** (.csproj) - Abstraction for data sources and persistence

All JSON serialization uses `JsonSerializerContext` for AOT compatibility.

## Platform Compatibility

This project uses abstraction layers to support both desktop and browser:

- **Threading**: `IPauseSync` / `IWorkerHandle` interfaces with platform-specific implementations
- **Console Output**: `FancyConsole` with browser-safe fallback
- **Data Access**: `IMotelyRepository` for pluggable storage backends

## Troubleshooting

### MIME Type Errors

If you see "Expected a JavaScript-or-Wasm module script but the server responded with a MIME type of 'application/octet-stream'":

- This is a `WasmAppHost` limitation when running standalone
- **Solution**: Use the npm package + bundler instead of `dotnet run`

### Build Warnings

Common warnings and how they're resolved:

- **CA1416** ("unsupported on 'browser'"): Fixed via platform abstraction layers, not suppressed
- **CS0649** (unassigned fields): Fixed via explicit initialization, not `#pragma`
- **NETSDK1022** (duplicate content): Fixed by NOT adding `<Content Include="wwwroot/**" />` (SDK does this automatically)
