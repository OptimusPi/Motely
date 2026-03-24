# AGENTS.md — MotelyJAML technical ground truth

Companion to `SOUL.md`. **If workflow docs, chat, or memory disagree with this file, this file wins** for how the repo actually builds.

---

## Versioning

- **Single source:** `<MotelyVersion>` in `Directory.Packages.props`.
- **Sync to npm:** `sync-version.mjs` updates `motely-wasm/package.json` and `motely-node/package.json`, or run **`node build.mjs`** (wasm/node targets) which also syncs before building.
- **JAML JSON schema:** `version` inside `jaml.schema.json` / `public/jaml.schema.json` comes from the same property via generator — **do not hand-edit**; run:
  `dotnet run --project Motely.CLI/Motely.CLI.csproj -c Release -- --write-jaml-schema`

---

## Projects (what ships where)

- **`Motely/Motely.csproj`** — multi-targets **`net10.0`** (CLI, servers, tests) and **`net10.0-browser`** (browser BCL / managed WASM). Use the browser TFM when you want **Motely as a library** inside a Blazor WASM or other `net10.0-browser` app — **no JS exports required**. Prefer in-memory JAML (`JamlConfigLoader.TryLoad(string,…)`) in the browser; file-path helpers may not apply.
- **Two different “browser” stacks:** (1) **`net10.0-browser`** = managed DLL for browser workloads. (2) **`motely-wasm` npm** = **NativeAOT-LLVM + Bootsharp** from **`Motely.Orchestration`** only — that is what produces `index.mjs` + native WASM. You cannot get that npm bundle by publishing `Motely.csproj` alone; for a **minimal Bootsharp surface**, add a tiny host exe that references `Motely` and copies the Orchestration-style publish properties, or keep using Orchestration.
- **`Motely.Orchestration/Motely.Orchestration.csproj`** — publish entry for **Bootsharp browser WASM** and **Node** native addon. It **ProjectReference**s `Motely` (`net10.0`); the engine is linked into the published output.
- **Browser (npm):** `dotnet publish Motely.Orchestration/Motely.Orchestration.csproj -c Release -p:WasmBuild=true` (Bootsharp + ILCompiler LLVM, RID `browser-wasm`).
- **Node:** `dotnet publish Motely.Orchestration/Motely.Orchestration.csproj -c Release -p:NodeBuild=true -r linux-x64` (or `win-x64` on Windows for local iteration).

---

## Staging npm packages

- **Canonical script:** repo-root **`build.mjs`** — `node build.mjs wasm`, `node build.mjs node`, or `node build.mjs --pack` (packs **local** dirs: `npm pack ./motely-wasm`, `npm pack ./motely-node`).
- **Manual stage (WASM):** after publish, Bootsharp output is under **`Motely.Orchestration/bin/bootsharp/`**. `Motely/build/stage-wasm.mjs` copies `index.mjs` → **`motely-wasm/dist/index.mjs`**, optional `types/` → `motely-wasm/dist/types/`, and `jaml.schema.json` → `motely-wasm/dist/jaml.schema.json`. There is **no** `motely-wasm/dist/bootsharp/index.mjs` layout in the current stager.

---

## Operational rules (avoid “prod down” self-inflicted damage)

1. **One `dotnet` build/publish at a time** on the same clone (no overlapping agent + human `dotnet` on the same `bin`/`obj`). Race → missing `.pdb`/copy errors and corrupt incremental output.
2. **Do not** confuse **`dotnet publish Motely.csproj -f net10.0-browser`** with **`motely-wasm`**. Publishing the library for `net10.0-browser` is valid for **managed** browser apps; it does **not** produce the Bootsharp **npm** layout. The npm package still comes from **Orchestration** + `WasmBuild=true`.
3. **Do not** commit or hand-edit generated npm staging under `Motely.npm-staging/` unless the repo owner explicitly uses that path; prefer `build.mjs` / `motely-wasm/dist/`.
4. Human **`publish.ps1`** at repo root: tests → solution build → WASM publish (`WasmBuild`) → Node publish (`NodeBuild` + `linux-x64`). Align any doc with that order if you document the full release.

---

## Quick reference commands (from repo root)

```powershell
dotnet test Motely.Tests -c Release
dotnet publish Motely.Orchestration/Motely.Orchestration.csproj -c Release -p:WasmBuild=true
node Motely/build/stage-wasm.mjs
# or
node build.mjs wasm
```

```powershell
dotnet publish Motely.Orchestration/Motely.Orchestration.csproj -c Release -p:NodeBuild=true -r linux-x64
node build.mjs node
```

---

## Architecture note

- **Orchestration is the integration layer** exposed to JS (Bootsharp / NodeApi). **`MotelySearchOrchestrator`** and JAML loading live there or call into `Motely`. Keep browser/Node hosts thin; do not reintroduce redundant static “interop API” layers unless the maintainer asks.
