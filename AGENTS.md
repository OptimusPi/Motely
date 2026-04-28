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
- Prefer `legendaryJoker`; do not reintroduce `soulJoker` as public syntax.

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
