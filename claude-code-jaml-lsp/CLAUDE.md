# JAML (Jimbo's Ante Markup Language)

JAML is a YAML-based DSL for authoring Balatro seed filters in the Motely engine. This plugin gives Claude Code full LSP-powered intelligence for `.jaml` files.

## Language basics

A JAML filter has three sections:
- `must` — hard requirements (all must match)
- `should` — soft scoring clauses (score if matched)
- `mustNot` — exclusions (none may match)

Each section contains a list of clauses. Every clause is a mapping with an item-type key and properties:

```jaml
must:
  - joker: Baron
    edition: Negative
    antes: [1, 2, 3]
    sources:
      boosterPacks: [0, 1]
  - legendaryJoker: Perkeo
    antes: [1]
```

## Item-type keys (what to search for)

Use these keys to select what kind of item a clause matches:

- `joker` — any rarity joker by name (e.g. `Baron`, `Mime`, `Perkeo`)
- `commonJoker` / `uncommonJoker` / `rareJoker` / `legendaryJoker` — rarity-specific
- `jokers` / `commonJokers` / `uncommonJokers` / `rareJokers` / `legendaryJokers` — array of names
- `tarotCard` / `tarotCards` — e.g. `Judgement`, `TheFool`
- `spectralCard` / `spectralCards` — e.g. `Wraith`, `TheSoul`
- `planetCard` / `planetCards` — e.g. `Mercury`, `Mars`
- `voucher` / `vouchers` — e.g. `Hieroglyph`, `Overstock`
- `tag` / `tags` / `smallBlindTag` / `bigBlindTag` — e.g. `VoucherTag`, `NegativeTag`
- `boss` / `bosses` — e.g. `TheArm`, `TheHook`, `CrimsonHeart`
- `standardCard` / `card` / `standardCards` / `cards` — specific playing card
- `erraticRank` — for Erratic deck composition (Two, Three, ... Ace)
- `erraticSuit` — for Erratic deck composition (Hearts, Diamonds, Clubs, Spades)
- `event` — random events (Lucky, Wheel of Fortune, etc.)
- `and` — logical AND group (all nested clauses must match)
- `or` — logical OR group (at least one nested clause must match)

## Properties (modifiers on a clause)

- `edition` — `Foil`, `Holographic`, `Polychrome`, `Negative`, `Any`
- `enhancement` — `Bonus`, `Mult`, `Wild`, `Glass`, `Steel`, `Stone`, `Gold`, `Lucky`, `Any`
- `seal` — `Gold`, `Red`, `Blue`, `Purple`, `Any`
- `sticker` / `stickers` — `Eternal`, `Perishable`, `Rental`, `Any`
- `rarity` — `Common`, `Uncommon`, `Rare`, `Legendary`, `Any`
- `rank` — `Two` through `Ace`, `Any`
- `suit` — `Hearts`, `Diamonds`, `Clubs`, `Spades`, `Any`
- `antes` — array of ante numbers (0-based) where this applies
- `score` — weight for scoring in `should` clauses
- `min` / `max` — count thresholds
- `count` — exact count
- `label` — human-readable label for the clause
- `sources` — where the item was obtained:
  - `boosterPacks` — allowed pack indices
  - `judgement` — allowed Judgement tarot roll indices
  - `emperor` — allowed Emperor tarot roll indices
  - `seance` — allowed Seance roll indices
  - `wraith` — allowed Wraith spectral roll indices
  - `sixthSense` — allowed Sixth Sense roll indices
  - `riffRaff` — allowed Riff-Raff roll indices
  - `shopItems` — allowed shop item indices
- `requireMega` — boolean, requires Mega booster pack
- `negate` — boolean, invert the clause match
- `luck` — luck modifier threshold
- `rolls` — roll indices for random events

## Root keys (top-level filter metadata)

- `name` — display name
- `author` — filter author
- `description` — description (supports YAML folded `>-` strings)
- `deck` — `Red`, `Blue`, `Plasma`, `Erratic`, `Ghost`, `Magic`, etc.
- `stake` — `White`, `Red`, `Green`, `Black`, `Blue`, `Purple`, `Orange`, `Gold`
- `id` — unique identifier
- `seeds` — specific seed values to search
- `mode` — search mode override

## When to use LSP

When editing `.jaml` files, Claude should proactively use the LSP tool for:
- **Validation** — after each edit, check diagnostics for invalid enum values or syntax errors
- **Completion** — when the user is typing a clause key or value, use `completion` to suggest valid options
- **Hover** — when the user asks about a key or value, use `hover` to get documentation
- **Symbols** — when navigating a large filter, use `documentSymbol` to see the clause outline

The LSP server is the single source of truth for all JAML language intelligence — it is generated directly from the Motely C# engine enums. Any value the server recognizes is valid in the engine; any value it rejects is invalid.
