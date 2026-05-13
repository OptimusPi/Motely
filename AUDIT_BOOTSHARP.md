# Bootsharp Integration Audit — Consumer-side findings

**Date:** 2026-05-13
**Audited against:** jaml-ui `master@c9bac26`, MotelyJAML `master@84a79e4`.
**Companion document:** `MotelyJAML/AUDIT_BOOTSHARP.md` on branch `claude/audit-bootsharp-3mEjQ` (full report).

## Headline

This UI calls a Bootsharp surface (`Motely.MotelyWasm.*`, `Motely.MotelyWasmEvents.*`, the `Motely.Motely*` enums) that **does not exist** in the WASM host at `optimuspi/MotelyJAML/Motely.Wasm/Program.cs`. The host's actual `[Export]`s are: `Version`, `LoadJaml`, `ExplainJaml`, `PickRoot`, `MountRoot`, `UnmountRoot`, `ReadTextFile`, `WriteTextFile`, and event `OnFileChanges`. Everything past `Motely.Version()` is wired to nothing.

## Consumer-side blockers (mirrored from the host-side audit)

| ID | File | Issue |
|----|------|-------|
| B1 | `src/hooks/useSearch.ts` | Calls `validateJaml`, `startRandomSearch`, `startAestheticSearch`, `startSeedListSearch`, `startKeywordSearch`, `startSequentialSearch`, `getTallyLabels`, and `MotelyWasmEvents.notifyResult/notifyProgress/notifyComplete`. None are exported by the host. |
| B2 | `src/hooks/useAnalyzer.ts` | Calls `analyzeJamlSeeds`. Not exported. |
| B3 | `src/motelyDisplay.ts` | Reads `MotelyBossBlind`, `MotelyVoucher`, `MotelyTag`, `MotelyBoosterPack`, `MotelyItemType`. Host emits no enums; values resolve to `undefined` → placeholder labels. |
| B4 | `src/hooks/useShopStream.ts` | Expects `analyzer.initShop(ante)` / `analyzer.nextShopItem()` streaming methods. Not exported. |
| B7 | `src/motelyBoot.ts` | `await bootsharp.boot();` is called at module top-level with no resource-root arg (upstream README requires `boot("/bin")` when served from repo root). The accompanying `"use client"` directive is incompatible with top-level await in Next.js client components anyway. |

## Consumer-side should-fix

- **S1.** `motely-wasm@^16.0.1` is a full major behind MotelyJAML (`MotelyVersion 17.1.1`, Bootsharp `0.8.0-alpha.252`). Pin tighter or republish.
- **S2.** `test-motely.js` at repo root is broken-by-design: mixes CJS `require` with ESM-only `motely-wasm`, references undefined `Bootsharp`, has self-doubting comments. Delete or rewrite.
- **S7.** `useSearch.ts` notes "only ONE search can run at a time across all useSearch instances" because it *replaces* `MotelyWasmEvents` handlers globally rather than subscribing. If/when the host actually exposes events as `[Export] event`s, this needs to use the Bootsharp subscription API.

## Consumer-side nits

- **N6.** `vite.config.ts` externalizes `motely-wasm` with an unpkg importmap comment, but no importmap snippet ships in `examples/` or this repo's README. Consumers following the docs have nothing to copy.

## What's not wrong

The Vite/Storybook/TS scaffolding is fine. The schema files (`jaml.schema.json`, `enum.json`) look maintained. The `examples/` static site that `pages.yml` deploys is intact. The break is exclusively at the Bootsharp boundary.

## Recommended next step (not yet executed)

Don't fix consumer-side calls in isolation — they're pointing at an API that doesn't exist yet. The decision belongs on the host side (`Motely.Wasm/Program.cs`): either it grows to match what the UI calls, or the UI collapses back to the actual `LoadJaml`/`ExplainJaml`/`MountRoot` surface. Once that decision is made, **the build & publish gap** (no CI job emits `motely-wasm` from MotelyJAML; this repo consumes it from npm with a floating `^16` range) needs to close, or the next round of drift is already loaded.
