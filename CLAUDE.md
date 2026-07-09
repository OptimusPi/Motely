# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Motely is a vectorized Balatro seed-search engine: AVX-512 SIMD, 8 seeds per lane per thread. JAML (Jimbo's Ante Markup Language) is the filter language — YAML and JSON both load to the same typed `JamlConfig` the engine executes. The repo ships the engine as a library, a CLI, and an npm WebAssembly package (`motely-wasm`).

Write positive prose everywhere — docs, comments, commit messages say what to do and why it helps. pifreak's word is the spec: check code and docs against what he says. When a single fact is missing, ask him in one direct sentence.

## Commands

.NET SDK 10.0.301 (pinned in `global.json`). Warnings are errors repo-wide, so a clean build is a green build.

```sh
dotnet build                                              # whole solution (Motely.slnx: engine, CLI, tests)
dotnet test                                               # C# suite (xUnit + Verify snapshots)
dotnet test --filter "FullyQualifiedName~JummyLineTests"  # one test class
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
- **JAML** lives in `Motely/Filters/Jaml/` — `JamlConfig`, `JamlConfigLoader` (`FromYaml`/`FromJson`; validation is loud and every key is checked at load, so typos surface immediately), clause types, per-feature descriptors under `AnteCards/`, `AnteFeatures/`, `Events/`.
- **JAMLyzer** (`Motely/Analysis/MotelyJamlyzer.cs`) produces per-seed ante-by-ante breakdowns; supports paged analysis with resumable stream states.
- **JUMMY** (`Motely/Filters/Jummy/JummyLine.cs`) is the one-human-line spelling of a JAML clause, with `Validate`/`Canonicalize`.
- **Native filters** (`Motely/Filters/Native/`) are hand-written C# filters (PerkeoObservatory, Jimmolate, ErraticFinder, …), reachable via CLI `--native <name>`; coverage focuses on the JAML path and lets these speed demons run free.
- `LuaRandom`/`VectorLuaRandom` reproduce Balatro's RNG exactly — determinism here is the whole product.

### Motely.CLI

`Program.cs` + `CliSearchMode.cs` pick one exclusive input mode: seed list/source file, `--makeitrain` lake replay, keyword, random, aesthetic, or the default sequential sweep. **The seed lake** is bare seeds in `Seeds/<filterId>.csv`, appended live by `SeedLakeSink` and streamed back by `SeedSourceProvider` (DuckDB reads; sources may be .csv/.txt/.parquet/.json, a JAML file's `seeds:` block, or a .db/.duckdb/.sqlite database — table resolution prefers `seeds`, then `results` (the BSO archive shape), then the sole table; SQLite files attach through the sqlite extension automatically). `Seeds/bso/` holds curated scored imports from the 16-month BSO era. Replay always re-runs lake seeds through the *current* JAML's clauses and scoring, so weight changes take effect with zero invalidation bookkeeping.

### Motely.Wasm

Bootsharp turns `Program.cs` `[Export]` classes into the flat npm module: `[RenameModule] → "index"` folds every namespace so `import { MotelySearch, MotelyJaml } from "motely-wasm"` works directly (the fold is safe while exported short names stay unique — check when adding exports). `Jimmolate.Filter` is a JS `[Import]` bound before `boot()`, speaking the Immolate contract: `filter(inst) => score` (numbers; booleans coerce to 1/0). Search APIs return `Task<MotelyScoredSeedResult[]>` — call, await, use — with `onProgress`/`onSeedMatch`/`onScoredResult` events streaming alongside. Tallies cross the boundary as `Int32Array`. **Read all fourteen Bootsharp docs in full before working here** — the interop model rewards it.

### JAML editor toolchain

One source of truth drives the whole toolchain. `JamlDiscriminatorRegistry` maps each discriminator to its clause and source-config types, and every type carries its own complete `ClauseKeys`/`SourceKeys` list — so the loader, the schema generator, and the editor tooling all read the same facts. `dotnet run Motely.Schema.cs` (a C# file-based app, from the repo root) regenerates `jaml-lang/src/generated.ts`, `jaml-lsp/syntaxes/jaml.tmLanguage.json`, and `jaml-lsp/schemas/jaml.schema.json` straight from that registry and the engine's enums — rerun it after any vocabulary or enum change and the editor tooling stays in lockstep.

`jaml-lang/` is the TypeScript language core (`validate`/`getCompletions`/`getHover`/`getDiagnostics` over the generated vocab; `npm test` runs its Node suite). `jaml-lsp/` bundles that core two ways with esbuild: `dist/extension.js`, the VS Code extension for `.jaml`/`.jummy` files (highlighting, diagnostics, completions, hover, the `@jimbo` chat participant, seed search, and `.jamlnb` notebooks — `npm run build`, `npm run package` for the vsix), and `dist/server.js`, a standalone stdio LSP server any editor can spawn (Neovim, Zed, Claude Code's IDE diagnostics), exposed via the `jaml-language-server` bin. `jaml-codemirror/` is the third consumer of that same core: a CodeMirror 6 language package (`JamlCodeEditor`, `JimmolateEditor`, the Jimmolate JS-predicate bridge) for any React app that wants a JAML editor without embedding VS Code — its linter and completions call straight into `jaml-lang`'s `validate`/`getCompletions`, so it carries zero hand-written vocabulary of its own and can never drift from the engine.

`jaml-lang/src/context.ts` (the cursor-position walker all three consumers share via `getCompletions`/`getHover`) reads its discriminator set from the generated `Discriminators` export, not a hand-copied list — the same discipline as the rest of the toolchain.

`JamlConfigLoader` reads YAML/JSON to a typed `JamlConfig`; `JamlConfigLoader.ToYaml` writes one back out, so editors and apps can round-trip a filter through save and reload.

### Supporting directories

- `JamlFilters/` — authored `.jaml` corpus; `JamlCorpusLoaderTests` keeps it loading.
- `Seeds/` — the seed lake output root (`MOTELY_DATALAKE_PATH` or `--results-path` overrides).
- `Motely.Tests/GoldenJamlFiles/` — Verify snapshot goldens; `seeds/*.txt` fixtures copy to output.

## Project skills and hooks

- `.claude/skills/release-motely-wasm/` — the complete npm release ritual; pifreak invokes it (`/release-motely-wasm`) and confirms the version and the publish.
- `.claude/skills/jaml-authoring/` — the JAML clause/JUMMY/vocabulary reference; read it before writing any `.jaml` filter.

## Gotchas worth knowing

- Verify snapshot tests compare against `Motely.Tests/GoldenJamlFiles/`; an intended output change is accepted by copying the `.received.` file over its `.verified.` twin — read the diff first and confirm the change is the behavior you meant.
- Motely.TUI exists as `bin/`/`obj/` build artifacts; its project file lives outside the tree today, so the solution builds engine, CLI, and tests.
- `searchSequential` on the WASM surface takes bigints — the C# parameters are `long`.

## Build notes

- `Directory.Packages.props` owns every package version centrally and `<MotelyVersion>` — the one number that versions assemblies and the npm package.
- Release CLI builds enable AVX-512 intrinsics, TieredPGO, ServerGC; the engine is built for 512-bit SIMD.
- `nuget.config` keeps merge-friendly sources so a per-user local Bootsharp feed can join in.
- Installing Binaryen (`wasm-opt` on PATH) makes WASM builds fully optimized; builds succeed and stay correct without it.
