# JAML — how to write a seed filter

JAML is a filter language for Balatro seeds. You describe what a run must contain; the engine
searches 2.3 trillion seeds and hands back the ones that match.

This page is for people. Every example here loads.

---

## The shortest filter that works

```yaml
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

```yaml
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
builder, not by the loader, so a clause loaded straight from YAML still reports `Antes` empty.

The line form reads like the game reads: **stickers, then edition, then the thing.** Same order
the card shows it to you.

A line can also carry keys underneath it, when the one-liner has no spelling for what you want:

```yaml
must:
  - Negative Perkeo
    ante: 1
```

---

## The three lists

```yaml
must:      # every clause here has to hit, or the seed is rejected
should:    # each hit adds score; used for ranking, never rejection
mustNot:   # if this hits, the seed is rejected
```

A `should` clause without a `score:` is worth 1.

---

## Where things come from (`sources:`)

By default a clause looks everywhere reasonable. Narrow it when you care:

```yaml
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

```yaml
    sources:
      rareShopJokers: [0, 1]
      judgement: [0]
```

---

## Antes

```yaml
    ante: 1            # one
    antes: [1, 2, 3]   # several
    antes: [1-8]       # a range
```

Leave antes off entirely and the clause means "any ante, 1 through 8."

---

## Counting

```yaml
    min: 2    # at least two of them (default 1)
    max: 3    # no more than three
```

---

## Deck and stake

```yaml
deck: Ghost
stake: Gold
```

These change what the engine generates, so they are part of the filter, not a display setting.
Default is Red / White.

---

## A real filter

```yaml
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

```yaml
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

```yaml
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
