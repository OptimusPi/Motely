# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Motely is a vectorized (SIMD) seed-search engine for Balatro. Filters are authored
in JAML (Jimbo's Ante Markup Language). It is standalone — `Motely.slnx` builds and
runs on its own. The checkout may sit inside another repo as a vendored submodule;
that parent is not yours. Do not reach into it or assume it.

## Commands

```powershell
dotnet build Motely.slnx                                                    # compile everything
dotnet test  Motely.Tests/Motely.Tests.csproj                              # all tests
dotnet test  Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~Name"   # one test
dotnet run --project Motely.CLI -- --help                                  # CLI options
dotnet run --project Motely.TUI                                            # terminal UI
```

Toolchain: .NET 10 SDK, pinned in `global.json`. `Nullable` and `ImplicitUsings`
are on, `LangVersion=latest` (set in `Directory.Build.props`).

## Running policy

The **one** off-limits run is a full seed sweep — the whole ~2.3T space, days of
hashing. Never start that. Everything bounded is allowed and wanted: single-seed
analysis (~100ms), `Motely.CLI` with explicit params (filter/deck/stake/ante/count,
`--source`/`--keyword`/`--random`/`--startBatch`/`--endBatch`), targeted tests,
throwaway C# repros, building. Don't over-generalize the one rule into "never run
anything." Full text: `docs/running-policy.md`.

## Architecture

**The search loop is two execution shapes of the same filter.** A filter is an
`IMotelySeedFilterDesc<TFilter>` whose `CreateFilter` returns a `struct TFilter :
IMotelySeedFilter`. `TFilter.Filter(ref MotelyVectorSearchContext)` runs over a
**vector of seeds at once** (SIMD) and returns a `VectorMask` of survivors. Inside,
code branches to `MotelySingleSearchContext` for the per-seed walk (one seed,
materializes shop/packs/tags/vouchers/boss/etc). Vectorized = the hot search path;
single = analysis and anything not worth vectorizing.

**Searches are built with a fluent `MotelySearchSettings<TFilter>`** (`MotelySearch.cs`):
`.WithDeck` / `.WithStake` / `.WithThreadCount`, and one of `.WithListSearch([seeds])`
(explicit list — this is how single-seed analysis runs), `.WithRandomSearch(n)`,
`.WithSequentialSearch()` (the full space — bounded by `.WithStartBatchIndex` /
`.WithBatchCharacterCount`). `.CreateSearch()` → `RunSearchUntilCompletion()`.

**JAML is the filter authoring layer** (`Motely/Filters/Jaml/`). `JamlConfigLoader.TryLoad`
parses JAML (YAML via YamlDotNet — **no JSON load path**) into a `JamlConfig`: `deck`,
`stake`, and `must` / `should` / `mustNot` clauses (`JamlClause`, plus `RollClause` for
gameplay rolls). `must`/`mustNot` gate; `should` clauses score. Scoring `mode`: `sum`
(default — `count * score` per should), `max` (max raw `count`, per-clause score
ignored), `max_count`/`maxcount`. Each clause type has its own `*FilterDesc.cs`
(JokerFilterDesc, TagFilterDesc, VoucherFilterDesc, BossFilterDesc, …). `JamlScoring` /
`JamlScoop` do the counting; `JamlConfigLoader.NormalizeDefaultSources` decides where a
bare clause (no source) looks — shop slots `[0,1,2,3]` + packs `[0..5]`.

**The analyzer** (`Motely/Analysis/`) introspects ONE seed. `MotelyLegacyTextAnalyzer.Analyze`
takes a `MotelyLegacyTextAnalysisConfig` (seed/deck/stake), runs a single-seed walk, and
returns a `MotelyLegacyTextAnalysis` (boss/voucher/tags/shop/packs per ante via
`MotelyAnteAnalysis` / `MotelyAnalyzedItem`). Its `ToString()` is the **legacy flat text
block** — for test ground-truth (Verify()) and cross-tool comparison (miaklwalker,
mathisfun_), not UI.

## Heads (`Motely.slnx`)

- `Motely/` — the engine. `Motely.CLI/` — command line. `Motely.TUI/` — Terminal.Gui UI.
- `Motely.Wasm/` — Bootsharp WASM build. Bootsharp generates the ES module +
  `package.json`; output is `motely-wasm/`. `BootsharpPublishDirectory` = module,
  `BootsharpPackageDirectory` = `package.json`, `BootsharpBinariesDirectory` = binaries
  (set → sideloaded separate `.wasm`, `boot()` takes a root URL or `{ wasm }` bytes;
  empty → embedded, `boot()` takes no args). `[RenameNode]`/`[RenameMember]` returning
  null/empty erases that node/member from the generated JS surface. The full Bootsharp
  guide auto-loads via the `@` imports below — read it before changing this project.

@d:/bootsharp/docs/guide/index.md
@d:/bootsharp/docs/guide/getting-started.md
@d:/bootsharp/docs/guide/build-config.md
@d:/bootsharp/docs/guide/sideloading.md
@d:/bootsharp/docs/guide/interop-modules.md
@d:/bootsharp/docs/guide/interop-instances.md
@d:/bootsharp/docs/guide/declarations.md
@d:/bootsharp/docs/guide/serialization.md
@d:/bootsharp/docs/guide/specialization.md
@d:/bootsharp/docs/guide/renaming.md
@d:/bootsharp/docs/guide/llvm.md
@d:/bootsharp/docs/guide/extensions/dependency-injection.md
@d:/bootsharp/docs/guide/extensions/file-system.md
- `Motely.DataLake/` — results/data tooling. `Motely.Tests/` — the test project.

Filters live in `JamlFilters/` (ready-made `.jaml`), the language in `jaml-lang/`,
game mechanics in `docs/balatro-mechanics.md`.

## Working agreement

- **Consent first.** Do exactly what is asked, nothing adjacent. When the user says
  stop, stop — no defending, no "let me just finish this." Don't add unrequested
  content (including to docs). Confirm before anything hard to reverse.
- **No softening.** Straight answers. Don't dress up a wrong guess as obvious, don't
  pad admissions with praise, don't flatter bad work to blend in.
- **Read before assuming.** This is Motely, not its parent. Verify the boundary instead
  of inheriting an assumption from the checkout path.
