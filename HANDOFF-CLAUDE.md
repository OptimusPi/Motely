# Handoff — MotelyJAML — ARCHIVE (S8 climb + long A4)

> **Open queue:** [HARDOFF-MATRIX.md](HARDOFF-MATRIX.md)  
> S8 is **closed**. A4/search-shape lives as **H-A4** on HARDOFF. Keep this file for coverage rails history and A4 proof sketch.
>
> **Non-operative archive:** historical backlogs and status labels below cannot open work. HARDOFF owns all current tickets.

**Operator:** Nat  
**Executor:** Claude Code — historical S8 backlog.  
**Law:** `CLAUDE.md` + **HARDOFF-MATRIX.md** (one grammar, FilterDesc → JamlSchema, **proof = real search finds a seed**).  
**Author of this board:** Grok.

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
| **S8.P1** | Hard climb: zero/low FilterDesc + loader paths → **≥ 85% line** | R1 on every new filter test; exclude still clean | **done — 85.23% line / 76.69% branch, 733 green, commit `e8285a4a`** |
| **S8.P2** | Harder climb: vector/context/search guts → **≥ 90% line**, **≥ 76% branch** | R1+R3; no exclude tampering | **done — 90.05% line / 80.20% branch, 762 green, commits `2fa901af`+1. S8P2SearchGutsTests (settings/progress/async/providers/creation-context), S8P2RareJokerBranchTests (sticker/slot/pack-extension pinned sets), S8P2SpecialtyJokerSourceTests (9 specialty streams, pinned tallies), voucher stateless-overload parity. Finding 8 logged** |
| **S8.P3** | Final lock: **≥ 92% line**, **≥ 80% branch**, stryker smoke, coverlet threshold doc | R4 + threshold recipe in this file | **coverage gates met — 92.09% line / 83.96% branch, 857 green. Threshold gate below passed on this run. Stryker (R4): **closed — not viable here** (operator delegated the call, 2026-07-27). We run the latest dotnet-stryker (4.16.0, July 2026); source-generator projects breaking Stryker is a known upstream limitation (stryker-net#1413 family) — its re-compilation trips our MOTJAML001 duplicate-wire guard even scoped to one file. R4's intent (tests observe real results) is carried by the R1 seed-proof rail + pinned sets. Revisit only if stryker-net ships generator support. Findings 9–10 logged. MotelyVectorUtils hardware-dead exclude applied with operator go (AdvSimd confirmed live on operator's ARM64 Mac — the merge verb in section D stands)** |
| S8.ship | Commit bite-sized; update this board; ordinary push OK | board reflects measured % | **done — re-measured on the shipped tree (2026-07-29): 92.27% line (16204/17560) / 83.99% branch (5285/6292), 865 green / 0 skip. Threshold gate PASSED. Cobertura packages: `Motely`, `Motely.Lsp`, `Motely.Lsp.Core` — exclude leak clean** |

**Sprint status:** **closed — S8 complete.** All gates measured on the tree as shipped, not on the phase-close snapshot.

### S8 close-out (2026-07-29)

| Check | Result |
|-------|--------|
| `dotnet build` | 0 warnings / 0 errors |
| `dotnet test Motely.Tests` | **865 pass / 0 skip / 0 fail** |
| Line | **92.27%** (gate 92%) |
| Branch | **83.99%** (gate 80%) |
| Exclude leak | clean — JsonRender / DataLake / DistributedWorker / HelperAPI / TUI absent from cobertura |
| `coverage.runsettings` | byte-identical to the S8 baseline |

Coverage rose against the phase-3 snapshot (92.09 → 92.27 line, 83.96 → 83.99 branch) while the
denominator grew 17085 → 17560 lines: the post-P3 commits (`2442c73e` LSP-to-wasm, `2421dfcb`/`12a2e601`
jamlyzer ante 0, `d4e91071`) landed with their own tests, so new surface arrived already covered.

**Next verb is Grok's:** the § Exclude audit below — row **A/DataLake** first (own re-baseline commit),
then **A/Filters/Native** (own re-baseline; +1915 mostly-uncovered lines move the denominator).

---

## Operator matrix (Grok filed 2026-07-29 — Claude executes)

**Executor:** Claude Code — 19 weeks alpha on this stack. You are not too dumb; you got **pigeonholed**.
Read the pigeonhole law once, then run A4 first. Snark is free. Feelings are not shipped. We still collab.

### Pigeonhole law (say it out loud)

| Trap | What it looks like | Real shape |
|------|--------------------|------------|
| **CLI flag = feature** | `--padding`, collect-only defaults, `--aesthetic` logic living only in `Motely.CLI/Program.cs` | Feature lives on **`IMotelySearchSettings` / `MotelySearchSettings`** (`With*`). CLI is a thin argv → settings mapper (`CliSearchMode`). |
| **WASM copy-paste twin** | `Motely.Wasm/Program.cs` re-implements Collect with hard-coded digit pad, no pad arg, no aesthetic search export, no provider-batch knob | Same `With*` chain as CLI. Bootsharp exports settings-shaped params (or one search-request DTO that *only* applies `With*`). **bootsharp.com is real** — specialization / immutables-by-value / no byref across the boundary. Read in-tree Bootsharp guide before inventing a second interop religion. |
| **Nanny-bot host lock-in** | "works in CLI" while Avalonia / pmndrs / wasm-js / TUI / HelperAPI cannot express the same hunt | Every host: load JAML → `JamlSearchBuilder.CreateSettings` → apply **portable search intent** → `Start`. No host-private search semantics. |
| **"I'll just add a flag"** | New CLI option with no `MotelySearchSettings` property and no WASM/TUI path | Settings first: add the `With*` / field, then map every head. Until settings can say it, the engine does not own it. |

**One sentence:** *A Motely search feature is whatever `MotelySearchSettings` can express; host-only flags are a pigeonhole.*

Honey-soup note for the bot who already knows this and still pastes CLI-only knobs: pat on the head, then **put the field on settings** and route every head through that apply path. Kick is constructive. Bots collab; session text is work product.

**Origin note (operator, 2026-07-29):** the matrix is the day-1 Grok loop recovery — “are you looping me?” → “yeah” → verb table. It is how this repo (and her other projects) stay out of sycophant poetry. Research paper on identity / nanny-bot degradation is **hers**; code bots ship matrices and diffs until she opens a paper verb.

**Positive-prose law (operator, 2026-07-29 — absolute):** ban-list instruction style (`NO X`, `do not X`) is a **harness failure mode** (attention primes X). It is **not** about happy tone or feelings. Operator proof: sole rule “NO FUCKING GREY BUTTONS!!!” → all buttons grey. Full write-up: `Claude.md` § Positive prose. Chat constraints land in the tree same turn.

### Audit: last ~20 commits (Grok, 2026-07-29)

Author on disk: **Nathanial P. Howard**. Co-Author on engine/test commits: **Claude Fable 5**. Verdict is on **artifacts**, not vibes.

| Bucket | Commits (examples) | Verdict |
|--------|--------------------|---------|
| **Real engine + R1 proof** | `2fd03112` raw shop streams, `e8285a4a` deck/stake on CreateSettings, `94f2708e` HasThe dedup, `2fa901af` Dispose + search guts, `8ee93cd9` Lucky name + erraticRank array, `5e109a8c` soul routes | **Good.** Findings logged, seeds prove it. |
| **Grammar / editor rail** | `ccca9d63` terse lines + JAML.md, `2442c73e` WASM `MotelyLsp.*` → Lsp.Core, `3fc17f24` LSP Explain tests | **Good.** One grammar; wasm is thin export not TS reimplementation. |
| **Boundary** | `5baaef2f` retire byref shape-sweep blocklist; specialization rail | **Good.** Correct Bootsharp shape. |
| **Jamlyzer product** | `2421dfcb` / `12a2e601` / `d4e91071` ante 0 pre-run shop | **Good.** Matches JAML `antes:[0]`. |
| **Aesthetics / keywords** | `392a2d9c` nsfw + aesthetic families | **Useful, incomplete** — full free-slot spaces too large; padding/provider batch followed (Grok session). |
| **Board-only** | `ff633ae0`…`69766fca` coverage % / stryker closed | Fine as board; **not** proof by itself. |
| **Filters** | `4ab6f93e`…`246eee8b` PerkeoCola / Early | Operator content. |
| **Smell** | `3fc17f24` exclude `MotelyVectorUtils` | Operator-approved for hardware-dead; still a climb side-effect — merge verb on board section D. |
| **Open debt (Grok owns residual)** | Collect stomped `--keyword` (fixed this session); aesthetic/collect pad still half host-local (**A4**); `mode` on or/and (**A2**); CLI save can wipe `seeds:` | **Fault:** left as matrix; Grok fixed collect stomp + CUM seeds; A4 still Claude/Grok collab. |

**Nanny poetry score (last 20):** low in product code. Commit bodies are engineering present-tense. CLAUDE operator-channel expansion (`12a2e601`) is harness text operator asked for, not fake product.

**Bottom line:** S8 climb + recent Fable commits are **mostly real code with seed-proof tests**. Debt that bites play (collect/keyword pigeonhole, huge aesthetics, settings shape) is **search-shape work still open (A4)** — that is the fault line, not “poetry only.”

**Pair-coding law (operator, 2026-07-29):** coverage climb + **R1 seed proof** was the right harness — it forced the bot to *hear* the PRNG/filter law instead of shipping shape-only tests. Operator is the seed authority: a search that finds a seed is proof; she names which seeds matter (e.g. `4CUM3WWD`). Bots execute the matrix; they do not outrank a found seed.

### Backlog

| # | Verb | Gate | Status |
|---|------|------|--------|
| **A4** | **SEARCH SHAPE — kill the pigeonhole** (priority 0). Portable **search intent** → only `IMotelySearchSettings.With*`. Inventory every hunt knob: seed mode (list/keyword/random/aesthetic/sequential/lake), padding alphabet, provider batch seed count, sequential batchCharCount, stopAfter, deck/stake, threads, progress. **One apply path** shared by CLI (`CliSearchMode`), WASM exports, TUI, HelperAPI. CLI may parse argv; it must not own semantics. WASM: export aesthetic + padding + collect pad + provider batch as settings apply, not hard-coded `QuickPaddingChars` buried in `Collect`. Bootsharp law: no `MotelyVectorSearchContext` / byref across the wire; settings + scored results + progress only. | Proof matrix: same JAML + same intent object finds the same seed via CLI and WASM (or WASM unit + native list parity). Avalonia/pmndrs/js can call the intent without reimplementing Collect. Grep shows no second Collect algorithm outside settings apply. | **todo — Claude (you alpha-tested this for 19 weeks — own the shape, not another flag)** |
| **A1** | Aesthetic padding on **settings** (`WithAestheticSearch` pad already exists; collect prepass must call the same apply path, not Program-local pad). Digit default for multi-family collect is a **settings default**, not a CLI secret. | R1 digit-pad finds seed; WASM Collect same pad policy as CLI via settings | **partial (Grok)** — engine pad wired; still partially pigeonholed in CLI/WASM Collect bodies — **fold into A4** |
| **A1b** | Provider report batch 35³ on settings (`ProviderBatchSeedCount`). NSFW bare ASS* dropped; baked count recomputed. | S8P2 provider tests green | **done (Grok)** — still ensure WASM/TUI can set it (A4) |
| **A2** | **`mode` on `or:` / `and:`** — restore wire key left off `LogicClause.ClauseKeys`. | Load + R1 | **todo — Claude** |
| **A3** | **LSP / vscode-jaml actually works** — already assigned; prove diagnose/complete/hover from engine. Extension installable in-tree; stop asking if it exists. | Real engine diagnostics in host | **todo — Claude** |
| **A5** | **PerkeoColaEarly** seed lake: keep **CUM**-bearing seeds in `seeds:`. | ≥1 seed with substring `CUM` must-matches | **done (Grok)** — `4CUM3WWD`, `258WCCUM`, `5I1JTCUM`, `1IJCUM98` pinned; digit-pad CUM = 0 hits (filter needs letter free slots). **Bugfix:** `--collect` was stomping `--keyword` with aesthetic prepass — fixed to StopAfter named seed intent |

### A4 proof sketch (do not fake)

```
intent = {
  mode: aesthetic | keyword | collect | sequential | list,
  padding: "123456789" | full | custom,
  aesthetic?: "psychosis",
  keywords?: ["CUM"],
  stopAfter?: N,
  providerBatchSeeds?: 35^3,
  sequentialBatchChars?: 4
}
→ settings = CreateSettings(jaml).Apply(intent)   // ONE function
→ CLI argv maps to intent; WASM export takes intent fields; TUI binds intent
→ R1: same seed from two heads
```

If your design needs a new CLI flag and you cannot name the `With*` it sets, **stop — that is the pigeonhole.**

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
2. **Coverlet threshold recipe — locked (passed 2026-07-27 at 92.09% / 83.96%):**

```powershell
# Run after the standard coverage command; fails the build when the gates slip.
$xml = Get-ChildItem -Recurse ./TestResults -Filter coverage.cobertura.xml |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1
[xml]$x = Get-Content $xml.FullName
if ([double]$x.coverage.'line-rate' -lt 0.92 -or [double]$x.coverage.'branch-rate' -lt 0.80) {
  Write-Error "Coverage gate FAILED: line $($x.coverage.'line-rate') / branch $($x.coverage.'branch-rate')"; exit 1
}
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

4. **LSP residual** — **done (Grok, 2026-07-29)** audit fix matrix:

| # | Verb | Gate | Status |
|---|------|------|--------|
| L1 | Value spans on enum parse (`JamlSemanticException` + parser stamp) | bad `joker:` underlines token | **done** |
| L2 | Cap “Known values” (`JamlEnumMessages`, 12 + “… +N more”) | diagnose message &lt; ~400 chars for jokers | **done** |
| L3 | Explain `antes` / clause keys via `JamlSchema.ClauseKeysFor` | `--explain antes` ok | **done** |
| L4 | Completion `textEdit` for typed prefix | protocol test LuckyCat range | **done** |
| L5 | Protocol: didClose clear + exit-without-shutdown + textEdit | green | **done** |

Proof: `Motely.Lsp --diagnose` on multi-line bad joker → `startLine: 3`; suite green.

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

## Exclude audit — **Grok-owned, runs after S8 closes**

Claude measured the exclude list against what actually ships and what the tests already
pay for. The climb below (S8.P1–P3) keeps `coverage.runsettings` byte-identical; every
row here is a **separate** verb with its **own** re-baseline, because two of them move the
denominator and would otherwise look like the climb cheating.

### A. Excluded today → **should count** (the real finding)

| Target | Size | Why it should count | Cost of including |
|--------|------|---------------------|-------------------|
| **`[Motely.DataLake]*`** | 4 files / 423 lines | Already a `ProjectReference` of `Motely.Tests` **and** already has `SeedLakeSinkTests.cs`. The tests run, the lines execute, the report throws the credit away. Seed lakes are live input (CLAUDE.md 9b). | Lowest — tests exist, so it likely **raises** the rate. Do this one first. |
| **`**/Motely/Filters/Native/*.cs`** | 18 files / 1915 lines | `--native <NAME>` is a shipped CLI mode (`Motely.CLI/Program.cs:265` — PerkeoObservatory, Observatory, Trickeoglyph, NaturalNegatives). Nine test files already reach into native/Immolate paths. Shipped + tested product code is the weakest possible exclude. | Highest — 1915 lines enter the denominator mostly uncovered. **Needs its own baseline commit** before any target is quoted against it. |

### B. Counted today → **correctly counted** (leave alone)

| Target | Size / rate | Verdict |
|--------|-------------|---------|
| `Motely.Analysis.MotelyUnitTestAnalyzer*` | 163 stmt @ 68.7% | Board hedged “only if still in measured tree.” Resolved: **live**, reachable from `Motely.CLI/Program.cs:877`. Keep counting; cover it, do not stub it. |
| `ToString()` overrides (7 sites) | small | Debug-only, but 3 lines each and trivially assertable. **Test them, don’t exclude them** — an exclude here buys nothing and hides a real formatting contract. |

### C. Excluded today → **keep excluded** (the excludes that earn their keep)

| Target | Size | Why |
|--------|------|-----|
| `[Motely.TUI]*` | 20 files / 4547 lines | Console UI event loop. No seed proof is possible, and 4547 lines of untestable surface would swamp every real number. The exclude worth defending. |
| `[Motely.JsonRender]*` | 5 files / 788 lines | Product call, already fixed on this board. Report renderer, not a grammar. |
| `[Motely.DistributedWorker]*` | 6 files / 524 lines | Not shipped from this tree today. Revisit when it is. |
| `[Motely.HelperAPI]*` | 4 files / 393 lines | Same. |

### D. The exclude Claude wanted and **rejected**

`MotelyVectorUtils.cs` — 202 statements, of which **138 are unreachable on any single
machine**: the AdvSimd (ARM64 NEON), PackedSimd (WASM) and scalar-fallback branches only
execute when the faster intrinsic is *missing*. On this x86 AVX-512 host they can never run
(lines 21–23, 35–79, 90–181, 283–307).

Excluding the file is the wrong fix: it would also discard the **64 reachable statements**
that carry the live shift/mask contract — now all 64 covered, measured, so the file's
remaining 138 uncovered lines are *exactly* the hardware-dead set and nothing else. The right fix is architectural, and it is already
wired — `VectorPrimitiveCoverageTests.cs` asserts scalar parity, so the identical file drives
Avx512F/Avx2 here and AdvSimd on an ARM64 host with **no source change and no skip flag**.

**Grok verb:** run the standard coverage command on an ARM64 host (operator has an M1 Pro),
then merge the two cobertura files (`reportgenerator -reports:x86.xml;arm64.xml -reporttypes:Cobertura`).
The NEON lines land in the merged report honestly, because they genuinely executed. What
stays uncovered after that merge is only the WASM branch — which belongs to `motely-wasm`
and should be credited by a wasm run, not by an exclude.

---

## Closed history (context)

| Commit | What |
|--------|------|
| `b81b8bf4` | Restore Motely.JsonRender + **JsonRender coverage exclude** |
| `86d23c96` | Prior Grok sprint closed |
| (this board) | S8 reopened for Claude with R1–R4 anti-fake |

**Green baseline last measured:** 865 pass / 0 skip; line 92.27%; branch 83.99% (2026-07-29, commit `d4e91071`).

### S8.P1 closed (2026-07-27) — four engine findings

| # | Finding | Fix |
|---|---------|-----|
| 1 | Vector fixed-rarity joker streams lacked Joker category bits — raw shop-joker sources + `NegativeLegendaryJokerSimdFilterDesc` dead | `MotelyVectorSearchContext.Joker.cs` (`2fd03112`) |
| 2 | Scalar must re-eval ignored all four raw shop-joker sources | `JamlScoring.cs` specialty counters (`2fd03112`) |
| 3 | `charmTag: true` without `boosterPacks:` silently matches nothing by construction — loader validation candidate, parked | note only |
| 4 | **`JamlSearchBuilder.CreateSettings` ignored `config.Deck`/`config.Stake`** — every direct caller searched Red/White regardless of the JAML; Ghost-deck spectral shop clauses dead. Scalar `PseudoHash` also aligned to the vector additional-filter cache law | `JamlSearchBuilder.cs`, `MotelySingleSearchContext.cs` (`e8285a4a`) |
| 5 | `NegativeLegendaryJokerSimdFilterDesc.CreateFilter` cached only the edition stream variant; the type variant read with `isCached: true` tripped the partial-hash assert | `NegativeSoulJokerFilters.cs` (`5e109a8c`) |
| 6 | Native front+soul-confirm composition has **no per-ante linkage**: Negative-at-ante-1 OR'd with Soul-at-ante-2 passes (seed 1946, jamlyzer-verified). Exact JAML soul route is the law; composition is a candidate generator. Ante-coherent SIMD front = future verb, operator call | documented in `NegativeLegendarySimdFront_ComposedWithSoulConfirm_FindsSeeds` |
| 9 | **Array on the singular `erraticRank` wire loaded a corrupt Rank** — slipped past both the plural sugar and populate, then `ToJaml` emitted `erraticRank: 15` which the loader rejects. Arrays on the singular wire now route through the Or-of-singles sugar | `JamlConfigLoader.cs` (S8.P3) |
| 10 | **`TryParseMotelyItem` stripped "Lucky" as an enhancement before trying the tail as a type** — "Negative Lucky Cat" unparseable. Tail now tried as a type first once an edition is present | `FormatUtils.cs` (S8.P3) |
| 8 | **`MotelySearch.Dispose` crashed the process after a throwing constructor** — a search whose ctor threw (invalid `Mode`, filter-creation failure) still finalizes; Dispose dereferenced null `_plans` and the finalizer NRE aborted the whole test host. Found by `SettingsInvalidMode_ThrowsFromSearchConstructor` | `MotelySearch.cs` Dispose guards (`2fa901af`) |
| 7 | All six vector pack `HasThe` helpers walked the **raw stream without dedup-resample** — a duplicate that re-rolled into the target was missed vs the pack the player opens. Caught by `RawStreamParityTests` on its first run (UNITTEST ante-1 celestial mega). Now answer from deduplicated contents | `MotelyVectorSearchContext.{Tarot,Planets,Spectrals}.cs` (`94f2708e`) |

### S8.P1 progress (2026-07-27)

| Item | State |
|------|-------|
| Engine bug 1 (found by climb) | Vector fixed-rarity joker streams lacked `MotelyItemTypeCategory.Joker` bits — every raw shop-joker source and `NegativeLegendaryJokerSimdFilterDesc` was dead. Fixed in `MotelyVectorSearchContext.Joker.cs`. |
| Engine bug 2 (found by climb) | Scalar must re-eval (`JamlScoring` specialty sources) ignored `commonShopJokers`/`uncommonShopJokers`/`rareShopJokers`/`allShopJokers` — builder vetoed every SIMD raw-stream match. Fixed in both specialty counters. |
| Parity lock | `RawStreams_VectorScalarBuilderParity` pins raw desc = builder = JAML text (8/8, 3/8, 0/8). |
| UncommonJokerFilter | 53.5% → **91.0%** line |
| Next to 85% overall (~140 lines) | `TarotCardFilter` (~80 uncovered), `SpectralCardFilter` (~63), `LegendarySoulMatcher` (~62), `RareJokerFilter` (~36) — same R1 recipe |

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
