# Motely WASM: npm dual-package + SeedSearcherWebsite verification

## Verification requirements (non-negotiable)

Ship **two real npm packages** built from **two distinct WASM publish outputs** (no symlink fakery, no hand-edited `index.mjs`):

| Package | WASM build | Browser expectation |
|--------|------------|---------------------|
| **`motely-wasm`** | `WasmEnableThreads=true` (pthread / shared-memory capable) | **COOP + COEP** so `crossOriginIsolated === true` and **SharedArrayBuffer** / threaded runtime work as intended |
| **`motely-wasm-compat`** | `WasmEnableThreads=false` (single-threaded ABI) | Works **without** COOP/COEP (maximum compatibility; no SAB requirement) |

**Acceptance:** `npm pack` on each output directory produces a tarball that installs and imports cleanly; versions stay aligned with [`Directory.Packages.props`](X:/JammySeedFinder/src/MotelyJAML/Directory.Packages.props) `MotelyVersion`.

---

## Test site: [Motely.SeedSearcherWebsite](X:/JammySeedFinder/src/MotelyJAML/Motely.SeedSearcherWebsite) (local)

Must be a **working** smoke site for **both** variants—**not** placeholder copy like “Search not available” on the compat build while pretending the product works.

### A) COEP / SAB / threaded (`motely-wasm`)

- Serve at least one URL **with** response headers:
  - `Cross-Origin-Opener-Policy: same-origin`
  - `Cross-Origin-Embedder-Policy: require-corp` (or `credentialless` if you standardize CORP on assets—pick one strategy and document it)
- Page loads **`motely-wasm`** from `node_modules` (or copied build output) and:
  - Shows **`crossOriginIsolated`** (and optionally `SharedArrayBuffer` availability) in the boot status
  - Exercises threaded search path when `IMotelyProgram` exposes search (or proves multi-thread Motely path once wired); until then, prove **WASM boots** and **Analyze/Validate** on threaded build without falling back to compat artifacts

### B) Compat (`motely-wasm-compat`)

- Served **without** requiring COOP/COEP (plain static is fine)
- Imports **`motely-wasm-compat`** only—proves single-thread package works in the “boring” hosting case

### Local dev ergonomics

- Add a **small dev server** (or extend scripts) so developers can run **both** modes locally without manual header hacking:
  - Example patterns: `vite` / `serve` config / a minimal Node static server that sets COOP/COEP only for `/coep/` (or `/threaded/`)
- Production parity: [`vercel.json`](X:/JammySeedFinder/src/MotelyJAML/Motely.SeedSearcherWebsite/vercel.json) (or equivalent) mirrors the same header rules for the COEP route

### Website `package.json`

- Depend on **both** packages (e.g. `motely-wasm` + `motely-wasm-compat` with correct versions or `file:` paths during dev)
- Update [`scripts/copy-motely-wasm.mjs`](X:/JammySeedFinder/src/MotelyJAML/Motely.SeedSearcherWebsite/scripts/copy-motely-wasm.mjs) (rename or split) to copy **both** bundles into e.g. `motely-wasm/` and `motely-wasm-compat/` under the site root

### HTML/JS structure (suggested)

- **`index.html`** (compat): imports `./motely-wasm-compat/index.mjs`, full Validate + Analyze (+ search when API exists) with honest UX
- **`coep/index.html`** (or `threaded.html` behind COEP): imports `./motely-wasm/index.mjs`, same features + isolation probe
- Shared UI is OK; **duplicate entry points are OK** to avoid one bundle importing the wrong WASM

---

## Build system (MSBuild)

- **Two publish outputs**: second `.csproj` (e.g. `Motely.BrowserWasm.MultiThread`) **or** two configurations (`Release` vs `ReleaseMt`) with `WasmEnableThreads` toggled—same source, different `BootsharpPublishDirectory` / `BootsharpName` to avoid overwriting:
  - Output A → pack as **`motely-wasm`** (from [`Motely/package.json`](X:/JammySeedFinder/src/MotelyJAML/Motely/package.json) duplicated/overlaid with name override—or separate `package.json` templates if Bootsharp overlay is single-file)
  - Output B → pack as **`motely-wasm-compat`** (`package.json` name field `motely-wasm-compat`, distinct `main`/types still valid)

Resolve overlay conflict: today `MotelyWasmNpmOverlay` copies one [`Motely/package.json`](X:/JammySeedFinder/src/MotelyJAML/Motely/package.json). For two packages, use **two template `package.json` files** (e.g. `Motely/npm/motely-wasm.package.json` and `Motely/npm/motely-wasm-compat.package.json`) or MSBuild transforms.

---

## Core Motely + DI (unchanged intent)

- [`Motely.BrowserWasm/Program.cs`](X:/JammySeedFinder/src/MotelyJAML/Motely.BrowserWasm/Program.cs): Bootsharp.Inject + `IMotelyProgram` registration + `RunBootsharp()`
- [`Motely/MotelySearch.cs`](X:/JammySeedFinder/src/MotelyJAML/Motely/MotelySearch.cs): single-thread path must not `new Thread` when `ThreadCount==1` (compat + honest async)

---

## Verification checklist (before “done”)

1. `dotnet publish` **both** WASM projects/configs green
2. `npm pack` in each output dir; install in a temp folder; `node -e "import('motely-wasm')"` / `import('motely-wasm-compat')"` (or equivalent) succeeds
3. Local SeedSearcherWebsite: open compat page—works without special headers
4. Local SeedSearcherWebsite: open COEP page—`crossOriginIsolated === true`, threaded package boots
5. No placeholder “not supported” for the **primary** advertised flows on the build you claim is “working”

---

## Out of scope (unless pulled in later)

- Publishing to the public npm registry (plan assumes pack/install verification; registry is a follow-up)
