# Bootsharp Integration Audit

**Date:** 2026-05-13
**Scope:** The Bootsharp (https://bootsharp.com) boundary between `optimuspi/MotelyJAML` (C#/WASM producer) and `optimuspi/jaml-ui` (TS consumer).
**Heads commit audited:** MotelyJAML `master@84a79e4`, jaml-ui `master@c9bac26`.

## TL;DR

The JS consumer and the C# host describe two different APIs. The Wasm project currently exports a JAML-validation + file-mount surface (`Version`, `LoadJaml`, `ExplainJaml`, `PickRoot`, `MountRoot`, `UnmountRoot`, `ReadTextFile`, `WriteTextFile`, event `OnFileChanges`). The React app calls a **search / analyzer / enum / progress-event** surface that has no producer. Nothing past `Motely.Version()` can work against the code in `Motely.Wasm/Program.cs`.

The two repos are also one major version apart on `motely-wasm` (host 17.1.1 vs consumer `^16.0.1`) and there is no CI that builds or publishes the npm package, so the wire that does exist is unreproducible from source.

---

## Blockers

### B1. Consumer calls a search API surface the host never declares
`jaml-ui/src/hooks/useSearch.ts` references:

- `Motely.MotelyWasm.validateJaml`
- `Motely.MotelyWasm.startRandomSearch`
- `Motely.MotelyWasm.startAestheticSearch`
- `Motely.MotelyWasm.startSeedListSearch`
- `Motely.MotelyWasm.startKeywordSearch`
- `Motely.MotelyWasm.startSequentialSearch`
- `Motely.MotelyWasm.getTallyLabels`
- `Motely.MotelyWasmEvents.notifyResult / notifyProgress / notifyComplete`

**None** of these are `[Export]`ed in `Motely.Wasm/Program.cs`. The only declared event is `OnFileChanges`. Every call will throw at runtime.

### B2. Consumer calls analyzer API the host never declares
`jaml-ui/src/hooks/useAnalyzer.ts` calls `Motely.MotelyWasm.analyzeJamlSeeds(jaml, [seed])`. Not exported. The README describes an intended contract, not the shipped one.

### B3. Display layer reads enums that aren't emitted
`jaml-ui/src/motelyDisplay.ts` reads `Motely.MotelyBossBlind`, `MotelyVoucher`, `MotelyTag`, `MotelyBoosterPack`, `MotelyItemType`. The csproj has no `<BootsharpEmit*>` / `<BootsharpInject*>` props to opt these enums into the emitted TS, and no `[Export]` on the enums. At runtime these reads return `undefined` and every display name falls through to placeholders like `boss#N` / `voucher#N`.

### B4. Shop stream hook expects methods the host doesn't provide
`jaml-ui/src/hooks/useShopStream.ts` expects `analyzer.initShop(ante)` / `analyzer.nextShopItem()` streaming methods. Nothing analogous is exported.

### B5. `Motely.Wasm.csproj` is unbuildable off one developer's machine
`Motely.Wasm/Motely.Wasm.csproj` hardcodes `<BootsharpExtraRoot>D:\extra\bootsharp</BootsharpExtraRoot>` and ProjectReferences `$(BootsharpExtraRoot)\cs\Bootsharp.FileSystem\Bootsharp.FileSystem.csproj`. No `Bootsharp.FileSystem` NuGet package is referenced. Combined with the `nuget.config` instruction that the Bootsharp alpha feed must be installed at the user level, CI and any second contributor cannot build the WASM project.

### B6. No CI publishes the `motely-wasm` npm package
MotelyJAML has no `.github/workflows/` directory. jaml-ui's only workflow (`pages.yml`) uploads the static `examples/` folder. The `motely-wasm@^16.0.1` package the UI consumes from npm is produced manually, with no pinning back to the MotelyJAML commit that produced it. Drift is invisible.

### B7. Unconditional top-level `await bootsharp.boot()` on import with no resource root
`jaml-ui/src/motelyBoot.ts` calls `await bootsharp.boot();` at module top level with no argument, contradicting the upstream Bootsharp README, which requires `boot("/bin")` (or equivalent) when serving from repo root. The `"use client"` directive on the same file is also incompatible with top-level await in Next.js client components.

---

## Should-fix

- **S1. Version skew.** Host: `MotelyVersion 17.1.1` + Bootsharp `0.8.0-alpha.252`. Consumer: `motely-wasm@^16.0.1`. A full major behind and `^` allows further drift.
- **S2. `jaml-ui/test-motely.js` is committed and broken-by-design** (mixes CJS `require` with ESM-only `motely-wasm`, references undefined `Bootsharp`, has self-doubting comments). Delete or fix.
- **S3. Bootsharp alpha pinned with `TreatWarningsAsErrors=true`** (`Directory.Packages.props`). Source-generator diagnostic updates will break the build.
- **S4. Missing `<BootsharpEmit*>` / `<BootsharpInject*>` props** in `Motely.Wasm.csproj`, yet consumers rely on emitted enums and event hooks. The boundary contract is undefined — either opt in or stop calling them.
- **S5. `[assembly: Preferences(Space = [".+", "Motely"])]`** in `Program.cs` maps every namespace to the JS `Motely` namespace. Overly broad and collides with any future mapping.
- **S6. No Release / AOT story exercised.** README claims Release uses NativeAOT-LLVM, but `<RunAOTCompilation>`, `<WasmEnableSIMD>`, `<PublishTrimmed>`, and trim-warning controls are absent. Motely is large and numerics-heavy — trimming without `DynamicDependency` / roots will likely strip filter types at Release.
- **S7. `MotelyWasmEvents` treated as mutable global handler state** in `useSearch.ts` ("only ONE search can run at a time across all useSearch instances"). If this is meant to be a Bootsharp `[Export] event`, *replacing* the handler instead of subscribing is fragile.

---

## Nits

- N1. `"use client"` on `src/motelyBoot.ts` is meaningless with top-level await (see B7).
- N2. `Mounter()` resolves `IFileMounter` from DI on every call (`Program.cs`); cache it.
- N3. `UnmountRoot` removes the FS entry then unmounts via the mounter without awaiting `fs` disposal; leaks per-FS handles the mounter doesn't track.
- N4. `Version()` reaches into `typeof(MotelyDeck).Assembly` with `!`. If `MotelyDeck` is trimmed under AOT-without-a-root, this NREs.
- N5. `InvariantGlobalization=true` is fine for the engine, but any culture-sensitive parsing in `JamlConfigLoader` will silently diverge from the CLI host.
- N6. `motely-wasm` is externalized in `vite.config.ts` with an unpkg importmap comment, but no importmap snippet ships in `examples/` or the README. Consumers following docs have nothing to copy.

---

## Key files referenced

**MotelyJAML**
- `Motely.Wasm/Motely.Wasm.csproj`
- `Motely.Wasm/Program.cs`
- `Motely.Wasm/README.md`
- `Directory.Packages.props`
- `nuget.config`

**jaml-ui**
- `package.json`
- `vite.config.ts`
- `src/motelyBoot.ts`
- `src/motelyDisplay.ts`
- `src/motely.ts`
- `src/hooks/useSearch.ts`
- `src/hooks/useAnalyzer.ts`
- `src/hooks/useShopStream.ts`
- `test-motely.js`
- `.github/workflows/pages.yml`

---

## Recommended next step (not yet executed)

Treat **B1–B4** as one decision: pick whether the C# host grows to match the JS contract, or the JS contract collapses back to what the host actually offers. Whichever direction, **B5 + B6** must land first (reproducible build + a CI job that publishes the npm package and pins jaml-ui to it) or the next round of drift is already loaded.
