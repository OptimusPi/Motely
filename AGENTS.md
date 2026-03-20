# MotelyJAML — Agent Guidelines

## What This Project Is

A .NET library + WASM/Node packages for Balatro seed analysis. It predicts what items, jokers, tags, vouchers, etc. a given seed will produce using Balatro's PRNG system.

## Architecture: Two Paths

```
motely-wasm/   ← Browser WASM output (published to npm)
motely-node/   ← Node.js native addon output (published to npm)
```

Both are **OUTPUT DIRECTORIES**. They are populated by the build/pack/publish pipeline.

### DO NOT EDIT these directories:
- `motely-wasm/` — generated output
- `motely-node/` — generated output
- `motely-wasm/index.js` — generated
- `motely-node/index.cjs` — generated

### DO NOT EDIT core Motely library files:
- `Motely/MotelySingleSearchContext*.cs` — core PRNG engine, not ours
- `Motely/MotelySingle*Stream.cs` — stream types, not ours
- `Motely/Filters/` — search filters, not ours
- Any file authored by TacoDiva (the Motely library author)

### Files YOU should edit:
- `Motely/MotelyGameplayState.cs` — our game state wrapper
- `Motely.Orchestration/MotelyExports.cs` — shared export logic (handle management, API)
- `Motely.BrowserWasm/MotelyWasmExports.cs` — thin `[JSExport]` wrappers calling MotelyExports
- `Motely.NodeAddon/MotelyNodeExports.cs` — thin Node-API wrappers calling MotelyExports
- `Motely/Analysis/` — analyzer and DTOs

## The Layering Rule

```
Platform-specific (WASM/Node)  →  MotelyExports (orchestration)  →  Motely (core library)
         thin wrappers                  all shared logic                 don't touch
```

WASM and Node exports must be **one-liner pass-throughs** to `MotelyExports`. All logic lives in orchestration. Never duplicate logic across WASM and Node.

## Key Concepts

### Streams Are Doubles

Every PRNG stream's state is ultimately a `double`. `MotelySinglePrngStream(double state)` wraps it. Higher-level streams (ShopItemStream, TarotStream, etc.) compose multiple PrngStreams. Each call to get the next item advances the double. That's it.

### ref struct vs struct

Some stream types are `ref struct` (stack-only, cannot be stored on the heap). Others are plain `struct`. When you need to store a `ref struct` as a class field, decompose its fields into a storage struct (they're just doubles and strings), then reconstruct on demand. See `StreamCache` in `MotelyGameplayState.cs`.

Do NOT change `ref struct` to `struct` in core Motely files. Work around it.

### The Parked Filter Pattern

`MotelyGameplayState` uses the search pipeline as a **context factory**. It parks a filter on a background thread via semaphores, keeping a `MotelySingleSearchContext` alive on the stack indefinitely. This is the ONLY way to get a context for single-seed analysis.

**DO NOT touch the parked filter code** (`ParkedFilterDesc`, `ParkedFilter`, `CheckSeed`, the semaphore dance). It is correct.

**DO NOT touch the `Cmd<T>` delegate dispatch.** It is correct.

### One Ante At A Time

The game state represents sequential gameplay. One ante at a time. When the ante changes, reset streams. Do NOT use `Dictionary<int, StreamType>` to cache per-ante. Think fibonacci: hold (a, b), advance. Don't cache every step.

### MotelyGameplayState IS the Single Seed Context

It's a stateful object that wraps `MotelySingleSearchContext` for one seed. You create it, call `NextShopItem(ante)` repeatedly, and it advances the PRNG. Infinite scroll. Don't pre-compute, don't batch, don't wrap the analyzer.

## Common Pitfalls

1. **"Let me redesign the architecture"** — No. Make the minimal fix. Use what exists.
2. **"Let me wrap the analyzer output"** — No. The analyzer pre-computes a fixed set. GameState is infinite scroll.
3. **"Let me add a Dictionary for caching"** — No. One ante at a time. Direct fields, reset on ante change.
4. **"Let me create a new search/filter"** — No. The parked filter IS the mechanism. Use it.
5. **"Let me edit MotelySingleSearchContext"** — No. Use Motely. Don't edit it.
6. **"Let me edit the output JS/CJS files"** — No. They're build output.
7. **"These stream types should be struct not ref struct"** — Don't change core Motely types. Write storage wrappers if needed.
8. **"Let me hand-roll PRNG logic"** — No. Motely already provides high-level stream interfaces. Use them.

## Build

```bash
dotnet build Motely.BrowserWasm/Motely.BrowserWasm.csproj
dotnet build Motely.NodeAddon/Motely.NodeAddon.csproj
```

Build/pack/publish is handled by the user's pipeline. Don't run publish commands.

## Testing Reference

Seed `AAAAA`, Deck `Red`, Stake `White`, Ante 1 shop items should produce:
DNA, Gift Card, Abstract Joker, Odd Todd, Venus, Crazy Joker, Erosion, Green Joker, Red Card, Shortcut, Superposition, Mystic Summit, Greedy Joker, The Magician, Juggler
