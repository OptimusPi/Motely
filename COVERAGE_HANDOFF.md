# Code Coverage Handoff — MotelyJAML

## Current State

- **596 tests pass** (up from 490), 1 intentionally skipped, 0 failures
- **Overall: 74.61% line / 64.06% branch** (was 72.04% / 61.83%)
- **Motely engine: 75.2% line / 64.6% branch** (was 72.5% / 62.2%)

## What Was Added

5 new test files in `Motely.Tests/Coverage/`:

| File | Tests | What it covers |
|---|---|---|
| `Cover_JamlScoringExpandedTests.cs` | 30 | Boss scoring, Planet/Tarot/Spectral scoring, starting draw, erratic rank/suit, event early-exit/fail, voucher multi-roll, joker wildcard+legendary split, and/or clauses, CapScoreCount, spectral TheSoul/BlackHole |
| `Cover_UncommonJokerFilterDescTests.cs` | 13 | Raw shop joker streams (Common/Uncommon/Rare/All), edition/sticker matching, booster pack paths |
| `Cover_JokerStreamStakeTests.cs` | 16 | Joker stream sticker/edition paths at White/Black/Orange/Gold stakes, fixed-rarity streams |
| `Cover_MotelyItemVectorTests.cs` | 20 | Vector property accessors, AsType bit arithmetic, Equals, With* scalar overloads, indexer, ToString |
| `Cover_FilterCreationContextTests.cs` | 7 | Full pipeline tests for vouchers, tags, spectrals, standard cards, legendary jokers |

## Remaining Opportunities (Ranked by Effort→Usefulness)

### Tier 1: Low Effort, High Usefulness

1. **`JamlScoring.CountJokerOccurrencesWildcard` + `CountSpecialtyJokerSourcesWildcard`** (~200 uncovered lines)
   - Write `RunShould` with `JokerClause { IsWildcard = true }` + source configs that exercise Judgement, Wraith, RiffRaff, RareTag, UncommonTag specialty sources
   - Effort: ~30 min. Impact: covers the entire wildcard scoring path

2. **`JamlScoring.CountRawOccurrences` / `CountRawAndOccurrences` / `CountRawOrOccurrences`** (~70 uncovered lines)
   - These are the JAMLyzer analysis path. Call `JamlScoring.CountRawOccurrences` directly (it's `internal` + `InternalsVisibleTo`)
   - Effort: ~20 min. Impact: covers the raw count path used by analysis

3. **`JamlScoring.MatchStandardCard` with Enhancement/Seal/Edition** (~15 uncovered lines)
   - Add `StandardCardClause` with enhancement/seal/edition filters in should
   - Effort: ~10 min. Impact: small but complete coverage of card matching

### Tier 2: Medium Effort, High Usefulness

4. **`MotelyVectorSearchContext.Spectrals` — Soul/BlackHole resample path** (~80 uncovered lines)
   - The `GetNextSpectral` resample loop when soul/blackhole appears in arcana packs
   - Needs a spectral pack stream with `soulBlackHoleable=true` and specific PRNG values
   - Effort: ~1 hr. Impact: covers the most complex spectral logic

5. **`MotelyFilterCreationContext.CacheShopStream` exclusion flags** (~30 uncovered lines)
   - `ExcludeJokers`, `ExcludeTarots`, `ExcludePlanets` flags on shop stream caching
   - Needs filter creation context with `MotelyShopStreamFlags` set
   - Effort: ~45 min. Impact: covers shop stream configuration branches

6. **`MotelyVectorSearchContext.Joker` — GetNextBuffoonPackContentsMasked** (~40 uncovered lines)
   - The masked variant of buffoon pack content generation
   - Effort: ~1 hr. Impact: covers a self-contained SIMD method

### Tier 3: High Effort, Medium Usefulness

7. **`MotelyVectorUtils` — ISA-specific branches** (~156 uncovered lines)
   - `ShiftLeft` Avx2/AdvSimd/PackedSimd/fallback paths, `VectorMaskToConditionalSelectMask`
   - Unreachable on current hardware (x64 only hits Avx512F/Avx2 paths). Needs multi-arch CI or mock
   - Effort: ~3 hrs (needs architecture abstraction). Impact: limited without multi-arch CI

8. **`MotelySearch.cs` — Sequential/Browser search plans** (~400 uncovered lines)
   - `MotelySequentialSearchPlan`, `RunProviderBrowserPumpAsync`, `RunSequentialBrowserPumpAsync`
   - These are WASM/sequential paths not reachable via CLI tests
   - Effort: ~4 hrs (needs WASM test harness). Impact: high but infrastructure-heavy

### Tier 4: Low Effort, Low Usefulness (skip unless bored)

9. **`MotelyUnitTestAnalyzerFilterDesc`** (203 uncovered lines) — entirely zero-coverage legacy analyzer filter. Dead code? Ask nat.

10. **`JamlSchema.g.cs`** (187 uncovered lines) — generated code, coverage here is noise

11. **`DuckDbResultsSeedProvider`** (146 uncovered lines) — requires DuckDB + actual .parquet files, infrastructure-heavy

12. **`JamlDocumentParser.JNode`** (40 uncovered lines) — internal parser node, low-level

13. **`VectorEnum<T>`** (two classes, 0% coverage) — SIMD enum wrapper, only reachable through filter hot paths that are already partially covered

## Key Insights for the Next Agent

- **Use C# object construction** (like `JamlSimdCoverageTests.RunMust/RunShould`), not JAML strings, for clauses that have complex source configs.
- ~~Many JAML discriminator keys don't work for scoring-path tests.~~ **Wrong.** The keys used were invented: the real ones are `planetCard`/`planetCards`, `spectralCard`/`spectralCards`, `tarotCard`/`tarotCards`, all listed in `JamlDiscriminatorRegistry.cs:58-63` and unchanged since JAML was created in `b716793a`. There is no `planet:`, `spectral:`, or `tarot:` and there never was.
- ~~The `*any*` wildcard syntax doesn't work in JAML for joker clauses. Use `IsWildcard = true` in C# instead.~~ **Wrong.** JAML spells it `Any`, matched case-insensitively by `JamlConfigLoader.IsAny` (line 540). `*any*` is YAML *alias* syntax and was never part of this language, so hitting it proved nothing about the engine. Pinned now by `Motely.Tests/JamlWildcardTests.cs`, which also pins that `*any*` keeps being rejected.
- ~~Boss scoring in `should` only works as a standalone clause, not nested inside `and:`/`or:`.~~ **That was a bug, not a limitation — now fixed.** `PrepareRunState` decided whether to cache bosses with a flat `clauses[i] is BossClause` check while the `maxAnte` calculation beside it recursed. A nested boss clause therefore left `CachedBosses` null: an assert in Debug, and in Release (asserts compiled out) a `NullReferenceException`, or an `IndexOutOfRangeException` when nested at a higher ante than a standalone one. Fixed via `GetMaxBossAnte`/`MaxNestedBossAnte`; covered by `Motely.Tests/JamlNestedBossScoringTests.cs`.
- **`mustNot` with voucher clauses** doesn't exclude seeds the way you'd expect — investigate before writing tests.
- **Run coverage with `dotnet test --collect:"XPlat Code Coverage"`**, not just `dotnet test`, to get full instrumentation.
- **The `MotelyFilterCreationContext`** is a `ref struct` — you can construct it directly in tests but it can't be stored in fields.
- **`Coverage.runsettings`** already excludes `Motely/Filters/Native/*.cs` — native filters are intentionally out of scope.

## Coverage Baseline (for tracking)

```
Overall:          74.61% line / 64.06% branch
Motely:           75.2% line / 64.6% branch
Motely.DataLake:  30.7% line / 32.1% branch
Motely.Lsp.Core:  81.6% line / 73.9% branch
Motely.Lsp:       85.1% line / 66.3% branch
```
