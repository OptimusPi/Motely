---
description: Prepare a MotelyJAML version update - bump version, clean, build all projects, AOT-publish WASM, prepare npm packages, and guide user through npm publish
auto_execution_mode: 3
---

# Prepare Update

Bumps the MotelyVersion, cleans everything, builds the solution, publishes **Motely** for `net10.0-browser` (Bootsharp), stages **`motely-wasm`**, and walks the user through npm publish. The old **`Motely.NodeAddon`** / `Motely.npm` flow is gone.

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

`Motely` multi-targets `net10.0-browser`. Publish, then run `motely-wasm`'s build script (`Motely/build/stage-wasm.mjs` copies `bootsharp/` into `motely-wasm/dist/`).

// turbo

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely\Motely.csproj -c Release -f net10.0-browser
npm --prefix x:\JammySeedFinder\src\MotelyJAML\motely-wasm install
npm --prefix x:\JammySeedFinder\src\MotelyJAML\motely-wasm run build
```

Verify staged output:

// turbo

```powershell
Test-Path x:\JammySeedFinder\src\MotelyJAML\motely-wasm\dist\bootsharp\index.mjs
```

---

## Step 8: Node native addon

**`Motely.NodeAddon` was removed.** There is no second C# package to publish for Node in this workflow. Browser delivery is **Bootsharp / `net10.0-browser` only**.

If you add a new Node `.node` target later, document its `dotnet publish` command and how artifacts land under `motely-node/`. Until then, **skip** `npm pack` for `motely-node` unless you intentionally ship JS-only stubs.

---

## Step 9: Pack `motely-wasm`

// turbo

```powershell
npm --prefix x:\JammySeedFinder\src\MotelyJAML\motely-wasm pack
```

---

## Step 10: (Optional) Pack `motely-node`

Only when a tested linux-x64 `.node` (or agreed layout) is present.

// turbo

```powershell
npm --prefix x:\JammySeedFinder\src\MotelyJAML\motely-node pack
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

`motely-wasm` ships `dist/` (Bootsharp runtime + `index.mjs`). Do not reference removed `Motely.npm` / `Motely.NodeAddon` paths.

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
