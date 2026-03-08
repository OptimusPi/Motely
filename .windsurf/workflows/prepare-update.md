---
description: Prepare a MotelyJAML version update - bump version, clean, build all projects, AOT-publish WASM, prepare npm packages, and guide user through npm publish
auto_execution_mode: 3
---

# Prepare Update

Bumps the MotelyVersion, cleans everything, builds all .NET projects, AOT-publishes both WASM targets, prepares all three npm packages, and walks the user through publishing.

**User provides**: the new version number — OR if not specified, read the current `<MotelyVersion>` from `Directory.Packages.props` and auto-increment the patch (e.g. `2.2.1` → `2.2.2`).

---

## Step 1: Bump MotelyVersion

Edit `x:\JammySeedFinder\src\MotelyJAML\Directory.Packages.props` — set `<MotelyVersion>` to the new version.

Then update **all three** npm package.json files to match:

- `x:\JammySeedFinder\src\MotelyJAML\Motely.npm\package.json` → set `"version"` to the new version
- `x:\JammySeedFinder\src\MotelyJAML\Motely.npm.singlethread\package.json` → set `"version"` to the new version
- `x:\JammySeedFinder\src\MotelyJAML\Motely.node\package.json` → set `"version"` to the new version

Print the version you just set so the user can confirm.

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

## Step 6: Clean _framework dirs before AOT publish

// turbo

```powershell
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm.singlethread\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.node\_framework -ErrorAction SilentlyContinue
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

## Step 8: AOT Release Publish — Single-thread WASM (Node + browser fallback)

This publishes `Motely.SingleThread` using the same browser publish AOT path and then stages `_framework` into:

- `Motely.node/_framework/`
- `Motely.npm/_framework_st/`
- `Motely.npm.singlethread/_framework/`

The packaged `_framework` output keeps raw `.wasm` assets only and excludes `.br` / `.gz` sidecars.

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.SingleThread\Motely.SingleThread.csproj -c Release
node x:\JammySeedFinder\src\MotelyJAML\stage-packages.mjs singlethread node
```

If this fails, capture the full SDK error and stop before npm packaging.

Verify the outputs exist:
// turbo

```powershell
@(Get-ChildItem x:\JammySeedFinder\src\MotelyJAML\Motely.node\_framework\dotnet.native*.wasm -ErrorAction SilentlyContinue).Count -gt 0
@(Get-ChildItem x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st\dotnet.native*.wasm -ErrorAction SilentlyContinue).Count -gt 0
@(Get-ChildItem x:\JammySeedFinder\src\MotelyJAML\Motely.npm.singlethread\_framework\dotnet.native*.wasm -ErrorAction SilentlyContinue).Count -gt 0
```

---

## Step 9: Prepare npm package — motely-wasm (browser)

// turbo

```powershell
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.npm install
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.npm run build
```

---

## Step 10: Prepare npm package — motely-wasm-singlethread (browser)

// turbo

```powershell
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.npm.singlethread install
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.npm.singlethread run build
```

---

## Step 11: Prepare npm package — motely-node (backend)

// turbo

```powershell
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.node install
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.node run build
npm --prefix x:\JammySeedFinder\src\MotelyJAML\Motely.node run stage-framework
```

---

## Step 12: Tell user to publish

Tell the user:

```md
All npm packages are ready to publish!

1. Log into npm once:
   npm login

2. Publish motely-wasm (browser frontend):
   cd x:\JammySeedFinder\src\MotelyJAML\Motely.npm
   npm publish

3. Publish motely-wasm-singlethread (browser fallback):
   cd x:\JammySeedFinder\src\MotelyJAML\Motely.npm.singlethread
   npm publish

4. Publish motely-node (backend/Node.js):
   cd x:\JammySeedFinder\src\MotelyJAML\Motely.node
   npm publish
```

Wait for user to confirm they published.

---

## Step 13: Cleanup _framework files

After user confirms publish, clean up the large _framework dirs to keep the repo lean:

```powershell
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm.singlethread\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.node\_framework -ErrorAction SilentlyContinue
```

---

## Step 14: Summary

Print:

```sh
✅ MotelyJAML update complete!
   Version: <THE_VERSION>
   Published: motely-wasm@<THE_VERSION>, motely-wasm-singlethread@<THE_VERSION>, motely-node@<THE_VERSION>
```
