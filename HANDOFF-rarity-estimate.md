# Handoff — pre-run rarity and time-to-find

## State (2026-08-20, end of day)

Rarity is analytic, instant, and validated against the engine, end to end:

```
clause → desc.EstimateRarity(clause, ctx) → JamlRarityEstimator → JamlRarityReport → --estimate
```

```
$ ... --jaml Whimsy_Dicetricks314 --estimate
  Cost:  ~0.6 crunches/seed (5 per 8-seed batch, worst case)
  Rare:  ~1 in 127.88K  (model: 3/3 clauses)
  Space: 2.25T seeds — full sequential sweep
  Odds:  >99.9% at least one here — expect ~17.61M
  Find:  unknown until a run has been timed on this machine
```

Hand check of that number: NegativeTag@4 = 1/24 · Perkeo@1 ≈ 0.00289 (four weighted pack slots,
The Soul 0.003/card, 1 of 5) · Showman@1–3 ≈ 0.0648 (24 shop slots, 20/28 · 0.25 · 1/64) → 1 in 127.9K.
The run that measured 1 in 75.8M was gated by the score cutoff as well, so must-only must come out
more common than that; it does.

## Coverage — 28 of 29 families

| tier | families | how |
|---|---|---|
| events (12) | luckyMoney … wheelStaysFlipped | `JamlRollRarity.Window` — binomial over distinct rolls at the game's constant |
| ante features | tag, voucher, boss, boosterPack | pool sizes read off the engine: ante-1 tag re-roll list, 16 base vouchers, boss eligibility + no-repeat, pack weights, ante-1 fixed Buffoon |
| jokers | joker, common/uncommon/rare, legendaryJoker | `JamlJokerRarity` — shop weight 20/total, rarity poll 0.7/0.25/0.05, pool, edition bands, stake-gated stickers, buffoon packs, specialty streams; soul path 0.003/card then 1 of 5 |
| consumables | tarotCard, planetCard, spectralCard, standardCard | shop weights by deck, pack cards with the soul/black-hole diversion, Emperor/purple-seal/sixth-sense/séance rolls |
| deck | erraticRank, erraticSuit, startingDraw | Binomial(52, ·) with replacement; hypergeometric 8 of 52 |
| **unmodelled** | **pokerHand** | best-5-of-8 needs an enumerated table — honest NaN |

Also NaN by design (state this model does not follow): `charmTag`, `etherealTag`, `omenGlobe`
sources, and the `certificate`/`incantation`/`familiar`/`grim`/`deckDraw` standard-card sources.
A clause using one is reported as "no model yet for: …" rather than undercounted — the report's
"rarer than 1 in N" wording depends on every *modelled* clause counting every source the runtime
counts.

## Where the code is

| file | what |
|---|---|
| `Motely/Filters/Jaml/JamlRarityContext.cs` | deck + stake, and the shop item-type rates they imply (mirrors `CreateShopItemStream` with the deck's default run state) |
| `Motely/Filters/Jaml/JamlCountDistribution.cs` | the pmf toolkit: Bernoulli, Binomial, Hypergeometric, Mixture, Convolve, Window (Window mirrors `MeetsOccurrenceBounds`: Max ≤ 0 = no ceiling) |
| `Motely/Filters/Jaml/JamlPoolRarity.cs` | shared odds: edition bands, sticker gates, pack weights/card counts, ante-1 slot rules |
| `Motely/Filters/Jaml/JamlJokerRarity.cs` | the five joker families incl. the soul path and the `joker:`→legendary split |
| `Motely/Filters/Jaml/JamlRollRarity.cs` | the twelve events |
| `Motely/Filters/Jaml/JamlRarityEstimator.cs` | composes must × mustNot, and/or groups |
| `Motely/Filters/Jaml/JamlRarityReport.cs` | the printed block |
| each `*FilterDesc.cs` | `EstimateRarity(clause, in ctx)` — the family states its own pool/sources; maths lives in the helpers |

Engine visibility widened to `internal` so the model reads the engine's own constants instead of
copies: `MotelySingleSearchContext.ShopJokerRate`, `.DisallowedAnteOneTags`, `.CanBeEternal`;
`MotelyWeightedPool.Items` / `.Probability` expose the declared pack weights (the native table
inflates the last weight as a guard — never read that).

## Tests

| file | what |
|---|---|
| `Motely.Tests/JamlRollRarityTests.cs` | events, hand values |
| `Motely.Tests/JamlPoolRarityTests.cs` | every other family, hand values; toolkit; coverage (every discriminator but `pokerHand` and `and`/`or` is modelled) |
| `Motely.Tests/JamlRarityEstimatorTests.cs` | composition: product, mustNot inversion, unmodelled-is-skipped, at-least-N |
| `Motely.Tests/JamlRarityReportTests.cs` | rendering |
| `Motely.Tests/JamlRarityValidationTests.cs` | **the oracle** — 26 clauses run through the real engine over 3×35³ seeds, analytic within 1.5× of measured (all landed within 5%; the modelled-impossible rows found zero). ~25 s. |

## Things the model surfaced that are engine behaviour, not model choices

- **A shop-only `standardCard:` can never match.** Shop playing cards exist only under Magic Trick,
  and every scoring path builds its shop stream from `Deck.GetDefaultRunState()`, which no deck
  seeds with it. The default `standardCard:` sources are shop-only, so the default clause prints
  "impossible" — and the engine finds none. Either the default sources should be packs, or shop
  streams should see the run state; that is a design call, not made here.
- **A wildcard `joker:` counts legendaries too** (default six pack slots), even when it asks for
  stickers the soul path never applies. `CountJokerClauseOccurrences` does exactly that; the model
  follows it and `JamlPoolRarityTests.Joker_Wildcard_CountsLegendariesToo` pins it.
- A `spectralCard:` with shop-only sources is impossible off the Ghost deck.

## Still open

1. **`Find:` is always "unknown until a run has been timed"** — `seedsPerSecond: null` at the call
   site in `Program.cs`. Either a sub-second warm-up or reading the live rate off the running
   search; the report already renders a real number. A measured rate to sanity-check against:
   ~61.9M seeds/s on this machine for `Whimsy_Dicetricks314` (release, full threads).
2. `pokerHand` needs an enumerated best-of-8 table.
3. Narrowed sequential ranges report no size (see `Program.cs` around the `JamlSearchSpace`).
4. The prefilter/confirm roll-index divergence noted in the previous handoff
   (`JamlScoring.cs` per-entry `rollIndex` advance vs the vector filters' distinct walk) is untouched.
5. Ante-1 pack slots 4–5 under Hieroglyph/Petroglyph are not followed (understates a clause that
   names them at ante 1 by the small chance that voucher was awarded).

## Known test-suite noise

`SeedMath_BatchAndRangeHelpersUseInclusiveSearchIndices` fails on `master` and predates this work
(batch order is the digit-reversal of lexicographic order; see the previous handoff). Three tests —
`Explain_Antes_AsClauseKeyFromSchema`, `JamlFiltersCorpus_LoadsAllJamlFiles`,
`ToJaml_RoundTripsEveryCorpusFile` — fail under the parallel full run and pass in isolation. A
`testhost` is sometimes left behind after a run and locks the test DLLs; kill it before building.
