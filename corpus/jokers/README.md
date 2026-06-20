# JAML Corpus

RAG corpus of canonical, **tight** JAML example filters covering **everything JAML can find** —
not just jokers. Categories mirror the JAML clause types in `JamlConfigLoader.Models.cs`:

- **jokers/** — all 150 jokers
- **consumables/** — tarot, planet, spectral cards
- **vouchers/** — all 32 vouchers
- **tags/** — all skip tags (incl. small/big blind tags)
- **bosses/** — boss blinds (avoidance + finisher targeting)
- **cards/** — standard playing cards (rank/suit/seal/enhancement/edition)
- **decks/** — top filters per deck, including Erratic Deck rank/suit spike filters
- **events/** — gameplay-event clauses (luckyMoney, spaceLevelup, businessPayout,
  bloodstoneTrigger, glassDestroy, wheelOfFortune, gros-michel/cavendish extinction,
  judgement, wraith, parkingPayout, misprintMult, …) — anything a player would ask to find.

## Design philosophy

The fastest, tightest way to find any item of interest is to **look in the first shop and lean
on the mechanics that actually synergize** — not broad `antes: [10..89]` (or even `[1,2,3]`)
ranges. Every file here follows one canonical shape:

- **`must`** — the item itself, found in **Ante 1's first shop**:
  ```yaml
  must:
    - joker: <Name>
      antes: [1]
      sources:
        shopItems: [0, 1, 2, 3]   # the first shop's item slots
        boosterPacks: [0, 1]      # the two packs Ante 1 offers
  ```
- **`should`** — the item's *real* synergy partners (copy engines, retriggers, the planet
  for its hand shape, its suit/seal/edition enablers), each with a `score`.
- **`mustNot`** — used sparingly, only for hard build-killers (e.g. the boss that debuffs the
  joker's suit).

The generic `joker:` key is used for every joker regardless of rarity (the engine resolves
rarity from the name). Other items use their own keys: `tarotCard`, `planetCard`,
`spectralCard`, `voucher`, `tag`, `boss`, `standardCard`, plus the event keys above.

**No faking**: every effect line is grounded in `d:\Balatro` Lua first (`game.lua`,
`functions/common_events.lua`, `card.lua`, and `localization/en-us.lua`) plus `Motely/Enums/*`
for exact JAML spellings. EXA/wiki/community sources are discovery tools for player language
and fun combos; mechanics still need Lua verification before corpus edits.

Legendaries (Canio/Triboulet/Yorick/Chicot/Perkeo) never appear in the shop — they only spawn
from **The Soul**, so those files use `sources: { arcanaPacks, spectralPacks, soulCard }`.

## RAG strategy (subtask c)

- **Embedding key**: `name` + `description` (the description carries the item's mechanical
  effect, availability gates, sloppy-player aliases, and why the synergy holds). One chunk per
  file — files are small and self-contained.
- **Retrieval signal**: filename slug == item/build slug, so an NL query naming a joker, deck,
  event, rank spike, or suit spike hits the exact file. Synergy partners named in `should` give
  cross-retrieval (a query for "King steel held-in-hand" surfaces baron/mime/chariot-adjacent
  files; "erratic all hearts flush" surfaces Erratic suit and Heart payoff files).

## Status

Stats prose is not ground truth. `description` text should be a compact Lua-verified RAG chunk
with EXA/wiki wording only when it helps match real player queries.
