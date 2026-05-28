# MotelyJAML

Balatro seed search engine. Filters are written in JAML (a YAML dialect, schema at `jaml.schema.json`). The engine is C# (`Motely/`), packaged as a NuGet plus a WASM JS package (`motely-wasm`, currently `19.0.2`, see `Directory.Packages.props` → `MotelyVersion`).

**This directory is its own repo**, vendored at `X:\BalatroSeedOracle\src\MotelyJAML` only as a working location. Do **not** read or modify anything under `X:\BalatroSeedOracle\` (the Oracle app) when working here unless the user explicitly asks — they are separate codebases with separate concerns.

## Layout

- `Motely/` — core search engine (C# library). `Motely.csproj`.
- `Motely.CLI/` — command-line entry point.
- `Motely.TUI/` — Terminal.Gui frontend.
- `Motely.Wasm/` — Bootsharp/NativeAOT-LLVM WASM build that produces the JS package. `dotnet publish Motely.Wasm -c Release` is the publish gate.
- `motely-wasm/` — published npm package output (`dist/`, `bin/`, `package.json` with the v19.x version). `Motely.Wasm/README.md` is the canonical consumer-facing API doc.
- `Motely.DataLake/` — DuckDB-backed result store.
- `Motely.Tests/` — xunit suite.
- `JamlFilters/`, `Seeds/` — sample filters and seed inputs.
- `jaml.schema.json` — legacy JSON Schema (still on disk and still shipped in the `motely-wasm` package for editor autocomplete, but the source of truth is now the TypeScript types in `motely-wasm/dist`). `jaml.schema.jaml` is the running joke; if it shows up, that's why.

## Build / verify

- C# build: `dotnet build` (sln is `Motely.slnx`, .NET SDK floor is whatever the slnx + Directory.Build.props pin).
- Tests: `dotnet test Motely.Tests`.
- WASM publish gate (after touching anything WASM-facing):
  1. `dotnet publish Motely.Wasm -c Release`
  2. `node Motely.Wasm/motely.test.mjs` — expect `RESULT: PASS`
  3. `node Motely.Wasm/pack-consumer-smoke.mjs` — `npm pack` + fresh install + same boot path
- TreatWarningsAsErrors is on. Don't suppress; fix.

## Bootsharp / WASM specifics

`AGENTS.md` (same directory) is the canonical reference for the Bootsharp build — local source at `D:\bootsharp`, `feat/delegates` is force-pushed (use `git reset --hard origin/feat/delegates`, never `git pull`), and `Bootsharp.FileSystem` versioning is a separate timestamp pin. **Read `AGENTS.md` before touching anything Bootsharp-related** — do not rely on public Bootsharp docs.

## API surface (WASM consumers)

`Motely.Wasm/README.md` documents the public JS API: `bootsharp.boot(...)`, `Motely.validateJaml`, `Motely.fromJaml`, `Motely.createStreamCursor`, packed-int decoders, event subscriptions, and the Web Worker pattern. When changing exports or behavior, update that README — it is the file npm consumers see.
