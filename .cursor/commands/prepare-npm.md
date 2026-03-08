# Prepare MotelyJAML npm packages

Prepare `motely-wasm` and `motely-node` for npm publish from `X:\JammySeedFinder\src\MotelyJAML`.

Goal: bump version if requested, clean, restore, build, AOT publish both WASM targets, prepare both npm packages, and stop with publish-ready output. Do **not** publish automatically.

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
Remove-Item -Recurse -Force X:\JammySeedFinder\src\MotelyJAML\Motely.node\_framework -ErrorAction SilentlyContinue
```

### 6. AOT publish browser WASM

This should publish `Motely.BrowserWasm`, then stage `_framework` into `Motely.npm\_framework`.

```powershell
dotnet publish X:\JammySeedFinder\src\MotelyJAML\Motely.BrowserWasm\Motely.BrowserWasm.csproj -c Release
node X:\JammySeedFinder\src\MotelyJAML\stage-packages.mjs browser
Test-Path X:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework\dotnet.native.wasm
```

### 7. AOT publish node WASM

This should publish `Motely.SingleThread`, then stage `_framework` into `Motely.node\_framework`.

```powershell
dotnet publish X:\JammySeedFinder\src\MotelyJAML\Motely.SingleThread\Motely.SingleThread.csproj -c Release
node X:\JammySeedFinder\src\MotelyJAML\stage-packages.mjs singlethread node
Test-Path X:\JammySeedFinder\src\MotelyJAML\Motely.node\_framework\dotnet.native.wasm
```

### 8. Prepare `motely-wasm`

```powershell
cd X:\JammySeedFinder\src\MotelyJAML\Motely.npm
npm install
npm run build
```

### 9. Prepare `motely-node`

```powershell
cd X:\JammySeedFinder\src\MotelyJAML\Motely.node
npm install
npm run build
npm run stage-framework
```

Optional (addon path): copy addon DLL + Motely.dll to `Motely.node\bin\` so `loadMotely({ addonPath: path.join(__dirname, 'bin', 'Motely.NodeAddon.dll') })` works when `node-api-dotnet` is installed.

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

   (WASM: _framework is included. Addon: build Motely.NodeAddon and pass addonPath to loadMotely, or ship bin/ with DLLs.)
```

Then stop and wait for the user to confirm publish.

## Optional cleanup after publish

Only after the user confirms publish:

```powershell
Remove-Item -Recurse -Force X:\JammySeedFinder\src\MotelyJAML\Motely.npm\_framework -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force X:\JammySeedFinder\src\MotelyJAML\Motely.node\_framework -ErrorAction SilentlyContinue
```
