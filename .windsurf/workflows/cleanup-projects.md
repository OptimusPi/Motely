# Project Cleanup Guide

Remove the redundant/haunted projects that are confusing the build.

## Projects to Remove

### 1. Motely.BrowserWasm/ (DUPLICATE)
- Same as Motely.NodeWasm/
- Just copies to Motely.npm/_framework
- **Action**: Delete this entire folder

### 2. Motely.WASI/ (FAILED EXPERIMENT)
- WASI AOT compilation is broken in .NET 10
- Missing wasi-sdk requirement
- LLVM linker issues
- **Action**: Delete this entire folder

### 3. Motely.node/ (MESSY ADAPTER)
- Previous AI tried to create a Node.js "adapter"
- Unnecessary complexity
- **Action**: Delete this entire folder

### 4. Motely.NodeWasm/Motely.BrowserWasm.csproj (MISNAMED)
- Project file name doesn't match folder
- **Action**: Rename to Motely.NodeWasm.csproj

## Clean Commands

```bash
# Remove duplicate projects
rm -rf Motely.BrowserWasm/
rm -rf Motely.WASI/
rm -rf Motely.node/

# Rename project file
mv Motely.NodeWasm/Motely.BrowserWasm.csproj Motely.NodeWasm/Motely.NodeWasm.csproj

# Clean build artifacts
dotnet clean
rm -rf */bin */obj
```

## What's Left

- `Motely/` - Core library
- `Motely.NodeWasm/` - Browser WASM build (use this for Node.js too!)
- `Motely.CLI/` - Command line tool
- `Motely.npm/` - Browser npm package
- `Motely.Tests/` - Unit tests

## Result

Single source of truth: `Motely.NodeWasm` produces WASM that works in:
- Browser (via Motely.npm)
- Node.js (direct dotnet.js import)
- Web Workers
- Any JS host with WASM support
