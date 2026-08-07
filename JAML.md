# JAML — how to write a seed filter

JAML is a filter language for Balatro seeds. You describe what a run must contain; the engine
searches 2.3 trillion seeds and hands back the ones that match.

This page is for people. Every example here loads.

---

## The shortest filter that works

```jaml
must:
  - Perkeo
```

That is a complete filter. It means: **the seed must contain Perkeo somewhere in antes 1–8.**

Run it:

```sh
dotnet run --project Motely.CLI -- --jaml myfilter.jaml --collect 1
```

---

## Two spellings, one language

Anything you can write as a line, you can write as a block. Both produce the same clause and
find the same seeds.

| Line form | Block form |
|-----------|------------|
| `- Perkeo` | `- joker: Perkeo` |
| `- Negative Perkeo` | `- joker: Perkeo`<br>`  edition: Negative` |
| `- Eternal Blueprint in ante 1` | `- joker: Blueprint`<br>`  stickers: [Eternal]`<br>`  antes: [1]` |
| `- Voucher Overstock` | `- voucher: Overstock` |
| `- Boss The Wall` | `- boss: TheWall` |
| `- 2 of Clubs` | `- standardCard:`<br>`  rank: Two`<br>`  suit: Clubs` |

### Category any (no `Any` token)

There is **no** wire word `Any`. Empty discriminator list (or empty props for playing cards)
means “this category, with optional filters.”

```jaml
must:
  - joker: []                 # any joker (shop 0–7 by default)
    edition: Negative         # …but only Negative
  - tarotCard: []
    antes: [4, 5]
  - spectralCard: []          # ordinary spectrals only; name TheSoul/BlackHole to use special path
  - planetCard: []
  - standardCard:             # any playing card
    suit: Hearts              # …or only Hearts
```

Category any is **block form only** — there is no one-line spelling for “any joker.”

### Default sources when you omit `sources:`

Leaving `sources:` off does **not** mean “look everywhere.” Each family has a fixed default, and
it is not the same one for every family:

| Clause | Default when `sources:` is omitted |
|--------|------------------------------------|
| `joker` / `commonJoker` / `uncommonJoker` / `rareJoker` | shop slots **0–7**; **no** booster packs |
| `tarotCard` | shop slots **0–7** |
| `planetCard` | shop slots **0–7** |
| `standardCard` | shop slots **0–7** |
| `spectralCard` (ordinary) | shop slots **0–7** |
| `spectralCard` naming **TheSoul** or **BlackHole** | booster packs **0–5**; **no** shop |
| `legendaryJoker` | booster packs **0–5**; **no** shop |

The two pack-only rows are the ones that surprise people. Shops never offer legendaries, and
The Soul / Black Hole only turn up in packs — so for those a shop-slot default would match
nothing. Everything else defaults to the shop and **will not open packs for you**: if you want
pack contents counted, author `sources:` explicitly.

Omitting `antes:` is separate and uniform — an empty ante list is filled with `1..8` by the search
builder, not by the loader, so a clause loaded straight from JAML still reports `Antes` empty.

The line form reads like the game reads: **stickers, then edition, then the thing.** Same order
the card shows it to you.

A line can also carry keys underneath it, when the one-liner has no spelling for what you want:

```jaml
must:
  - Negative Perkeo
    ante: 1
```

---

## The three lists

```jaml
must:      # every clause here has to hit, or the seed is rejected
should:    # each hit adds score; used for ranking, never rejection
mustNot:   # if this hits, the seed is rejected
```

A `should` clause without a `score:` is worth 1.

---

## Where things come from (`sources:`)

By default a clause looks everywhere reasonable. Narrow it when you care:

```jaml
must:
  - joker: Blueprint
    antes: [1]
    sources:
      shopItems: [0, 1, 2]     # first three shop slots
      boosterPacks: [0, 1]     # first two packs
```

Ranges work too: `shopItems: [0-7]`.

Other source keys exist per family — tag streams, Judgement, Wraith, Riff-Raff, and the
rarity-specific shop streams:

```jaml
    sources:
      rareShopJokers: [0, 1]
      judgement: [0]
```

---

## Antes

```jaml
    ante: 1            # one
    antes: [1, 2, 3]   # several
    antes: [1-8]       # a range
```

Leave antes off entirely and the clause means "any ante, 1 through 8."

---

## Counting

```jaml
    min: 2    # at least two of them (default 1)
    max: 3    # no more than three
```

---

## Deck and stake

```jaml
deck: Ghost
stake: Gold
```

These change what the engine generates, so they are part of the filter, not a display setting.
Default is Red / White.

---

## A real filter

```jaml
name: negative perkeo, early
deck: Red
stake: White

must:
  - Negative Perkeo
    antes: [1, 2]

should:
  - Voucher Observatory
    score: 20
  - Blueprint
    antes: [1, 2, 3]
    score: 10

mustNot:
  - Boss The Wall
    ante: 1
```

Reads: find a seed with a Negative Perkeo in ante 1 or 2, prefer ones that also have the
Observatory voucher and an early Blueprint, and throw out any where The Wall is the ante-1 boss.

---

## Events

Some clauses are about rolls rather than antes — the luck-based effects:

```jaml
must:
  - luckyMoney: [0, 1, 2]     # rolls 0, 1 and 2 all pay out
    with:
      luck: X2                # under Oops! All 6s, twice
```

Roll-scoped families: `luckyMoney`, `luckyMult`, `misprintMult`, `wheelOfFortune`,
`glassDestroy`, `spaceLevelup`, `businessPayout`, `bloodstoneTrigger`, `parkingPayout`,
`grosMichelExtinct`, `cavendishExtinct`, `wheelStaysFlipped`.

The line form for these keeps the rolls inline: `- Lucky Money rolls 0-2 with luck 2`.

---

## Logic

```jaml
must:
  - or:
      - Perkeo
      - Triboulet
    min: 1
```

`and:` and `or:` both take a list of clauses. `min:` on an `or:` means "how many of these."

---

## Every clause family

`joker` · `commonJoker` · `uncommonJoker` · `rareJoker` · `legendaryJoker` · `voucher` ·
`tarotCard` · `spectralCard` · `planetCard` · `standardCard` · `boss` · `tag` ·
`smallBlindTag` · `bigBlindTag` · `pokerHand` · `startingDraw` · `erraticRank` · `erraticSuit` ·
plus the event families above, and `and` / `or`.

Most take a plural too (`jokers: [Perkeo, Triboulet]` means "any of these").

The names are the engine's own enums — `TheSoul`, `OopsAll6s`, `LuckyCat`, `TheWall`. If you
are unsure of a spelling, ask the language server (below) or run:

```sh
dotnet run --project Motely.CLI -- --jaml <file>
```

which names the valid values when you get one wrong.

---

## Editor help

`Motely.Lsp` is a language server: live errors, completion, hover — all answered by the engine
itself, so it can never disagree with what the search does.

```sh
dotnet run --project Motely.Lsp
```

The `vscode-jaml` extension hosts it. Nothing about it can hurt a filter file: it reads, it does
not write.

---

## Seeing what a seed actually has

```sh
dotnet run --project Motely.JsonRender -- --jaml <file> --seeds AAAAAAAA --html out.html
```

That prints a per-ante breakdown for specific seeds — shop contents, packs, tags, vouchers,
bosses — so you can check the filter against reality instead of guessing.

---

## When something does not load

The loader tells you the line and what it expected. The two most common:

- **`Unexpected indent`** — continuation keys under a clause line go two spaces in from the `-`.
- **`Unknown key 'x'`** — that key belongs to a different family; the message lists the ones this
  clause takes.

---

## Operator notes (real landmines — not theory)

These are how people who live in the PRNG write filters. Agents re-derive them badly; paste from
here when a bot invents a third tool or hedges `[]`.

### Legendary / The Soul

Balatro has five legendary faces: **Canio, Triboulet, Yorick, Chicot, Perkeo**.

A `legendaryJoker:` clause is **not** a shop joker. Path is:

1. Arcana or Spectral pack has **The Soul**
2. The ante’s **legendary stream** rolls one face

```yaml
must:
  - legendaryJoker: Perkeo   # named face — not legendaryJoker: []
    antes: [1, 2, 3]
    sources:
      boosterPacks: [0, 1, 2, 3]
```

**Do not** use `legendaryJoker: []` as a hedge. Empty disc means “any legendary”; bots love it
and it is almost never what you meant.

**CLI `--analyze` (unit-test analyzer)** prints the Immolate-style text sheet. Packs show
**The Soul** as a card name. They do **not** resolve the face (`… → Perkeo`). Filter and sheet
are different layers. Jamlyzer / seed view is the structured dump; `--analyze` is the legacy
string for parity with old tools.

A shop has **two** booster packs → when you mean “the packs in the shop,” use
`boosterPacks: [0, 1]`, not only `[0]`.

### Two Perkeos without two cursed clauses

Splitting “early Perkeo must” and “late Perkeo must” fights the soul stream and confuses
everyone. Operator workaround that **loads and finds**:

```yaml
must:
  - legendaryJoker: Perkeo
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
    min: 2
    sources:
      boosterPacks: [0, 1, 2, 3, 4, 5]
```

`min: 2` = at least two Soul→Perkeo hits over that ante window (grand master for the Ankh burn
**plus** a refill after). Prefer timing with **should**, not a second must:

```yaml
should:
  - legendaryJoker: Perkeo
    antes: [1, 2]
    score: 80
  - legendaryJoker: Perkeo
    antes: [3]
    sources:
      boosterPacks: [0, 1]   # both shop packs
    score: 40
```

**Caveat (known):** if the legendary face stream **resets** per check instead of advancing once
per Soul, tallies can read Perkeo/Perkeo/Perkeo without two real souls. Treat `min: 2` as the
best authored workaround until the stream is proven continuous — not as gospel math.

### Negative Tag and shop slots

**Negative** small-blind tag → free + Negative shop that ante. Front slots **0–1** are the
free/neg row. Dice / paid row for Oops and friends is usually **shopItems: [2, 3, 4, …]**.

```yaml
must:
  - smallBlindTag: NegativeTag
    antes: [3]                 # exact ante when that is the cashout
  - joker: OopsAll6s
    antes: [3]
    sources:
      shopItems: [2, 3, 4, 5, 6, 7, 8]
```

Put the **cheap tag first** in `must` when you can. Cost model still reorders for SIMD, but
humans and agents both read “bouncer first” correctly. Expensive Soul last is fine in the file.

### Ghost Ankh + Perkeo press (play line, not a solver)

Typical Ghost hybrid (see `JamlFilters/GhostColaDicetrick.jaml`):

1. **Ankh** early (shop) — hold it  
2. **Diet Cola** banked before the Neg cashout — don’t sell early  
3. **Perkeo** via Soul — grand master  
4. Burn into the loop: Ankh / dupe path so you get twin souls, not one pet you coddle  
5. **Fool** + Ghost = Ankh factory when the press is online  
6. **Neg tag** ante → free-neg shop → **Oops** on slots 2+

That is confab + filter must. Economy still kills runs. Motely finds seeds that *contain* the
pieces; it does not play the hand for you.

### Score vs seed view

- **Search / `ScoreSeeds`** — must gate + should tallies. Fast. No full shop sheet.  
- **Jamlyzer** — filter that dumps antes/shops/packs/events; can attach score in the same pass.  
- **Unit-test `--analyze`** — legacy text sheet only.

Do not invent a third “guide CLI.” Use `--analyze` or Jamlyzer / JsonRender. If interop needs
score + sheet for one seed, chain Jamlyzer **after** scoring and return the captured dump — do
not re-simulate for free mid-SIMD.

### Daily door (CLI)

```sh
dotnet run -c Release --project Motely.CLI -- \
  --jaml JamlFilters/GhostColaDicetrick.jaml \
  --aesthetic nsfw \
  --cutoff auto \
  --threads 7
```

Release config. Sequential is the fast path. Aesthetic / list modes are different providers.
`--cutoff auto` only prints at/above the running max (log looks empty; search still runs).
