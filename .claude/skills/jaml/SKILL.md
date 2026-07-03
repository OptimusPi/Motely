# JAML Skill

Help the user write and validate **JAML** (Jimbo's Ante Markup Language) filters for the Balatro seed finder.

## What JAML is

JAML is a YAML-based filter language that describes which Balatro seeds are interesting. A filter has metadata, optional fixed seeds, and three clause lists:

- `must` — clauses that must all match.
- `should` — clauses that increase a seed's score.
- `mustNot` — clauses that disqualify a seed.

## Basic structure

```yaml
name: Blueprint in Ante 1
description: Find seeds where Blueprint (Eternal) appears in the first ante.
deck: Red
stake: White
must:
  - joker: Blueprint
    edition: Foil
    stickers:
      - Eternal
    antes: [1]
```

## JUMMY one-line clauses

Inside `must`, `should`, and `mustNot`, plain strings are parsed as **JUMMY** lines:

```yaml
must:
  - "Eternal Blueprint in ante 1"
  - "Foil Polychrome in antes 1 or 2"
  - "Tag Handy in ante 1"
  - "Boss The Flint in ante 2"
```

Valid prefixes include `Eternal`, `Perishable`, `Rental`, `Foil`, `Holographic`, `Polychrome`, `Negative`, `Voucher`, `Tag`, `Small Blind Tag`, `Big Blind Tag`, `Boss`, `Starting Draw`, and event names like `Lucky Money`, `Wheel of Fortune`, etc.

## Common clause keys

- `joker` / `jokers` — match jokers.
- `edition` — `Foil`, `Holographic`, `Polychrome`, `Negative`.
- `stickers` — `Eternal`, `Perishable`, `Rental`.
- `antes` — list of ante numbers.
- `min` / `max` — how many times the clause must/may match.
- `score` — score weight for `should` clauses.
- `sources` — where items can appear (shop, packs, tarot cards, etc.).
- `and` / `or` / `clauses` — nested logic.

## Validation

Always prefer real validation over guessing. If the project has `motely-wasm` available, call `MotelyJaml.validate(jaml)` and report the exact error. For single JUMMY lines, use `MotelyJaml.validateLine(line)`.

## When helping the user

1. Ask what they want to find (item, ante range, edition, stickers, sources).
2. Write the smallest JAML filter that captures it.
3. Suggest using `jaml_validate` / `jummy_validate` if an MCP server is connected.
4. Keep filters readable: prefer JUMMY one-liners for simple criteria and structured mappings for complex `min`/`max`/`sources` logic.
