# Handoff — jaml-ui → motely-wasm@23

Verified facts only. Where something is unverified or risky it says so. Do **not**
trust the older `HANDOFF.md` / `HARVEST.md` / `JAML_UI_HANDOFF.md` files — verify
against code and the engine, which is what this doc did.

Branch: `claude/seedfinder-motely-wasm-s77ggu` — pushed, authored `pifreak`.

## Done + verified
- **motely-wasm@23 migration.** v23 dropped the `./*` subpath wildcard and the
  `Program` namespace; the surface is now root-level namespaces (`MotelyJaml`,
  `MotelySearch`, `MotelyJamlyzer`, `MotelyUtilities`, `Jimmolate`) + flattened
  enums/types. All `motely-wasm/...` subpath imports repointed to the root.
  Adapters centralized in `src/lib/motely/runtime.ts`.
- **typecheck + build green.** Engine stays external (8 MB dist; the old ~14.8 MB
  double-bundle is gone).
- **r3f / three.js stripped** — `./r3f` export, vite entry, devDeps, `src/r3f/`.
- **FS JAML library rebuilt for real** on the browser File System Access API
  (`useJamlLibrary.ts`): mount a folder, list/read/write `.jaml`. Not stubbed.
- **Proven by booting v23 in node:** `searchRandom` returned scored seeds;
  `Jimmolate.findSeed` was invoked 622× by the engine (real C#→JS round-trip — it
  is NOT fake); `analyzeSeeds` returns 8 antes.
- **C# golden parity passes.** All 8 invariants from
  `MotelyJAML/Motely.Tests/AnalyzerUnitTests.cs` hold in the WASM for seed
  `UNITTEST`: 1 result, 8 antes, ante 1 = 4 packs, antes 2–8 = 6 packs, sequential
  ante numbers, multi-seed order `UNITTEST/ALEEB/1234567`, ghost deck `KK1XD111`
  runs. (Repro script lived in scratch; port it — see TODO.)

## Not done / risky — do not treat as finished
- **PR not opened** (`→ master`, ready).
- **Aesthetic search is a guess.** `runtime.ts` `aestheticSeeds()` maps
  `Palindrome→mirrorPatternKeywords(8)`, `Echo→repeatCharKeywords(8)` — the `8`
  and the generator choices are invented and UNVERIFIED against engine semantics.
- **Analyzer carries stream state by design.** `MotelyJamlyzerSeedResult` includes
  `StreamStates`; the engine can resume from it (`AnalyzerUnitTests` resume tests).
  Observed: analyzing a seed *after* prior searches gave a different ante-1 boss
  than on a clean boot (ALEEB: `7/TheWindow` clean vs `27/VioletVessel` after
  searches). This is the resume/paging mechanism keeping its place — a feature, but
  a sharp edge: for a *fresh* analysis the app must start from a clean state, not a
  carried one. **Verify how a fresh vs resumed analyze is selected in the hooks.**
- **Forced engine regressions** (v23 removed the APIs, not our choice): no
  mid-search cancel (best-effort via worker termination), no per-seed tallies,
  Jamlyzer match-highlight flags dropped (v23 ships no match metadata).

## Verified reference facts
- `jaml-lsp` **does not exist** (no dir, 404 on npm).
- `jaml-lang` is real (`0.1.2` installed). Latest `3.14.1` is a **rewrite** —
  `getCompletions/getDiagnostics/Severity/CompletionKind` are gone; root now
  re-exports `./generated.js` instead of `./authoring.js`; `./vocab` subpath
  removed. Upgrading = rewriting `src/lib/jaml/jamlLangCodemirror.ts`, not a bump.
- **MCP:** spec `2026-07-28`; MCP Apps = SEP-1865 (`@modelcontextprotocol/ext-apps`);
  Vercel "MCP Apps Next.js Starter" is the real path. `json-render` = the View,
  motely-wasm@23 = the engine behind the tools.
- **Tests:** `vitest.config.ts` exists, but there is **no `test` script and zero
  `.test` files**. Booting motely-wasm in node needs a `Uint8Array.fromBase64`
  polyfill (Node < 24); browsers have it native. Bootsharp embeds the WASM as
  base64 so `boot()` takes no args.

## Next
1. Port `AnalyzerUnitTests` to a wasm/mjs vitest suite (golden invariants above).
2. Resolve the fresh-vs-resumed analyze state question in the hooks.
3. Verify the aesthetic-search mapping against the engine.
4. jaml-lang `3.14.1` rewrite of the CodeMirror bridge.
5. MCP Apps server (Next.js + Vercel) — json-render View, motely-wasm@23 tools.
