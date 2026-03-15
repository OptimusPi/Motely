# Build MotelyJAML to new npm packages — both packages, one version, every time

**This is the only workflow. Both motely-wasm and motely-node are always built and released together. One version. No optional steps. No fallbacks.**

---

## Rules

- **Version:** Single source `Directory.Packages.props` → `<MotelyVersion>`. Sync runs automatically before `dotnet build`/`dotnet publish` (Directory.Build.props). Do not edit package.json or `jaml.schema.json` by hand.
- **Linux addon:** linux-x64 only via Docker (`build-linux.ps1` on Windows, `build-linux.sh` on Unix); no WSL.
- **Publish:** Run `npm publish` only when explicitly asked; otherwise stop at pack/ready and print next steps.

---

## Phase A: Version (one source, both packages)

1. Edit **only** `Directory.Packages.props`: set `<MotelyVersion>` to the new version (e.g. 3.1.6 → 3.1.7).
2. **No separate sync step.** `Directory.Build.props` runs `node sync-version.mjs` automatically before any `dotnet build` or `dotnet publish` of BrowserWasm, SingleThread, or NodeAddon. So the next `dotnet publish` (Phase B or C) updates both Motely.npm and motely-node package.json in one go.
3. Do not edit any package.json or jaml.schema.json by hand.

---

## Phase B: motely-wasm (always)

All from **repo root** unless stated.

4. Remove existing staged WASM: delete `Motely.npm\_framework` and `Motely.npm\_framework_st` if present.

5. **Browser WASM (threaded):**  
   `dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release`  
   Then: `node stage-packages.mjs browser`.  
   Confirm output under `Motely.npm\_framework`.

6. **Browser WASM (single-thread):**  
   `dotnet publish Motely.SingleThread/Motely.SingleThread.csproj -c Release`  
   Then: `node stage-packages.mjs singlethread`.  
   Confirm output under `Motely.npm\_framework_st`.

7. **Build motely-wasm:**  
   `cd Motely.npm`, `npm install`, `npm run build`.  
   motely-wasm is now ready to pack/publish (same version as Phase A).

---

## Phase C: motely-node (always)

Same version. From **repo root**.

8. **win-x64 addon:**  
   `dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64`  
   Confirm: `motely-node\bin\win-x64\Motely.NodeAddon.node` exists.

9. **linux-x64 addon (Docker only):**  
   `.\build-linux.ps1` (Windows) or `./build-linux.sh` (Unix).  
   Confirm: `motely-node\bin\linux-x64\Motely.NodeAddon.node` exists. If it fails, fix Docker; do not publish without the linux binary.

10. **Pack motely-node:**  
    `cd motely-node`, then `npm pack` (motely-node has no `build` script; pack uses bin/ as-is).  
    Confirm tarball has both `bin/win-x64/` and `bin/linux-x64/` with `.node` files.

---

## Phase D: Publish and consume

11. **Publish (only if user asked):**  
    From `Motely.npm`: `npm publish` (motely-wasm).  
    From `motely-node`: `npm publish` (motely-node).  
    Same version for both.

12. **Update JAMMY (after publish):**  
    From JAMMY repo root: `pnpm add motely-node@<V> motely-wasm@<V>` (use the version from Phase A), then `pnpm run build`. Build must succeed.

---

## Execution rules

- **Order:** A → B → C → D. Do not skip any phase. Both packages are built every time.
- **Version:** One bump in Directory.Packages.props only; sync-version.mjs updates both package.json files. Never edit package.json version or jaml.schema.json manually.
- **Linux:** Only `build-linux.ps1` / `build-linux.sh` for linux-x64. No WSL, no host `dotnet publish -r linux-x64`.
- **Publish:** Run `npm publish` for both packages only when the user explicitly asks. Otherwise stop after pack and print the two publish commands.

---

## Checklist (no optional items)

- [ ] A: Bump `<MotelyVersion>` in Directory.Packages.props only (sync runs on next dotnet build/publish).
- [ ] B: Clean Motely.npm _framework dirs; publish BrowserWasm + stage browser; publish SingleThread + stage singlethread; `cd Motely.npm && npm install && npm run build`.
- [ ] C: win-x64 publish → `motely-node\bin\win-x64\*.node`; `.\build-linux.ps1` (or `./build-linux.sh`) → `motely-node\bin\linux-x64\*.node`; `cd motely-node && npm pack`; tarball has both bin dirs.
- [ ] D: If user asked: `npm publish` in Motely.npm and in motely-node. JAMMY: `pnpm add motely-node@<V> motely-wasm@<V>` and `pnpm run build` — green.

Both packages, same version, every run. No optional steps.
