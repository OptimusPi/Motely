---
name: immolate-to-jimmolate
description: >-
  Convert a legacy Immolate OpenCL (.cl) seed filter into the Motely world —
  either declarative JAML (preferred: fast, vectorized) or a Jimmolate scalar
  seed => bool predicate (single-seed, slower, but faithful to imperative .cl
  logic). TRIGGER when the user has an Immolate `.cl` filter and wants it
  running on Motely/JAML, mentions porting/converting legacy or "throwback"
  Immolate filters, says "jimmolate this", or names a classic filter like
  perkeo_observatory, emperor_fool, showman_emperor_fool, cavendish, or analyzer.
---

# Immolate `.cl` → Jimmolate / JAML converter

Old-school Balatro seed hunters still reach for **Immolate** (the OpenCL GPU
searcher) even though it's slow, because they like writing plain code against a
seed. This skill ports an Immolate `.cl` filter onto the **Motely** engine so it
runs in the modern stack (CLI, TUI, and WASM/JAML-IDE) without giving up that
"just code against the seed" feel.

## The three things, and how they relate

- **Immolate `.cl`** — an OpenCL `long filter(instance* inst)` that walks a
  seed's streams (`next_voucher`, `next_joker`, `next_pack`, ...) and returns a
  number: `0` = no match, non-zero = match (often an encoded score).
- **JAML** — declarative YAML filter (`must` / `should` / `mustNot` clauses).
  Runs on Motely's **vectorized SIMD** base filter. Fast. This is the preferred
  target whenever the `.cl` is really just "does this seed contain X at ante N".
- **Jimmolate** — the bridge that runs a scalar `seed => bool` predicate
  (`MotelyIndividualSeedSearcher`) **only on the seeds the base filter let
  through** (`JimmolateFilterDesc` → `ctx.SearchIndividualSeeds`). This is the
  faithful home for imperative `.cl` logic — loops, counting, scoring,
  thresholds — that JAML clauses can't express. Slower (one seed at a time) but
  it's the same procedural experience as Immolate.

## Step 1 — Decide the target: JAML or a Jimmolate predicate

Read the `.cl`'s `filter()` and classify it:

| If the `.cl` does this...                                              | Target |
|------------------------------------------------------------------------|--------|
| Checks for specific items at specific antes, returns 1 on first hit (vouchers, jokers, souls, bosses, tags, pack contents) | **JAML** |
| Counts / scores / loops until a streak breaks / returns an encoded number / has thresholds, cutoffs, or "best ante" logic | **Jimmolate predicate** |
| Prints a full seed dump (it's `analyzer.cl`)                           | Already ported — use the existing **Analyzer** (`AnalyzerUnitTests.cs` / `analyzer.test.mjs`), don't re-port |

Rule of thumb: **a boolean "contains" filter → JAML. A numeric/scoring filter →
Jimmolate.** When unsure, start with JAML; fall back to a predicate the moment
you need a loop or a counter.

## Step 2 — Map the Immolate primitives to Motely

Immolate's `instance*` helpers map onto `MotelySingleSearchContext`
(scalar/Jimmolate) or JAML clause keys (declarative). Use this table:

| Immolate `.cl`                         | JAML clause            | Motely scalar (`MotelySingleSearchContext`) |
|----------------------------------------|------------------------|---------------------------------------------|
| `next_voucher(inst, ante)`             | `voucher:`             | `CreateVoucherStream(ante)` / `GetAnteFirstVoucher` |
| `activate_voucher(inst, v)`            | (implicit in stream)   | voucher stream is stateful — just keep reading it |
| `next_joker(inst, src, ante)`          | `joker:`               | `GetNextJoker(...)` with the matching stream |
| `next_joker(inst, S_Soul, ante)` + `The_Soul` in a pack | `joker:` (soul comes through pack sources) | `CreateLegendaryJokerStream` / Soul stream |
| `next_boss(inst, ante)`                | `boss:`                | `GetBossForAnte(ante)` / `CreateBossStream` |
| `next_tag(inst, ante)`                 | `tag:` / `smallBlindTag:` / `bigBlindTag:` | `CreateTagStream(ante)` + `GetNextTag` |
| `next_pack(inst, ante)` + `pack_info`  | `sources:` on a clause | `CreateBoosterPackStream(ante)` + `GetNextBoosterPack` |
| `arcana_pack(...)`                      | (Arcana source)        | `GetNextArcanaPackContents` |
| `spectral_pack(...)`                    | (Spectral source)      | `GetNextSpectralPackContents` |
| `celestial_pack(...)`                  | (Celestial source)     | `GetNextCelestialPackContents` |
| `buffoon_pack(...)`                    | (Buffoon source)       | `GetNextBuffoonPackContents` / `CreateBuffoonPackJokerStream` |
| `standard_pack(...)`                   | `standardCard:`        | `GetNextStandardPackContents` |
| `next_shop_item(inst, ante)`           | (shop source)          | `CreateShopItemStream(ante)` + `GetNextShopItem` |
| `next_tarot(inst, S_Emperor, ante)`    | `tarot:` (no streaks)  | `CreateArcanaPackTarotStream` / `GetNextEmperorTarots` |
| deck / stake constants                 | top-level `deck:` / `stake:` | search settings |

Naming is **PascalCase** in both worlds (`Telescope`, `Observatory`, `Perkeo`,
`TheEmperor`, `TheFool`, `Showman`).

## Step 3a — Emit JAML (declarative case)

Template:

```yaml
name: <Human name> (Immolate throwback)
deck: <Deck>      # default Red
stake: <Stake>    # default White
must:
  - voucher: <Name>
    antes: [<n>]
  - joker: <Name>
    antes: [<n>, <n>]
```

Use the **generic `joker:`** discriminator (never `commonJoker:` / `rareJoker:`
etc. when handing JAML to the IDE — a rarity mismatch silently matches nothing).
Then **validate and deliver**:

1. `validate_jaml` (structural pre-check)
2. `open_jaml_ide` — hands the filter to the user in JAML-IDE so they can tweak
   values and run the search live. Always deliver JAML this way, not as a chat
   blob.

### Worked example — `perkeo_observatory.cl` → JAML

The `.cl`: Telescope voucher ante 1 → activate → Observatory voucher ante 2 →
find `The_Soul` in Arcana/Spectral packs antes 1–2 → if its joker is Perkeo,
`return 1`. Pure boolean contains → JAML:

```yaml
name: Perkeo Observatory (Immolate throwback)
deck: Red
stake: White
must:
  - voucher: Telescope
    antes: [1]
  - voucher: Observatory
    antes: [2]
  - joker: Perkeo
    antes: [1, 2]
```

## Step 3b — Emit a Jimmolate predicate (imperative / scoring case)

When the `.cl` loops, counts, or returns an encoded score, port it to a scalar
predicate. Two equivalent surfaces — pick by where it needs to run:

**JavaScript / WASM (Node-runnable — see Step 4):**

```js
// Bind BEFORE bootsharp.boot() — Bootsharp snapshots [Import] bindings at boot;
// assigning jimmolateProbe after boot is a silent no-op.
Motely.jimmolateProbe = (seed, deck, stake) => {
    // ...port the .cl filter() body here, returning a boolean...
    return /* match? */;
};
await bootsharp.boot();
Motely.enableJimmolate();
```

**C# (`Motely.Tests` / native):**

```csharp
new JimmolateFilterDesc((ref MotelySingleSearchContext ctx) =>
{
    // ...port the .cl filter() body here using the Step 2 scalar API...
    return /* match? */;
})
// added via settings.WithAdditionalFilter(...) or settings.WithJimmolate()
```

**Boolean-ising a scoring `.cl`:** Immolate filters often `return score*10+ante`.
Jimmolate predicates are boolean, so turn the score into a **threshold**: pick
the cutoff the `.cl` implied (e.g. `showman_emperor_fool.cl` already cuts off
`bestScore < 5`) and `return bestScore >= CUTOFF;`. Expose the cutoff as a const
at the top so the user can tweak it.

### Worked example — `emperor_fool.cl` → Jimmolate predicate

The `.cl` loops every ante, pulls Emperor tarot pairs, counts how long the
`The_Fool` chain runs, and returns `bestScore*10+bestAnte`. Counting + scoring →
**not** JAML. Predicate (JS form):

```js
const CHAIN_CUTOFF = 1; // .cl returned a score; we threshold it. Tweak freely.
Motely.jimmolateProbe = (seed, deck, stake) => {
    let best = 0;
    for (let ante = 1; ante <= 8; ante++) {
        let score = 0;
        // pull Emperor tarot pairs; count while The Fool keeps showing up
        // (use the seed's Emperor tarot stream — GetNextEmperorTarots in C#)
        for (;;) {
            const [a, b] = nextEmperorTarotPair(seed, ante); // helper over the stream
            if (a === "TheFool" || b === "TheFool") score++;
            else break;
        }
        if (score >= best) best = score;
    }
    return best >= CHAIN_CUTOFF;
};
```

The C# port is cleaner because the stream API is first-class: build the Emperor
tarot stream and call `GetNextEmperorTarots(ref stream)` in the loop. The
`showman_emperor_fool.cl` variant adds shop/Buffoon-pack scans for Showman and a
`hasEmperor` gate before counting — same shape, more state, still a predicate.

## Step 4 — Run / verify

- **JAML:** delivered via `open_jaml_ide`; the user runs it in the browser
  (motely-wasm). The IDE's "JAML valid" card is the authoritative verdict.
- **Jimmolate predicate, in Node:** the WASM build is AOT-compiled and runs
  under plain Node — no browser needed. Mirror `Motely.Wasm/tests/jimmolate.test.mjs`
  (it boots `motely-wasm/dist/index.mjs`, binds the probe pre-boot, calls
  `Motely.enableJimmolate()`, then `Motely.runPassthroughListSearch(seeds)` and
  reads matches via `Motely.onSeedMatch.subscribe`). Run with `node --test`.
  See `Motely.Wasm/tests/jimmolate-demo.mjs` for a standalone runnable demo.
- **Jimmolate predicate, in C#:** add an xUnit case alongside
  `JimmolateFilterDescTests.cs` using `MotelySearchSettings<...>` +
  `.WithAdditionalFilter(new JimmolateFilterDesc(...))`.

## Gotchas

- **Bind the JS probe BEFORE `boot()`** — post-boot assignment is a silent no-op
  (Bootsharp `[Import]` rule).
- **Predicate runs only on base survivors**, once per seed — not once per SIMD
  lane. For a pure-predicate port with no base narrowing, use
  `runPassthroughListSearch` so the base filter passes everything and the probe
  does all the culling (the true Immolate experience).
- **Score → boolean:** never lose the `.cl`'s implied cutoff; surface it as a
  tunable constant.
- **`analyzer.cl` is already ported** — it's the Analyzer, not a filter. Don't
  re-implement it as a Jimmolate predicate.
- Prefer JAML whenever the logic is declarative; only drop to a predicate when
  you genuinely need imperative control flow. JAML is dramatically faster.
