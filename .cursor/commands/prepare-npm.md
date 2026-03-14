# Prepare MotelyJAML npm packages

Prepare `motely-wasm` and `motely-node` for npm publish from `X:\JammySeedFinder\src\MotelyJAML`.

Goal: bump version if requested, clean, restore, build, publish the threaded and single-thread browser WASM runtimes, prepare both npm packages, and stop with publish-ready output. Do **not** publish automatically.

## Inputs

- Optional version number from the user.
- If no version is provided:
  1. Read `X:\JammySeedFinder\src\MotelyJAML\Directory.Build.props`
  2. Read `<MotelyVersion>`
  3. Auto-increment the patch version

## Files to update

Set the same version in:

- `X:\JammySeedFinder\src\MotelyJAML\Directory.Build.props` (MotelyVersion)
- `X:\JammySeedFinder\src\MotelyJAML\Motely.npm\package.json`
- `X:\JammySeedFinder\src\MotelyJAML\Motely.node\package.json`
- `X:\JammySeedFinder\src\MotelyJAML\jaml.schema.json` (root — **single source** for schema)

Then **copy** root schema to the other locations (so there is only one schema file to edit):

```powershell
$root = "X:\JammySeedFinder\src\MotelyJAML\jaml.schema.json"
Copy-Item -Path $root -Destination "X:\JammySeedFinder\src\MotelyJAML\public\jaml.schema.json" -Force
Copy-Item -Path $root -Destination "X:\JammySeedFinder\src\MotelyJAML\Motely.npm\jaml.schema.json" -Force
Copy-Item -Path $root -Destination "X:\JammySeedFinder\src\MotelyJAML\Motely.node\jaml.schema.json" -Force
```

Print the version you set so the user can confirm it.

## Workflow

### 1. Nuke `bin/` and `obj/`

```powershell
Get-ChildItem -Path X:\JammySeedFinder\src\MotelyJAML -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
```

### 2. Clean

```powershell
dotnet clean X:\JammySeedFinder\src\MotelyJAML\Motely.sln -c Release
```

### 3. Restore

```powershell
dotnet restore X:\JammySeedFinder\src\MotelyJAML\Motely.sln
```

### 4. Build solution

```powershell
dotnet build X:\JammySeedFinder\src\MotelyJAML\Motely.sln -c Release
```

If any project fails, report the error and stop.

### 4b. Build Node addon (for node-api-dotnet path)

```powershell
dotnet build X:\JammySeedFinder\src\MotelyJAML\Motely.NodeAddon\Motely.NodeAddon.csproj -c Release
```

Addon output: `Motely.NodeAddon\bin\Release\net10.0\` (Motely.NodeAddon.dll + deps). Optional: copy to `Motely.node\bin\` when shipping addon with the package.

### 5. Clean `_framework` dirs before publish

```powershell
Remove-Item -Recurse -Force X:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force X:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st -ErrorAction SilentlyContinue
```

### 6. AOT publish browser WASM

This publishes `Motely.BrowserWasm`, then stages the threaded runtime into `Motely.npm\_framework`.

```powershell
dotnet publish X:\JammySeedFinder\src\MotelyJAML\Motely.BrowserWasm\Motely.BrowserWasm.csproj -c Release
node X:\JammySeedFinder\src\MotelyJAML\stage-packages.mjs browser
Test-Path X:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework\dotnet.native.wasm
```

### 7. AOT publish browser single-thread fallback WASM

This publishes `Motely.SingleThread`, then stages the single-thread fallback runtime into `Motely.npm\_framework_st`.

```powershell
dotnet publish X:\JammySeedFinder\src\MotelyJAML\Motely.SingleThread\Motely.SingleThread.csproj -c Release
node X:\JammySeedFinder\src\MotelyJAML\stage-packages.mjs singlethread
Test-Path X:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st\dotnet.native*.wasm
```

### 8. Prepare `motely-wasm`

```powershell
cd X:\JammySeedFinder\src\MotelyJAML\Motely.npm
npm install
npm run build
```

### 9. Prepare `motely-node`

```powershell
npm install
npm pack
```

Run this in:

```powershell
cd X:\JammySeedFinder\src\MotelyJAML\Motely.node
```

The Node package ships native `.node` outputs in `bin/`; it does not stage browser `_framework` assets.

## Final output to user

Print:

```text
Both npm packages are ready to publish!

1. Publish motely-wasm:
   cd X:\JammySeedFinder\src\MotelyJAML\Motely.npm
   npm login
   npm publish

2. Publish motely-node:
   cd X:\JammySeedFinder\src\MotelyJAML\Motely.node
   npm login
   npm publish

   (`motely-wasm` includes `_framework/` and `_framework_st/`. `motely-node` ships native `.node` binaries in `bin/`.)
```

Then stop and wait for the user to confirm publish.

## Optional cleanup after publish

Only after the user confirms publish:

```powershell
Remove-Item -Recurse -Force X:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force X:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework_st -ErrorAction SilentlyContinue
```
