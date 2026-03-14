# Project Cleanup Guide

Remove the dead runtime experiments and keep the current publish flow explicit.

## Projects to Remove

### 1. Motely.NodeWasm/
- Not part of the solution
- Not the source of truth for `motely-node`
- Duplicates browser-wasm concepts already covered by `Motely.BrowserWasm` and `Motely.SingleThread`
- **Action**: Delete this entire folder

### 2. Motely.npm.singlethread/
- Dead package surface
- Replaced by `motely-wasm`, which now carries both `_framework/` and `_framework_st/`
- **Action**: Delete this entire folder

## Projects to Keep

### 1. Motely.BrowserWasm/
- Primary threaded browser runtime

### 2. Motely.SingleThread/
- Single-thread browser runtime
- Source of truth for `motely-node`

### 3. Motely.node/
- npm package wrapper for the single-thread runtime
- Keep, but publish only the native `.node` binaries in `bin/`

## Clean Commands

```bash
# Remove dead runtime experiment
rm -rf Motely.NodeWasm/

# Remove dead browser package split
rm -rf Motely.npm.singlethread/

# Clean build artifacts
dotnet clean
rm -rf */bin */obj
```

## What's Left

- `Motely/` - Core library
- `Motely.BrowserWasm/` - Threaded browser WASM build
- `Motely.SingleThread/` - Single-thread browser/Node WASM build
- `Motely.CLI/` - Command line tool
- `Motely.npm/` - Browser npm package
- `Motely.node/` - Node npm package
- `Motely.Tests/` - Unit tests

## Result

Single source of truth:
- `Motely.BrowserWasm` produces the threaded browser runtime
- `Motely.SingleThread` produces the single-thread runtime for browser fallback and Node.js
- `stage-packages.mjs` stages browser publish output into `Motely.npm/_framework` and `Motely.npm/_framework_st`
