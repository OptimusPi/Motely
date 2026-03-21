# MotelyJAML — Agent Guidelines

## What This Project Is

A .NET library + WASM/Node packages for Balatro seed analysis. It predicts what items, jokers, tags, vouchers, etc. a given seed will produce using Balatro's PRNG system.

## Architecture: Two Thin Hosts, One Brain

```
JS (browser) → Bootsharp → IMotelyWasmBackend → MotelyWasmBackend → MotelySearchOrchestrator
JS (node)    → Node-API  → MotelyNodeExports                      → MotelySearchOrchestrator
```

```
motely-wasm/   ← Browser WASM output (published to npm, Bootsharp bundle)
motely-node/   ← Node.js native addon output (published to npm)
```

Both are **OUTPUT DIRECTORIES**. They are populated by `build-and-pack.ps1`.

### The Layering Rule

```
Platform hosts (WASM/Node)  →  MotelySearchOrchestrator  →  Motely (core library)
    thin interop only              all search logic              don't touch
```

- `MotelyWasmBackend` builds `MotelySearchRequest`, manages `MotelySearchSession`, wires Bootsharp events, calls `MotelySearchOrchestrator` directly.
- `MotelyNodeExports` builds `MotelySearchRequest`, calls `MotelySearchOrchestrator` directly.
- There is NO intermediate static class. If you feel the urge to create a "shared interop API" between the two hosts, stop. They both call the orchestrator.

### DO NOT EDIT these directories:
- `motely-wasm/` — generated output
- `motely-node/` — generated output

### DO NOT EDIT core Motely library files:
- `Motely/MotelySingleSearchContext*.cs` — core PRNG engine, not ours
- `Motely/MotelySingle*Stream.cs` — stream types, not ours
- `Motely/Filters/` — search filters, not ours
- Any file authored by TacoDiva (the Motely library author)

### Files YOU should edit:
- `Motely/MotelyGameplayState.cs` — our game state wrapper
- `Motely.Orchestration/MotelySearchOrchestrator.cs` — all search logic lives here
- `Motely.Orchestration/MotelySearchSession.cs` — browser instance handles / cancellation
- `Motely.BrowserWasm/Interop/` — Bootsharp interfaces + implementation (calls orchestrator)
- `Motely.NodeAddon/MotelyNodeExports.cs` — `[JSExport]` boundary (calls orchestrator)
- `Motely/Analysis/` — analyzer and DTOs

## Browser WASM: Bootsharp

The browser build uses [Bootsharp](https://bootsharp.com) with `Microsoft.NET.Sdk` (not `Sdk.WebAssembly`).

- `IMotelyWasmBackend` — `[JSExport]` interface, auto-generates JS bindings + TypeScript types
- `IMotelyJsUi` — `[JSImport]` interface, events pushed from .NET to JS (`NotifySearchProgress`, `NotifySearchResult`)
- `Program.cs` — DI wiring: `AddBootsharp()` + `AddSingleton<IMotelyWasmBackend, MotelyWasmBackend>()` + `RunBootsharp()`
- `Motely.BrowserWasm.csproj` has a `BootsharpLLVM` flag (currently `false`) for future NativeAOT-LLVM

Bootsharp output lands in `Motely.BrowserWasm/bin/bootsharp/`. The stage script copies it to `motely-wasm/bootsharp/` (and `bootsharp_st/` for single-thread).

## Build Pipeline

```powershell
./build-and-pack.ps1   # auto-bumps patch version, builds ST+MT WASM, stages, packs both npm packages
```

The script:
1. Bumps patch version in `Directory.Packages.props` + both `package.json` files
2. Publishes single-thread WASM → stages to `motely-wasm/bootsharp_st/`
3. Publishes multi-thread WASM → stages to `motely-wasm/bootsharp/` (falls back to ST copy if MT fails)
4. Runs `npm pack` for motely-wasm
5. Builds linux-x64 Node addon via Docker
6. Runs `npm pack` for motely-node
7. Prints 3 copy-paste blocks: `npm login`, publish wasm, publish node

## Key Concepts

### Streams Are Doubles

Every PRNG stream's state is ultimately a `double`. `MotelySinglePrngStream(double state)` wraps it. Higher-level streams compose multiple PrngStreams. Each call to get the next item advances the double.

### ref struct vs struct

Some stream types are `ref struct` (stack-only, cannot be stored on the heap). When you need to store a `ref struct` as a class field, decompose its fields into a storage struct (they're just doubles and strings), then reconstruct on demand. See `StreamCache` in `MotelyGameplayState.cs`.

Do NOT change `ref struct` to `struct` in core Motely files. Work around it.

### The Parked Filter Pattern

`MotelyGameplayState` uses the search pipeline as a **context factory**. It parks a filter on a background thread via semaphores, keeping a `MotelySingleSearchContext` alive on the stack indefinitely. This is the ONLY way to get a context for single-seed analysis.

**DO NOT touch the parked filter code** (`ParkedFilterDesc`, `ParkedFilter`, `CheckSeed`, the semaphore dance). It is correct.

**DO NOT touch the `Cmd<T>` delegate dispatch.** It is correct.

### One Ante At A Time

The game state represents sequential gameplay. One ante at a time. When the ante changes, reset streams. Do NOT use `Dictionary<int, StreamType>` to cache per-ante.

### MotelyGameplayState IS the Single Seed Context

It's a stateful object that wraps `MotelySingleSearchContext` for one seed. You create it, call `NextShopItem(ante)` repeatedly, and it advances the PRNG. Infinite scroll. Don't pre-compute, don't batch, don't wrap the analyzer.

## Common Pitfalls

1. **"Let me redesign the architecture"** — No. Make the minimal fix. Use what exists.
2. **"Let me add a shared interop API class"** — No. Both hosts call orchestrator directly.
3. **"Let me wrap the analyzer output"** — No. The analyzer pre-computes a fixed set. GameState is infinite scroll.
4. **"Let me add a Dictionary for caching"** — No. One ante at a time. Direct fields, reset on ante change.
5. **"Let me create a new search/filter"** — No. The parked filter IS the mechanism. Use it.
6. **"Let me edit MotelySingleSearchContext"** — No. Use Motely. Don't edit it.
7. **"Let me edit the output JS/CJS files"** — No. They're build output.
8. **"These stream types should be struct not ref struct"** — Don't change core Motely types.
9. **"Let me hand-roll PRNG logic"** — No. Motely provides high-level stream interfaces. Use them.

## Build

```bash
dotnet build Motely.BrowserWasm/Motely.BrowserWasm.csproj
dotnet build Motely.NodeAddon/Motely.NodeAddon.csproj
```

Build/pack/publish is handled by `build-and-pack.ps1`. Don't run publish commands.

## Testing Reference

Seed `AAAAA`, Deck `Red`, Stake `White`, Ante 1 shop items should produce:
DNA, Gift Card, Abstract Joker, Odd Todd, Venus, Crazy Joker, Erosion, Green Joker, Red Card, Shortcut, Superposition, Mystic Summit, Greedy Joker, The Magician, Juggler
