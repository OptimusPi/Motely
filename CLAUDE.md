# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Motely is a high-performance **Balatro seed searcher**. Balatro's runs are fully determined by an
8-character seed fed into the game's Lua-based PRNG, so Motely reimplements that PRNG in C# and then
brute-forces or replays seeds through a vectorized filter/scoring pipeline to find seeds matching
user-defined criteria (e.g. "Perkeo appears in antes 1-2 with high luck").

The solution (`Motely.slnx`) has 5 projects:

| Project | Role |
|---|---|
| `Motely` | Core engine: SIMD seed search, Balatro PRNG reimplementation, the JAML filter DSL, hand-coded "native" filters, scoring/analysis. No entry point. |
| `Motely.CLI` | Headless command-line runner (`OutputType=Exe`, AOT-publishable). References `Motely` + `Motely.Data`. |
| `Motely.Data` | DuckDB/Parquet "seed lake" persistence (writing found seeds, replaying/"drowning" previously found seeds). Only project touching DuckDB; excluded under `#if !BROWSER`. |
| `Motely.TUI` | Interactive Terminal.Gui v2 shell, plus an embedded ASP.NET Core API for distributed/remote search workers. Running it with CLI args just tells you to use `Motely.CLI` instead — CLI and TUI are cleanly separated. |
| `Motely.Tests` | xUnit + Verify.Xunit (snapshot testing) + coverlet. References only core `Motely` (not CLI/Data/TUI). |

`Motely.Mcp` does **not** exist yet in this tree — it's only referenced via a package-version comment
in `Directory.Packages.props` (pinning `ModelContextProtocol`) and an unmerged `origin/MCP-DOCS` branch.
Don't assume an MCP server exists until that lands.

Motely also targets **Blazor WebAssembly** (browser build) — code with `OperatingSystem.IsBrowser()`
checks or `#if !BROWSER` guards exists for that reason (DuckDB and native intrinsics aren't available
in-browser).

## Build, test, run

SDK is pinned by `global.json` (10.0.204, `rollForward: latestPatch`). Central package management is
on (`Directory.Packages.props`, `ManagePackageVersionsCentrally=true`), and `TreatWarningsAsErrors=true`
solution-wide — a build with new warnings will fail.

```
dotnet build
dotnet test
dotnet test --filter FullyQualifiedName~JummyLineTests.SomeTestName   # single test; no custom [Trait]s exist, filter by name
dotnet run --project Motely.CLI -- --jaml JamlFilters/Perkeo.jaml
dotnet run --project Motely.CLI -- --native PerkeoObservatory --random 1000000
dotnet run --project Motely.CLI -- --analyze <seed[,seed...]>
dotnet run --project Motely.TUI
```

Release builds enable AVX2/AVX/AVX512F/AVX512BW hardware intrinsics and `PublishAot` for the CLI —
keep filter code AOT- and intrinsics-safe (avoid reflection-heavy patterns in `Motely`/`Motely.CLI`).

Test coverage config lives at `coverage.runsettings` (repo root), referenced via
`RunSettingsFilePath=../coverage.runsettings` from `Motely.Tests.csproj`.

## Core search architecture (`Motely/`)

Everything hangs off interfaces in `Motely/MotelySearch.cs`:

- `IMotelySeedFilter.Filter(ref MotelyVectorSearchContext ctx)` — the core filter contract. Operates on
  **8 seeds at once** as SIMD lanes (`Vector512<double>`), returning a bitmask of which lanes passed.
- `IMotelySeedFilterDesc<TFilter>` — factory that builds a concrete filter `struct` given a
  `MotelyFilterCreationContext` (one-time setup, e.g. caching pseudo-hash key lengths across filters).
- `IMotelySeedScoreProvider` / `IMotelySeedScoreDesc` — scoring pass run on filter survivors; this is
  what drives JAML `should:` weighted scoring.
- `IMotelySeedAnalyzeProvider` — per-seed human-readable breakdown, used by `--analyze`.
- `IMotelySeedRouter` — hands a *single* seed (`MotelySingleSearchContext`, non-vectorized) to a
  consumer for logic that doesn't lend itself to SIMD (e.g. some analysis/native filters).
- `MotelySearchSettings<TBaseFilter>` — fluent builder (`WithDeck`, `WithThreadCount`,
  `WithSequentialSearch`, `WithProviderSearch`, `WithSeedScoreProvider`, `WithAutoScoreCutoff`, ...) that
  CLI/TUI construct and call `.Start()` on.
- `MotelySearch<TBaseFilter>` — the engine itself: spins up dedicated native `Thread`s (not the task
  pool, to pin long CPU-bound loops), each running a `MotelySequentialSearchPlan` or
  `MotelyProviderSearchPlan`. Uses thread-local counters aggregated via `Volatile.Read` to avoid
  `Interlocked` in the hot path. Has a cooperative-yield path for `OperatingSystem.IsBrowser()`.
  Auto score cutoff (`AutoCutoffState`) is a lock-free monotonic-max clamp that only engages once match
  rate exceeds a threshold, to avoid flooding the result callback/WASM interop boundary.

Game-state queries live in per-domain partials — `MotelySingleSearchContext.*.cs` (scalar) and
`MotelyVectorSearchContext.*.cs` (SIMD twin): `.Jokers`, `.Shop`, `.Packs`, `.Tags`, `.Vouchers`,
`.Tarot`, `.Spectral`, `.Planet`, `.Boss`, `.Shuffle`, `.StandardCards`, `.Misc`. Read one of these plus
the base context file to see how filters ask "what would the game deal in ante N, shop slot M".

The PRNG itself: `Motely/LuaRandom.cs` (scalar) and `Motely/VectorLuaRandom.cs` (SIMD) reimplement
Balatro's Lua/Love2D xoshiro-family generator, seeded from a hashed double. This is reverse-engineered
to match the game exactly — don't "clean up" the math without verifying against real game output.

`SeedMath.cs` handles seed↔index conversion for brute-force enumeration (35-char alphabet, batch
prefixing). `MotelyItem`/`MotelyItemVector` are packed-int representations of a game item
(type/edition/enhancement/seal/sticker), scalar and vectorized.

## The JAML filter DSL

Two related but distinct things share the "JAML" name:

- **`JamlFilters/`** (repo root) is a library of ~130 *user-authored filter instances* — YAML files like
  `Perkeo.jaml` — not the DSL implementation itself.
- **The JAML engine** lives in `Motely/Filters/Jaml/`:
  - `JamlConfig.cs` — the config POJOs (`JamlConfig`: id/name/deck/stake/seeds/must/should/mustNot, plus
    per-item-type `*SourceConfig` classes for shop slots, pack slots, special voucher slots).
  - `JamlConfigLoader.cs` — hand-rolled YAML→`JamlConfig` parser built on **SharpYaml** (not YamlDotNet,
    despite both being referenced). Validates every key strictly (throws on unknown keys) and dispatches
    on a discriminator key (`joker`, `legendaryJoker`, `voucher`, `tarotCard`, `standardCard`, `boss`,
    `tag`, `luckyMoney`, `and`/`or`, ...) into ~30 concrete clause types.
  - `JamlClause.cs` — the clause contract (`IJamlClause`: Label/Min/Max/Score; `IAnteScopedClause` adds
    `Antes`).
  - `must:`/`mustNot:` clauses are hard filters; `should:` clauses feed scoring (weighted).
  - `Motely/Filters/Jaml/AnteCards/`, `AnteFeatures/`, `Events/` hold the concrete SIMD `*FilterDesc`
    implementation per clause type.

**JUMMY** (`Motely/Filters/Jummy/JummyLine.cs`) is a companion single-line mini-language nested inside
JAML clause lists — e.g. `"Eternal Blueprint in antes 1 or 2"` — that round-trips losslessly to/from the
same clause objects (`JummyLine.TryToClause` / `FromClause`). A `must:`/`should:` entry can be either a
full YAML mapping or one of these terse lines. Covered extensively by
`Motely.Tests/JummyLineTests.cs` — when changing clause parsing, keep both forms in sync.

`Motely/Filters/Native/` holds hand-coded, non-JAML C# filters runnable via `--native <Name>`
(`PerkeoObservatoryDesc.cs`, `ErraticFinderDesc.cs`, `Trickeoglyph.cs`, `TwoBlackHoleFilterDesc.cs`,
`JimmolateFilterDesc.cs`, ...) — bespoke/experimental filters that predate or bypass JAML.

## Motely.Data — the seed lake

- `MotelyParquetSeedSink.cs` — buffers scored results, then on dispose opens an in-memory DuckDB
  connection and `COPY`s them to `.seeds/<filterId>/<timestamp>.parquet`.
- `DuckLakeDrownProvider.cs` — the reverse: reads all `*.parquet` for a filter
  (`SELECT DISTINCT seed FROM read_parquet(...)`) and replays them as a seed list — this is what
  `--drown` uses to re-score previously found seeds (e.g. after tweaking `should:` weights) without
  re-running the full brute-force search.

## Testing conventions

xUnit `[Fact]`/`[Theory]` only — no custom `[Trait]` categories, so filter by test name. Verify.Xunit +
Verify.DiffPlex do snapshot testing (wired in `ModuleInitializer.cs`). `Motely.Tests/filters/*.jaml` is a
golden corpus (one clause per card/joker/boss) used for systematic coverage testing — when adding a new
clause type, add a corresponding golden file rather than only a unit test. `Motely.Tests/seeds/*.txt` are
fixture seed lists copied to the output dir.

## Related repos

`d:\jaml-ui` (a Next.js app under `apps/balatro-seed-app`) and `d:\seedfinder.app` / `d:\thelongblind`
are sibling projects that consume or relate to Motely/JAML — relevant if changes here need to stay
compatible with a frontend that reads JAML files or Motely output.
