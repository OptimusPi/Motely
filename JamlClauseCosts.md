# JAML Clause Cost Model — fastest to slowest

*A SIMD-crunch investigation of every JAML clause in `Motely/Filters/Jaml/{Events,AnteCards,AnteFeatures}`.
All counts verified against source with file:line references. Vector batch = 8 seeds per `Filter()` call.*

---

## The unit: one **crunch**

The right thing to count is not "operations" — it's **PRNG pulls**, because Motely (faithfully to
Balatro) **re-seeds a complete LuaRandom generator on every single random draw**. Balatro's Lua does
`pseudorandom(pseudoseed(key))` per draw; Motely's vector twin does the same:

- **1 PULL** (`GetNextRandom` / `GetNextRandomInt`, `MotelyVectorSearchContext.cs:420-437`) =
  `IteratePRNG` (mul, add, floor, FMA-round, **1 vector divide**, `:346-365`) +
  pseudoseed combine (**1 more divide**, `:395-397`) +
  a **full `VectorLuaRandom` re-init**: 4 states × ~11 tempering rounds ≈ **~230 bitwise/arith
  Vector512 ops** (`VectorLuaRandom.cs:130-220`).
  ≈ **2 vector divisions + ~230 cheap vector ops, for 8 seeds at once.** That is **1 crunch**.

- **1 STREAM** (`CreatePrngStream(key)`) = pseudohash of the key: **1 vector divide per character**
  plus ~5 cheap ops per character (`MotelyVectorSearchContext.cs:322-337`).
  A typical 6–12 char key ≈ **1–2 crunches**. Paid once per batch per stream, not per pull.

- **1 RESAMPLE iteration** = a fresh STREAM with `key + "_resample" + N` (~11 extra chars) + 1 PULL
  ≈ **3–4 crunches** (`:476-482`). Hard-capped at 64 (`:168`). Typically 0–2 happen.

- **Scalar fallback** (`SearchIndividualSeeds`, `:204-262`) = drops out of SIMD and replays each
  surviving lane one seed at a time. One scalar pull does 1 seed in roughly the time a vector pull
  does 8. **Effective penalty: up to 8×.** This is the single biggest cost lever in the whole DSL —
  bigger than any pull count.

- **Composition is free**: chaining must-clauses ANDs masks (~1 vector op), `mustNot` is `~mask`
  (`NegationFilterDesc.cs:27`). The OR/AND intuition is exactly right: `|=` / `&=`, one op each.

**Key fact:** masked overloads do **not** save arithmetic. `GetNextRandom(ref stream, mask)` runs the
full pull on all 8 lanes and `ConditionalSelect`s the state (`:380-390`). Dead lanes cost the same as
live ones. Killing lanes only pays off at *filter boundaries* (a later filter can skip entirely when
the mask is all-false) — which is exactly why clause ordering matters.

---

## The ranking — fastest → slowest

Crunches are per 8-seed batch. `A` = antes in the clause, `S` = shop slots (default 8),
`P` = pack slots (default 6), `R` = `max(rolls)+1`.

| # | Tier | Clause(s) | Mode | Cost (typical) | Why |
|---|------|-----------|------|----------------|-----|
| 1 | 🟢 free | `and` / `or` / `mustNot` wrapper | SIMD | ~1 vector op | mask `&`, `\|`, `~` |
| 2 | 🟢 trivial | **Events** (except Wheel of Fortune): glassDestroy, spaceLevelup, wheelStaysFlipped, parkingPayout, businessPayout, misprintMult, cavendishExtinct, luckyMult, bloodstoneTrigger, grosMichelExtinct, luckyMoney | SIMD | **R + ~1.5** ≈ 2–9 total (flat — NOT ante-scaled) | 1 short stream + 1 pull per roll index. No scalar, no allocs. Within the tier, key length orders them: glass/space/wheel (5 chars) → grosMichel/luckyMoney (11). `Events/*.cs` |
| 3 | 🟢 trivial+ | **wheelOfFortune** | SIMD | **3R + ~3** ≈ 6–27 | 3 pulls per spin (success + joker pick + edition poll, `Misc.cs:57-65`) and the longest key in the DSL (16 chars). ~3× any other event |
| 4 | 🟢 cheap | **tag** | SIMD | **~(R + 2) × A**, +resamples on ante 1 only (`Tags.cs:44-75`) | per-ante stream `"Tag"+ante` (cached), 1 pull per draw index |
| 5 | 🟢 cheap | **voucher** | SIMD | **~2–4 × maxAnte** (walks antes 1→maxAnte *always*) | stateful: asking about ante 8 costs the full 8-ante walk (`VoucherFilterDesc.cs:61-86`). Resamples for prereq/unlocked vouchers, memoized |
| 6 | 🟢 cheap, flat | **erraticRank / erraticSuit** | SIMD | **≤52** flat, early-exits when all lanes satisfied (`ErraticRankFilterDesc.cs:48-51`) | one deck-global cached stream, fixed 52-card loop, ante-independent |
| 7 | 🟡 moderate | **legendaryJoker edition prefilter** (`LegendarySoulEditionFilterDesc` / auto path when `edition` + `min: 1`) | SIMD | **~8–12 × A** | reads the `"Joker4"` soul stream directly — 6-char, **ante-invariant, cacheable** key + ~6 fixed-rarity pulls/ante (`LegendarySoulEditionPrefilter.cs:39-48`) |
| 8 | 🟠 heavy | **planet / tarotCard / spectralCard (normal) / joker common / rare / non-legendary `joker`** | SIMD | **~90–130 × A** | shop stream setup alone is ~10–12 streams (`Shop.cs:41`), then `GetNextShopItem` ≈ **8 pulls per slot, unconditionally** (tarot+planet polled even at rate 0, `Shop.cs:87`) × S, + P pack pulls + pack contents (~6–10/pack). **No mid-ante early exit** — all antes × all slots run even after the clause is satisfied |
| 9 | 🟠 heavier | **uncommon joker** | SIMD | **~100–200 × A** | tier-8 plus up to 4 extra raw-rarity shop-joker streams walked per ante (`UncommonJokerFilterDesc.cs:162-310`) |
| 10 | 🔴 scalar | **boss** | 8× scalar | ~8 lanes × maxAnte pulls | entirely `SearchIndividualSeeds` (`BossFilterDesc.cs:48`); stateful pool pruning forces computing antes 1→maxAnte |
| 11 | 🔴 scalar | **startingDraw** | 8× scalar | ~8 × A × (1 reseed + 51 draws) **+ a `MotelyItem[52]` heap alloc per ante per lane** (`StartingDrawFilterDesc.cs:43-49`) | also: shuffle key `"nr1"` is ante-independent → same deck order every ante (likely bug, `:51`) |
| 12 | 🔴 scalar | **legendaryJoker (no edition, or min > 1)** | 8× scalar | ~8 × A × 10–30 | the "immolate way": walk packs, check The Soul, then roll the joker (`LegendarySoulMatcher.cs:27-139`). See case study below |
| 13 | 🔴 scalar-tail | **spectralCard: theSoul / blackHole** (SpecialSpectral) | SIMD narrow → scalar count | cheap vector pack-type narrow, then 8× scalar on survivors (`SpecialSpectralCardFilterDesc.cs:58-77`) | cost ∝ survivor rate; good two-phase design |
| 14 | 🔴🔴 slowest | **standardCard** (and spectral with `requireMegaPack`) | 8× scalar, always | **~8 × A × 70–100 ≈ 550–800 × A** | the full shop+pack walk of tier 8, per lane, with zero SIMD (`StandardCardFilterDesc.cs:78`). The slowest clause in the DSL by roughly an order of magnitude |

Rule of thumb for humans: **events ≪ tag/voucher/erratic ≪ soul-edition prefilter ≪ shop/pack SIMD family ≪ anything scalar ≪ standardCard.**

---

## Case study: negative legendary jokers — the instinct was right

Two ways to ask "does this seed have a Negative Perkeo":

**The immolate way** (what `LegendaryJokerFilterDesc` does today for the general case):
per lane, per ante: pack stream + `GetNextBoosterPack` × P + soul-check streams + 3–5 soul pulls per
Arcana/Spectral pack + legendary roll on hit ≈ **5–12 pulls + 3–4 streams per lane per ante, scalar**
(`LegendarySoulMatcher.cs:40-128`). Across 8 lanes that's ~100+ crunch-equivalents per ante.

**The stream way**: `CreateLegendaryJokerStream` reads `"Joker4"` — a 6-character, **ante-invariant**
key — plus the `"edisou"+ante` edition stream. **1–2 pulls total, fully SIMD**
(`Joker.cs:170`, `LegendarySoulEditionPrefilter.cs:46-48`). Roughly **5–10× cheaper per ante before
the 8× scalar penalty even enters the math**.

The caveat (and why both exist): the stream only answers *"IF a Soul appears, which legendary and
what edition would it be"* — it doesn't prove a Soul actually spawns in a reachable pack. But
negative legendaries are so rare that the stream is a near-perfect **prefilter**: it kills ~all
lanes for pennies, and the expensive pack walk runs only on the survivors.

The code already knows this — `LegendaryJokerFilterDesc.cs:81-90` engages the vector prefilter —
**but only when `edition` is set AND `min == 1`**. Every other legendary search goes straight to the
8× scalar pack walk on all lanes. Widening that gate (any edition-constrained search, and a
plain-rarity variant of the prefilter for edition-less searches) is the single biggest speedup
available in the AnteCards folder.

---

## The metric — and why the JAML author never sees it

**Metric: estimated crunches per 8-seed batch**, computed by the engine from the clause config:

```
cost(clause) = scalarPenalty × Σ_antes ( streams×streamCost + pulls + resampleRisk )
```

Every term is statically known the moment the clause is parsed: antes count, `max(rolls)`, shop/pack
slot counts come from the config; pulls-per-call and scalar-path predicates are fixed properties of
each FilterDesc (the tables above). No profiling, no benchmarks, no user knobs.

**Why crunches and not wall-clock or op counts:** pulls dominate (each is a full RNG reseed),
divisions dominate within a pull, and the scalar/SIMD split dominates everything — three facts that
a single integer captures well enough to *sort* by, which is all we need.

**Why the author must never be asked to order clauses:** the JAML author is possibly a gamer who has
never seen a profiler and shouldn't have to. JAML is declarative — *what* to find, never *how*.
So the cost lives in the engine:

1. **Auto-order the must chain.** `JamlSearchBuilder.CreateSettings` currently chains `config.Must`
   **in author order** (`JamlSearchBuilder.cs:51-57`) — a `standardCard` clause written first runs
   its 8× scalar walk on every seed before a 3-crunch event clause gets a chance to kill the lane.
   Sort by estimated cost ascending before `WithAdditionalFilter` and cheap SIMD clauses prune lanes
   before expensive scalar ones ever run. Semantics are unchanged: must-clauses are pure AND.
2. **Home for the number:** `JamlClauseAttribute` is already the declared source of truth for
   clause metadata/vocab generation. Add a base cost tier there, with the instance-level estimate
   computed from config by a small `CostModel.Estimate(IJamlClause)` — attribute gives the shape,
   config gives the multipliers.
3. **v2, if wanted:** sort by `cost ÷ selectivity` (a 2-crunch event that kills 50% of lanes is
   worth less up front than a 10-crunch clause that kills 99.9%). Selectivity is also statically
   estimable — event chance, pool sizes, edition rarity. Cost-only sorting captures most of the win
   because the cost spread (3 → 800) is far wider than the selectivity spread.
4. **Optional UX cherry:** a one-line CLI hint — *"heads-up: `standardCard` is the slow part of
   this filter"* — teaches without demanding.

The same ordering also pays off twice: `JamlShouldScoreDesc` re-verifies every must clause scalar
per surviving seed with early-`return false` on the first miss (`JamlShouldScoreDesc.cs:100-110`) —
cheapest-first saves there too.

---

## Bonus findings (free speed lying on the ground)

1. **Every JAML must-clause pays a hidden pseudohash tax.** The partial-seed-hash cache is bypassed
   whenever `IsAdditionalFilter` (`MotelyVectorSearchContext.cs:272`) — and *every* JAML clause is an
   additional filter (the chain seeds with `PassthroughFilterDesc`, `JamlSearchBuilder.cs:47`). So
   every stream creation in every clause re-hashes the seed characters instead of hitting the
   length-keyed cache. Stream-heavy clauses (shop family: ~10–12 streams/ante) pay it worst.
2. **`MultiVoucherFilterDesc` exists but is never emitted** (`JamlSearchBuilder.cs:81`) — N voucher
   clauses each redo the full stateful ante 1→maxAnte walk instead of sharing one.
3. **No mid-ante early exit in the shop/pack family** — clause satisfied at shop slot 0 of ante 1
   still walks all S slots × P packs × A antes (`CommonJokerFilterDesc.cs:205-210` compares only at
   the end). Events and erratic already do this right.
4. **`GetNextShopItem` pulls tarot + planet unconditionally** (~8 pulls/slot) even when the shop
   rates make them impossible (`Shop.cs:87`).
5. **`startingDraw` shuffle key `"nr1"` never varies by ante** (`StartingDrawFilterDesc.cs:51`) —
   every ante deals the identical deck order. Probable correctness bug, not just a perf note.
6. **`GetNextSpectralPackContentsPerLane` always iterates max pack size (4)** and masks the excess,
   regardless of actual size (`Spectrals.cs:234`).
7. **Event cost scales with `max(rolls)`, not `len(rolls)`** — asking about roll 40 alone costs 41
   pulls because the stream must be advanced through every index (`Events/*.cs`, shared loop shape).
   Worth a docs note in the JAML/JUMMY reference so authors don't ask for roll 500 casually.
8. **THE RECORD — runtime guards where debug assertions belong, a cost class this sweep MISSED.**
   The four counting agents measured PRNG work only; they did not measure guard overhead, and it
   exists. Specimen: `MotelyRunState`/`MotelyVectorRunState` had *explicit static constructors*
   throwing `UnreachableException` on enum-width invariants ("does the voucher enum fit in an int")
   — a compile-time-true fact asserted at runtime, forever, in Release. The two compares are cheap;
   the damage is structural: an explicit cctor strips `beforefieldinit`, so every static-field
   access (`ResetFinisherBosses`/`ResetNormalBosses`, called in the per-ante boss walk) carries
   class-init guard checks unless AOT preinit rescues it. **The rule (positive prose): invariants
   live in `Debug.Assert` (free in Release) or tests; static data lives in field initializers or
   consts; runtime guard overhead budget is 0.** This codebase already uses `Debug.Assert` as
   convention — the throws were bot-added guard-goop ("looks responsible") in work clothes.
   Caught and excised by pifreak, 2026-07-01. Agents: measure this class next time.

---

## Methodology

Four parallel source sweeps: (1) shared `MotelyVectorSearchContext.*` partials — pulls per context
call; (2) `Events/` — 12 FilterDescs; (3) `AnteCards/` — 15 files incl. the legendary path;
(4) `AnteFeatures/` — 4 FilterDescs plus chain assembly in `JamlSearchBuilder`. Counts are static
reads of loop bounds and call sites, cross-referenced against `MotelyPrngKeys.cs` for key lengths and
`VectorLuaRandom.cs` / `MotelyVectorSearchContext.cs` for per-primitive arithmetic. Numbers marked
"typical" assume default config (S=8, P=6) and 0–2 resamples.
