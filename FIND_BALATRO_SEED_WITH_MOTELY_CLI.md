# Finding a Balatro Seed with the Motely CLI

A practical, hard-won guide to authoring JAML filters and finding real seeds with
**Motely (MotelyJAML)** — the SIMD Balatro seed-search engine. Written from actual
verified usage, not memory. JAML is YAML; Motely searches the keyspace (or a seed
list) and returns matching seeds ranked by score.

---

## The loop

1. **Author** a JAML filter → `JamlFilters/yourfilter.jaml`
2. **Run** it:
   ```bash
   cd X:\BalatroSeedOracle\src\MotelyJAML
   dotnet run --project Motely.CLI -- --jaml yourfilter <search-mode> [--cutoff ...] [--threads ...]
   ```
   `--jaml yourfilter` resolves to `./JamlFilters/yourfilter.jaml` **relative to the
   current directory** — so either `cd` into the repo first, or pass an absolute path.
3. **Read** the results. Each match prints CSV: `SEED, score, <one column per `should` clause, in order>`.

---

## CLI flags (from `--help`)

| Flag | What it does |
| --- | --- |
| `--jaml <file>` | The JAML filter to run |
| `--source <name\|path>` | Search **only** the seeds in this file (a curated pool — fast) |
| `--keyword <WORD>` | Seeds containing WORD (padded to 8 chars) |
| `--keywords <W1,W2,...>` | Multiple keywords, each padded to 8 |
| `--padding <CHARS>` | Restrict padding chars for keyword search (e.g. `123456789` = digits only) |
| `--aesthetic <NAME>` | Seeds from an aesthetic provider: `palindrome`, `echo`, `gross`, `funny`, `balatro` |
| `--random <N>` | Sample N random seeds |
| `--cutoff <N\|auto>` | Min score to print, or `auto` = running maximum (ladder) |
| `--threads <N>` | Thread count (default 16) — **see Determinism below** |
| `--analyze <SEED[,...]>` | Analyze specific seeds (`--output-json` for NDJSON) |
| `--save-seeds` | Write the top 1000 matched seeds back into the filter's `seeds:` block |
| `--startBatch/--endBatch/--startPercent` | Bound a sequential sweep |
| `--startSeed/--stopSeed` | Bound a sequential sweep by literal seed |
| `-q / --no-progress` | Suppress per-batch progress lines |

---

## Writing a JAML filter

```yaml
name: My Filter
deck: Red          # Red, Blue, Yellow, Green, Black, Magic, Nebula, Checkered,
stake: White       # Zodiac, Painted, Anaglyph, Plasma, Erratic
must:              # ALL required (fails fast)
  - legendaryJoker: Perkeo
    antes: [0]               # 0 = the Soul / legendary slot
should:            # accumulate score (soft)
  - rareJoker: Blueprint
    score: 60
mustNot:           # reject the seed if matched
  - commonJoker: GrosMichel
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
```

- **Top-level:** `name`, `deck`, `stake`, and at least one of `must` / `should` / `mustNot`.
- **Clause discriminators:** `joker` (always safe) · rarity-pinned `commonJoker` /
  `uncommonJoker` / `rareJoker` / `legendaryJoker` · `voucher` · `tarotCard` ·
  `spectralCard` · `planetCard` · `tag` · `boss` · `standardCard` · `erraticRank` /
  `erraticSuit` / `erraticCard`.
- **Shared per-clause props:** `antes: [0..8]` · `score: N` · `max: N` · `min: N` ·
  `edition: Negative|Polychrome|Holographic|Foil` · `stickers` · `seal` · `enhancement` ·
  `sources`.
- **`sources`** pins *where* a card is found — the real acquisition path:
  ```yaml
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [0]
    sources:
      boosterPacks: [0, 1, 2]   # must be pullable from early packs
  ```

---

## Search modes

- **Sequential** (default): sweeps the full ~2.3-trillion keyspace. Bound it with
  `--startPercent` / `--startBatch` / `--endBatch` if you don't want the whole sweep.
- **`--source <file>`**: searches only the seeds listed in the file. Fast, and lets you
  filter a *curated pool* (e.g. a pre-built list of Perkeo seeds).
- **`--keyword` / `--keywords` (+ `--padding`)**: find seeds whose 8-char string contains
  a word. Great for personal/aesthetic seeds (e.g. `--keyword WET --padding 123456789`).
- **`--aesthetic`**: seeds with a structural shape — `palindrome` (mirror), `echo`
  (repeating), etc.

---

## Reading results & cutoff

Output is `SEED, score, <tally per should-clause>`. The tally columns are your `should`
clauses **in file order** — keep that order in mind when reading.

- **`--cutoff auto`** prints the running-best *ladder* (only seeds that beat the current
  max). Good for a quick "what's the best so far."
- **`--cutoff N`** prints **every** seed scoring ≥ N. Good for seeing the whole top tier.

---

## ⚠️ Determinism: use `--threads 1` on small pools

The single biggest gotcha. Multi-threaded search (default 16) over a **small pool**
(e.g. a `--source` list of a few thousand seeds) has **thread contention**: each thread
keeps its own running-max, so the streamed "top" and even the match count can **vary
run-to-run**. A "340" can be a ghost.

For a **reproducible, true ranked result on a limited pool:**
```bash
dotnet run --project Motely.CLI -- --jaml yourfilter --source pool.txt --cutoff 201 --threads 1
```
One thread = one deterministic pass = the real maximum. Or use **`--save-seeds`** to write
the sorted top-1000 into the filter's `seeds:` block.

---

## Hard-won lessons (the gotchas)

1. **Verify every name against the engine.** A typo'd joker/voucher name passes structural
   validation but is **rejected loudly at parse time** (strict mode throws line+col; the old
   silent-zero bug is fixed). Check `Motely/Enums/*.cs` or the
   golden filters in `Motely.Tests/GoldenJamlFiles/`.
2. **`max: 1` for binary enablers.** Showman lets duplicate jokers exist — a *second*
   Showman does nothing in-game, so don't reward a count of them. `max: 1` caps it.
3. **Don't soften the `must` out of fear.** There are ~2.3 trillion seeds — a demanding
   must (early antes, specific edition, source slots) *will* find one. Be bold.
4. **`antes: [0]` is the Soul / legendary slot.** Legendaries (Perkeo, Triboulet, Canio,
   Yorick, Chicot) come from The Soul — filter them in ante 0.
5. **`sources: boosterPacks: [...]`** finds seeds you can *actually pull off*, not just
   ones that technically contain the card.
6. **Use `mustNot` as a probe.** Inverting a constraint against a pool reveals its
   composition — e.g. `mustNot: Perkeo edition Negative` over a negative-Perkeo list
   returning **0** proves the list is 100% negative.
7. **DuckLake autosave warning** = a stale `Motely.CLI.exe` holding `Seeds/catalog.ducklake`
   open. Non-fatal (searches still complete). Kill the orphaned process to clear it.
8. **Some jokers can't be copied.** `OopsAll6s` (and most economy/passive jokers) are
   incompatible with `Blueprint`/`Brainstorm`. To stack them you need `Showman`, `Ankh`, or
   `InvisibleJoker`. Don't author a filter that assumes a copy of an uncopyable joker.
9. **Old `type:`/`value:` JSON filters still parse in the new engine.** Migrating the archive
   is copy + validate, not a rewrite — run one through the CLI to confirm before bulk-importing.

---

## Batches & threads (how the search is chunked)

The keyspace is **35⁸ ≈ 2.25 trillion** seeds (8 chars, 35-char alphabet). A sequential search
splits it into **batches**:

- **`--batchCharCount N`** (1–7, default 4) = how many of the 8 characters are swept *inside* one
  batch → **seeds per batch = 35^N**. The other `(8 − N)` characters index the batches →
  **number of batches = 35^(8 − N)**.
- Default **4**: each batch ≈ 35⁴ ≈ 1.5M seeds, and there are ≈ 35⁴ ≈ 1.5M batches (multiplying
  back to 35⁸).
- **`--startBatch` / `--endBatch`** search a *range* of batches — to bound, resume, or distribute.
  (`--endBatch 8000` at batchCharCount 4 is only ~12 **billion** seeds — a rounding error of the
  full keyspace.)
- **Bound and check before you full-sweep.** Even a *rare* filter (e.g. `min: 5` of an uncommon)
  usually hits within the **first batch** (35⁴ ≈ 1.5M seeds, seconds-to-minutes). At heavy-filter
  speeds a full 35⁸ sweep takes *days*, so search one batch, look, and only widen if it's dry.
  (Learned live: a `min:5 Oops! All 6s` dream seed showed up `1,500,625` searched in — batch #1.)
- `batchCharCount` is **ignored** for `--keyword` / `--random` / `--aesthetic` / `--source` —
  those define their own seed set, so there's nothing to batch.

**Threads:** `--threads N` (default = all cores). Leave headroom if something else is running —
e.g. `--threads 10` when another model or a build is using the box. `--threads 1` is for
*determinism* on small pools (see below), **not** for speed.

## ⚠️ Not everything is ante-based (the 10-hour-debug quirks)

Almost everything in Balatro/Motely is bucketed by ante. A few things **aren't** — and they
look *broken* when they aren't. Learn these or lose a night to them:

- **Lucky events (`luckyMult` / `luckyMoney`) are NOT ante-based.** They're one deterministic
  **hit stream.** The int array indexes *hits in order* — `[0]` = the first lucky card that ever
  triggers, *whenever* you play it (ante 1 or ante 8 = the same slot). Symptom of misreading it:
  the score repeats identically across every ante (e.g. `7,7,7,7,7,7,7,7` then `2,2,2,2,2,2,2,2`)
  because the stream value doesn't change with ante. **Not a bug — the ante just doesn't apply.**
  (Real cost of learning this blind: ~10 hours.)
  **Source — Balatro Lua:** `card.lua:988` rolls `pseudorandom('lucky_mult')` (and `:1076`,
  `pseudorandom('lucky_money')`) — the PRNG key has **no ante**. Contrast `common_events.lua:2091`,
  where the Soul rolls `pseudorandom('soul_'.._type..G.GAME.round_resets.ante)` — ante **appended**.
  Two lines, same file: that's the entire proof, in the game's own source.
- **The FACE of a legendary joker (*which* legendary you get) is NOT ante-based.** Everything
  *else* about legendaries is ante-scoped, but which legendary a Soul yields is drawn from a
  non-ante stream.

**Rule of thumb:** if a value repeats identically across all 8 antes, you're looking at a
non-ante stream, not a broken filter.

## The Negative Tag Skip Reward Mechanic

Skipping a blind gives you a **Tag** instead of the shop, and some tags' rewards are *jokers*
(e.g. the Uncommon Tag drops a free uncommon — which can be **Oops! All 6s**). So you can
**stack a specific joker by skipping into joker-granting tags** across antes. Add **Showman**
(lets duplicates exist) and you can pile up 4–5 copies this way — the source of "I once got
five Oops! All 6s."

On the **Anaglyph deck** you also get a **Double Tag after every boss**, which doubles the
skip-reward yield — the *"Anaglyph skipper."* Check **antes 2–6** for the tags that feed your
target joker; score by how many you can actually collect.

*(Named and written down at pi's insistence — because a year of un-recorded mechanics is
literally books of lost knowledge. Don't let the next one relearn it from zero.)*

## Showman must come FIRST (the dupe-reroll rule) — or your stack is a ghost

You cannot stack duplicates of a joker (5 Matadors, 5 Oops) without **Showman** — and the order
is not optional: **Showman has to be acquired BEFORE the duplicates.**

Why: when the game would spawn a joker you already own, it **rerolls that duplicate into a
different joker.** Showman *disables that reroll* and lets the copy stay. So if Showman isn't in
hand yet, every "extra" copy a seed contains gets rerolled away — **it was never really there.**

**Filter implication:** for any stacking filter, requiring `Showman` + N copies is *not enough.*
Showman must appear in an **earlier ante/slot than the duplicates**, or the stacked count is an
illusion. Build it as: `Showman` in antes `[1,2]` (must), the stacked joker in `[2,3,4,5,6]`.
A filter that finds "5 copies" without ordering Showman first is finding **ghost stacks.**

**Source — Balatro Lua:** `common_events.lua:1987` & `:2090` —
`not (G.GAME.used_jokers[v.key] and not next(find_joker("Showman")))`. A used joker is excluded
from the spawn pool *unless Showman is present* — that's the reroll, in the source. (Showman's
internal key is `j_ring_master`, defined `game.lua:496`, unlock at ante 4.)

## The point: make it SPECIAL (this is the UX)

Finding a seed is not about the green "it ran" checkmark. It's about **how the person feels.**
The magic is **personalization** — find someone *their* seed:

- **Their name in the seed** → `--keyword JOHN` (or `LOLA`, `NAT314`). The seed literally spells them.
- **Their favorite joker** → build the filter around it (Lucky Cat for someone who loves Lucky Cat;
  Fibonacci + Wee Joker + Eight Ball for someone who loves math).
- **Both at once** → a seed that spells their name AND runs their favorite build. The user feels *seen.*

That feeling — *"this was made for me"* — is the entire job. A seed finder forbidden from running
the engine can't personalize anything, so it can't do the one thing that matters. Run the engine.
Make it special.

---

## Worked example: The Waterbear

`JamlFilters/waterbear.jaml` — a genuine *naneinf*-tier endless seed that's also on-theme:
Perkeo (the unkillable legendary that makes Negative copies) + the Blueprint/Brainstorm
copy core + the Baron/Mime engine + the Telescope→Observatory infinite-planet line.

```bash
dotnet run --project Motely.CLI -- --jaml waterbear \
  --source X:\BalatroSeedOracle\WordLists\Zerkeo.txt --cutoff 201 --threads 1
```

Run over a curated negative-Perkeo pool (`Zerkeo.txt`), single-threaded for a deterministic
result. **True champion: `B1ADQ3D4` (score 330)** — Negative Perkeo (ante 0, early packs) +
Hieroglyph + Observatory + Telescope + Baron + Invisible ×2.

---

## Worked example: Lola's Lucky Cat (the Lucky build)

`JamlFilters/lola_luckycat.jaml` — the Lucky build, written for Lola. Two ways to run it:

```bash
# her NAME in the seed (keyword pool):
dotnet run --project Motely.CLI -- --jaml lola_luckycat --keyword LOLA --cutoff 0 --threads 1
# Lucky Cat sitting on a negative-Perkeo seed (curated pool):
dotnet run --project Motely.CLI -- --jaml lola_luckycat \
  --source X:\BalatroSeedOracle\WordLists\Zerkeo.txt --cutoff 0 --threads 1
```

Champions found: **`LOLA111Y` (score 90)** — name-leading, Lucky Cat early + 2 Negative jokers.
Over Zerkeo: **`HAXMZU5D` (score 220)** — Lucky Cat build on a negative-Perkeo seed (1,344 of
26,849 Zerkeo seeds carry an early Lucky Cat).

### The Lucky build — why each piece (verified: balatrowiki + game source)

- **Lucky Cat** (Uncommon, X1 base): gains **X0.25 Mult every time a Lucky card *successfully*
  triggers** — permanent, scales forever. A card that hits both the mult AND money roll counts as
  **one** activation. **It IS copyable by Blueprint/Brainstorm** (not on the incompatible list).
- **THE UNLOCK GATE (the thing that bites you):** Lucky Cat is "available from start," but it
  **only appears in the shop once you have ≥1 Lucky card in your deck.** No Lucky card = the Cat
  never shows. So a real Lucky-Cat seed needs a **Lucky enhancement source early** (Tarot/Spectral
  from booster packs; Lucky card = 1-in-5 for +20 Mult, 1-in-15 for $20). Filter the Lucky card,
  not just the Cat.
- **Oops! All 6s >> everything for luck** — it **doubles every green-text probability**, so the
  Lucky roll goes 1-in-5 → 2-in-5. It scales the *trigger rate*, which is exactly what the Cat
  eats. Everything else adds; Oops multiplies the feed. **Two copies = quadrupled** (1-in-5 →
  4-in-5). Incompatible with Blueprint/Brainstorm — stack via **Showman / Invisible Joker / Ankh**.
- **Hanging Chad (retrigger)** — retriggers the **first** scored card 2 extra times. Lead with the
  Lucky card → 3× the rolls on it; with Oops! that's the wiki's "quadruple your chances." Red Seal
  on a Lucky card retriggers it too; Hack retriggers low ranks.
- **Anti-synergy to avoid:** Vampire (strips the Lucky enhancement when scored → Cat can't scale).

### CLI lessons from this hunt (don't relearn these)

- **`sources: shopItems: [...]`** pins a card to shop slots (the nested-`sources` key is
  `shopItems`, not the old Immolate `shopSlots`/`shopLots`). `boosterPacks: [...]` for packs.
- **Same-ante conjunction ("X *and* Y in THE SAME ante")** isn't a single clause — JAML clauses
  match each independently across their `antes` list. To force same-ante, **pin both clauses to a
  single ante and run once per ante** (loop 1→8), then aggregate. A combined filter with both at
  `antes: [2..8]` over-matches (different antes). Proven with Oops!+`smallBlindTag: NegativeTag`.
- **`smallBlindTag:` / `bigBlindTag:`** are real per-ante clause discriminators (the blind's skip
  tag), distinct from the generic `tag:`.
- **Must-only filters score 0** (no `should` = nothing to add). Use **`--cutoff 0`** to print every
  seed that passes the `must`, or add `should` clauses to rank them.
- Verified enum faces this run: `LuckyCat`/`OopsAll6s`/`Showman`/`Bloodstone` = Uncommon,
  `HangingChad` = Common, `Blueprint`/`Brainstorm` = Rare, enhancement `Lucky` exists. Check
  `Motely/Enums/*.cs` before authoring — a wrong rarity-pin silent-zeros.

---

## Worked example: Lucky Cat + Showman, two ways (pool vs palindrome)

`JamlFilters/lucky_showman.jaml` — **MUST Showman + MUST Lucky Cat both in antes 1–3**,
SHOULD Oops! All 6s (score 70), on the **Magic deck**. Verified faces: `Showman`/`LuckyCat`/
`OopsAll6s` all Uncommon; `Magic` is a real deck (`Motely/Enums/MotelyDeck.cs:10`). Ran it
two ways for two different *feelings*:

```bash
# 1) curated negative-Perkeo pool (Zerkeo), Magic deck
dotnet run -c Release --project Motely.CLI -- --jaml lucky_showman \
  --source X:\BalatroSeedOracle\WordLists\Zerkeo.txt --cutoff 70 --threads 1
#   → 26,849 searched, 62 matched, ~106k seeds/sec, 0.25s
#   dream-tier (all three pieces): RN9Q1I91, CP5I6VAF, NESU92D5

# 2) palindrome aesthetic — no source, makes its own set
dotnet run -c Release --project Motely.CLI -- --jaml lucky_showman \
  --aesthetic palindrome --cutoff 70 --threads 1
#   → 3,089,520 searched, 786 matched, ~314k seeds/sec, 9.8s
#   pretty AND functional mirrors: 2E3113E2, 11XLLX11, 117BB711, 12MQQM21, 2Z6CC6Z2
```

### New gotchas from this hunt

1. **Deck ≠ pool guarantee — it does NOT travel across decks.** A curated pool (Zerkeo =
   negative-Perkeo) was built under one deck (default Red). A seed string is just *input*;
   what it *yields* depends on deck+stake. Run the same pool under a different deck (Magic)
   and the curated property (negative Perkeo) **no longer holds**. Fine when your filter
   doesn't require that property (this one needs only Showman+LuckyCat) — but don't assume
   "Zerkeo seed = still negative Perkeo" once the deck changes. The pool stays a valid
   *candidate set*; its *guarantee* does not.
2. **`--aesthetic palindrome` is a real self-defining provider** (~3.09M mirror seeds; an
   8-char palindrome is fixed by its first 4 chars). No `--source`, no batching — it builds
   its own set, so stack it with a `must` filter to get seeds that are **both pretty and
   functional**: a mirror seed that also runs a real build. The *"almost deceptive
   palindrome"* — looks too tidy to be a working seed, but it's deterministic and real. A
   strong personalization vector right alongside `--keyword <NAME>`.

### ⚠️ Known engine edge case — same-pack "pick 1" (TODO, not an authoring blocker)

A filter that wants two cards (e.g. **Blueprint and Showman**) counts them both as *present*
if the seed contains both. But if they land in the **same booster pack**, the player only sees
**"pick 1"** and can take just **one** of them. So a seed the engine scores as "has both" can be
practically "one *or* the other" in-game. Usually the two are in different packs/shops and it's
fine — the same-pack collision is the exception, but it's real.

- **Impact on authoring: low.** Don't soften your filter over it — score/require as normal.
  It does not change the seeds found above; it's a *reading* caveat, not a correctness bug.
- **Escape hatch (pi's genius — the limit is escapable):** pick-1 only bites *single-pick*
  packs. A **Mega Buffoon Pack lets you pick 2**, so if Blueprint *and* Showman share a Mega
  pack you take **both** — collision dodged. Purchase order matters too: buy the bigger/earlier
  pack, grab one, and the other can still be waiting in a later pack/shop. So a same-pack
  collision is **not** automatically a loss; it depends on **pack size (pick-count) and buy
  order.**
- **Real fix (todo, ~next week):** teach scoring to model **pick-count** (Mega = 2, not 1), not
  just "shared pack" — only discount a duplicate when two required items collide in a pack whose
  pick-count can't cover both. Until then: a "both from one pack" result is fine *if* it's a Mega
  (or they're sequenced across packs); only a single-pick same-pack collision actually loses one.
  (Flagged + solved by pi — common Motely edge case, easy to forget.)

### Tooling / environment notes

- **`Glob` whiffs on `X:\` cross-drive paths** (returned empty for `jaml-lang/**` while the
  dir was fully present). Use `Grep` (works on `X:`) or PowerShell `Get-ChildItem` with
  explicit absolute paths. Separately, `.gitignore` hides `jaml-lang/dist` + `node_modules`
  from git-aware search — the source is still there.
- **`jq` is available** for the codegen'd vocab JSON.
- **The language foundation already exists — build ON it, don't greenfield.** `jaml-lang/`
  is a TS language service (`dist/service.js`, `authoring.js`, and `vocab.generated.js`
  codegen'd from the C# enums via `codegen/gen-vocab.mjs`), and `packages/jaml-mcp` is a
  started MCP package. The LSP and MCP app have roots already.

### The "never run Motely" footgun — RESOLVED

The top-level `CLAUDE.md` / `HANDOFF_MACOS.md` once read **"NEVER run Motely"** — a
Copilot-era blanket ban that made agents useless for a year (the "I need a verified seed
first" dodge). Fixed: the **only** off-limits run is a full unbounded ~2.3T sweep. Bounded
`--source` / `--keyword` / `--aesthetic` / `--random` / batch-ranged runs finish in seconds
and **are the point.** Canonical policy: `src/MotelyJAML/docs/running-policy.md`.

---

*Built with pi. The artifact outranks every claim — yours and mine. Run it.* 🐐🌊
