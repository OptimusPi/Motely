# WASM Search Context De-Glue Plan (Deprecated)

Branch intent: `V/16-GLUE-SNIFFERS-BEGONE`

Deprecated note: this plan captured an exploratory de-glue direction for a WASM search-context API. The current Motely/WASM shape already works for the intended procedural use cases, so this document is now historical reference rather than active design guidance.

## Problem

`IMotelyWasmSearchContext` currently exposes Motely engine internals in a JS-hostile shape:

- create raw stream struct
- pass raw stream struct back into another method
- receive updated stream struct wrapped inside a result record
- repeat for nearly every stream type

This is fast enough, but ergonomically wrong. It mirrors ref-mutation internals instead of user task flow.

## What To Keep

- Core Motely search semantics remain in `MotelySingleSearchContext` and related core types.
- `motely-wasm` remains a first-class runtime because it is fast and portable.
- Bootsharp instance bindings remain the host boundary.
- Chunked/paged access stays. Chunks are good UX and good interop.
- Packed integer item payloads stay where they help throughput and compactness.

## What To Replace

- `CreateXStream()` plus `GetNextX(stream)` plus wrapper-record handback.
- Result records whose main purpose is to shuttle mutated stream state back to JS.
- Flat, engine-shaped host APIs that force consumers to think in `ref` choreography.

## Target Shape

The host API should feel like a thoughtful SDK, not a raw engine port.

Desired pattern:

- `context.createBossStream()` returns a stream object
- `bossStream.getNextForAnte(ante, runState)` returns data
- `bossStream.getNextChunk(startAnte, count, runState)` returns chunked data

Instead of:

- `createBossStream()` returns raw struct
- `getNextBossForAnte(stream, ante, runState)` returns `{ boss, stream, runState }`

## Constraints

- Do not move engine behavior into WASM-only code.
- Keep core Motely as source of truth.
- Use Bootsharp exported instances, not fake JS-side reimplementation.
- Preserve compatibility while migrating.

## Inventory Summary

Current glue-heavy areas in `IMotelyWasmSearchContext`:

- Boss streams
- Tag streams
- Booster pack streams
- Shop item streams
- Joker streams
- Fixed-rarity joker streams
- Tarot streams
- Planet streams
- Spectral streams
- PRNG streams

Common smell:

- raw `MotelySingle*Stream` crosses boundary
- updated stream is returned every call
- wrapper records proliferate per stream family

## First Vertical Slice

Start with `LuckyMoney` / PRNG flows, not bosses.

Why:

- Bosses are one of the most state-sensitive flows because they depend on evolving run state.
- `LuckyMoney` is a simpler manual/procedural use case that still exercises stream stepping and chunk access.
- It is a better first proof for "use Motely like original Motely" from `.mjs`.

## Bootsharp Constraint To Test First

Current repo notes say interop instances cannot be arguments or return values of another instance method.

That means this attractive target shape may be blocked in the current Bootsharp version:

- `context.createBossStream()` returns `IMotelyWasmBossStream`
- `context.createLuckyMoneyStream()` returns `IMotelyWasmLuckyMoneyStream`

If that rule holds for our current export setup, the first de-glue pass cannot rely on nested instance returns from `IMotelyWasmSearchContext`.

## Practical First Slice

Phase 1 should therefore prove a simpler path:

- add a manual `.mjs` smoke flow for `LuckyMoney`
- confirm current API can already express the original Motely procedural style
- then decide whether the de-glue target is:
	- nested stream instances, if Bootsharp allows it here, or
	- better convenience methods / handle ownership on `IMotelyWasmSearchContext`, if it does not

## Compatibility Strategy

Phase 1:

- Add new object-owned stream APIs
- Keep current glue APIs intact
- Update one `.mjs` test to prefer the new API

Phase 2:

- Migrate extension/runtime consumers
- Expand to next stream families

Phase 3:

- Remove old raw-stream shuttle APIs once parity is proven

## Validation Plan

- Build `Motely` after each slice
- Build/publish `Motely.Wasm` after interface changes
- Update and run the relevant `.mjs` e2e test for the migrated slice
- Confirm no semantic drift versus existing behavior

## If Work Stalls

First question for the next agent:

- Does current Bootsharp in this repo cleanly support returning a dedicated exported stream instance from `IMotelyWasmSearchContext` and calling methods on that instance from Node/browser tests?

If yes, continue with a PRNG or boss stream object slice.

If no, document the exact Bootsharp limitation and fall back to the next-best context-owned convenience pattern that avoids raw stream structs in user code.