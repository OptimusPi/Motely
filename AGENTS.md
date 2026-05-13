@BOOTSHARP
# MotelyJAML

Motely is the Balatro seed-search engine. JAML is its YAML-based filter language. This repo is the engine, CLI, WASM package, tests, and JAML language tooling.

## Project map

| Project | Purpose | Target |
|---|---|---|
| `Motely` | Core engine, JAML parser, analysis, portable runtime | `net10.0` |
| `Motely.CLI` | Command-line searcher | `net10.0` |
| `Motely.Tests` | xUnit + golden/corpus regression | `net10.0` |
| `Motely.Wasm` | Browser build via Bootsharp | `net10.0` + `browser-wasm` |
| `motely-wasm` | Published npm package output | npm |
| `packages/jaml-language-core` | JAML schema helpers | Node |
| `packages/jaml-language-support` | VS Code language support | Node |
| `Motely.Run` | Minimal host showing the core search shape | `net10.0` |

## Build & publish

```powershell
# build + test
dotnet build Motely.slnx -c Release
dotnet test Motely.Tests

# publish the npm package
dotnet publish Motely.Wasm -c Release
cd motely-wasm
npm publish --access public
```

Version lives in `<MotelyVersion>` in `Directory.Packages.props`. The `prepublishOnly` hook (`sync-version.mjs`) syncs it into `package.json`. Confirm the published version with `npm view motely-wasm version` — the npm CLI notice can lie.

CDN delivery (unpkg/jsdelivr) is automatic after `npm publish`. No manual upload step.

## Hard rules

- **No private paths in public files.** No `D:\…`, `X:\…`, local NuGet feeds, or personal drive layouts in `.csproj` / `.props` / `.config` / package metadata.
- **Warnings are errors.** Fix the cause.
- **Browser-only stays browser-only.** `Bootsharp.FileSystem` lives in `Motely.Wasm`. Do not leak it into core `Motely`, CLI, or other targets. Do not force native/server packages into `browser-wasm`.
- **JAML is JAML.** Not YAML. User-facing surfaces and docs say JAML.
- **No facade wrappers.** Export the real Motely public surface from `Motely.Wasm`. Fix the contract in core, do not paper over it in JS.
- **PRNG files are fragile.** `MotelySingleSearchContext.*.cs` and `MotelyVectorSearchContext.*.cs` carry stream generation. Touch only when the task explicitly requires it; never via IDE find-replace.
- **Generated artifacts come from the generator.** Do not hand-edit `jaml.schema.json` or other generated outputs.

## Read before editing integrations

- `BOOTSHARP.md` — Bootsharp reference (compiled from `D:\bootsharp\docs\` + `D:\extra\bootsharp\AGENTS.md`). Read this instead of the raw docs directories.
- DuckDB: native packages do not compile to `browser-wasm`. Use DuckDB's own WASM/JS path separately when the browser needs it.
