---
name: jaml
description: Author and validate JAML (Jimbo's Ante Markup Language) seed filters for Motely. Use whenever writing, editing, reviewing, or explaining a .jaml file or a JAML/JUMMY clause.
---

# Writing JAML

JAML is a YAML dialect describing which Balatro seeds are interesting. The engine (Motely, via motely-wasm) is the only judge of validity — always validate, never guess.

## Filter shape

```yaml
name: Copy Machine Opening
description: Blueprint engine online by ante 2.
deck: Red        # deck name, e.g. Red, Magic
stake: White     # stake name
must:            # every clause must match — gates, contribute no score
  - tag: CharmTag
    antes: [1]
should:          # each match adds its score — this is how seeds rank
  - rareJoker: Blueprint
    antes: [1, 2]
    score: 10
mustNot:         # any match disqualifies the seed
  - boss: TheNeedle
    antes: [1]
seeds: [ALEEB]   # optional: fixed list — makes the run finite (list mode)
```

At least one of `must` / `should` / `mustNot` is required.

## Clause keys

Item keys: `joker`, `rareJoker`, `uncommonJoker`, `commonJoker`, `legendaryJoker`, `voucher`, `tag`, `smallBlindTag`, `bigBlindTag`, `boss`, `tarotCard`, `planetCard`, `spectralCard`, `standardCard`.

Modifiers: `edition` (Foil, Holographic, Polychrome, Negative), `stickers` (Eternal, Perishable, Rental), `antes` (integers 0–39 — ante 0 is a real ante, the Hieroglyph/Petroglyph rewind visit), `score` (should only), `min` / `max` (occurrence bounds), `and` / `or` (nested clause lists).

`sources` scopes where an item may appear, per stream:

```yaml
sources:
  shopSlots: [0, 1, 2]     # shop positions
  boosterPacks: [0, 1]     # pack index within the ante (Hieroglyph rewind reaches slot 5 in ante 1)
  judgement: [0]           # Judgement-tarot joker rolls
  emperor: [0, 1, 2]       # Emperor-tarot tarot rolls
  luck: 5                  # lucky-card multiplier
```

A sourceless clause defaults to all shop + pack sources across its antes; an explicit `sources` is never widened.

## Semantics that bite

- `must` clauses gate but score 0 — points come from `should` only.
- `and` tallies complete conjunctions: all children must hit for the clause to count once.
- Vocabulary is exact engine enum names (`LuckyCat`, `CharmTag`, `TheNeedle`) — a typo'd name validates structurally but can never match. Use `MotelyJaml.listItems(kind, query)` to look names up; kinds include jokers, vouchers, tags, bosses, tarots, planets, spectrals, editions, seals.

## JUMMY one-liners

Plain strings inside a clause list parse as JUMMY lines:

```yaml
must:
  - "Eternal Blueprint in ante 1"
  - "Boss The Flint in ante 2"
```

Validate a single line with `MotelyJaml.validateLine(line)`.

## The validation ritual (non-negotiable)

Every filter you author gets judged by the engine before you call it done:

```js
import { MotelyJaml } from "motely-wasm";
const err = MotelyJaml.validate(jamlText);   // null = valid, else exact error string
```

From a repo with motely-wasm installed:

```sh
node -e "import('motely-wasm').then(m => console.log(m.MotelyJaml.validate(require('fs').readFileSync('FILE.jaml','utf8'))))"
```

Report the engine's verdict, not your own. A filter is real when `validate` returns null and a search or Jamlyzer run shows it discriminating — matching seeds that have the thing and rejecting seeds that don't.
