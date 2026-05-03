# MotelyJAML Agent Instructions

## What this repository is

Motely is the Balatro seed search engine and JAML is its public filter language. This repo contains the core engine, CLI, WASM package, tests, generated JAML schema artifacts, and language tooling.

## Read this before touching code

- **Motely core is the crown jewel.** The `MotelySearchSettings<TFilter>` flow is the real engine shape. Preserve its simplicity and do not bury it under facade soup.
- **Jamlyzer is good.** `MotelyJamlyzer` is the JAML-powered analyzer product name and should stay. Remove fake `Legacy` naming from analyzer models instead of attacking the Jamlyzer moniker.
- **Bootsharp integrations are docs-first or no-op.** Before editing `Motely.Wasm`, npm package shape, generated bindings assumptions, or filesystem integration, read `D:\BOOTSHARP` docs/samples/source first.
- **Do not turn AGENTS.md into a scratch pad.** Keep this file factual, prescriptive, and repo-specific. Do not dump random ideas, frustrations, or temporary notes here.

## Non-negotiable rules

- **Read docs before editing integrations.** Do not pattern-match Bootsharp, DuckDB, MCP Apps, VS Code extension, npm packaging, or .NET NativeAOT behavior.
- **No private machine paths in public files.** Do not commit absolute local paths, local NuGet feeds, or personal drive layouts in `.csproj`, `.props`, `.config`, package metadata, or public docs.
- **Warnings are errors.** Do not hide warnings. Fix the cause.
- **Motely is the source of truth.** Do not add fake APIs or wrapper facades in consumers to paper over missing Motely functionality.
- **No WASM glue layers.** Export the real Motely public surface. Avoid duplicate business logic in JavaScript or TypeScript consumers.
- **JAML is JAML, not YAML.** It is YAML-based, but user-facing surfaces and docs should call it JAML.
- **One careful change at a time.** Avoid broad multi-file edits unless the task truly requires them.
- **Do not touch fragile stream internals unless the task explicitly requires it.** PRNG/stream generation in `MotelySingleSearchContext.*.cs` is sensitive; payload and API cleanup should happen around it, not inside it.
- **Do not force native/server libraries into `browser-wasm`.** If a package does not have a real browser-wasm story, keep it out of the shared/browser path. Use the platform's native solution instead of pretending names imply compatibility.
- **Browser-only integrations must stay browser-only.** `Bootsharp.FileSystem` is valid in `Motely.Wasm`; it must not leak into core `Motely`, CLI, or other non-browser targets.
- **Do not add tiny interop methods just because Bootsharp makes exports easy.** Cross the boundary for authoritative Motely/JAML operations, not trivia or convenience probes.

## Runtime boundary rules

- **Core `Motely`** should stay portable, engine-focused, and free of browser-only or native-host-specific package coupling.
- **`Motely.Wasm`** is allowed to contain Bootsharp-specific export/import wiring and browser-specific integrations.
- **`MotelyWasmHost.cs`** is intentionally excluded from `Motely.csproj` and compiled in `Motely.Wasm`. Keep it that way.
- **Filesystem support** is currently verified through `Bootsharp.FileSystem` and the existing `filesystem-smoke.mjs`. Do not rewrite it blindly.
- **DuckDB rule:** if `.NET` DuckDB packages do not compile to browser WASM, do not jam them into the WASM target. Use DuckDB's own WASM/JS path separately when needed.

## Public API shape rules

- **Prefer the real Motely shape.** The old `Motely.Run/Program.cs` is the conceptual reference: configure `MotelySearchSettings<TFilter>`, then start the search.
- **Do not fake elegance with wrappers.** If the public WASM API is awkward, improve the real contract carefully; do not hide it behind JS helper facades.
- **Source cleanup is good.** Splitting large interfaces into cohesive sub-interfaces is fine if the umbrella contract remains stable and no fake new layer is invented.
- **Structured analysis is the direction.** `SeedAnalysisDto` is the canonical Jamlyzer payload for UI/serialization. Text formatting is formatting, not the core analysis model.

## Project map

| Project | Purpose | Target |
|---|---|---|
| `Motely` | Core engine, JAML parser, analysis, portable search/runtime logic | `net10.0` |
| `Motely.CLI` | Command-line searcher and tooling commands | `net10.0` |
| `Motely.Tests` | xUnit tests and schema/golden checks | `net10.0` |
| `Motely.Wasm` | Browser/JS WASM build via Bootsharp | `net10.0` + `browser-wasm` |
| `motely-wasm` | Published npm package output | JavaScript package |
| `packages/jaml-language-core` | Shared JAML language package/schema helpers | Node |
| `packages/jaml-language-support` | JAML language support packaging/output | Node/VS Code |
| `Motely.Run` | Historical/simple host illustrating the core Motely search shape | `net10.0` |


# read the f8ucking docs at d:\Bootsharp ya dummies