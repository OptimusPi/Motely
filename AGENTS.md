# MotelyJAML — Agent Guidelines

## What This Project Is

A .NET library + WASM/Node packages for Balatro seed analysis. It predicts what items, jokers, tags, vouchers, etc. a given seed will produce using Balatro's PRNG system.

### Call it JAML

Filter documents are **JAML** (`.jaml`). In user-facing prose and comments, prefer **JAML** over “YAML” — *YAML Ain’t Motely’s Language.* (YamlDotNet is still the parser; that’s implementation.)

Optional top-level **`aesthetics`** (e.g. `- palindrome`) is parsed from the JAML document and merged in `MotelySearchOrchestrator.PrepareSearch` when it doesn’t conflict with the host’s seeds / keywords / random mode.

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

### JAML JSON schema — no hand edits, no drift

`jaml.schema.json`, `jaml-schema.js`, and `jaml-schema.d.ts` are **generated from C#** by `Motely.CLI`:

```bash
dotnet run --project Motely.CLI/Motely.CLI.csproj -- --write-jaml-schema
```

`JamlSchemaGenerator` writes the same content to every mirror path (repo root, `public/`, `Motely.NodeAddon/`, `motely-wasm/`, `motely-node/`). `build-and-pack.ps1` runs this step after version bump so packs stay consistent. Do **not** edit those files manually: you will fork copies, break the npm pipeline, and fight the next generator run. New JAML surface area (e.g. `JamlAesthetic` values) belongs in **`Motely.CLI/JamlSchemaGenerator.cs`** and in the parser/enum in **`Motely`**, then regenerate.

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

Some stream types are `ref struct` (stack-only, cannot be stored on the heap). When you need to store a `ref struct` as a class field, decompose its fields into a storage struct (they're just doubles and strings), then reconstruct on demand.

Do NOT change `ref struct` to `struct` in core Motely files. Work around it.

### analyzeSeed Returns a Fixed Snapshot

`MotelySeedAnalyzer.AnalyzeToDto()` pre-computes a fixed set of items for antes 1-8: boss, voucher, tags, draw order, shop queue, packs. This is NOT infinite — it's a snapshot. The shop queue list is finite and exhaustible.

### One Ante At A Time

The game state represents sequential gameplay. One ante at a time. When the ante changes, reset streams. Do NOT use `Dictionary<int, StreamType>` to cache per-ante.

### TODO: MotelyGameplayState (Not Yet Implemented)

Infinite shop item streaming requires a stateful C# object that wraps `MotelySingleSearchContext` for one seed, advancing the shop PRNG on each call (e.g. `createSeedContext → nextShopItem(ante)`). This does NOT exist yet. Today, infinite shop streaming only works on the TypeScript Game path. Building this on the Motely/WASM side requires:
1. A new C# class (`MotelyGameplayState`) that holds live PRNG state
2. New WASM exports: `createSeedContext(seed, deck, stake)` → contextId, `nextShopItem(contextId, ante)` → item
3. JS glue in `motelyWasm.ts` to manage context lifecycle

## Common Pitfalls

1. **"Let me redesign the architecture"** — No. Make the minimal fix. Use what exists.
2. **"Let me add a shared interop API class"** — No. Both hosts call orchestrator directly.
3. **"Let me wrap the analyzer output"** — No. The analyzer pre-computes a fixed snapshot. It is not infinite.
4. **"Let me add a Dictionary for caching"** — No. One ante at a time. Direct fields, reset on ante change.
5. **"Let me edit MotelySingleSearchContext"** — No. Use Motely. Don't edit it.
6. **"Let me edit the output JS/CJS files"** — No. They're build output.
7. **"These stream types should be struct not ref struct"** — Don't change core Motely types.
8. **"Let me hand-roll PRNG logic"** — No. Motely provides high-level stream interfaces. Use them.

## Build

```bash
dotnet build Motely.BrowserWasm/Motely.BrowserWasm.csproj
dotnet build Motely.NodeAddon/Motely.NodeAddon.csproj
```

Build/pack/publish is handled by `build-and-pack.ps1`. Don't run publish commands.

## Testing Reference

Seed `AAAAA`, Deck `Red`, Stake `White`, Ante 1 shop items should produce:
DNA, Gift Card, Abstract Joker, Odd Todd, Venus, Crazy Joker, Erosion, Green Joker, Red Card, Shortcut, Superposition, Mystic Summit, Greedy Joker, The Magician, Juggler
