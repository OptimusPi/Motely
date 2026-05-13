# Bootsharp Integration Audit — MotelyJAML (host-side, master)

**Date:** 2026-05-13
**Scope:** The Bootsharp boundary between this repo (C#/WASM producer) and `optimuspi/jaml-ui` (TS consumer).
**Heads audited:** `MotelyJAML/master`, `jaml-ui/master`.
**Companion:** `jaml-ui/AUDIT_BOOTSHARP.md` on branch `claude/audit-bootsharp-3mEjQ` (consumer-side mirror).
**In-flight work:** PR [#36 — `feat(wasm): add StartSearch export with JamlSearchOptions`](https://github.com/OptimusPi/MotelyJAML/pull/36) implements a subset of the recommendations below.

## TL;DR — three contracts, none agree

There are **three different Bootsharp surfaces** in play right now, and no two of them line up:

| Source | What it claims exists |
|---|---|
| `Motely.Wasm/Program.cs` (what actually ships) | `Version`, `LoadJaml`, `ExplainJaml`, `PickRoot`, `MountRoot`, `UnmountRoot`, `ReadTextFile`, `WriteTextFile`, event `OnFileChanges` |
| `Motely.Wasm/README.md` (what the host's own README documents) | `getHostInfo`, `validateJaml`, `analyzeSeed`, `analyzeJamlSeed`, `analyzeJamlSeeds`, `searchJamlPage` |
| `jaml-ui/src/hooks/*` (what the consumer calls) | `validateJaml`, `startRandomSearch`, `startAestheticSearch`, `startSeedListSearch`, `startKeywordSearch`, `startSequentialSearch`, `getTallyLabels`, `analyzeJamlSeeds`, `initShop`, `nextShopItem`, and events `notifyResult / notifyProgress / notifyComplete` |

Past `Motely.Version()`, **nothing** the UI calls is wired to anything on the host. The repo's own README disagrees with the repo's own code. The decision is on the host side: pick one contract and make the other two match it.

---

## Blockers

### B1. Consumer calls a search API surface the host never declares
**Cite:** `jaml-ui/src/hooks/useSearch.ts` references `MotelyWasm.validateJaml`, `startRandomSearch`, `startAestheticSearch`, `startSeedListSearch`, `startKeywordSearch`, `startSequentialSearch`, `getTallyLabels`, plus `MotelyWasmEvents.notifyResult / notifyProgress / notifyComplete`.
**Reality (`Motely.Wasm/Program.cs`):** The only `[Export]` event declared is `OnFileChanges` (Program.cs:19-20). No `MotelyWasm` namespace, no `MotelyWasmEvents` namespace, no `startXxxSearch` methods. Every call past `Version()` throws at runtime.

**Constructive next step (already started in PR #36):** Add `[Export] StartSearch(string jaml, JamlSearchOptions? options)` returning `IMotelySearch`. Because `IMotelySearch` is a *class*, Bootsharp proxies it by reference — JS gets `.cancel()`, `.matchingSeeds`, `.isCompleted` live, with no JSON round-trip (see `BOOTSHARP.md § Interop Instances` and `BOOTSHARP.md § Key Rules for MotelyJAML`).

### B2. Consumer calls analyzer API the host never declares
**Cite:** `jaml-ui/src/hooks/useAnalyzer.ts` calls `(Motely.MotelyWasm as any).analyzeJamlSeeds(jaml, [seed])` (the `as any` cast is the smoking gun — the type system already knows this method doesn't exist).
**Reality:** Not exported by `Program.cs`. Coincidentally, `Motely.Wasm/README.md`'s "Exported Contract" section *also* lists this method — but the README documents wishes, not the code.

### B3. Display layer reads enums that aren't emitted
**Cite:** `jaml-ui/src/motelyDisplay.ts` reads `Motely.MotelyBossBlind`, `MotelyVoucher`, `MotelyTag`, `MotelyBoosterPack`, `MotelyItemType` to turn numeric values into display names.
**Reality:** `Motely.Wasm.csproj` declares no `<BootsharpEmitTypes>` / `<BootsharpInjectTypes>` / equivalent enum-emit opt-in. No `[Export]` on any enum, and they're not transitively referenced through an `[Export]`ed method that takes/returns them.
**Effect at runtime:** Every read returns `undefined` → fallback labels like `boss#N`, `voucher#N`, `tag#N`, `pack#N`, `item#N` are what users see.
**Per BOOTSHARP.md:** Enums marshal as numbers with name↔index maps — but only when the enum participates in the emitted boundary. Either add `[Export]` methods that return these enums in their signatures, or thread them through DTOs that already cross the wire.

### B4. Shop stream hook expects iterator methods the host doesn't provide
**Cite:** `jaml-ui/src/hooks/useShopStream.ts` docstring: `"called once to initialize the stream (e.g. analyzer.initShop(ante))"` / `"called to get the next item from the stream (e.g. analyzer.nextShopItem())"`.
**Reality:** No `analyzer` interop instance exists. Per `BOOTSHARP.md § Interop Instances`, this would naturally be modeled as an `[Export]`ed factory returning an `IShopStream` interop instance — same pattern as `IMotelySearch`. Until then, the hook is shape-only.

### B5. `Motely.Wasm.csproj` is unbuildable off one developer's machine
**Cite (`Motely.Wasm/Motely.Wasm.csproj:12`):**
```xml
<BootsharpExtraRoot Condition="'$(BootsharpExtraRoot)' == ''">D:\extra\bootsharp</BootsharpExtraRoot>
```
**Cite (`Motely.Wasm/Motely.Wasm.csproj:22`):**
```xml
<ProjectReference Include="$(BootsharpExtraRoot)\cs\Bootsharp.FileSystem\Bootsharp.FileSystem.csproj" />
```
This directly violates the repo's own rule in `AGENTS.md`: **"No private paths in public files. No `D:\…`, `X:\…`, local NuGet feeds, or personal drive layouts in `.csproj` / `.props` / `.config` / package metadata."**

Per `BOOTSHARP.md § File System Extension`, the documented way to consume Bootsharp.FileSystem is:
```xml
<PackageReference Include="Bootsharp.FileSystem" Version="*-*"/>
```
No `nuget.config` package source is committed pointing at a public Bootsharp.FileSystem feed either (`nuget.config` only declares `nuget.org`), so any second contributor and any CI runner cannot restore. The current state is "works on Pi's D: drive only".

### B6. No CI publishes the `motely-wasm` npm package
There is no `.github/workflows/` directory in MotelyJAML. The only thing pinning the consumer to the producer is jaml-ui's `"motely-wasm": "^16.0.1"` in `package.json` — a floating caret range, manually published, with no commit-of-record. Drift is invisible until runtime.

**Fix shape:** A workflow that runs `dotnet publish Motely.Wasm -c Release`, then `cd motely-wasm && npm publish`, and tags the commit with the published version. Pin jaml-ui to that exact version (no `^`).

### B7. Unconditional top-level `await bootsharp.boot()` on import with no resource root
**Cite (`jaml-ui/src/motelyBoot.ts`):**
```ts
import bootsharp, { Motely } from "motely-wasm";
await bootsharp.boot();
export { Motely };
```
The host's own `Motely.Wasm/README.md` says: *"Bootsharp 0.8's browser boot API takes the runtime resource root directly: `await bootsharp.boot("/bin");`"* — required because `Motely.Wasm.csproj` overrides `BootsharpBinariesDirectory` to `..\motely-wasm\bin`, so the default lookup doesn't resolve.

(Correction vs. the prior pass of this audit: `motelyBoot.ts` does **not** carry a `"use client"` directive — the previous report claimed one. The Next.js-incompatibility nit was misplaced. The hooks that consume `Motely` do have `"use client"`, which is correct for them.)

---

## Should-fix

### S1. Version skew, floating caret
Host: `<MotelyVersion>17.1.1</MotelyVersion>` + Bootsharp `0.8.0-alpha.252` (`Directory.Packages.props:3,8-10`). Consumer: `motely-wasm@^16.0.1` (`jaml-ui/package.json`). One major behind, `^` allows further drift. Couple this to **B6** — once CI publishes deterministically, pin jaml-ui to exact versions.

### S2. `jaml-ui/test-motely.js` is committed and broken-by-design
Six lines, all wrong:
```js
const { MotelyWasm, Motely } = require('motely-wasm');     // CJS require of ESM-only package
async function run() {
    await Bootsharp.import(); // wait, Bootsharp is needed
}
const { Bootsharp } = require('motely-wasm');              // shadowed re-require, not exported anyway
MotelyWasm.createSearchContext("1", 0, 0); // wait this is wrong
```
Self-doubting comments included. Delete or rewrite end-to-end.

### S3. Bootsharp alpha pinned with `TreatWarningsAsErrors=true`
**Cite (`Directory.Packages.props:5`):** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` combined with **`Bootsharp 0.8.0-alpha.252`** (`Directory.Packages.props:9`). Any source-generator diagnostic update between alphas breaks the build with no path to ignore-and-ship. Either pin tighter (single explicit version), or whitelist Bootsharp generator diagnostics in `WarningsAsErrors`/`NoWarn` until 0.8 GA.

### S4. Missing `<BootsharpEmit*>` / `<BootsharpInject*>` props in `Motely.Wasm.csproj`
The boundary contract is undefined — consumers depend on enums (B3) and event signatures that nothing in the csproj opts into. Either opt in explicitly (recommended; makes the contract reviewable in PRs) or stop calling them.

### S5. `[assembly: Preferences(Space = [".+", "Motely"])]` is dangerously broad
**Cite (`Motely.Wasm/Program.cs:9`).** This collapses *every* C# namespace to JS `Motely`. Per `BOOTSHARP.md § Preferences`, the canonical narrow pattern is:
```csharp
[assembly: Preferences(Space = [@"^Motely\.Wasm\.Program$", "Motely"])]
```
Tightening this prevents accidental name collisions the moment any other namespace (e.g. an analyzer module) starts getting emitted.

### S6. No Release / AOT story exercised
`Motely.Wasm/README.md` claims Release uses NativeAOT-LLVM, but the csproj has no `<RunAOTCompilation>`, `<WasmEnableSIMD>`, `<PublishTrimmed>`, or trim-warning controls. Motely is large and numerics-heavy; trimming without `DynamicDependency` / roots will strip filter types at Release and Bootsharp's emitter will emit references to types that no longer exist. Validate Release end-to-end before claiming it in docs.

### S7. `MotelyWasmEvents` treated as mutable global handler state
**Cite (`jaml-ui/src/hooks/useSearch.ts`):** the comment `"only ONE search can run at a time across all useSearch instances because MotelyWasmEvents handlers are shared global state"` and the code that *replaces* handlers via `MotelyWasmEvents.notifyResult = (...) => ...`.
Per `BOOTSHARP.md § Events`, the correct pattern is:
```ts
Program.onSomethingChanged.subscribe(handler);
Program.onSomethingChanged.unsubscribe(handler);
```
Once the host exports events with `[Export] public static event Action<...>`, the consumer must `subscribe` / `unsubscribe` — not overwrite. The current "only one at a time" constraint is a symptom of the wrong pattern, not a real limit.

---

## Nits

- **N1.** `Mounter() => services.GetRequiredService<IFileMounter>()` (`Program.cs:97`) resolves DI on every call. Cache once into a field at `Main()`.
- **N2.** `UnmountRoot` (`Program.cs:79-84`) removes the FS entry from `MountedFileSystems` *then* calls `Mounter().Unmount(root)`. If `Unmount` throws, the dictionary is already mutated. Reverse the order or guard.
- **N3.** `Version()` (`Program.cs:29-33`) uses `typeof(MotelyDeck).Assembly.GetCustomAttribute<...>()!` with null-forgiving `!`. If `MotelyDeck` is trimmed under AOT-without-a-root, this NREs at runtime.
- **N4.** `<InvariantGlobalization>true</InvariantGlobalization>` (`Motely.Wasm.csproj:11`) is right for the engine, but any culture-sensitive parsing in `JamlConfigLoader` will silently diverge from the CLI host. Spot-check decimal/date paths.
- **N5.** `jaml-ui/vite.config.ts` externalizes `motely-wasm` with a comment about an unpkg importmap, but no importmap snippet ships in `examples/` or the README. Consumers following the docs have nothing to copy.
- **N6.** `Motely.Wasm/README.md`'s "Exported Contract" section documents a contract that doesn't ship. Either implement it or delete the section — having it sit there as aspirational copy is what produced this entire mismatch.

---

## Key files referenced

**MotelyJAML**
- `Motely.Wasm/Motely.Wasm.csproj` — private path B5; missing emit/AOT props S4/S6
- `Motely.Wasm/Program.cs` — actual exports; Preferences S5; nits N1-N3
- `Motely.Wasm/README.md` — documents a third contract; N6
- `Directory.Packages.props` — version skew S1; alpha + warnings-as-errors S3
- `nuget.config` — no Bootsharp.FileSystem feed declared; B5
- `AGENTS.md` — self-cited rules ("No private paths", "No facade wrappers")
- `BOOTSHARP.md` — compiled Bootsharp reference; cited throughout

**jaml-ui**
- `package.json` — `motely-wasm@^16.0.1`; S1
- `vite.config.ts` — unpkg importmap promise without payload; N5
- `src/motelyBoot.ts` — top-level `boot()` no arg; B7
- `src/motelyDisplay.ts` — enum lookups against missing emit; B3
- `src/hooks/useSearch.ts` — phantom search API + global event mutation; B1, S7
- `src/hooks/useAnalyzer.ts` — phantom analyzer call (with `as any` escape); B2
- `src/hooks/useShopStream.ts` — phantom stream methods in docstring; B4
- `test-motely.js` — broken-by-design; S2

---

## Recommended next step

Treat **B1–B4 as a single contract decision**, not four bugs. The host has to grow to match the JS contract, *or* the JS has to collapse to the actual `LoadJaml` / `ExplainJaml` / `MountRoot` surface. PR #36 picks the first direction for the search slice (`StartSearch` returning `IMotelySearch` proxy + `OnSeedMatch` / `OnProgress` events) — that's the right shape per `BOOTSHARP.md § Interop Instances`. Extend it to cover the analyzer (B2) and enums (B3) the same way.

**Whichever direction wins, land B5 + B6 first** (drop the `D:\` ProjectReference; replace with a `PackageReference Include="Bootsharp.FileSystem"` from a real feed; add a workflow that publishes `motely-wasm` and pins `jaml-ui`). Otherwise the next iteration of drift is already loaded.

---

*Audit prepared on branch `claude/audit-bootsharp-3mEjQ`. Citations are file-level where line numbers were stable; inline-quoted otherwise. Bootsharp behavior claims are cross-referenced against the in-repo `BOOTSHARP.md` (compiled from upstream `D:\bootsharp\docs\`) and the project home at https://bootsharp.com.*
