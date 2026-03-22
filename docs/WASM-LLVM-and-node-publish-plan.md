# WASM (Bootsharp LLVM) + Node publish — consolidated plan

This document supersedes the earlier high-level LLVM sketch. It incorporates a critique of that plan and grounds decisions in this repo’s history and current artifacts.

## Historical context (explicit)

Commit **`c7689f35`** removed **`Motely.BrowserWasm`** and **`Motely.NodeAddon`** with the message *“Bootsharp handles everything.”* For this codebase, that was **incorrect as an architecture conclusion**:

- **Bootsharp does not remove the need for a browser host executable.** WASM publish requires an **app** entry (`Main` → `RunBootsharp()` or equivalent). The library **`Motely`** alone hits **CS5001** when forced into `browser-wasm` publish.
- The **pre-delete** [`Motely.BrowserWasm`](https://github.com/OptimusPi/MotelyJAML) layout is the **known-good pattern**: `OutputType=Exe`, `TargetFramework=net10.0-browser`, `RuntimeIdentifier=browser-wasm`, references **`Motely`** + **`Motely.Orchestration`**, Bootsharp packages on the **host only**.

**Decision:** Treat the next step as **restoring** `Motely.BrowserWasm` (same name and responsibilities), not inventing a “new” concept.

## Interop surface (no hand-waving)

The **exported WASM API** is not “minimal Program”; it is whatever **`IMotelyWasmBackend`** (and friends) expose to JS. From **`c7689f35^`**:

**File:** `Motely.BrowserWasm/Interop/IMotelyWasmBackend.cs` (restore from git)

Responsibilities include:

- Instance lifecycle: `CreateInstance` / `DestroyInstance`
- Search: `StartJamlSearch`, `StartSeedListSearch`, `StartKeywordSearch`, `StartRandomSearch`, `StartPalindromeSearch`, `StopSearch`
- Analysis / validation: `AnalyzeSeed`, `ValidateJaml`, `ValidateJamlWithError`
- Capabilities: `GetVersion`, `IsSimdEnabled`, `GetProcessorCount`
- **Shop stream:** `GetShopItems(seed, deck, stake, ante, offset, count)` — JSON chunk for deterministic paging

**Hard design work (post-restore):**

- **Seed router alignment:** Desktop analysis and tests use **`IMotelySeedRouter`** / **`MotelySeedRouterDesc`** ([`Motely/Analysis/MotelySeedRouterDesc.cs`](../Motely/Analysis/MotelySeedRouterDesc.cs)). WASM must either:
  - **Keep** the existing JSON/DTO-oriented methods and implement them **on top of** the same orchestrator/router paths the analyzer uses (preferred for parity), or
  - **Extend** the interface only where the UI needs new capabilities (document every addition; update TS in [`Motley.TestUI/motely-ui/`](../Motley.TestUI/motely-ui/)).

Do **not** reintroduce a second divergent “shop only” codepath unless it is clearly marked temporary.

## Option A vs B for `Motely` TFM

**Pick Option A:** **`Motely` = `net10.0` only** (library for desktop, tests, NodeApi managed output).

- Remove **`net10.0-browser`** from [`Motely/Motely.csproj`](../Motely/Motely.csproj): browser-specific compile excludes move to **Orchestration** / shared code as today, and **only the host** targets `net10.0-browser`.
- **Rationale:** One publish graph for WASM (the host). No duplicate Bootsharp/LLVM vs Mono property fights on the same project.

`Motely.Orchestration` may keep `net10.0-browser` **only if** it remains a thin dependency of the host without needing LLVM props; if it pulls conflicting workload assets, mirror the same split (host-only browser TFM).

## LLVM vs Mono WASM properties

**Do not mix** on the publish project:

| Mono / workload style (remove from LLVM host) | NativeAOT-LLVM / Bootsharp (set on host when `BootsharpLLVM=true`) |
|------------------------------------------------|----------------------------------------------------------------------|
| `WasmStripILAfterAOT`, `WasmEnableSIMD`, … as used for Mono path | `BootsharpLLVM`, `DotNetJsApi`, `UsingBrowserRuntimeWorkload=false`, `PublishTrimmed`, `EmccFlags`, `EmscriptenEnvVars`, experimental feed |

The **restored** `Motely.BrowserWasm.csproj` already had **`BootsharpLLVM` false by default** and a **conditional** property group for LLVM — keep that toggle; flip default to `true` only after the first green publish.

## ILCompiler.LLVM versioning (concrete)

**Do not** use [`Directory.Packages.props`](../Directory.Packages.props) **7.0.0-preview** lines for net10 LLVM.

**Discovery command** (repeat when upgrading SDK / Bootsharp):

```bash
dotnet package search Microsoft.DotNet.ILCompiler.LLVM --prerelease --take 15 ^
  --source "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json"
```

**Example result (2026-03-21):** `Microsoft.DotNet.ILCompiler.LLVM` **10.0.0-rc.1.26117.1**, with matching **`runtime.win-x64`** / **`runtime.linux-x64`** / **`runtime.browser-wasm`** companions on the same feed.

Pin those **three** package versions centrally in `Directory.Packages.props` and reference them **only** from the browser host when `BootsharpLLVM=true`.

## Node.js side (not “unchanged by LLVM”)

LLVM work is **browser-only**. Node is a **separate artifact**:

| What `dotnet publish -f net10.0` produces today | What `stage-node.mjs` / `motely-node` often assumed |
|------------------------------------------------|------------------------------------------------------|
| **Managed** NodeApi: `Motely.js`, `import.cjs`, `Motely.dll`, dependencies | **Native** `*.node` (NativeAOT + `PublishNodeModule`, linux RID) |

**These are incompatible stories.** Pick one and align:

1. **Managed path:** Stage `publish/*.js`, `import.cjs`, `*.dll` (and `Motely.d.ts`); update [`motely-node`](../motely-node) `package.json` / loader accordingly.
2. **Native path:** Separate project (or explicit publish profile) with `PublishAot` + `PublishNodeModule` + `-r linux-x64`, then stage **that** output — **not** the managed layout.

Until then, **`build-and-pack.ps1`** must not claim it produces **`motely.node`** if publish does not emit it.

## Staging and scripts

- **WASM:** Point [`Motely/build/stage-wasm.mjs`](../Motely/build/stage-wasm.mjs) (or env `MOTELY_WASM_PUBLISH_DIR`) at **`Motely.BrowserWasm/bin/.../publish/`**, not `Motely`’s (library) output.
- **Node:** Point [`Motely/build/stage-node.mjs`](../Motely/build/stage-node.mjs) at the **actual** net10.0 publish folder for the **chosen** Node strategy (managed vs native).
- Update [`build-and-pack.ps1`](../build-and-pack.ps1) and [`.windsurf/workflows/prepare-update.md`](../.windsurf/workflows/prepare-update.md) to publish **two projects** if needed: `Motely.BrowserWasm` (WASM) and optionally a Node host project.

## Consumer validation: [`Motley.TestUI`](../Motley.TestUI)

- **Smoke:** App boots, `boot()` completes, one **Analyze** and one **shop stream** request return data.
- **Latency (manual or scripted):** Time from button click to first shop rows / analysis result (target: document baseline in ms after LLVM vs before).
- **Files of interest:** [`useMotelyShopStream.ts`](../Motley.TestUI/motely-ui/useMotelyShopStream.ts), [`MotelyShopStreamAnalyzer.tsx`](../Motley.TestUI/motely-ui/MotelyShopStreamAnalyzer.tsx), [`motelySeedAnalysis.ts`](../Motley.TestUI/motely-ui/motelySeedAnalysis.ts).

## Implementation checklist (ordered)

1. Restore **`Motely.BrowserWasm`** from **`c7689f35^`** (csproj + `Program.cs` + `Interop/*`).
2. Add project to **`Motely.sln`**; set **`Motely`** to **`net10.0` only**; remove Bootsharp from **`Motely.csproj`** (host-only).
3. Fix **`Motely.Orchestration`** browser targeting if it blocks restore/publish without the library browser TFM.
4. Wire **LLVM**: experimental feed, bump **ILCompiler.LLVM** to e.g. **10.0.0-rc.1.26117.1** (re-verify with search command), set **`BootsharpLLVM=true`** on host when ready.
5. **`dotnet publish Motely.BrowserWasm -c Release -f net10.0-browser -r browser-wasm -v n`** — confirm **`bootsharp`** (or doc layout) under publish.
6. Align **Node** staging + **motely-node** with **managed** or **native** choice; fix **`build-and-pack.ps1`** wording and paths.
7. Run **`Motley.TestUI`** against staged `motely-wasm` and record latency smoke notes.

## Risks (short)

- **NativeAOT-LLVM** is experimental; pin versions and expect SDK drift.
- **Trim / AOT** vs YamlDotNet / ImageSharp / generated code — budget time for `TrimmerRootDescriptor` or API fixes after `PublishTrimmed` tightens.
- **Threading / SIMD** semantics differ between Mono WASM and LLVM; retest search and UI progress paths.
