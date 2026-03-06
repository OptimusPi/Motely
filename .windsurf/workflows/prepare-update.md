---
description: Prepare a MotelyJAML version update - bump version, clean, build all projects, AOT publish WASM, prepare npm packages, and guide user through npm publish
---

# Prepare Update

Bumps the MotelyVersion, cleans everything, builds all .NET projects, AOT-publishes both WASM targets, prepares both npm packages, and walks the user through publishing.

**User provides**: the new version number — OR if not specified, read the current `<MotelyVersion>` from `Directory.Build.props` and auto-increment the patch (e.g. `2.2.1` → `2.2.2`).

---

## Step 1: Bump MotelyVersion

Edit `x:\JammySeedFinder\src\MotelyJAML\Directory.Build.props` — set `<MotelyVersion>` to the new version.

Then update **both** npm package.json files to match:
- `x:\JammySeedFinder\src\MotelyJAML\Motely.npm\package.json` → set `"version"` to the new version
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

---

## Step 6: Clean _framework dirs before AOT publish

// turbo
```powershell
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\MotelyNode\_framework -ErrorAction SilentlyContinue
```

---

## Step 7: AOT Release Publish — Browser WASM (frontend)

This publishes `Motely.BrowserWasm` and auto-copies `_framework` into `Motely.npm/_framework/` via MSBuild target.

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.BrowserWasm\Motely.BrowserWasm.csproj -c Release
```

Verify the output exists:
// turbo
```powershell
Test-Path x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework\dotnet.native.wasm
```

---

## Step 8: AOT Release Publish — Node WASM (backend)

This publishes `Motely.SingleThread` and auto-copies `_framework` into `MotelyNode/_framework/` via MSBuild target.

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.SingleThread\Motely.SingleThread.csproj -c Release
```

Verify the output exists:
// turbo
```powershell
Test-Path x:\JammySeedFinder\src\MotelyJAML\MotelyNode\_framework\dotnet.native.wasm
```

---

## Step 9: Prepare npm package — motely-wasm (browser)

// turbo
```powershell
cd x:\JammySeedFinder\src\MotelyJAML\Motely.npm; npm install; npm run build
```

---

## Step 10: Prepare npm package — motely-node (backend)

// turbo
```powershell
cd x:\JammySeedFinder\src\MotelyJAML\Motely.node; npm install; npm run build; npm run copy-framework
```

---

## Step 11: Tell user to publish

Tell the user:

```
Both npm packages are ready to publish!

1. Publish motely-wasm (browser frontend):
   cd x:\JammySeedFinder\src\MotelyJAML\Motely.npm
   npm login       # if not already logged in
   npm publish

2. Publish motely-node (backend/Node.js):
   cd x:\JammySeedFinder\src\MotelyJAML\Motely.node
   npm login       # if not already logged in
   npm publish
```

Wait for user to confirm they published.

---

## Step 12: Cleanup _framework files

After user confirms publish, clean up the large _framework dirs to keep the repo lean:

```powershell
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force x:\JammySeedFinder\src\MotelyJAML\MotelyNode\_framework -ErrorAction SilentlyContinue
```

---

## Step 13: Summary

Print:

```
✅ MotelyJAML update complete!
   Version: <THE_VERSION>
   Published: motely-wasm@<THE_VERSION>, motely-node@<THE_VERSION>
```
