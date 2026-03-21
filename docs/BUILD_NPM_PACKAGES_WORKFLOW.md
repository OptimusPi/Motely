# Build MotelyJAML to new npm packages — both packages, one version, every time

**This is the only workflow. Both motely-wasm and motely-node are always built and released together. One version. No optional steps. No fallbacks.**

---

## Rules

- **Version:** Single source `Directory.Packages.props` → `<MotelyVersion>`. Sync runs automatically before `dotnet build`/`dotnet publish` (Directory.Build.props). Do not edit package.json by hand.
- **JAML schema (`jaml.schema.json`, `jaml-schema.js`, `jaml-schema.d.ts`):** Generated only from C# via `Motely.CLI --write-jaml-schema` (`JamlSchemaGenerator`). Never edit the mirror paths by hand — that causes drift. `build-and-pack.ps1` runs schema generation right after the version bump.
- **motely-node:** Package root is `motely-node/`. The `index.cjs` loader and `index.d.ts` types live there (committed). `dotnet publish` via Docker puts the `.node` binary into `motely-node/bin/linux-x64/`. `npm pack` runs from `motely-node/` — not via the SDK's PackNpmPackage. Linux-x64 only (Vercel target).
- **Linux addon:** linux-x64 only via Docker (`build-linux.ps1` on Windows, `build-linux.sh` on Unix); no WSL.
- **Publish:** Run `npm publish` only when explicitly asked; otherwise stop at pack/ready and print next steps.

---

## Phase A: Version (one source, both packages)

1. Edit **only** `Directory.Packages.props`: set `<MotelyVersion>` to the new version (e.g. 3.1.6 → 3.1.7).
2. **No separate sync step.** Directory.Build.props runs `node sync-version.mjs` before Build/Publish of BrowserWasm or NodeAddon. The next dotnet publish updates `motely-node/package.json` and `motely-wasm/package.json`.
3. Do not edit package.json by hand. Regenerate JAML schema files with `Motely.CLI --write-jaml-schema`, not by editing copies.

---

## Phase B: motely-wasm (always, Bootsharp)

All from **repo root** unless stated.

4. Remove existing staged bundles: delete `motely-wasm\bootsharp` and `motely-wasm\bootsharp_st` if present.

5. **Browser WASM (single-thread):**  
   `dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release -p:SingleThread=true`  
   Then: `node stage-packages.mjs bootsharp-st`.  
   Confirm output under `motely-wasm\bootsharp_st`.

6. **Browser WASM (multi-thread):**  
   `dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release`  
   Then: `node stage-packages.mjs bootsharp`.  
   Confirm output under `motely-wasm\bootsharp`.

7. **Build motely-wasm:**  
   `cd motely-wasm`, `npm install`, `npm run build`.  
   motely-wasm is ready to pack/publish (same version as Phase A).

---

## Phase C: motely-node (always, Linux-only)

Same version. From **repo root**. Package root is `motely-node/`; binary lands in `motely-node/bin/linux-x64/`.

7. **linux-x64 addon (Docker only):**  
   `.\build-linux.ps1` (Windows) or `./build-linux.sh` (Unix).  
   Confirm: `motely-node\bin\linux-x64\Motely.NodeAddon.node` exists. If it fails, fix Docker; do not consider the package ready without the linux binary.

9. **Copy jaml-schema:** Copy `jaml-schema.js`, `jaml-schema.d.ts`, `jaml.schema.json` from `motely-wasm/` to `motely-node/`.

10. **Pack:** `cd motely-node && npm pack`. Inspect the tarball: must contain `index.cjs`, `index.d.ts`, `bin/linux-x64/Motely.NodeAddon.node`, and jaml-schema files.

---

## Phase D: Publish and consume

11. **Publish (only if user asked):**  
    From `motely-wasm`: `npm publish` (motely-wasm).  
    From `motely-node`: `npm publish <tgz>` (motely-node).  
    Same version for both.

12. **Update JAMMY (after publish):**  
    From JAMMY repo root: `pnpm add motely-node@<V> motely-wasm@<V>` (use the version from Phase A), then `pnpm run build`. Build must succeed.

---

## Execution rules

- **Order:** A → B → C → D. Do not skip any phase. Both packages are built every time.
- **Version:** One bump in Directory.Packages.props only; sync-version.mjs updates `motely-node/package.json` and `motely-wasm/package.json`. Never edit package.json version manually. Never hand-edit generated JAML schema artifacts.
- **motely-node:** Package root = `motely-node/`. Committed `index.cjs` + `index.d.ts` + `package.json`. Binary from Docker in `bin/linux-x64/`. `npm pack` from `motely-node/`.
- **Linux:** Only `build-linux.ps1` / `build-linux.sh` for linux-x64. No WSL, no host `dotnet publish -r linux-x64`.
- **Publish:** Run `npm publish` for both packages only when the user explicitly asks. Otherwise stop after pack and print the two publish commands.

---

## Checklist (no optional items)

- [ ] A: Bump `<MotelyVersion>` in Directory.Packages.props only (sync runs on next dotnet build/publish).
- [ ] B: Clean motely-wasm bootsharp dirs; publish BrowserWasm SingleThread + stage `bootsharp_st`; publish BrowserWasm MultiThread + stage `bootsharp`; `cd motely-wasm && npm install && npm run build`.
- [ ] C: `.\build-linux.ps1` (or `./build-linux.sh`) → `motely-node\bin\linux-x64\*.node`; copy jaml-schema from motely-wasm; `cd motely-node && npm pack`; confirm .tgz has `bin/linux-x64/`, `index.cjs`, `index.d.ts`.
- [ ] D: If user asked: `npm publish` in motely-wasm and `npm publish <tgz>` from motely-node. JAMMY: `pnpm add motely-node@<V> motely-wasm@<V>` and `pnpm run build` — green.

Both packages, same version, every run. No optional steps.
