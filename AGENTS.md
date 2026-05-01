# MotelyJAML Agent Instructions

## What this repository is

Motely is the Balatro seed search engine and JAML is its public filter language. This repo contains the core engine, CLI, WASM package, tests, generated JAML schema artifacts, and language tooling.

## Non-negotiable rules

- **Read docs before editing integrations.** Do not pattern-match Bootsharp, DuckDB, MCP Apps, VS Code extension, npm packaging, or .NET NativeAOT behavior.
- **No private machine paths in public files.** Do not commit absolute local paths, local NuGet feeds, or personal drive layouts in `.csproj`, `.props`, `.config`, package metadata, or public docs.
- **Warnings are errors.** Do not hide warnings. Fix the cause.
- **Motely is the source of truth.** Do not add fake APIs or wrapper facades in consumers to paper over missing Motely functionality.
- **No WASM glue layers.** Export the real Motely public surface. Avoid duplicate business logic in JavaScript or TypeScript consumers.
- **JAML is JAML, not YAML.** It is YAML-based, but user-facing surfaces and docs should call it JAML.
- **One careful change at a time.** Avoid broad multi-file edits unless the task truly requires them.

## Project map

| Project | Purpose | Target |
|---|---|---|
| `Motely` | Core engine, JAML parser, analysis, runtime WASM host implementation | `net10.0` + browser-compatible target |
| `Motely.CLI` | Command-line searcher and tooling commands | `net10.0` |
| `Motely.Tests` | xUnit tests and schema/golden checks | `net10.0` |
| `Motely.Wasm` | Browser/JS WASM build via Bootsharp | `net10.0` + `browser-wasm` |
| `motely-wasm` | Published npm package output | JavaScript package |
| `tools/jaml-language` | JAML schema and VS Code language tooling | Node/VS Code |

## Bootsharp rules

Before touching `Motely.Wasm`, read the relevant Bootsharp docs and samples from the Bootsharp checkout or official docs:

- `docs/guide/build-config.md`
- `docs/guide/interop-interfaces.md`
- `docs/guide/extensions/dependency-injection.md`
- React/backend WASM sample project and `Program.cs`

Key facts:

- `Motely.Wasm` must consume `Bootsharp` as a NuGet package, not as raw project references.
- The Bootsharp package supplies required MSBuild assets in `build/Bootsharp.props` and `build/Bootsharp.targets`.
- Those build assets set WASM project shape such as `OutputType=Exe`, browser target settings, code generation, LLVM wiring, and packaging.
- Do **not** manually add `OutputType=Exe` to `Motely.Wasm` as a workaround.
- Do **not** commit local Bootsharp project references or local feed paths in public Motely files.
- If testing unpublished Bootsharp changes locally, use a user-local NuGet source outside committed files.

`Motely.Wasm` should use package references for Bootsharp dependencies:

```xml
<PackageReference Include="Bootsharp" />
<PackageReference Include="Bootsharp.Inject" />
<PackageReference Include="Bootsharp.FileSystem" />
```

Central package versions belong in `Directory.Packages.props`.

## Bootsharp local package testing

If a developer needs to test a local Bootsharp checkout:

1. Build Bootsharp packages using Bootsharp's own documented packaging flow.
2. Add the produced local package folder as a user-local NuGet source using `dotnet nuget add source` or a local uncommitted NuGet config.
3. Restore Motely from package references.
4. Never commit that local feed path.

This preserves Bootsharp package build assets and avoids raw project-reference failures.

## JAML schema rules

- Public schema generation is tooling-only.
- `Motely.Wasm/Jaml.cs` contains the typed public schema contract.
- `Motely.Wasm/MotelyJAML.schema.generator.cs` generates schema from that public contract.
- `Motely.CLI` and `Motely.Tests` compile the tooling files.
- Browser-WASM `Motely.Wasm` excludes those tooling files from runtime compilation.
- Runtime `Motely` returns the bundled schema artifact; it must not generate schema at runtime.

Public schema contract goals:

- `must`, `should`, and `mustNot` are arrays of the same reusable `JamlCriterion` shape.
- `score` and `label` are valid everywhere for editor UX.
- Roll criteria like `luckyMoney`, `luckyMult`, and `wheelOfFortune` are explicit keys.
- Public `event` is reserved for an advanced string/pseudohash-style criterion.
- Public schema must not expose runtime-only/internal fields such as `aesthetics` or `earlyAntesMaxPack`.
- Prefer `legendaryJoker`; do not reintroduce `legendaryJoker` as public syntax.

Regenerate schema with:

```powershell
dotnet run --project Motely.CLI -- --write-jaml-schema
```

Generated schema artifacts are copied to the repo root, npm package, JAML schema package, and VS Code extension schema folder.

## Build and verification

Use targeted checks before broad checks:

```powershell
dotnet restore .\Motely.Wasm\Motely.Wasm.csproj
dotnet build .\Motely.CLI\Motely.CLI.csproj --no-restore -v:minimal
dotnet test .\Motely.Tests\Motely.Tests.csproj --filter "JamlFilterTypeTests|JamlConfigTests|JamlStructuralGapTests|JamlSchemaSnapshotTests"
```

When validating WASM packaging, follow Bootsharp docs and avoid local path hacks.

## Public packaging hygiene

- `motely-wasm/package.json` is hand-authored and version-controlled.
- Bootsharp only writes its default package metadata if `package.json` is absent; the hand-authored file should be preserved.
- Do not add generated local package output to the repo unless the package workflow explicitly requires it.
- Do not publish broken experimental versions. Verify schema artifacts, tests, package contents, and downstream type expectations first.

## Release process — all projects

Single source of version truth: `<MotelyVersion>` in `Directory.Packages.props` (this repo). Every consumer bump flows from that number.

**Do not use `X:\JammySeedFinder\build.ps1` for motely-wasm.** That parent-repo god-script crosses the repo boundary and conflates motely-wasm release with desktop/browser/android builds that have unrelated cadence. The motely-wasm release lives entirely inside MotelyJAML.

### 1. motely-wasm — npm registry

Bootsharp's `dotnet publish` already produces an npm-ready folder at `Motely.Wasm/bin/motely-wasm/{index.mjs, package.json, types/}`. Pack and publish from there.

```powershell
# from X:\JammySeedFinder\src\MotelyJAML
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release
cd Motely.Wasm/bin/motely-wasm
npm pkg set version=<MotelyVersion>
npm pack
npm publish motely-wasm-<MotelyVersion>.tgz --access public
```

Verify against the as-published artifact (not local build):

```powershell
# from X:\JammySeedFinder\src\MotelyJAML\Motely.Wasm\e2e
Remove-Item node_modules,package-lock.json -Recurse -Force -ErrorAction SilentlyContinue
npm install motely-wasm@<MotelyVersion>   # may need 30–60s for npm CDN propagation; retry if needed
node release-smoke.mjs
```

### 2. motely-wasm — Vercel Blob CDN (`cdn.seedfinder.app`)

Consumers (ErraticDeck.app, MCP server, jaml-ui-driven apps) pull from the CDN, not from npm directly. Forgetting this step leaves them stale.

```powershell
# from D:\mmm
pnpm install motely-wasm@<MotelyVersion>
pnpm cdn:upload-motely-wasm
```

Result: `https://cdn.seedfinder.app/motely-wasm/<MotelyVersion>/index.mjs`. Token `BLOB_READ_WRITE_TOKEN` lives in `D:\mmm\.env.local`.

### 3. Downstream consumer bumps (after motely-wasm publishes)

Each consumer pins motely-wasm and must be bumped + redeployed independently:

| Consumer | Repo | Bump command | Deploy |
|---|---|---|---|
| seedfinder-app / MCP | `D:\mmm` | `pnpm install motely-wasm@<v>` + commit + push | Vercel auto-deploys; `mcp.seedfinder.app` updates with it |
| jaml-ui | `X:\jaml-ui` | bump `peerDependencies.motely-wasm`, publish via `pnpm publish:jaml-ui` from `D:\mmm` | npm registry |
| ErraticDeck.app | `D:\ErraticDeck.app` | `pnpm install motely-wasm@<v>` + `pnpm install jaml-ui@<v>` | Vercel |
| weejoker.app | `X:\weejoker.app` | `pnpm install motely-wasm@<v>` (+ jaml-ui if used) | Vercel; migrating to `daily.erraticdeck.app` |

### 4. JAML schema artifacts

Schema flows lockstep with motely-wasm. Regenerated by `Motely.CLI -- --write-jaml-schema` and copied to repo root, npm package, `tools/jaml-language` schema folder, and VS Code extension. Already covered in **JAML schema rules** above. Re-run schema write before pack-npm if any DTO changed.

### 5. Bootsharp + Bootsharp.FileSystem (upstream / paid — DO NOT MODIFY)

**Hard rule for agents:** never edit, build, pack, regenerate dist, or run any build script under `D:\bootsharp` or `D:\extra`. Not the source, not the JS dist, not the nupkg, not the build scripts. Only pifreak runs anything in those repos. If a Motely.Wasm build is blocked because the staged Bootsharp `.nupkg` is stale, the agent **reports the blocked state and waits** — it does not "fix" upstream.

#### Who does what

| Action | Who |
|---|---|
| Edit Bootsharp / Bootsharp.FileSystem source | **pifreak only** (or upstream PR) |
| Run `npm run build` in `D:\bootsharp\src\js` | **pifreak only by default; agent OK when pifreak explicitly authorizes for the session** |
| Run `dotnet pack` in `D:\bootsharp\src\cs` or `D:\extra\bootsharp\cs` | **pifreak only by default; agent OK when pifreak explicitly authorizes for the session** |
| Copy produced `.nupkg` files into the user-level local feed at `%LOCALAPPDATA%\Temp\bootsharp-local` (= `C:\Users\pifre\AppData\Local\Temp\bootsharp-local`) | agent OK |
| Clear `C:\Users\pifre\.nuget\packages\bootsharp*` global cache | agent OK in spirit, but Claude Code auto-mode now treats recursive force-delete of global cache dirs as destructive and may require explicit per-session authorization |
| Edit `MotelyJAML/nuget.config`, `Directory.Packages.props`, Motely.Wasm `Program.cs` to match new attribute names | agent OK with explicit pifreak ask |

#### Build flow (pifreak runs these)

Bootsharp (`D:\bootsharp`):
```bash
# from src/cs:
bash ./.scripts/llvm.sh              # one-time per machine — fetches NativeAOT-LLVM artifacts into .llvm/
cd ../js && sh scripts/build.sh      # rebuilds dist if src/js/src changed (instances.ts/imports.ts/etc.)
cd ../cs && bash ./.scripts/pack.sh  # produces Bootsharp{,.Common,.Inject}.<version>.nupkg in .nuget/
```
The `pack.sh` script gates on `.llvm/microsoft.dotnet.ilcompiler.llvm/build/Microsoft.DotNet.ILCompiler.LLVM.targets` existing — if you wiped `.llvm/` (or it's a clean checkout), run `llvm.sh` first.

On Windows: Git Bash (`C:\Program Files\Git\bin\bash.exe`) runs `llvm.sh` and `pack.sh` cleanly because Git ships its own `unzip`. WSL bash also exists at `C:\WINDOWS\system32\bash.exe` but pulls Windows paths into the Linux filesystem view — avoid it for these scripts. If neither bash is acceptable, the manual equivalent is `dotnet build Bootsharp.Generate -c Release` then `dotnet pack Bootsharp.Common -o .nuget -c Release` (and same for `Bootsharp.Inject`, `Bootsharp`) — but you'll still need the `.llvm/` artifacts ported to PowerShell separately.

Bootsharp.FileSystem (`D:\extra`):
```pwsh
cd D:\extra\bootsharp\cs
dotnet pack Bootsharp.FileSystem -c Release -o .nuget
```
The `.csproj` time-stamps the version (`yyyy.MM.dd.HHmm`).

#### When agents need a re-pack

Triggers that mean the cached Bootsharp package is stale relative to a needed version and the consumer build will fail:

- Bootsharp version bump in `Directory.Packages.props`
- Bootsharp source/JS dist changed (rename in `instances.ts`, `imports.ts`, attribute renames, etc.)
- `Motely.Wasm` publish fails at the rollup step (`X is not exported by instances.js`)
- `Motely.Wasm` publish fails at AOT analysis with new `IL3050`/`IL2104`/`IL3053` errors after a Bootsharp bump

When any of those happens by default: agent reports "Bootsharp local nupkg looks stale because [reason]; pifreak please re-pack per the flow above." Agent does NOT run the upstream build to fix it unless pifreak explicitly authorizes for the session.

#### Staging produced `.nupkg` files

The active local feed is the user-level path **`%LOCALAPPDATA%\Temp\bootsharp-local`** (= `C:\Users\pifre\AppData\Local\Temp\bootsharp-local`), registered as `bootsharp-local` in pifreak's user-level `NuGet.Config` (`%APPDATA%\NuGet\NuGet.Config`). The historical `MotelyJAML\.nuget-local\` path referenced in earlier docs is no longer the active staging dir — `.nupkg.metadata` files in cache may still record it from old extractions, but new builds go through the user-level feed.

After pifreak re-packs (or after agent runs the build with explicit per-session authorization), agent may:
1. `Copy-Item D:\bootsharp\src\cs\.nuget\Bootsharp*.<version>.nupkg "$env:LOCALAPPDATA\Temp\bootsharp-local\"`
2. `Copy-Item D:\extra\bootsharp\cs\.nuget\Bootsharp.FileSystem.<version>.nupkg "$env:LOCALAPPDATA\Temp\bootsharp-local\"`
3. Bump versions in `Directory.Packages.props` (Bootsharp / Bootsharp.Inject / Bootsharp.FileSystem)

#### Making the local feed actually visible to a restore

The repo-committed `MotelyJAML/nuget.config` ships with `<clear />` then `nuget.org` only — this is **deliberate** (per the "no private machine paths in public files" rule) and means the user-level `bootsharp-local` source is wiped inside the project. **Three knock-on consequences**:

1. **Cache hits keep working silently.** Already-extracted bootsharp packages live in `C:\Users\pifre\.nuget\packages\bootsharp*\<version>\` and are reused for `dotnet restore` of any version that's already there, even when the local feed is wiped.
2. **A version that isn't yet cached needs the local feed re-enabled temporarily.** The dance:
   - Edit `MotelyJAML/nuget.config` *uncommitted*: add `<add key="bootsharp-local" value="<absolute path>" />` after the nuget.org line, plus a `<packageSourceMapping>` block routing `Bootsharp*` to `bootsharp-local` and `*` to `nuget.org` (see template below).
   - `dotnet restore Motely.sln` — now extracts the fresh `.nupkg`s into the global cache.
   - **Revert `nuget.config`** before commit. The committed form must stay clean.
3. **`packageSourceMapping` is now mandatory in NuGet strict mode.** Without it, even a registered local source is silently filtered out and you get `NU1102 / NU1101` with the misleading `Versions from <source> were not considered` line. Adding the source alone is not enough — you must add the mapping too. Template (uncommitted, in project nuget.config during the temp dance):

   ```xml
   <packageSourceMapping>
     <clear />
     <packageSource key="bootsharp-local">
       <package pattern="Bootsharp*" />
     </packageSource>
     <packageSource key="nuget.org">
       <package pattern="*" />
     </packageSource>
   </packageSourceMapping>
   ```

#### Cache eviction (only if absolutely necessary)

Cache eviction is only required when a package version label collides with a previously-extracted hash (rare — happens when re-packing the *same* version label after source changes). When needed:

- `dotnet build-server shutdown` first (release any MSBuild task-host locks on Bootsharp.Generate.dll, etc.)
- Then `Remove-Item C:\Users\pifre\.nuget\packages\bootsharp,bootsharp.common,bootsharp.inject,bootsharp.filesystem -Recurse -Force`

Note: Claude Code's auto-mode harness now treats recursive force-delete of global cache directories as destructive and may require explicit per-session authorization. If blocked, ask pifreak; do not work around the denial.

#### Consumer-side conformance

When Bootsharp renames public API (`JSImport` → `Import`, `JSExport` → `Export`, `JSPreferences` → `Preferences` in alpha.111+), the consumer (`Motely.Wasm/Program.cs`) updates to match. The consumer conforms to upstream — never the other way around.

### 6. JammySeedFinder app surfaces (parent repo)

These ship on independent cadence from motely-wasm and are orchestrated by `X:\JammySeedFinder\build.ps1`:

| Surface | Action | Output |
|---|---|---|
| Desktop | `build.ps1 -Action publish-desktop -Runtime win-x64\|linux-x64\|osx-x64` | `publish/<rid>/` self-contained |
| Browser (Avalonia WASM) | `build.ps1 -Action publish-browser` | `Motely.HelperAPI/wwwroot/jammy-seed-finder/` |
| Android | `build.ps1 -Action publish-android` | `publish/android/` |
| iOS | `build.ps1 -Action publish-ios` (macOS + Xcode) | `publish/ios/` |

There is no build script for motely-wasm. BootSharp's `dotnet publish` produces the npm-ready package. Then `npm publish` from the output directory and `D:\mmm pnpm cdn:upload-motely-wasm` for the CDN upload.

### Release checklist (motely-wasm only)

1. Bump `<MotelyVersion>` in `Directory.Packages.props`
2. Build + tests pass (`dotnet build Motely.sln -c Release`, `dotnet test Motely.Tests`)
3. Schema regenerated (`dotnet run --project Motely.CLI -- --write-jaml-schema`)
4. `dotnet publish Motely.Wasm` → produces `bin/motely-wasm/`
5. `npm pkg set version=<v>` + `npm pack` + `npm publish ... --access public`
6. `release-smoke.mjs` against `motely-wasm@<v>` from npm
7. `pnpm cdn:upload-motely-wasm` from `D:\mmm`
8. Bump downstream consumers (Section 3) one by one
