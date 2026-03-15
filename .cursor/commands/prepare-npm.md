# Prepare MotelyJAML npm packages

**Canonical workflow:** [docs/BUILD_NPM_PACKAGES_WORKFLOW.md](../docs/BUILD_NPM_PACKAGES_WORKFLOW.md). Follow it exactly. Both motely-wasm and motely-node, same version, every time. No optional steps.

Goal: bump version if requested, build both packages (WASM + Node addon), pack both, and stop with publish-ready output. Do **not** publish unless the user explicitly asks.

## Version (Phase A)

- **Single source:** `Directory.Packages.props` → `<MotelyVersion>`. Do not edit any package.json or jaml.schema.json by hand.
- If user provides a version: set it in Directory.Packages.props only. No separate sync — `dotnet build`/`dotnet publish` runs `sync-version.mjs` automatically (Directory.Build.props).
- If no version provided: read `<MotelyVersion>` from Directory.Packages.props, optionally auto-increment patch, set it there.
- Print the version. The next dotnet publish in Phase B or C will sync both Motely.npm and motely-node package.json in one go.

## Workflow (Phases B → C → D)

Execute in order. See [BUILD_NPM_PACKAGES_WORKFLOW.md](../docs/BUILD_NPM_PACKAGES_WORKFLOW.md) for full steps.

### Phase B: motely-wasm

- Remove `Motely.npm\_framework` and `Motely.npm\_framework_st` if present.
- `dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release` then `node stage-packages.mjs browser`.
- `dotnet publish Motely.SingleThread/Motely.SingleThread.csproj -c Release` then `node stage-packages.mjs singlethread`.
- `cd Motely.npm && npm install && npm run build`.

### Phase C: motely-node

- `dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64` → confirm `motely-node\bin\win-x64\Motely.NodeAddon.node`.
- `.\build-linux.ps1` (Windows) or `./build-linux.sh` (Unix) → confirm `motely-node\bin\linux-x64\Motely.NodeAddon.node`. Do not publish without linux binary.
- `cd motely-node && npm pack` (no `build` script; pack uses bin/ as-is). Confirm tarball has both bin/win-x64/ and bin/linux-x64/.

### Phase D

- If user asked to publish: `npm publish` in Motely.npm and in motely-node. Then in JAMMY: `pnpm add motely-node@<V> motely-wasm@<V>` and `pnpm run build`.
- If user did not ask: stop after pack and print the publish commands and JAMMY update command.

## Final output (when not publishing)

```text
Both npm packages are ready. Version: <V>

1. Publish motely-wasm:
   cd X:\JammySeedFinder\src\MotelyJAML\Motely.npm
   npm publish

2. Publish motely-node:
   cd X:\JammySeedFinder\src\MotelyJAML\motely-node
   npm publish

3. Update JAMMY:
   pnpm add motely-node@<V> motely-wasm@<V>
   pnpm run build
```

Then stop and wait for the user to confirm publish.
