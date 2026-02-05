# TypeScript Type Generation for Motely WASM

## Problem

Hand-rolling TypeScript types for C# exports leads to drift and maintenance burden. We need automatic type generation.

## Solutions

### Option 1: dotnet-node-api (For Node.js hosting)

**When to use:** If you host Motely as a Node.js addon (future)

**How it works:**
1. Add `Microsoft.JavaScript.NodeApi` NuGet package
2. Add `Microsoft.JavaScript.NodeApi.Generator` NuGet package (generates types)
3. Decorate C# methods with `[JSExport]` or use Node API attributes
4. Types are auto-generated during build to `.d.ts` files

**Limitation:** Only works for Node.js, not browser WASM

### Option 2: Source Generator for WASM Browser

**When to use:** For browser WASM (current use case)

**How it works:**
1. Create a C# source generator that scans for `[JSExport]` attributes
2. Generate TypeScript types during compilation
3. Output to `Motely.npm/index.d.ts`

**Implementation:**
- Use `ISourceGenerator` to scan for `[JSExport]` methods
- Parse method signatures and generate TypeScript equivalents
- Map C# types to TypeScript types (string → string, int → number, etc.)

### Option 3: MSBuild Target with Reflection

**When to use:** Quick solution, works now

**How it works:**
1. After build, use reflection to scan the compiled assembly
2. Find all `[JSExport]` methods
3. Generate TypeScript types via PowerShell/C# script
4. Output to `Motely.npm/index.d.ts`

**Limitation:** Requires assembly to be built first (can't use in IDE)

## Recommended Approach

**For now:** Use Option 3 (MSBuild target) - quick to implement
**Future:** Migrate to Option 2 (source generator) for better IDE integration

## Current State

Types are **hand-rolled** in `Motely.npm/index.d.ts`. The C# exports live in `Motely.WASM/WasmCapabilities.cs` (`MotelyWasm` static class, `[JSExport]` methods). When you add or change a JSExport there, you must update `Motely.npm/index.d.ts` and the loader in `Motely.npm/index.ts` / `index.js` by hand.

**Actual API surface** (from WasmCapabilities.cs):  
`GetVersion`, `GetCapabilities`, `IsSimdEnabled`, `IsThreadingEnabled`, `GetProcessorCount`, `AnalyzeSeed`, `StartJamlSearch`, `GetSearchStatus`, `StopSearch`.  
Loader: `loadMotely(basePath?: string): Promise<MotelyWasmApi>`.

## Next Steps (if you want auto-generation)

1. Implement Option 3 (MSBuild target + reflection) or Option 2 (source generator).
2. Wire it so build outputs/updates `Motely.npm/index.d.ts` from the compiled WASM assembly or source.
