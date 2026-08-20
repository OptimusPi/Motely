# Handoff — pre-run rarity and time-to-find

## Read this first: the approved plan is wrong

The plan at `~/.claude/plans/whimsical-wonderiong-what-is-curious-waffle.md` designs an **analytic**
rarity model — `EstimateRarity` per clause family, 29 overrides, four waves. **Do not build it as
the primary path.**

It was chosen on a false premise I introduced and never rechecked:

> "a 1-in-165M filter can't be measured by sampling in any reasonable pre-flight"

That is wrong, because the deliverable is not a number — it is a **verdict**. A short pre-flight
sample produces the verdict for every filter, exactly, with no per-family math:

| Filter | 10s sample (~hundreds of M seeds) | Verdict produced |
|---|---|---|
| 1 in 165M | several real hits | measured rate, real time-to-find |
| 1 in 40T | zero hits | "rarer than 1 in N — you will not find this in 2.25T" |

Both are the actionable answer. The second one is the whole reason the feature exists.

## Why sampling wins outright

- **It measures the real thing.** No binomial approximation over correlated PRNG streams, no
  hypergeometric-as-binomial, no per-family pool arithmetic to get wrong.
- **It cannot go stale.** The analytic model hard-codes engine internals (shop rates, edition
  bands, pool sizes, ante-1 pack quirks). Every engine change silently invalidates it. A sampler
  re-measures whatever the engine currently does.
- **One pass gives both unknowns.** Rarity *and* the machine's seeds/sec fall out of the same run,
  which removes the entire calibration tier-1/tier-2/tier-3 design and the calibration file.
- **Coverage is 100% on day one.** No NaN families, no partial-coverage bound labels, no
  "model: 2/5 clauses" — all of which exist only to manage the analytic model's incompleteness.
- **It already exists.** `Motely.Tests\RarityAndTimeToFindSweepTests.cs` measures exactly this,
  today, correctly (`SearchUntilEnoughHits` `:200`, `RunSequentialSlice` `:223`).

## What to build instead

Lift the sweep harness into `Motely` and run it as a pre-flight:

1. **Sample.** Run the real configured filter over a bounded slice — escalating batches, stop on
   `enoughHits` (8 is what the sweep uses) or a wall-clock budget (~5–10s, make it a flag).
2. **Derive.** `p̂ = matches / searched`; `seedsPerSecond` from the same run. Zero matches gives a
   one-sided bound `p < 1/searched`, which is sufficient and honest.
3. **Report.** Feed both into `JamlRarityReport.Render` — already written, tested, and unchanged
   by this pivot.

### Sampling design points that actually matter

- **Do not sample from batch 0.** Sequential seeds from the start of the space are not a uniform
  sample. Scatter the slices across the batch range, or sample the range the run will actually
  search.
- **Budget by wall-clock, not seed count.** A heavy scalar filter runs at K/s, not M/s; a fixed
  seed budget would take minutes. A fixed time budget self-scales — and a filter slow enough to
  sample poorly also has a long time-to-find, so the weak bound arrives attached to the case where
  it matters least.
- **Report the bound honestly when dry.** Zero hits means "rarer than 1 in {searched}", never
  "impossible" and never an extrapolated number.
- **The sample is not free.** It is wall-clock the user did not ask for. Gate it: skip when the
  space is tiny, allow `--no-estimate`, and make sure `Ctrl-C` during the sample cancels cleanly.

## What is already on disk and worth keeping

| File | Status |
|---|---|
| `Motely\Filters\Jaml\JamlRarityReport.cs` | **Keep — unaffected by the pivot.** Notation, odds math, and `Render`. It takes a probability and a speed; it does not care where they came from. |
| `Motely.Tests\JamlRarityReportTests.cs` | **Keep.** 28 tests, green. Includes the NaN sanitation matrix and regression pins on two real bugs (below). |
| `Motely.Tests\RarityAndTimeToFindSweepTests.cs` | **Edited, NOT yet verified.** Its three private formatters were replaced with calls to `JamlRarityReport`. This edit was made immediately before the handoff and the test run was interrupted — **run it before trusting it.** |
| `~/.claude/plans/whimsical-wonderiong-what-is-curious-waffle.md` | Superseded as a whole. Its CLI/seam/flag analysis is still good (see below). |

### Two real bugs found and fixed in passing

- `EstimateTimeToFind` used `TimeSpan.ToString(@"hh\:mm\:ss")`, whose `hh` is the hours-*within-day*
  component. Every sweep row taking longer than a day printed a number up to 24× too small,
  silently. `JamlRarityReport.Duration` fixes it.
- `Humanize` stopped at `B`, so 40 trillion rendered as `40200B`. Now runs to `T` and scientific.

### Two real bugs found and NOT yet fixed

- **`Program.cs:528-531` writes `Cost:` to stdout, ungated by `--quiet`.** So
  `--jaml x -q > seeds.txt` puts a cost line in the seed file. Worth fixing regardless of which
  design ships. Grepped: that write site is the only occurrence of the string in the repo.
- **There is no `.git` directory in `D:\MotelyJAML`.** `.gitignore`, `.gitattributes`, `.gitmodules`
  and `.github/` are all present; `.git` is gone. Origin is
  `https://github.com/OptimusPi/MotelyJAML.git`, so history is recoverable. **Nothing in this
  session is under version control.**

## Still-valid analysis from the superseded plan

These were verified against source and survive the pivot:

- **Print seam: `Program.cs:675-680`** — the one choke point after mode/space/deck/stake/threads are
  final and before all five `Start()` sites. `Program.cs:528` is too early (space unknown).
- **`--collect` hazard**: the collect branches at `:719-814` *rewrite* the settings via
  `MotelySearchIntent.ApplyTo`, so a space captured earlier is stale exactly when `--collect` is
  active. Hoist collect parsing above the seam.
- **Search space is 35^8 = 2,251,875,390,625.** Not the 2,318,107,019,761 in `SeedMath.cs:22` —
  that is the global bijective offset including shorter seeds.
- **`--estimate` flag** (print the block and exit without searching) is a good idea and becomes
  *more* useful under sampling, since it is then a real measurement on demand.
- The odds constants inventory in the plan is accurate and stays useful for documentation, even
  though the model that needed it is not being built.

## Verified corrections to note

If anyone revives analytic estimation for any reason, these were wrong in my briefs and are right
here (all re-verified against source):

- Edition bands are **ordered and disjoint** (`MotelySingleSearchContext.Jokers.cs:401-411`).
  At `editionRate=1`: Negative 0.003, **Polychrome 0.003** (not 0.006 — Negative eats its top
  band), Holographic 0.014, Foil 0.02, None 0.96.
- `startingDraw` is `8 × |Antes|` trials, not `|Antes|` (`JamlScoring.cs:1116-1142`).
- `pokerHand` is best-5-of-8, wildly non-uniform — **not** 1/9 (`JamlScoring.cs:1148-1176`).
- `erraticRank`/`erraticSuit` **ignore `Antes` entirely** — 52 with-replacement draws
  (`JamlScoring.cs:1178-1189`).
- Ante-1 pack slot 0 is a deterministic Buffoon Normal, no PRNG draw (`Packs.cs:14, 26-34`).
- Shop standard rate 4 comes from **MagicTrick**, not Wasteful (`Shop.cs:113-116`).

## Immediate next steps

1. Run `dotnet test Motely.Tests/Motely.Tests.csproj --filter "FullyQualifiedName~RarityAndTimeToFind"`
   — the sweep edit is unverified.
2. Restore git before anything else lands.
3. Build the sampler; keep `JamlRarityReport` as the presentation layer unchanged.
