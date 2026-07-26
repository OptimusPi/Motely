# Handoff — MotelyJAML (Claude-owned S8 coverage climb)

**Operator:** Nate  
**Executor:** Claude Code — runs the whole S8 backlog top→bottom. No poetry. No phase pick menu.  
**Law:** `CLAUDE.md` (one grammar, FilterDesc → JamlSchema, **proof = real search finds a seed**).  
**Author of this board:** Grok (baseline + rails + anti-fake law). Claude ships the tests.

## Product call (fixed)

| Keep | Park |
|------|------|
| Engine, CLI, tests, WASM, LSP, vscode-jaml | Sibling **jaml-ui** visual thrash |
| **`Motely.JsonRender`** in-tree + **coverage exclude** | Coverage theater without seed proof |
| Shop-only sources law | Deleting JsonRender, force-push, publish |

---

## Fresh baseline (Grok, 2026-07-25)

| Metric | Value | Command |
|--------|-------|---------|
| Tests | **417 pass / 1 skip / 0 fail** | `dotnet test Motely.Tests` |
| **Line** | **79.23%** (13537 / 17085) | coverlet + `coverage.runsettings` |
| **Branch** | **69.79%** (4229 / 6059) | same |
| Packages in report | `Motely`, `Motely.Lsp`, `Motely.Lsp.Core` only | exclude leak check **clean** |
| Exclude file | `coverage.runsettings` **healthy** | JsonRender / DataLake / DistributedWorker / HelperAPI / TUI **out** |

```sh
dotnet test Motely.Tests/Motely.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --settings coverage.runsettings \
  --results-directory ./TestResults/s8 \
  --nologo
```

Parse overall rate from the newest `coverage.cobertura.xml` root attrs (`line-rate`, `branch-rate`).

**Exclude law (do not “fix” by deleting):**

```xml
<Exclude>[Motely.DataLake]*,[Motely.DistributedWorker]*,[Motely.HelperAPI]*,[Motely.JsonRender]*,[Motely.TUI]*</Exclude>
<ExcludeByFile>**/Motely/Filters/Native/*.cs,**/*.g.cs,**/obj/**/*.cs</ExcludeByFile>
```

---

## Anti-fake law (how you stay a CODE bot)

Coverlet line % alone is gameable. This climb uses four stacked rails from .NET practice + Motely law:

| Rail | What it blocks | How |
|------|----------------|-----|
| **R1 Seed proof** | Shape-only / load-only / `Assert.True(true)` | Every new **filter** test calls `ProofSearch.MustMatchAll` / `MustFindOne` / `MustMatchNone` (or equivalent engine list/sequential search that asserts `MatchingSeeds`). Loading JAML alone is not a test. |
| **R2 Differential** | Magic defaults / silent no-ops | Prefer implicit-vs-explicit (see `DefaultFallbackTests`) or known-seed hit vs non-hit. |
| **R3 Parity** | SIMD-only lies | Vector paths that claim correctness need scalar or list-seed parity when the surface exists (`VectorScalarParityTests` family). |
| **R4 Mutation (Phase 3)** | Tests that never observe a return value | `dotnet-stryker` on Motely (scoped). Surviving mutants = rewrite the test. MS docs: mutation testing is how you test the tests. |

### Forbidden test shapes (rewrite as R1)

- Parse/load JAML + `Assert.NotNull` / `Assert.True(loaded)` and stop.
- Schema/discriminator string tables as the only assertion.
- Empty body, always-true predicates, `Assert.Equal(0, 0)`.
- Raising coverage by **removing** exclude entries or wrapping production in `#if false`.
- “Coverage” of `Motely.JsonRender` by deleting its exclude line.

### Required helper

`Motely.Tests/ProofSearch.cs` + smoke in `ProofSearchSmokeTests.cs`.

```csharp
ProofSearch.MustMatchAll(jaml, "KNOWNSEED");
ProofSearch.MustFindOne(jaml);           // sequential StopAfter(1) — real seed
ProofSearch.MustMatchNone(jaml, "NOPE"); // negative path
```

---

## Sprint backlog (Claude executes top→bottom)

| # | Verb | Gate | Status |
|---|------|------|--------|
| S8.0 | Re-read this board + `ProofSearch` + green `dotnet test` | 417+ pass | **ready** |
| **S8.P1** | Hard climb: zero/low FilterDesc + loader paths → **≥ 85% line** | R1 on every new filter test; exclude still clean | **todo** |
| **S8.P2** | Harder climb: vector/context/search guts → **≥ 90% line**, **≥ 76% branch** | R1+R3; no exclude tampering | **todo** |
| **S8.P3** | Final lock: **≥ 92% line**, **≥ 80% branch**, stryker smoke, coverlet threshold doc | R4 + threshold recipe in this file | **todo** |
| S8.ship | Commit bite-sized; update this board; ordinary push OK | board reflects measured % | **todo** |

**Sprint status:** **open — Claude-owned S8**. No inventing a parallel grammar. No jaml-ui detour.

---

## Phase 1 — stupid hard (FilterDesc / JAML → 85% line)

**Target:** line **≥ 85%** (~+986 covered lines from baseline).  
**Timebox mindset:** one FilterDesc family per commit; each commit green.

### Hotspots (lowest first — hit these)

| Priority | File / area | ~line rate | How to test (R1) |
|----------|-------------|------------|------------------|
| P1 | `Filters/Jaml/AnteFeatures/VoucherFilterDesc.cs` | **0%** | JAML voucher must/should + `MustFindOne` / known seed list |
| P1 | `SeedProviders/MotelySeedProviders.cs` | **0% class** | Provider enumeration + list search consumes seeds |
| P1 | `MotelyResultSink.cs` | **0%** | Callback/sink delivery on match |
| P1 | `AnteFeatures/StartingDrawFilterDesc.cs` | ~31% | starting-draw clause + seed proof |
| P1 | `AnteCards/PlanetFilterDesc.cs` | ~40% | planet packs/shop + list seeds |
| P1 | `JamlClauseDescDispatch.cs` | ~41% | dispatch every wire family through load→search |
| P1 | `Common/Rare/Uncommon JokerFilterDesc` | ~46–53% | edition/source branches; shop-only vs pack |
| P1 | `JamlLoaderValueReader.cs` | ~49% | bad values throw; good values change match set |
| P1 | `LegendarySoulMatcher.cs` | ~61% | Soul route + special pack defaults (sources law) |
| P1 | `Tarot/Spectral card FilterDescs` | ~63–70% | pack sources explicit; find seeds |
| P1 | `MisprintMultFilterDesc` / erratic rank-suit | partial | event + deck clauses |

### Phase 1 done checklist

- [ ] `dotnet test` green
- [ ] line ≥ **85%** with **same** `coverage.runsettings` excludes
- [ ] JsonRender still **absent** from cobertura packages
- [ ] Every new test file uses R1 for filter behavior (grep: `ProofSearch` or `MatchingSeeds` / `WithListSearch`)
- [ ] Board updated with measured %

---

## Phase 2 — harder (vector / search guts → 90% line / 76% branch)

**Target:** line **≥ 90%** (~+1840 lines from baseline), branch **≥ 76%**.

### Hotspots

| Priority | File | ~line rate | How (R1+R3) |
|----------|------|------------|-------------|
| P2 | `MotelyVectorUtils.cs` | ~30% | pure vector helpers + parity with scalar where exists |
| P2 | `MotelyVectorSearchContext.cs` (+ Spectrals/Tarot/Planets/Joker/Vouchers) | ~52–72% | JAML that forces those streams; multi-batch list (≥16 seeds) like `ChainedMustClauseSeedTests` |
| P2 | `VectorLuaRandom.cs` / `VectorMask` / `MotelyItemVector` | ~37–56% | deterministic stream keys; order-within-key law |
| P2 | `MotelyFilterCreationContext.cs` | ~56% | creation paths for shop-only vs pack sources |
| P2 | `MotelySearch.cs` | ~58–78% | sequential / list / stop-after / thread counts |
| P2 | `MotelySingleSearchContext.Boss.cs` | ~60% | boss filters + known boss seeds under `filters/boss-*.jaml` |
| P2 | `Analysis/MotelyUnitTestAnalyzer*` | ~70% | only if still in measured tree; no fake analyzer stubs |

### Phase 2 done checklist

- [ ] line ≥ **90%**, branch ≥ **76%**
- [ ] at least one multi-batch (≥16 seeds) vector regression in the new work
- [ ] exclude file unchanged in spirit (JsonRender stays excluded)
- [ ] board updated

---

## Phase 3 — final touches (92% line / 80% branch + anti-fake lock)

**Target:** line **≥ 92%**, branch **≥ 80%**.

### Work

1. **Fill residual gaps** from the latest cobertura (classes &lt; 80% with ≥15 lines).
2. **Lock coverlet threshold recipe** (document in this board after it passes once):

```sh
# After collector XML is produced, enforce with coverlet global tool OR msbuild props.
# Example (adjust assembly path after build):
#   coverlet Motely.Tests/bin/Debug/net10.0/Motely.dll \
#     --target "dotnet" --targetargs "test Motely.Tests --no-build" \
#     --threshold 92 --threshold-type line \
#     --exclude "[Motely.DataLake]*,[Motely.DistributedWorker]*,[Motely.HelperAPI]*,[Motely.JsonRender]*,[Motely.TUI]*"
```

3. **Stryker smoke (R4)** — mutation testing is the standard “test your tests” tool on .NET (MS Learn + Stryker.NET; Stryker targets **dotnet 10**):

```sh
dotnet tool install -g dotnet-stryker   # once
cd Motely.Tests
dotnet stryker
# Scope if needed: --project-file ../Motely/Motely.csproj
# Kill mutants that survive by strengthening R1 assertions (observe MatchingSeeds / returned seed).
```

Accept: mutation score reported; if wall-clock too long, scope to `Filters/Jaml/**` for one report and file the score on this board. **Do not** ship a stryker config that excludes everything.

4. **LSP residual** only if cheap (`Motely.Lsp` branch ~66%) — protocol tests that send real JSON-RPC and assert engine-backed answers (no second grammar table).

### Phase 3 done checklist

- [ ] line ≥ **92%**, branch ≥ **80%**
- [ ] stryker (or scoped stryker) report path noted here
- [ ] threshold command that fails when line &lt; 92% is written above with a real measured pass
- [ ] `dotnet test` still green; exclude leak still clean
- [ ] board status → **closed**

---

## Math cheat sheet (from 79.23% / 17085 lines)

| Goal | Approx new lines that must *execute* |
|------|--------------------------------------|
| 85% | ~986 |
| 90% | ~1840 |
| 92% | ~2182 |

Uncovered pool ≈ 3548 lines — 92% is aggressive but in-pool.

---

## Closed history (context)

| Commit | What |
|--------|------|
| `b81b8bf4` | Restore Motely.JsonRender + **JsonRender coverage exclude** |
| `86d23c96` | Prior Grok sprint closed |
| (this board) | S8 reopened for Claude with R1–R4 anti-fake |

**Green baseline last measured:** 417 pass / 1 skip; line 79.23%; branch 69.79%.

---

## Self-test before claim-done (any phase)

| Check | Pass condition |
|-------|----------------|
| Grammar | No new authoring tables; FilterDesc / JamlSchema only |
| Proof | New filter tests find or reject real seeds via engine |
| Exclude | JsonRender absent from cobertura packages |
| Numbers | Measured with `coverage.runsettings`, not vibes |
| Board | Status row + % updated in this file |

When blocked: one question max. When done with a phase: status table + next phase verb only.
