# Bootsharp Integration Audit — jaml-ui (consumer-side)

**Date:** 2026-05-13
**Audited against:** `jaml-ui/master`, `MotelyJAML/master`.
**Companion (authoritative):** `MotelyJAML/AUDIT_BOOTSHARP.md` on branch `claude/audit-bootsharp-3mEjQ`.
**Related in-flight work:** `MotelyJAML` PR [#36 — `feat(wasm): add StartSearch export with JamlSearchOptions`](https://github.com/OptimusPi/MotelyJAML/pull/36).

## Headline

This UI calls a Bootsharp surface (`Motely.MotelyWasm.*`, `Motely.MotelyWasmEvents.*`, the `Motely.Motely*` enums) that **does not exist** on the host. The host's actual `[Export]`s, from `MotelyJAML/Motely.Wasm/Program.cs`, are: `Version`, `LoadJaml`, `ExplainJaml`, `PickRoot`, `MountRoot`, `UnmountRoot`, `ReadTextFile`, `WriteTextFile`, and event `OnFileChanges`. Past `Motely.Version()`, nothing this UI calls is wired to anything.

The companion host-side audit explains the **three-contract mismatch** in detail (host code vs. host README vs. consumer expectations). This document focuses on what changes here once the host-side contract decision lands.

---

## Blockers (mirrored)

| ID | File | Issue |
|----|------|-------|
| **B1** | `src/hooks/useSearch.ts` | Calls `MotelyWasm.validateJaml`, `startRandomSearch`, `startAestheticSearch`, `startSeedListSearch`, `startKeywordSearch`, `startSequentialSearch`, `getTallyLabels`, plus `MotelyWasmEvents.notifyResult / notifyProgress / notifyComplete`. None are exported. |
| **B2** | `src/hooks/useAnalyzer.ts` | Calls `(Motely.MotelyWasm as any).analyzeJamlSeeds(jaml, [seed])`. The `as any` cast is the giveaway — TypeScript already knows this method isn't on the type. |
| **B3** | `src/motelyDisplay.ts` | Reads `Motely.MotelyBossBlind`, `MotelyVoucher`, `MotelyTag`, `MotelyBoosterPack`, `MotelyItemType`. Host emits no enums (no `<BootsharpEmit*>` props in its csproj); values resolve to `undefined` → fallback labels (`boss#N`, `voucher#N`, …) are what users see. |
| **B4** | `src/hooks/useShopStream.ts` | Docstring references `analyzer.initShop(ante)` / `analyzer.nextShopItem()` streaming methods. Not exported. |
| **B7** | `src/motelyBoot.ts` | ```ts<br>import bootsharp, { Motely } from "motely-wasm";<br>await bootsharp.boot();<br>export { Motely };<br>``` Top-level `boot()` is called with no resource-root arg. The host's own `Motely.Wasm/README.md` requires `boot("/bin")` because `Motely.Wasm.csproj` overrides `BootsharpBinariesDirectory` to `..\motely-wasm\bin`. **Correction vs. prior audit pass:** this file does *not* carry a `"use client"` directive — the previous Next.js-incompatibility note was misplaced. The hooks (`useSearch`, `useAnalyzer`, `useShopStream`) do, which is correct for React hooks. |

---

## Should-fix

- **S1. Version skew.** `package.json` pins `"motely-wasm": "^16.0.1"`. Host is at `MotelyVersion 17.1.1` + Bootsharp `0.8.0-alpha.252`. One major behind, with `^` allowing further drift. Tighten once host CI publishes deterministically (see host-side **S1 / B6**).

- **S2. `test-motely.js` is broken-by-design.** Six lines, all wrong: mixes CJS `require` with the ESM-only `motely-wasm` package, references undefined `Bootsharp`, shadows its own `require`, comments are self-doubting (`// wait this is wrong`, `// wait, Bootsharp is needed`). Either delete it or rewrite end-to-end against the real host surface once it stabilises.

- **S7. Replace event handlers with subscribe/unsubscribe.** `useSearch.ts` deliberately replaces `MotelyWasmEvents.notifyResult` / `notifyProgress` / `notifyComplete` globally and comments: *"only ONE search can run at a time across all useSearch instances because MotelyWasmEvents handlers are shared global state."* That constraint is a symptom of using the wrong pattern. Per `MotelyJAML/BOOTSHARP.md § Events`, the correct Bootsharp 0.8 pattern is:
  ```ts
  Motely.onSeedMatch.subscribe(handler);
  // and on cleanup:
  Motely.onSeedMatch.unsubscribe(handler);
  ```
  When the host exposes search events properly (PR #36 adds `OnSeedMatch` / `OnProgress`), each `useSearch` instance can have its own subscription with no global lock.

---

## Nits

- **N5.** `vite.config.ts` externalizes `motely-wasm` with a comment promising an unpkg importmap for the singlefile-MCP iframe path, but no importmap snippet ships in this repo's `README.md` or `examples/`. Anyone copying the docs has nothing to paste.

- **N6.** `src/motelyDisplay.ts` falls back to `boss#${value}` / `voucher#${value}` / etc. on lookup failure. Once enums are actually emitted (host-side B3), these fallbacks will silently mask future enum-value drift. Worth logging once to console when a fallback fires, at least in dev.

---

## What's not wrong

The Vite/Storybook/TS scaffolding is fine. The schema files (`jaml.schema.json`, `enum.json`) look maintained. The `examples/` static site that `pages.yml` deploys is intact. The break is exclusively at the Bootsharp boundary — and exclusively because the host hasn't shipped what this UI was written against.

---

## Recommended next step

**Do not fix consumer-side calls in isolation.** They're pointing at an API that doesn't exist yet, and "fixing" them by guessing what the host will look like will produce a third version of the same drift. The decision belongs on the host side (see companion audit). When PR [#36](https://github.com/OptimusPi/MotelyJAML/pull/36) (and follow-ups for the analyzer + enums) lands and a new `motely-wasm` is published:

1. Bump `motely-wasm` here to the exact published version (no `^`).
2. Fix `motelyBoot.ts` to pass the resource root (`bootsharp.boot("/bin")` or whatever the new build emits).
3. Rewrite `useSearch.ts` against `Motely.startSearch(...)` returning an `IMotelySearch` proxy (per `BOOTSHARP.md § Interop Instances`, the proxy gives you `.cancel()`, `.matchingSeeds`, `.isCompleted` directly — no global event mutation).
4. Subscribe to `Motely.onSeedMatch` / `Motely.onProgress` per-search; clean up on unmount.
5. Once host emits enums (host-side B3), drop `as Record<string, unknown>` casts in `motelyDisplay.ts`.
6. Delete `test-motely.js` (or replace with a real Vitest fixture).

---

*Audit prepared on branch `claude/audit-bootsharp-3mEjQ`. The authoritative version lives in `MotelyJAML/AUDIT_BOOTSHARP.md` on the same branch — this file mirrors the consumer-side rows so contributors editing jaml-ui have the context inline. Bootsharp behavior claims are cross-referenced against `MotelyJAML/BOOTSHARP.md` and the project home at https://bootsharp.com.*
