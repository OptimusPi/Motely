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


# read the f8ucking docs at d:\Bootsharp ya dummies