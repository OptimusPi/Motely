# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Motely is a vectorized Balatro seed-search engine: AVX-512 SIMD, 8 seeds per lane per thread. JAML (Jimbo's Ante Markup Language) is the filter language — YAML and JSON both load to the same typed `JamlConfig` the engine executes. The repo ships the engine as a library, a CLI, and an npm WebAssembly package (`motely-wasm`).

Write positive prose everywhere — docs, comments, commit messages say what to do and why it helps. Nat (pifreak) is the author; she/they. Her word is the spec: check code and docs against what she says. When a single fact is missing, ask her in one direct sentence.

Run the engine. `dotnet run --project Motely.CLI -- --jaml <file>` is a normal, expected part of working here — a search that runs and finds a seed is the proof, and a test that fakes the search proves nothing. Surface errors where she can see them rather than piping them away.

## Commands

.NET SDK 10.0.301 (pinned in `global.json`). Warnings are errors repo-wide, so a clean build is a green build.

```sh
dotnet build                                              # whole solution (Motely.slnx: engine, CLI, tests)
dotnet test                                               # C# suite (xUnit + Verify snapshots)
dotnet test --filter "FullyQualifiedName~JamlLineTests"   # one test class
dotnet test --filter "DisplayName~<fragment>"             # one test by name
```

WASM package, from `Motely.Wasm/`:

```sh
npm test          # dotnet publish -c Release into dist/, then Node suite against dist/index.mjs
npm run test:ui   # Playwright drives testui/ in real Chromium against the same artifact
npm run serve     # hand-drive the test UI at http://127.0.0.1:4173/
```

CLI examples (AOT publish is default-on; `-p:EnableCliAot=false` for a fast dev build):

```sh
dotnet run --project Motely.CLI -- --jaml JamlFilters/01WeeMonday.jaml
dotnet run --project Motely.CLI -- --jaml <file> --makeitrain   # replay the filter's saved seed lake
dotnet run --project Motely.CLI -- --analyze SEED --jaml <file>
```

Releasing motely-wasm: bump `<MotelyVersion>` in `Directory.Packages.props` (the single version source — a build target stamps `Motely.Wasm/package.json` from it), run both npm suites green, then `npm publish` from `Motely.Wasm/`. pifreak confirms the version number and the publish step.

## Architecture

Dependency direction points inward to the engine: **Motely** (library) ← Motely.CLI, Motely.Tests, Motely.Wasm. Motely.Data (DuckDB helpers) also references the engine. The solution builds engine, CLI, and tests; Motely.Wasm publishes separately.

### Motely — the engine

- **Two execution contexts, one filter model.** `MotelyVectorSearchContext` (partials per domain: Joker, Shop, Tarot, Packs, Tags, Vouchers, …) is the 8-wide SIMD path filters run on. `MotelySingleSearchContext` (same partial layout plus Boss, Shuffle, RunState) is the per-seed scalar path used for scoring, analysis, and the JS Jimmolate filter. `MotelySearch.cs` is the driver; `MotelySearch.Browser.cs` is its WASM-facing partial.
- **Filters are descriptors.** `IMotelySeedFilterDesc` describes a filter; `MotelyFilterCreationContext` instantiates it. `JamlSearchBuilder.CreateSettings` composes the chain from a `JamlConfig`: `must` clauses append filters, `mustNot` wraps in `NegationFilterDesc`, `should` installs `JamlShouldScoreDesc` for weighted scoring. A clause-free JAML (deck/stake/seeds only, host predicate carries the decision) is a first-class search.
- **JAML** lives in `Motely/Filters/Jaml/` — `JamlConfig`, `JamlConfigLoader` (`FromYaml`/`FromJson`; validation is loud and every key is checked at load, so typos surface immediately), clause types, per-feature descriptors under `AnteCards/`, `AnteFeatures/`, `Events/`. `JamlLine` (`Motely/Filters/Jaml/JamlLine.cs`) is the one-human-line spelling of a JAML clause, with `Validate`/`Canonicalize`.
- **JAMLyzer** (`Motely/Analysis/MotelyJamlyzer.cs`) produces per-seed ante-by-ante breakdowns; supports paged analysis with resumable stream states.
- **Native filters** (`Motely/Filters/Native/`) are hand-written C# filters (PerkeoObservatory, Jimmolate, ErraticFinder, …), reachable via CLI `--native <name>`; coverage focuses on the JAML path and lets these speed demons run free.
- `LuaRandom`/`VectorLuaRandom` reproduce Balatro's RNG exactly — determinism here is the whole product.

### Motely.CLI

`Program.cs` + `CliSearchMode.cs` pick one exclusive input mode: seed list/source file, `--makeitrain` lake replay, keyword, random, aesthetic, or the default sequential sweep. **The seed lake** is bare seeds in `Seeds/<filterId>.csv`, appended live by `SeedLakeSink` and streamed back by `SeedSourceProvider` (DuckDB reads; sources may be .csv/.txt/.parquet/.json, a JAML file's `seeds:` block, or a .db/.duckdb/.sqlite database — table resolution prefers `seeds`, then `results` (the BSO archive shape), then the sole table; SQLite files attach through the sqlite extension automatically). `Seeds/bso/` holds curated scored imports from the 16-month BSO era. Replay always re-runs lake seeds through the *current* JAML's clauses and scoring, so weight changes take effect with zero invalidation bookkeeping.

### Motely.Wasm

Bootsharp turns `Program.cs` `[Export]` classes into the flat npm module: `[RenameModule] → "index"` folds every namespace so `import { MotelySearch, MotelyJaml } from "motely-wasm"` works directly (the fold is safe while exported short names stay unique — check when adding exports). `Jimmolate.Filter` is a JS `[Import]` bound before `boot()`, speaking the Immolate contract: `filter(inst) => score` (numbers; booleans coerce to 1/0). Search APIs return `Task<MotelyScoredSeedResult[]>` — call, await, use — with `onProgress`/`onSeedMatch`/`onScoredResult` events streaming alongside. Tallies cross the boundary as `Int32Array`.

The fourteen Bootsharp docs, pinned so they load every session instead of relying on a reminder to go read them:

@../bootsharp/docs/index.md
@../bootsharp/docs/guide/index.md
@../bootsharp/docs/guide/getting-started.md
@../bootsharp/docs/guide/build-config.md
@../bootsharp/docs/guide/declarations.md
@../bootsharp/docs/guide/interop-instances.md
@../bootsharp/docs/guide/interop-modules.md
@../bootsharp/docs/guide/llvm.md
@../bootsharp/docs/guide/renaming.md
@../bootsharp/docs/guide/serialization.md
@../bootsharp/docs/guide/sideloading.md
@../bootsharp/docs/guide/specialization.md
@../bootsharp/docs/guide/extensions/dependency-injection.md
@../bootsharp/docs/guide/extensions/file-system.md

### JAML grammar lives in C#

**The C# engine is the only grammar.** `JamlDiscriminatorRegistry` maps each discriminator to its clause and source-config types, and every type carries its own `ClauseKeys`/`SourceKeys` list, so the loader reads the same facts the engine executes. `JamlConfigLoader` reads YAML/JSON into a typed `JamlConfig` and validates every key at load, so a typo surfaces immediately; `JamlConfigLoader.ToYaml` writes one back out, so an app can round-trip a filter through save and reload.

**The TypeScript reimplementations are gone, on purpose.** `jaml-lsp` (a VS Code extension and a stdio language server) and `jaml-codemirror` were both deleted: each one carried its own copy of the grammar, so every vocabulary change meant editing the same facts in three places and shipping three packages in lockstep. Editors reach the grammar through `motely-wasm` instead — the engine itself, compiled, doing the parsing it already does. **Leave them buried.** A third implementation of a grammar the engine already owns is a place for the truth to rot, not a feature.

`dotnet run Motely.Schema.cs` (from the repo root) still emits `jaml-lang/src/generated.ts` for any live TypeScript consumer, straight from the registry and the engine's enums. It writes **only to folders that already exist** and prints what it skipped — a generator that recreates its own deleted consumers is how a removed package comes back on the next build, and that is the exact loop this repo spent months in. Rerun it after any vocabulary or enum change.

### Supporting directories

- `JamlFilters/` — authored `.jaml` corpus; `JamlCorpusLoaderTests` keeps it loading.
- `Seeds/` — the seed lake output root (`MOTELY_DATALAKE_PATH` or `--results-path` overrides).
- `Motely.Tests/GoldenJamlFiles/` — Verify snapshot goldens; `seeds/*.txt` fixtures copy to output.

## Project skills and hooks

- `.claude/skills/release-motely-wasm/` — the complete npm release ritual; pifreak invokes it (`/release-motely-wasm`) and confirms the version and the publish.
- JAML clause/vocabulary reference lives in the **Balatro Seed Oracle MCP server** (part of seedfinder.app), not this repo — call its `learn_jaml` tool before writing any `.jaml` filter.

## Build notes

- `Directory.Packages.props` owns every package version centrally and `<MotelyVersion>` — the one number that versions assemblies and the npm package.
- Release CLI builds enable AVX-512 intrinsics, TieredPGO, ServerGC; the engine is built for 512-bit SIMD.
- `nuget.config` keeps merge-friendly sources so a per-user local Bootsharp feed can join in.
- Install Binaryen (`wasm-opt` on PATH) for fully optimized WASM builds; builds succeed and stay correct without it too.
- Accept an intended Verify snapshot change by reading the diff, confirming it's the behavior you meant, then copying the `.received.` file over its `.verified.` twin in `Motely.Tests/GoldenJamlFiles/`.
- The solution builds engine, CLI, and tests; Motely.TUI's project file lives outside the tree, with only its `bin/`/`obj/` build artifacts present here today.
- Call `searchSequential` on the WASM surface with bigints; they map to the C# `long` parameters underneath.
