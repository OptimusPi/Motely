# Prepare MotelyJAML npm packages

**Canonical workflow:** [docs/BUILD_NPM_PACKAGES_WORKFLOW.md](../docs/BUILD_NPM_PACKAGES_WORKFLOW.md). Follow it exactly. Both motely-wasm and motely-node, same version, every time. No optional steps.

Goal: bump version if requested, build both packages (WASM + Node addon), then stop with publish-ready output. Do **not** publish unless the user explicitly asks.

**Rules (do not break):**
- Do **not** add `main` or `exports` to `Motely.NodeAddon/package.json` — the SDK writes those during publish.
- Do **not** create any hand-written loader JS files for motely-node.
- Publish **both** RIDs for motely-node: win-x64 and linux-x64 (Vercel needs linux).

---

## Version (Phase A)

- **Single source:** `Directory.Packages.props` → `<MotelyVersion>`. Do not edit any package.json or jaml.schema.json by hand.
- If user provides a version: set it in Directory.Packages.props only. No separate sync — `dotnet build`/`dotnet publish` runs `sync-version.mjs` automatically (Directory.Build.props).
- If no version provided: read `<MotelyVersion>` from Directory.Packages.props, auto-increment patch (e.g. 3.14.2 → 3.14.3), set it in Directory.Packages.props only.
- Print the version. The next dotnet publish in Phase B or C will sync both Motely.npm and Motely.NodeAddon package.json.

---

## Workflow (Phases B → C → D)

Execute in order. See [BUILD_NPM_PACKAGES_WORKFLOW.md](../docs/BUILD_NPM_PACKAGES_WORKFLOW.md) for full steps.

### Phase B: motely-wasm

- Remove `Motely.npm\_framework` if present.
- `dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release` then `node stage-packages.mjs browser`.
- `cd Motely.npm && npm install && npm run build`.

### Phase C: motely-node

- **win-x64:** `dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64`  
  Confirm: `Motely.NodeAddon\bin\win-x64\Motely.NodeAddon.node` exists.
- **linux-x64:** `.\build-linux.ps1` (Windows) or `./build-linux.sh` (Unix).  
  Confirm: `Motely.NodeAddon\bin\linux-x64\Motely.NodeAddon.node` exists. Do not consider the package ready without the linux binary.
- **Pack:** The SDK runs `npm pack` during publish (`PackNpmPackage`). After both RIDs, the final `.tgz` is in `Motely.NodeAddon\pkg`. Optionally run `cd Motely.NodeAddon && npm pack` to regenerate the tarball for inspection. Confirm the tarball contains both `bin/win-x64/` and `bin/linux-x64/` with `.node` files.

### Phase D

- If user asked to publish: `npm publish` in Motely.npm (motely-wasm) and in Motely.NodeAddon (motely-node). Then in JAMMY: `pnpm add motely-node@<V> motely-wasm@<V>` and `pnpm run build`.
- If user did not ask: stop after pack and print the publish commands and JAMMY update command.

---

## Final output (when not publishing)

```text
Both npm packages are ready. Version: <V>

1. Publish motely-wasm:
   cd <REPO_ROOT>\Motely.npm
   npm publish

2. Publish motely-node:
   cd <REPO_ROOT>\Motely.NodeAddon
   npm publish

3. Update JAMMY:
   pnpm add motely-node@<V> motely-wasm@<V>
   pnpm run build
```

Use the actual repo root path (e.g. `X:\JammySeedFinder\src\MotelyJAML`). Then stop and wait for the user to confirm publish.
