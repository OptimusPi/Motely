---
description: Prepare a MotelyJAML version update - bump version, clean, build all projects, AOT-publish WASM, prepare npm packages, and guide user through npm publish
auto_execution_mode: 3
---

# Prepare Update

Bumps the MotelyVersion, cleans everything, builds all .NET projects, AOT-publishes both WASM targets, prepares the active npm packages, and walks the user through publishing.

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
- `Motely.npm\package.json`
- `Motely.node\package.json`

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

## Step 6: Clean _framework dir before AOT publish

// turbo

```powershell
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st -ErrorAction SilentlyContinue
```

---

## Step 7: AOT Release Publish — Browser WASM (frontend)

This publishes `Motely.BrowserWasm` using the validated browser-wasm AOT path. After publish, run the central staging script to copy the filtered `_framework` output into `Motely.npm/_framework/`.

Keep `x:\JammySeedFinder\src\MotelyJAML\Directory.Build.props` in place so browser-wasm projects explicitly reset `PublishAot=false` and `PublishReadyToRun=false` at the local repo level. The actual browser AOT settings live in the project files via `RunAOTCompilation=true` and `WasmBuildNative=true`.

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.BrowserWasm\Motely.BrowserWasm.csproj -c Release
node x:\JammySeedFinder\src\MotelyJAML\stage-packages.mjs browser
```

If this fails, capture the full SDK error and stop before changing other steps.

Verify the output exists:
// turbo

```powershell
@(Get-ChildItem x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework\dotnet.native*.wasm -ErrorAction SilentlyContinue).Count -gt 0
```

---

## Step 8: AOT Release Publish — Browser WASM single-thread fallback

This publishes `Motely.SingleThread` and stages the fallback browser runtime into `Motely.npm/_framework_st/`.

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.SingleThread\Motely.SingleThread.csproj -c Release
node x:\JammySeedFinder\src\MotelyJAML\stage-packages.mjs singlethread
```

Verify the fallback output exists:
// turbo

```powershell
@(Get-ChildItem x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st\dotnet.native*.wasm -ErrorAction SilentlyContinue).Count -gt 0
```

---

## Step 9: AOT Release Publish — Node Addon (win-x64 + linux-x64)

Publish the native AOT addon for both platforms:

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.NodeAddon\Motely.NodeAddon.csproj -c Release -r win-x64
wsl dotnet publish /mnt/x/JammySeedFinder/src/MotelyJAML/Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r linux-x64
```

Copy .node files to Motely.node/bin/ for packaging:

```powershell
New-Item -ItemType Directory -Force -Path x:\JammySeedFinder\src\MotelyJAML\Motely.node\bin\Release\net10.0\win-x64\publish
New-Item -ItemType Directory -Force -Path x:\JammySeedFinder\src\MotelyJAML\Motely.node\bin\Release\net10.0\linux-x64\publish
Copy-Item x:\JammySeedFinder\src\MotelyJAML\Motely.NodeAddon\bin\Release\net10.0\win-x64\publish\Motely.NodeAddon.node x:\JammySeedFinder\src\MotelyJAML\Motely.node\bin\Release\net10.0\win-x64\publish\
Copy-Item x:\JammySeedFinder\src\MotelyJAML\Motely.NodeAddon\bin\Release\net10.0\linux-x64\publish\Motely.NodeAddon.node x:\JammySeedFinder\src\MotelyJAML\Motely.node\bin\Release\net10.0\linux-x64\publish\
```

Verify .node files exist:
// turbo

```powershell
Test-Path x:\JammySeedFinder\src\MotelyJAML\Motely.node\bin\Release\net10.0\win-x64\publish\Motely.NodeAddon.node
Test-Path x:\JammySeedFinder\src\MotelyJAML\Motely.node\bin\Release\net10.0\linux-x64\publish\Motely.NodeAddon.node
```

---

## Step 10: Prepare npm package — motely-wasm (browser)

// turbo

```powershell
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.npm install
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.npm run build
```

---

## Step 11: Pack motely-node

The .node files are already published in Step 9. Just pack:

// turbo

```powershell
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.node pack
```

---

## Step 12: Tell user to publish

Tell the user:

```md
cd x:\JammySeedFinder\src\MotelyJAML\Motely.npm
npm login
npm publish

cd x:\JammySeedFinder\src\MotelyJAML\Motely.node
npm login
npm publish
```

Mention that `motely-wasm` includes `_framework/` and `_framework_st/`, while `motely-node` includes native AOT `.node` files in `bin/`.

Wait for user to confirm they published.

---

## Step 13: Cleanup _framework files

After user confirms publish, clean up the large _framework dirs to keep the repo lean:

```powershell
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st -ErrorAction SilentlyContinue
```

---

## Step 14: Summary

Print:

```sh
✅ MotelyJAML update complete!
   Version: <THE_VERSION>
   Published: motely-wasm@<THE_VERSION>, motely-node@<THE_VERSION>
```
