# Build MotelyJAML to new npm packages — both packages, one version, every time

**This is the only workflow. Both motely-wasm and motely-node are always built and released together. One version. No optional steps. No fallbacks.**

---

## Rules

- **Version:** Single source `Directory.Packages.props` → `<MotelyVersion>`. Sync runs automatically before `dotnet build`/`dotnet publish` (Directory.Build.props). Do not edit package.json or `jaml.schema.json` by hand.
- **motely-node:** Package is produced by the SDK from **Motely.NodeAddon** only. Do not add `main`/`exports` to Motely.NodeAddon/package.json; do not create hand-written loader JS; the SDK generates the loader and types. Output: `Motely.NodeAddon\bin` (per RID), `.tgz` in `Motely.NodeAddon\pkg`.
- **Linux addon:** linux-x64 only via Docker (`build-linux.ps1` on Windows, `build-linux.sh` on Unix); no WSL.
- **Publish:** Run `npm publish` only when explicitly asked; otherwise stop at pack/ready and print next steps.

---

## Phase A: Version (one source, both packages)

1. Edit **only** `Directory.Packages.props`: set `<MotelyVersion>` to the new version (e.g. 3.1.6 → 3.1.7).
2. **No separate sync step.** Directory.Build.props runs `node sync-version.mjs` before Build/Publish of BrowserWasm or NodeAddon. The next dotnet publish updates Motely.npm and Motely.NodeAddon package.json.
3. Do not edit any package.json or jaml.schema.json by hand.

---

## Phase B: motely-wasm (always)

All from **repo root** unless stated.

4. Remove existing staged WASM: delete `Motely.npm\_framework` if present.

5. **Browser WASM:**  
   `dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release`  
   Then: `node stage-packages.mjs browser`.  
   Confirm output under `Motely.npm\_framework`.

6. **Build motely-wasm:**  
   `cd Motely.npm`, `npm install`, `npm run build`.  
   motely-wasm is ready to pack/publish (same version as Phase A).

---

## Phase C: motely-node (always)

Same version. From **repo root**. Package lives in **Motely.NodeAddon**; publish outputs to `Motely.NodeAddon\bin`, `.tgz` to `Motely.NodeAddon\pkg`.

7. **win-x64 addon:**  
   `dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64`  
   Confirm: `Motely.NodeAddon\bin\win-x64\Motely.NodeAddon.node` exists.

8. **linux-x64 addon (Docker only):**  
   `.\build-linux.ps1` (Windows) or `./build-linux.sh` (Unix).  
   Confirm: `Motely.NodeAddon\bin\linux-x64\Motely.NodeAddon.node` exists. If it fails, fix Docker; do not consider the package ready without the linux binary.

9. **Pack:** The SDK runs `npm pack` during publish (`PackNpmPackage`). After both RIDs, the tarball in `Motely.NodeAddon\pkg` should contain both `bin/win-x64/` and `bin/linux-x64/`. Optionally run `cd Motely.NodeAddon && npm pack` to regenerate and inspect.

---

## Phase D: Publish and consume

10. **Publish (only if user asked):**  
    From `Motely.npm`: `npm publish` (motely-wasm).  
    From `Motely.NodeAddon`: `npm publish` (motely-node).  
    Same version for both.

11. **Update JAMMY (after publish):**  
    From JAMMY repo root: `pnpm add motely-node@<V> motely-wasm@<V>` (use the version from Phase A), then `pnpm run build`. Build must succeed.

---

## Execution rules

- **Order:** A → B → C → D. Do not skip any phase. Both packages are built every time.
- **Version:** One bump in Directory.Packages.props only; sync-version.mjs updates Motely.npm and Motely.NodeAddon package.json. Never edit package.json version or jaml.schema.json manually.
- **motely-node:** No hand-written loader or main/exports in package.json. SDK generates loader and types.
- **Linux:** Only `build-linux.ps1` / `build-linux.sh` for linux-x64. No WSL, no host `dotnet publish -r linux-x64`.
- **Publish:** Run `npm publish` for both packages only when the user explicitly asks. Otherwise stop after pack and print the two publish commands.

---

## Checklist (no optional items)

- [ ] A: Bump `<MotelyVersion>` in Directory.Packages.props only (sync runs on next dotnet build/publish).
- [ ] B: Clean Motely.npm _framework dirs; publish BrowserWasm + stage browser; publish SingleThread + stage singlethread; `cd Motely.npm && npm install && npm run build`.
- [ ] C: win-x64 publish → `Motely.NodeAddon\bin\win-x64\*.node`; `.\build-linux.ps1` (or `./build-linux.sh`) → `Motely.NodeAddon\bin\linux-x64\*.node`; confirm .tgz in `Motely.NodeAddon\pkg` has both bin dirs.
- [ ] D: If user asked: `npm publish` in Motely.npm and in Motely.NodeAddon. JAMMY: `pnpm add motely-node@<V> motely-wasm@<V>` and `pnpm run build` — green.

Both packages, same version, every run. No optional steps.
