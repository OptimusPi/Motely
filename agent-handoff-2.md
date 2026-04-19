# Agent Handoff — motely-wasm v11.3.2 Integration Notes

**From:** Claude Code session integrating motely-wasm v11 into Blueprint (pifreak's Balatro seed tool)
**Date:** 2026-04-18
**Context:** Upgraded Blueprint from motely-wasm v1.2.8 to v11.3.2 (NativeAOT-LLVM + Bootsharp)

---

## What Worked Well

- **`boot()` is clean** — single async call, no config needed for browser. Just `await motely.boot()` and go.
- **Search API is intuitive** — `MotelyWasm.startRandomSearch(jaml, count)` returns `IMotelyWasmSearch` with `.cancel()` and `.waitForCompletion()`. Easy to reason about lifecycle.
- **Event system is solid** — `MotelyWasmEvents.onProgress.subscribe(handler)` / `.unsubscribe(handler)` pattern is clean. The `Event` class with `.last` for most recent payload is a nice touch.
- **No COI/SAB/COEP required** — single-threaded NativeAOT just works in any browser. Massive DX improvement over the old multi-threaded WASM that needed `coi-serviceworker.js` and `SharedArrayBuffer`.
- **TypeScript types are comprehensive** — `bindings.g.d.ts` covers every enum, interface, and method. No guessing.
- **`IMotelyWasmSearchContext` streaming API** — the per-seed streaming (boss, voucher, tag, shop, booster pack streams) is very well designed for building seed explorers.

## Friction Points

### 1. No README / usage examples in npm package
The npm package ships `demo.html` but no `README.md` with quick-start code. First-time integrators have to read the `.d.ts` files to figure out the API. A 20-line "boot and search" example would save hours.

### 2. `MotelyItemType` enum → display name gap
`MotelyItemType` has 200+ entries like `SmearedJoker`, `OopsAll6s`, `GrosMichel`, `EightBall`, `ChaostheClown`. These are PascalCase identifiers, not display names. There's no built-in way to get `"Smeared Joker"`, `"Oops! All 6s"`, `"8 Ball"`, etc.

**Current workaround:** `jaml-ui/motely` exports `motelyItemDisplayNameFromKey()` which handles the full mapping including special cases. Consider shipping this in motely-wasm itself, or at minimum documenting that jaml-ui provides this.

### 3. `bigint` on progress events
`onProgress` fires with `(seedsSearched: bigint, matchingSeeds: bigint)`. Correct for large search spaces, but every UI consumer will immediately `Number()` these. Consider offering a `onProgressNum` convenience event, or documenting the `Number()` conversion pattern.

### 4. `getVersion()` return format
`MotelyWasm.getVersion()` exists but it wasn't clear from types alone what format the string is in (semver? build hash? something else?). Documenting the expected return value would help.

### 5. Vite browser externalization warnings
motely-wasm v11 imports `fs/promises`, `url`, `fs`, `process`, `module` for its Node.js code path. Vite warns about each one:
```
[plugin vite:resolve] Module "fs/promises" has been externalized for browser compatibility
```
These are harmless but noisy. Consider conditional imports or documenting that these warnings are expected in browser bundlers.

### 6. Bundle size (11.7MB)
The embedded NativeAOT binary makes the main chunk ~11.7MB (3.8MB gzipped). Expected for the architecture, but:
- Document recommended Vite/webpack config for code-splitting motely-wasm into its own chunk
- Consider lazy-loading guidance (dynamic `import('motely-wasm')` on first search, not on page load)

## Wish List for Future Versions

1. **`MotelyWasm.validateJaml()` error details** — currently returns a string. Structured errors (line number, column, error type) would enable inline IDE error highlighting.
2. **Search progress with elapsed time** — `onProgress` gives seeds searched and matches, but not elapsed time. The `MotelyWasmSearchSnapshot` has `elapsedMs` but you have to call `getSnapshot()` to get it. Having it in the progress event would be convenient.
3. **Seed list from search results** — `onResult` gives individual seeds. A batch/bulk result accessor on completion would reduce JS-side bookkeeping.
4. **Display name helpers built-in** — `motelyBossDisplayName(MotelyBossBlind.TheHook)` → `"The Hook"`, etc. jaml-ui has these but they belong closer to the source of truth.

## Integration Architecture (Blueprint)

```
motely-wasm v11.3.2
  └─ boot() once on JAML tab open
  └─ MotelyWasm.startRandomSearch(jaml, count)
  └─ MotelyWasmEvents.onProgress → update UI counters
  └─ MotelyWasmEvents.onResult → batch into results array
  └─ search.waitForCompletion() → final flush
  └─ search.cancel() → user stop

jaml-ui v0.4.0
  └─ JamlIde component (code/map/results tabs + search button)
  └─ jaml-ui/motely → display name decoders for all MotelyItemType/Boss/Tag/Voucher enums
```

No wrapper file. Direct imports from `motely-wasm` and `jaml-ui` in the consuming component.
