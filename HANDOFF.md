# Handoff: Publish motely-wasm 9.9.9

## What's done
- motely-wasm-compat is gone. One package: `motely-wasm`, always embedded binaries.
- MotelyItem.cs: field→property fix for Bootsharp serialization
- MotelyWasmHost.cs: search methods return void, JS uses SearchEvents
- MotelyJamlSearchBuilder.cs: tracks seeds/matches from progress callback, not polling search object properties
- tools.ts: stripped from 800→250 lines, 2 tools (search_seeds, analyze_seed), UX descriptions explaining JAML and Jummy
- MCP App UI built (`tools/balatro-seed-finder/mcp-ui/dist/`)
- Version 9.9.9 in Directory.Packages.props
- OSX platform condition added (uses linux-x64, can't actually compile on Mac ARM though)
- BootsharpPublishDirectory = repo-root/motely-wasm/ (build output, gets wiped)
- BootsharpPackageDirectory = Motely/ (where package.json lives)

## What's broken / needs fixing

### 1. package.json situation
The overlay `Motely/package.json` has hardcoded `../motely-wasm/` paths that are probably wrong now that BootsharpPublishDirectory changed to repo root.

**Best approach:** Delete `Motely/package.json`. Let Bootsharp generate it fresh. It computes relative paths from BootsharpPackageDirectory to BootsharpPublishDirectory automatically. Then edit the generated one ONCE to add: version, description, keywords, repository, license, browser, files.

### 2. Version in package.json
Bootsharp does NOT stamp versions. The generated package.json has no version field. You must set it manually. After Bootsharp generates it, add `"version": "9.9.9"`.

### 3. README.md
Currently in `Motely/README.md`. That's where package.json is, so npm will find it. If you move package.json, move README too.

## Steps to publish on Windows

```powershell
git pull

# Delete stale build output + overlay so Bootsharp generates fresh
Remove-Item -Recurse -Force motely-wasm -ErrorAction SilentlyContinue
Remove-Item Motely\package.json -ErrorAction SilentlyContinue

# Build
dotnet publish -c Release .\Motely.BrowserWasm\

# Bootsharp generated Motely/package.json with correct relative paths
# Now add your metadata:
# Open Motely\package.json and add: version, description, keywords, etc.
# Or use npm version:
cd Motely
npm version 9.9.9 --no-git-tag-version

# Publish
npm publish
```

## After publish, verify
```powershell
npm info motely-wasm version
# Should say 9.9.9
```

Then test the MCP:
- Restart Claude with the MCP configured
- Ask it to search for "Negative Perkeo in ante 1"
- Check that analyze_seed returns real data, not `{"_id": -2147483647}`

## Known issues
- Bootsharp 0.7.0 bug: can't export interfaces with properties (IMotelySearch). That's why IMotelySearch and IMotelyJamlSearchBuilder were removed from JSExport. MotelyWasmHost is the sole JS entry point.
- Mac ARM can't compile WASM (no osx-arm64 ILCompiler host binary exists). Windows/Linux only.
- The MCP App React UI calls back through the MCP server to search — ideally it should call WASM directly in the browser for offline search. Future work.
- Tool descriptions still need the full joker/voucher/boss catalog so AI clients know what values are valid (e.g. `legendaryJoker: Perkeo` not `joker: Perkeo`).

## Files that matter
- `Directory.Packages.props` — MotelyVersion (9.9.9)
- `Motely.BrowserWasm/Motely.BrowserWasm.csproj` — BootsharpPublishDirectory, BootsharpPackageDirectory
- `Motely/package.json` — npm overlay (or let Bootsharp generate)
- `Motely/README.md` — npm README
- `Motely.BrowserWasm/MotelyWasmHost.cs` — the sole JSExport entry point
- `Motely.BrowserWasm/BootsharpInterop.cs` — JSExport list
- `tools/balatro-seed-finder/api/tools.ts` — MCP tool definitions
- `tools/balatro-seed-finder/public/.well-known/mcp/server-card.json` — MCP metadata

## Bootsharp rules (read these every time)
1. BootsharpPackageDirectory ≠ BootsharpPublishDirectory. Two different folders.
2. Bootsharp generates package.json at BootsharpPackageDirectory IF one doesn't exist.
3. Generated package.json has correct relative paths computed automatically.
4. Bootsharp does NOT handle versioning. Set it manually.
5. Don't create MSBuild targets to hack around Bootsharp. Ever.
6. Don't export interfaces with properties. Bootsharp generates broken .get_X() calls.
7. The linux-x64 ILCompiler package is used for ALL non-Windows platforms.
