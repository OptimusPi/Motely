---
description: Prepare a MotelyJAML version update - bump version, clean, build all projects, AOT-publish WASM, prepare npm packages, and guide user through npm publish
auto_execution_mode: 3
---

# Prepare Update

Bumps the MotelyVersion, cleans everything, builds the solution, publishes **`Motely.Orchestration`** with **`WasmBuild=true`** (Bootsharp / browser-wasm), stages **`motely-wasm`**, optionally builds Node (`NodeBuild`), and walks the user through npm publish.

**Ground truth:** repo-root **`AGENTS.md`**. Older instructions that publish **`Motely/Motely.csproj`** with **`-f net10.0-browser`** are **obsolete** for this tree (engine is `net10.0` only; WASM entry is Orchestration).

**User provides**: the new version number — OR if not specified, read the current `<MotelyVersion>` from `Directory.Packages.props` and auto-increment the patch (e.g. `2.2.1` → `2.2.2`).

Do **not** publish automatically.

---

## Step 1: Bump MotelyVersion

Edit `x:\JammySeedFinder\src\MotelyJAML\Directory.Packages.props` — set `<MotelyVersion>` to the new version.

Then sync to all package.json files:

// turbo

```powershell
node x:\JammySeedFinder\src\MotelyJAML\sync-version.mjs
```

This reads `<MotelyVersion>` from Directory.Packages.props and updates:
- `motely-wasm/package.json`
- `motely-node/package.json`

---

## Step 2: Nuke bin/ and obj/

// turbo

```powershell
Get-ChildItem -Path x:\JammySeedFinder\src\MotelyJAML -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
```

---

## Step 3: dotnet clean

// turbo

```powershell
dotnet clean x:\JammySeedFinder\src\MotelyJAML\Motely.sln -c Release
```

---

## Step 4: dotnet restore

// turbo

```powershell
dotnet restore x:\JammySeedFinder\src\MotelyJAML\Motely.sln
```

---

## Step 5: Build all projects

// turbo

```powershell
dotnet build x:\JammySeedFinder\src\MotelyJAML\Motely.sln -c Release
```

If any project fails, report the error and stop.

If browser or cross-platform builds fail because of incompatible APIs, do not force the shared/core project to reference platform-specific code. Keep shared code platform-agnostic, move platform services into platform-specific projects, or exclude incompatible files/references from the browser target.

---

## Step 6: Regenerate JAML schema

Writes schema + helper files to paths used by `motely-wasm` / `motely-node` (see `JamlSchemaGenerator`).

// turbo

```powershell
dotnet run --project x:\JammySeedFinder\src\MotelyJAML\Motely.CLI\Motely.CLI.csproj -c Release -- --write-jaml-schema
```

---

## Step 7: Publish browser WASM (Bootsharp)

Publish **`Motely.Orchestration`** with **`-p:WasmBuild=true`**, then stage into `motely-wasm/dist/`.

**Preferred (one command):**

// turbo

```powershell
node x:\JammySeedFinder\src\MotelyJAML\build.mjs wasm
```

**Manual:**

// turbo

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.Orchestration\Motely.Orchestration.csproj -c Release -p:WasmBuild=true
node x:\JammySeedFinder\src\MotelyJAML\Motely\build\stage-wasm.mjs
```

Verify staged output (current stager puts **`index.mjs` at `dist` root**, not under `dist/bootsharp/`):

// turbo

```powershell
Test-Path x:\JammySeedFinder\src\MotelyJAML\motely-wasm\dist\index.mjs
```

---

## Step 8: Node native addon

**`Motely.Orchestration`** with **`-p:NodeBuild=true`** publishes the native addon into `motely-node/` (see **`build.mjs`** for RID: `linux-x64` vs `win-x64`).

**Preferred:**

// turbo

```powershell
node x:\JammySeedFinder\src\MotelyJAML\build.mjs node
```

**Manual (Linux binary from Windows cross-publish may require your toolchain):**

// turbo

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.Orchestration\Motely.Orchestration.csproj -c Release -p:NodeBuild=true -r linux-x64
```

If Step 8 is skipped, only pack/publish **`motely-wasm`** unless `motely-node` is intentionally JS-only stubs.

---

## Step 9: Pack `motely-wasm`

Use a **path** so npm packs the **local** folder (not a registry version):

// turbo

```powershell
cd x:\JammySeedFinder\src\MotelyJAML
npm pack ./motely-wasm
```

Or: `node build.mjs --pack` (wasm + node + both tarballs; see **`AGENTS.md`**).

---

## Step 10: (Optional) Pack `motely-node`

Only when Step 8 produced the native artifacts under `motely-node/`.

// turbo

```powershell
cd x:\JammySeedFinder\src\MotelyJAML
npm pack ./motely-node
```

---

## Step 11: Tell user to publish

Tell the user:

```md
cd x:\JammySeedFinder\src\MotelyJAML\motely-wasm
npm login
npm publish

# Optional — only if Step 10 was used and motely-node is meant for npm:
cd x:\JammySeedFinder\src\MotelyJAML\motely-node
npm login
npm publish
```

`motely-wasm` ships **`dist/index.mjs`** (+ `types/`, `jaml.schema.json`). Do not follow stale docs that publish **`Motely.csproj`** for browser.

Wait for user to confirm they published.

---

## Step 12: (Optional) Cleanup large staged artifacts

If you want a lean working tree after publish, remove generated WASM staging (adjust if you rely on a committed `dist/`):

```powershell
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\motely-wasm\dist -ErrorAction SilentlyContinue
```

---

## Step 13: Summary

Print:

```sh
✅ MotelyJAML update complete!
   Version: <THE_VERSION>
   Published: motely-wasm@<THE_VERSION> (and motely-node@<THE_VERSION> only if applicable)
```
