# JAML — Jimbo's Ante Markup Language

**The definition. Once and for all.**

This is not a promise of a schema. It is the schema, derived from the one place that
cannot drift: `Motely/Filters/Jaml/JamlClause.cs` → `JamlClauseExtensions.CreateDesc()`.
Every clause in the language must pass through that switch to become a runnable filter.
If a key is not backed by a case in that switch, the engine **cannot** run it — so that
switch, and nothing else, is the registry. Display strings, MCP instructions, TypeScript
types, and any `*spec*` file are downstream and may be wrong. This file tracks the switch.

The truth lives in C# clause classes. JAML's surface syntax is YAML, but JAML is not "just
YAML" — it is this fixed vocabulary of clauses, and the vocabulary is closed.

---

## 0. The core idea: WHAT + WHERE

A JAML clause names two things: the **WHAT** (the item — a joker, tarot, voucher…) and the
**WHERE** (the PRNG stream + slot it comes from). **Source is the heart of PRNG**: the same
item from a different stream is a different RNG event, so the WHERE is not a detail — it is
half the meaning. The discriminator picks a base stream; `sources:` pins the exact slot.

This is why **one JAML file is three things at once**:

- a **filter** — "find seeds WHERE this WHAT appears at this WHERE"
- the **content** — the seed(s) that satisfy it
- an **introspection template** — "in this seed, show me this WHAT at this WHERE"

Same statement, three directions. Introspection walks **every real ante** (the engine is not
capped at the legacy 1–8) and **any** of the engine's PRNG streams (`MotelyPrngKeys` — far
more than the shop stream alone), not antes-1–8 / shop-only like the legacy analyzer.

WHAT + WHERE, exposed for UX (Rule #1): a found seed glows with *where* it is — "Perkeo HERE,
ante 1, soul slot" — not merely *that* it is.

---

## 1. Document

A JAML document is a runtime `JamlConfig` (`Motely/Filters/Jaml/JamlConfig.cs`):

| Field         | Type            | Default       | Notes |
|---------------|-----------------|---------------|-------|
| `id`          | string          | (required)    | identifier |
| `name`        | string          | —             | display name |
| `description` | string          | —             | |
| `author`      | string          | —             | |
| `deck`        | `MotelyDeck`    | `Red`         | |
| `stake`       | `MotelyStake`   | `White`       | |
| `seeds`       | string[]        | `[]`          | seed-list search input |
| `must`        | clause[]        | `[]`          | all required |
| `should`      | clause[]        | `[]`          | scored, optional |
| `mustNot`     | clause[]        | `[]`          | all forbidden |

At least one of `must` / `should` / `mustNot` must be non-empty
(`JamlConfigExtensions.HasAnyClauses`).

---

## 2. Clause = one discriminator + shared props + payload

Every clause is keyed by exactly one **discriminator** (the YAML key). On top of its own
payload it carries the shared props from its base class.

### Shared props — `JamlClause` base
| Prop     | Type   | Default | |
|----------|--------|---------|--|
| `label`  | string | —       | optional name for the clause |
| `antes`  | int[]  | `[]`    | antes 1..8 to search |
| `min`    | int    | `1`     | minimum count to satisfy |
| `max`    | int?   | —       | optional cap |
| `score`  | int    | `0`     | points when matched (for `should`) |
| `sources`| object | varies  | per-clause source config (see §4) |

### Shared props — `RollClause` base (the event clauses)
Note: `RollClause` has **no `antes`** — events search by `rolls`, not antes.
Verified live: `EventFilterUtils.ProcessRollClause` reads `Rolls` (asserts length > 0),
`Luck`, and `Min` in the SIMD hot path.

| Prop    | Type  | Default | Notes |
|---------|-------|---------|-------|
| `label` | string| —       | |
| `rolls` | int[] | `[]`    | **required** (engine asserts length > 0) |
| `luck`  | int   | `1`     | used by all events **except `misprintMult`** |
| `min`   | int   | `1`     | match threshold |
| `max`   | int?  | —       | |
| `score` | int   | `0`     | |

`misprintMult` ignores `luck` and instead takes `value` (`int?`, 0–23; null = match any).

### `luck` — the probability multiplier (verified)

`luck` is the **numerator** of an event's trigger probability; each event divides by its own
denominator: `chance = luck / denominator`. `luck:1` (`Base1x`) = base-game odds.

In-game, `luck` models **Oops! All 6s**, which *doubles ALL listed probabilities* and stacks
— so the reachable numerators are **powers of two** (`2^(number of Oops! All 6s)`). That makes
luck honest to name **by its cause** (how many doublers are active), not by the resulting
per-event odds. Proposed enum — backing int = numerator, so the hot path
`double luck = (int)clause.Luck` is unchanged:

```csharp
public enum MotelyLuck { Base1x = 1, Base2x = 2, Base4x = 4, Base8x = 8, Base16x = 16, Base32x = 32 }
// Base32x = 5 stacked Oops! All 6s. Reachable, and the move for "Cavendish breaks
// instantly" prank seeds: 32/1000 ≈ 3.2% per roll on cavendishExtinct.
```

Per-event denominators (read from `Motely.cs` → `MotelyGlobals`):

| event clause | denom | `chance` at `Base1x` |
|---|---|---|
| `luckyMoney`        | 15   | 1/15 |
| `luckyMult`         | 5    | 1/5  |
| `wheelOfFortune`    | 4    | 1/4  |
| `cavendishExtinct`  | 1000 | 1/1000 |
| `grosMichelExtinct` | 6    | 1/6  |
| `spaceLevelup`      | 4    | 1/4  |
| `businessPayout`    | 2    | 1/2  |
| `bloodstoneTrigger` | 2    | 1/2  |
| `parkingPayout`     | 2    | 1/2  |
| `glassDestroy`      | 4    | 1/4  |
| `wheelStaysFlipped` | 7    | 1/7  |
| `misprintMult`      | —    | uses `value`, not `luck` |

**Footgun, by design (NOT a Motely bug):** the *same* luck value means a *different* resulting
probability per event, because denominators differ — `Base2x` is a guaranteed trigger on a
denom-2 event but ≈never on `cavendishExtinct` (denom 1000). That bluntness is the *joker's*
(Oops! All 6s is a blanket doubler); Motely models it faithfully. Naming luck by **cause**
(`Base2x`) stays honest; naming by resulting odds would lie.

Cross-checked: `wheelStaysFlipped` denom 7 matches the Master Encyclopedia's "The Wheel boss:
1 in 7 face down; Oops! All 6s → 2 in 7." Two independent sources agree.

---

## 3. The discriminators — the closed set

Derived 1:1 from the `CreateDesc()` switch. The wire key is the clause class name minus
`Clause`, camelCased. **The `Card` suffix is real** for tarot/spectral/planet/standard/erratic
— it is in the live class names (`TarotCardClause`, `SpectralCardClause`, `PlanetCardClause`)
and in the desc routing comments (`spectralCard:` routes to `SpecialSpectralCardFilterDesc`).
Bare `tarot` / `spectral` / `planet` are **drift**, not the language.

### Jokers — keyed by source stream

These discriminators are **not interchangeable**: each names a *different PRNG stream* (the
WHERE), even when the joker (the WHAT) is the same. A joker drawn from the rare stream is a
different RNG event than that same joker in the mixed stream — so the clauses find different
seeds, on purpose. (Backed by the separate stream arrays in `JokerSourceConfig`:
`CommonShopJokers`, `UncommonShopJokers`, `RareShopJokers`, `AllShopJokers`.)

| Key              | Clause                | Source stream |
|------------------|-----------------------|---------------|
| `joker`          | `JokerClause`         | the **mixed** all-rarity shop stream — name any joker |
| `commonJoker`    | `CommonJokerClause`   | the common-rarity shop joker stream |
| `uncommonJoker`  | `UncommonJokerClause` | the uncommon-rarity shop joker stream |
| `rareJoker`      | `RareJokerClause`     | the rare-rarity shop joker stream — accepts `Any`, e.g. `rareJoker: Any` + `edition: Negative` = any negative rare joker |
| `legendaryJoker` | `LegendaryJokerClause`| the soul / legendary stream |
| `mixedJoker`     | `MixedJokerClause`    | same stream as `joker:`; use `joker:` |

### Cards (Card suffix is canonical)
| Key            | Clause               | Payload |
|----------------|----------------------|---------|
| `tarotCard`    | `TarotCardClause`    | `MotelyTarotCard[]` (`tarots`) |
| `spectralCard` | `SpectralCardClause` | spectral(s); BlackHole routes to Special desc |
| `planetCard`   | `PlanetCardClause`   | planet(s) |
| `standardCard` | `StandardCardClause` | scalar card id or rank/suit object |

### Other top-level
| Key       | Clause          | Payload |
|-----------|-----------------|---------|
| `voucher` | `VoucherClause` | voucher name(s) |
| `boss`    | `BossClause`    | boss blind |
| `tag`     | `TagClause`     | tag |

### Erratic
| Key           | Clause             |
|---------------|--------------------|
| `erraticRank` | `ErraticRankClause`|
| `erraticSuit` | `ErraticSuitClause`|
| `erraticCard` | `ErraticCardClause`|

### Events (RollClause base — use `rolls` / `luck`)
| Key                 | Clause                    |
|---------------------|---------------------------|
| `luckyMoney`        | `LuckyMoneyClause`        |
| `luckyMult`         | `LuckyMultClause`         |
| `misprintMult`      | `MisprintMultClause`      |
| `wheelOfFortune`    | `WheelOfFortuneClause`    |
| `cavendishExtinct`  | `CavendishExtinctClause`  |
| `grosMichelExtinct` | `GrosMichelExtinctClause` |
| `spaceLevelup`      | `SpaceLevelupClause`      |
| `businessPayout`    | `BusinessPayoutClause`    |
| `bloodstoneTrigger` | `BloodstoneTriggerClause` |
| `parkingPayout`     | `ParkingPayoutClause`     |
| `glassDestroy`      | `GlassDestroyClause`      |
| `wheelStaysFlipped` | `WheelStaysFlippedClause` |

### Other
| Key            | Clause              |
|----------------|---------------------|
| `startingDraw` | `StartingDrawClause`|

### Logic (nesting)
| Key   | Clause       | Payload |
|-------|--------------|---------|
| `and` | `AndClause`  | `clauses: [...]` — all must match |
| `or`  | `OrClause`   | `clauses: [...]`, `min` — at least `min` match |

Anything not in this table is **not JAML**. The switch's `default` throws
`Unsupported clause type`.

---

## 4. Sources (per-clause `sources:`)

Card/joker clauses carry a typed `Sources` config naming which streams/slots to check
(e.g. `TarotCardSourceConfig`: `shopItems`, `boosterPacks`, `emperor`,
`purpleSealOrEightBall`, `charmTag`). These live in `JamlConfig.cs` as the
`*SourceConfig` types and are the load-bearing runtime model — **not** garbage, not a
duplicate. They are how a clause knows where to look.

---

## 5. What is NOT canonical (and has caused the drift)

- Bare `tarot` / `spectral` / `planet` — drift; the real keys keep `Card`.
- Display strings `"Tarot"`, `"Planet"`, `"Spectral"` in `Describe()` / TUI — these are
  human labels, not discriminators.
- MCP `SERVER_INSTRUCTIONS` vocabulary — downstream documentation; defer to this file.
- Any standalone JSON-schema or `.d.ts` — generate it FROM the `CreateDesc()` switch, or
  it drifts again.

**Rule:** when in doubt, the `CreateDesc()` switch wins. It is the only definition that
the engine itself obeys.

---

## 6. Name normalization at the boundary

Motely keys entities by **ordinal/enum**, not by the game's display strings — which immunizes
it against LocalThunk's source typos. The known one: the legendary joker **Canio** is spelled
`Caino` in Balatro's own source. Motely uses the correct `Canio` and keys legendaries by
position (`MotelyJokerLegendary.Canio = 0`), so the typo cannot bite it internally.

But at any **external seam** — the MCP server, the encyclopedia, a community DB, a screenshot
scraper — Motely emits `Canio` while outside sources may say `Caino`. A naive string-join
silently drops the legend. Add a normalization map (`Caino ↔ Canio`) at the WASM/MCP edge.
