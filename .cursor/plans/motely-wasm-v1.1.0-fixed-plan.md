---
name: motely-wasm v1.1.0 fixed plan
overview: Corrected plan for shipping a real motely-wasm npm package (v1.1.0). Assumes Motely.WASM was deleted and Motely.sln is gone; keeps Motely.npm and progress; recreates what's missing and fixes the rest.
todos:
  - id: slnx
    content: Recreate solution as Motely.slnx (all .csproj projects)
    status: pending
  - id: wasm_project
    content: Recreate Motely.WASM project (entry + MotelyWasm exports for npm API)
    status: pending
  - id: json_aot
    content: JSON in WASM path uses JsonSerializerContext only
    status: pending
  - id: wasm_build_fix
    content: No duplicate Content in Motely.WASM.csproj
    status: pending
  - id: check_framework
    content: check-framework.js + prepublishOnly in Motely.npm
    status: pending
  - id: readme_publish
    content: README publishing steps + optional coi-serviceworker
    status: pending
  - id: workflow
    content: CI dotnet publish then npm publish from Motely.npm
    status: pending
  - id: version_on_publish
    content: Bump to 1.1.0 only when you actually publish
    status: pending
isProject: false
---

# Motely-WASM v1.1.0 — Fixed Plan

**If anything here is unclear or you're stuck, ask before assuming. Don't guess.**

**Do not use the current/broken Motely.npm package (index.ts or its types) as the source of truth for what the WASM must export.** The task is to build browser WASM for JAML searches and the Analyzer. The source of truth is Motely + Motely.Orchestration. Design the WASM API from that; then make the npm package load and expose it.

---

## Current state (truth)

- **Motely.WASM** — Deleted. Gone. There is no WASM project in the repo.
- **Motely.sln** — Gone (you deleted it; you wanted .slnx).
- **Motely.npm** — Exists. Package name `motely-wasm`. Has index.ts/js, plugins, package.json. **No _framework** (nothing produces it).
- **publish-motely-wasm.yml** — Still uses `working-directory: Motely.WASM` (broken).
- **Goal** — Ship motely-wasm v1.1.0 that actually contains the WASM runtime so users get a real package. Keep your progress; don't reset.

---

## Source of truth (NOT the npm package)

The **source of truth is the .NET code**, not the existing npm package (which is known to not work).

- **Analyzer**: [Motely/Analysis/MotelySeedAnalyzer.cs](Motely/Analysis/MotelySeedAnalyzer.cs) — `MotelySeedAnalyzer.Analyze(MotelySeedAnalysisConfig)`. Config includes seed, deck, stake, ante, shop, and filter config. Use ConfigFormatConverter for JSON config (AOT-safe).
- **Search**: [Motely.Orchestration](Motely.Orchestration/) — `MotelySearchOrchestrator`, `MultiSearchManager.StartSearchAsync`, etc. Config comes from JAML/JSON via ConfigFormatConverter / JamlConfigLoader.
- **Config**: [Motely.Orchestration/ConfigFormatConverter.cs](Motely.Orchestration/ConfigFormatConverter.cs) — `LoadFromJsonString` / `LoadFromJamlString` → MotelyJsonConfig. Use JsonSerializerContext everywhere in this path.

**Design the WASM export from this.** The WASM project exposes a single [JSExport] type that the browser can call: e.g. methods that take (seed, deck, stake, ante, shop, configJson) and call MotelySeedAnalyzer.Analyze; methods that take (jamlContent, options) and call the orchestration search; methods for version/capabilities and for search status/stop. Shape the export to what the browser actually needs to run JAML searches and the analyzer. **Then** the npm package is a thin loader: it loads `_framework/dotnet.js`, gets that export, and re-exposes it. The npm package’s TypeScript and `loadMotely()` must be updated to match whatever the WASM export actually is—not the other way around.

---

## Step 1: Recreate solution as Motely.slnx

- No .sln or .slnx exists. You need a solution so `dotnet build` works at repo root.
- Create **Motely.slnx** (XML solution format) that includes all existing .csproj projects: Motely, Motely.API, Motely.CLI, Motely.DB, Motely.GPU, Motely.MCP, Motely.Orchestration, Motely.Repository, Motely.Tests, Motely.TUI, and (after Step 2) Motely.WASM.
- If you prefer to add Motely.WASM in Step 2 and then add it to the solution in Step 2, that's fine; just ensure the solution lists every project you need to build.

---

## Step 2: Recreate Motely.WASM project

- Add folder **Motely.WASM** and a browser WASM app that **exposes Motely’s analyzer and JAML search to JS**. Do not derive the API from the broken npm package; derive it from what the browser needs and what Motely/Orchestration actually provide.
- **Motely.WASM.csproj**: SDK `Microsoft.NET.Sdk.WebAssembly`. TargetFramework `net10.0-browser`. References Motely + Motely.Orchestration. **Do not** add duplicate `<Content Include="wwwroot\**" ... />`. **Include** the `PublishToNpmPackage` target that copies `$(PublishDir)wwwroot\_framework\**` to `../Motely.npm/_framework`. Set all browser feature and size properties per **Browser features (from features.md)** below.
- **Program.cs / Main**: With **Microsoft.NET.Sdk.WebAssembly** (browser + dotnet.js), the supported model is **OutputType=Exe** and an entry point. The SDK does not offer a “library only” browser build that still uses dotnet.js and `getAssemblyExports()`. So if the goal is “run in browser, load via Motely.npm and dotnet.js”, we stay with Exe and a minimal Main. The [official dotnet/runtime browser sample](https://github.com/dotnet/runtime/blob/main/src/mono/sample/wasm/browser-advanced/Program.cs) works that way: it has `Main` (with real demo code) and the real API is the class with `[JSExport]` that JS calls via `getAssemblyExports()`. We follow the same pattern; our Main is minimal only because we’re library-style (no UI). Use `public static void Main()` — body empty or e.g. `Console.WriteLine("Motely WASM ready")`. The npm host does not call `runMain()`; it only uses `getAssemblyExports()` and the exported type.
- **WASM export class**: One [JSExport] type (e.g. `Motely.WASM.MotelyWasm`) whose methods **call the real Motely/Orchestration APIs**:
  - Version/capabilities (for the host to detect runtime).
  - **Analyzer**: takes (seed, deck, stake, ante, shop, configJson string). Builds MotelySeedAnalysisConfig (use ConfigFormatConverter.LoadFromJsonString for config if needed), calls `MotelySeedAnalyzer.Analyze`, returns a shape JS can use (e.g. DTOs or JSON via JsonSerializerContext).
  - **Search**: takes (jamlContent, deck?, stake?, threads?, batchSize?, startBatch?, endBatch?, cutoff?). Loads config via ConfigFormatConverter/JamlConfigLoader, starts search via Orchestration (MultiSearchManager or MotelySearchOrchestrator), returns a searchId. Plus GetSearchStatus(searchId), StopSearch(searchId).
- All JSON in this path must use **JsonSerializerContext** (MotelyJsonSerializerContext). No reflection-based JsonSerializer in the WASM build.
- **After** the WASM project builds and exports this API, **update Motely.npm** (index.ts, index.d.ts) so `loadMotely()` loads _framework and returns that export. The npm package is a thin loader that matches the WASM—not the source of truth for the API.

---

## Browser features (from features.md)

All of the following are from [Configuring and hosting .NET WebAssembly applications](https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md). Put these in a `<PropertyGroup>` in **Motely.WASM.csproj**. Requires **wasm-tools** workload (`dotnet workload install wasm-tools`).

**Motely’s choices:**

| Feature | Choice | MSBuild / notes |
|--------|--------|------------------|
| **Multi-threading** | YES | `<WasmEnableThreads>true</WasmEnableThreads>`. Requires a unique runtime build. **Server/host must send COOP/COEP** (SharedArrayBuffer); use coi-serviceworker if the host can’t (see Step 6). [JSExport]/[JSImport] remain **main-thread only**; do not block main thread with `Task.Wait` or `Monitor.Enter`. |
| **SIMD** | YES | `<WasmEnableSIMD>true</WasmEnableSIMD>`. (Default is already true; set explicitly so it’s clear.) |
| **Exception handling** | Default | Leave default (enabled). Higher perf for try/catch. |
| **BigInt** | No | Not needed; no action. |
| **fetch / HttpClient** | No | Motely WASM path doesn’t use HttpClient; no action. |
| **Initial heap size** | YES | Motely on Windows is &lt;50MB; set initial heap to avoid grow delays. Value **must be a multiple of 16384**. Example: 64MB = 67108864 → `<EmccInitialHeapSize>67108864</EmccInitialHeapSize>`. |
| **Maximum heap size** | Optional | Default is 2GB; Motely is lean. To cap: `<EmccMaximumHeapSize>268435456</EmccMaximumHeapSize>` (256MB). Omit if you prefer default. |
| **JITerpreter** | NO | Disable (we’re AOT): `<BlazorWebAssemblyJiterpreter>false</BlazorWebAssemblyJiterpreter>`. |
| **AOT** | YES | `<RunAOTCompilation>true</RunAOTCompilation>`. Effective when **publishing**; wasm-tools required. |
| **IL trimming** | YES | `<PublishTrimmed>true</PublishTrimmed>` and `<TrimMode>full</TrimMode>`. Reduces startup and size. **Trimming and JSON:** use source generation only (no reflection-based `JsonSerializer.Deserialize<T>`). ConfigFormatConverter / MotelyJsonSerializerContext and any other JAML/JSON in the WASM path must use JsonSerializerContext; audit and fix any that don’t (Step 3). |
| **C / native libs** | No | Do not set `WasmBuildNative` or add `NativeFileReference`. |
| **Bundler-friendly** | YES | `<WasmBundlerFriendlyBootConfig>true</WasmBundlerFriendlyBootConfig>` for the npm/JS host loading pattern. |

**JavaScript host API:** The runtime is configured and called via the JS API in [dotnet.d.ts](https://github.com/dotnet/runtime/blob/main/src/mono/browser/runtime/dotnet.d.ts); examples are in the [samples](https://github.com/dotnet/runtime/tree/main/src/mono/sample/wasm). Motely.npm’s `loadMotely()` uses this (dotnet.create, getAssemblyExports, etc.).

**Example PropertyGroup for Motely.WASM.csproj:**

```xml
<PropertyGroup>
  <TargetFramework>net10.0-browser</TargetFramework>
  <WasmBundlerFriendlyBootConfig>true</WasmBundlerFriendlyBootConfig>
  <RunAOTCompilation>true</RunAOTCompilation>
  <WasmEnableThreads>true</WasmEnableThreads>
  <WasmEnableSIMD>true</WasmEnableSIMD>
  <BlazorWebAssemblyJiterpreter>false</BlazorWebAssemblyJiterpreter>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>full</TrimMode>
  <EmccInitialHeapSize>67108864</EmccInitialHeapSize>
  <!-- Optional: <EmccMaximumHeapSize>268435456</EmccMaximumHeapSize> (256MB) -->
</PropertyGroup>
```

(Plus project reference to Motely and Motely.Orchestration, and the PublishToNpmPackage target; no duplicate Content.)

---

## Alternative: componentize-dotnet (WASI 0.2, no Main)

[bytecodealliance/componentize-dotnet](https://github.com/bytecodealliance/componentize-dotnet) is a **different** stack: it builds **WASI 0.2 components** from C# (NativeAOT-LLVM, WIT, wit-bindgen). When you **export** a component, you set **OutputType=Library** and **delete Program.cs** — no Main at all. That’s the “I don’t want Exe” path. The output runs in **wasmtime** or **WAMR**, not in the browser via dotnet.js. To run a WASI component in the browser you’d need a WASI-in-browser runtime (e.g. wasmtime compiled to WASM), which is a different integration than “load dotnet.js, getAssemblyExports()”. So for **this** plan we stay with the Microsoft browser SDK and Exe + minimal Main so the existing Motely.npm (dotnet.js loader) keeps working. componentize-dotnet is worth a separate evaluation if you want WASI 0.2 / wasmtime / non-browser or a future browser-WASI story.

---

## Step 3: JSON AOT in WASM path

- Every `JsonSerializer.Deserialize` / `Serialize` in Motely and Motely.Orchestration that runs in the WASM build must use a source-generated context (e.g. `MotelyJsonSerializerContext.Default.MotelyJsonConfig`). No raw `JsonSerializer.Deserialize<T>(json)` in that graph.
- ConfigFormatConverter already does; audit other call sites (JamlConfigLoader, executors, etc.) and fix any that don’t.

---

## Step 4: No duplicate Content in Motely.WASM.csproj

- Do not add an explicit `<Content Include="wwwroot\**" ... />` in the WASM csproj. The WebAssembly SDK already includes wwwroot; adding it again causes NETSDK1022.

---

## Step 5: Publish safeguard (Motely.npm)

- Add **check-framework.js**: Node script that exits 1 with clear instructions if `_framework` is missing or `_framework/dotnet.js` is absent.
- In **package.json**, set `prepublishOnly` to `"node check-framework.js && npm run build"`. Do not add check-framework.js to the `files` array.

---

## Step 6: README (Motely.npm)

- Add **Publishing this package (maintainers)**: (1) From repo root: `dotnet publish Motely.WASM/Motely.WASM.csproj -c Release`. (2) `cd Motely.npm`. (3) Optional: `npm pack --dry-run` and/or local install to verify _framework. (4) When ready to release: `npm version minor` (or set 1.1.0) then `npm publish`.
- **COOP/COEP (e.g. GitHub Pages, Blueprint)**: Browsers need cross-origin isolation for SharedArrayBuffer (used by .NET WASM). If the host can’t set `Cross-Origin-Opener-Policy` and `Cross-Origin-Embedder-Policy` headers (e.g. GitHub Pages, static hosting), use [coi-serviceworker](https://github.com/gzuidhof/coi-serviceworker): serve `coi-serviceworker.js` from the same origin and add `<script src="coi-serviceworker.js"></script>` so the page gets COOP/COEP via a service worker. Document this in the README for consumers who deploy to GH Pages or similar.

---

## Step 7: Fix GitHub Actions workflow

- **publish-motely-wasm.yml**: Do not use `working-directory: Motely.WASM` for npm. Steps: (1) Checkout, setup .NET, setup Node. (2) From repo root run `dotnet publish Motely.WASM/Motely.WASM.csproj -c Release` (this fills Motely.npm/_framework). (3) Set version from tag or workflow_dispatch input. (4) `cd Motely.npm`, `npm install`, `npm publish --access public` with NODE_AUTH_TOKEN.

---

## Step 8: Version 1.1.0 only when you publish

- Leave **package.json** version as-is (e.g. 1.0.10) until you’re ready to publish. When you publish, run `npm version minor` in Motely.npm (to get 1.1.0) or set `"version": "1.1.0"` manually, then run the publish steps. No need to change the version in the repo until then.

---

## Summary

| Step | What |
|------|------|
| 1 | Recreate solution as Motely.slnx (all projects) |
| 2 | Recreate Motely.WASM (csproj, Program.cs, MotelyWasm exports → Motely/Orchestration) |
| 3 | JSON in WASM path uses JsonSerializerContext only |
| 4 | No duplicate Content in Motely.WASM.csproj |
| 5 | check-framework.js + prepublishOnly in Motely.npm |
| 6 | README: publishing steps + optional coi-serviceworker |
| 7 | CI: dotnet publish then npm publish from Motely.npm |
| 8 | Bump to 1.1.0 only when you actually publish |

When in doubt, ask.
