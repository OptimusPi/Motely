# MotelyJAML

Balatro seed searcher. Given a JAML config (the YAML-based filter DSL defined here), grind seeds in parallel and report matches with scores. Library + CLI + TUI. .NET 10, C# latest, `TreatWarningsAsErrors=true`, Nullable enabled. Current `MotelyVersion` is `17.1.0` (`Directory.Packages.props`). A WebAssembly host has existed under a few names (`Motely.Wasm/`, `Motely.WebAssembly/`) — none are checked in right now; re-add when ready instead of carrying empty scaffolding.

## What "JAML" is

JAML = "Jimbo's Ante Markup Language" — a YAML doc describing a filter. It has three clause sets:

- **`must`** — every clause must pass. Failures short-circuit.
- **`should`** — contributes to score, doesn't gate.
- **`mustNot`** — any match rejects the seed.

Each clause is a typed shape (Joker, Voucher, Tarot, Spectral, Planet, StandardCard, Tag, Boss, ErraticRank/Suit/Card, the various event clauses like `LuckyMoney`/`WheelOfFortune`/etc., plus `And`/`Or`/`StartingDraw`). The set is open — every shape lives at `Motely/Filters/Jaml/*FilterDesc.cs` paired with a `*Clause` POCO in `Motely/Filters/Jaml/JamlConfig.cs` / `JamlConfigLoader.Models.cs`.

Top-level JAML keys (`JamlConfig`): `id`, `name`, `description`, `author`, `deck`, `stake`, `hashtags`, `seeds`, plus `must` / `should` / `mustNot`. Schema in `jaml.schema.json` (regenerated from `jaml-schema.cs`).

Loader entry point: `JamlConfigLoader.cs`. Builder that turns a `JamlConfig` into a runnable native filter: `JamlSearchBuilder.CreatePlan()` — and note, every production code path (`CreatePlan`, every `*FilterDesc`, scoring, search) reads only `JamlClauseSet.OrderedClauses`. The 28 typed lists (`Jokers`, `CommonJokers`, …) on `JamlClauseSet` are still populated by the loader (`JamlConfigLoader.cs` switch arms) and are read *only* in tests — `Motely.Tests/JamlConfigTests.cs` and `JamlEnumCaseInsensitivityTests.cs` have ~35 assertions like `Assert.Single(config.Must.Jokers)`. So they're not free to delete: the cleanup is a small refactor (drop the 28 props, drop the loader switch arms, rewrite the tests to use `OrderedClauses.OfType<JokerClause>()` etc.). Don't add new *production* code that depends on them; new tests should also prefer `OrderedClauses`.

## Projects

| Project | Role |
|---|---|
| `Motely/` | The library. Filters, search engine, JAML, PRNG, analysis. |
| `Motely.CLI/` | `dotnet run --project Motely.CLI`. Uses McMaster.Extensions.CommandLineUtils. |
| `Motely.TUI/` | Terminal.Gui 2.0 front-end. Editor, results browser, distributed worker window. |
| `Motely.Tests/` | xUnit. Golden JAML corpus in `GoldenJamlFiles/`. |

`Motely.slnx` is the source-of-truth solution file.

## Search engine layout (`Motely/`)

- `MotelySearch.cs` — top-level orchestration. Sequential / random / keyword / aesthetic / source-file modes. **Known bugs (`ISSUES.md` #1, #2): single-thread path can deadlock; multi-thread workers swallow exceptions.** If you touch threading, fix those.
- `MotelySingleSearchContext.*.cs` — per-seed single-lane evaluator, partial classes split by domain (Jokers, Packs, Tags, Tarot, Spectral, Planet, StandardCards, Vouchers, Boss, Shop, Shuffle, Misc).
- `MotelyVectorSearchContext.*.cs` — SIMD/vectorized evaluator. Same domain split.
- `MotelyPrngKeys.cs` / `LuaRandom.cs` — seeded PRNG matching Balatro's. **PRNG identity strings are load-bearing**; case and spelling have been broken by IDE rename refactors before (see `a9bf9aa9`). Don't bulk-rename across them.
- `MotelyRunState.cs` / `MotelyVectorRunState.cs` — run state passed to filters.
- `Filters/Jaml/` — JAML clause types and their `FilterDesc` implementations.
- `Filters/Native/` — hand-rolled native filters (e.g. `PerkeoObservatory`, `Trickeoglyph`, `NaturalNegatives`, `Jimmolate`, `NaNSeedFilter`, …) — invoked via `--native <Name>`.
- `Analysis/` — `MotelyJamlyzer` curated-list runner and `MotelySeedAnalyzer` per-seed deep dive.

Seeds are base-35: digits `1–9` + letters `A–Z` (no `0` — `0` is folded to `O` on input). Max length 8 (`MotelyGlobals.MaxSeedLength`). Short seeds left-significant-pad with `1` for range parsing (`Motely.CLI/Program.cs:80–84`).

## CLI surface

Run `dotnet run --project Motely.CLI -- -h` for the truth. Key shapes:

- **Search**: `--jaml <path>` (with optional `--cutoff`, `--save-seeds` to write top-1000 hits back into the JAML).
- **Native filter**: `--native <Name>` — same seed-source flags as JAML.
- **Analyze**: `--analyze <SEED[,SEED...]>` — single seed or NDJSON batch (`--output-json`).
- **Jamlyzer**: `--jamlyzer --jaml <path>` — scores a curated `seeds: [...]` list.
- **Replay**: `--drown --jaml <path>` — DuckDB-backed replay of stored results.
- **Seed sources** (one of): default sequential (`--startBatch/--endBatch/--startPercent` or `--startSeed/--stopSeed`), `--random <N>`, `--keyword(s)` with optional `--padding`, `--aesthetic <name>`, `--source <name|path>`.

## Tests

```
dotnet test Motely.Tests/Motely.Tests.csproj
```

Golden fixtures live in `Motely.Tests/GoldenJamlFiles/`. `JamlCorpusRegressionTests` runs every `.jaml` in `JamlFilters/` through the loader — if you add a syntax-affecting clause, expect to update fixtures. **There is a duplicate** (`ISSUES.md` #9): `GoldenJamlFiles/legendary-perkeo.jaml` and `Zerkeo.jaml` are byte-identical. Delete one when you're in the area.

## Conventions and gotchas

- **No comments unless the *why* is non-obvious.** This codebase is large and well-named; resist adding restatements.
- **PRNG key strings are identities.** Treat them like protocol — don't lowercase, don't rename, don't reformat. See `MotelyPrngKeys.cs`.
- **TreatWarningsAsErrors is on.** A nullable-reference warning will break the build. `NU1603` is in `WarningsAsErrors`.
- **Vector vs single must agree.** When you add a clause type, you implement it once on `MotelySingleSearchContext` and once on `MotelyVectorSearchContext`. `SearchConsistencyTests` enforces parity — don't skip the vectorized side.
- **`InvariantGlobalization`** is on in `Motely.csproj` — don't introduce culture-dependent parsing/formatting.
- **PowerShell shell.** When scripting locally use PS syntax (`$env:VAR`, `$null`, backtick line continuation) — Bash is available via the Bash tool for POSIX scripts.

## Where else to look

- `ISSUES.md` — current honest audit of pain points (HIGH: threading bugs; MED: empty docs, magic constants, dead code, dropped test).
- `Seeds/` — sample seed lists and a `motely.ducklake` DuckDB store used by `--drown`.
- `JamlFilters/` — the working corpus of `.jaml` files. Treat it as a living test set.
- `analysis.json` — sample analyzer output.
- `clean.ps1` — repo-wide `bin/obj` blast.
