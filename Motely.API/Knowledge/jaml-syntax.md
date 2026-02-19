# JAML Syntax Reference

**JAML** (Joker Ante Markup Language) is a YAML-based configuration format for creating Balatro seed search filters. This document provides a complete reference for all JAML syntax features.

## Table of Contents

1. [Basic Structure](#basic-structure)
2. [Metadata Fields](#metadata-fields)
3. [Clause Types](#clause-types)
4. [Item Types](#item-types)
5. [Modifiers](#modifiers)
6. [Boolean Logic](#boolean-logic)
7. [Shorthand Syntax](#shorthand-syntax)
8. [Defaults](#defaults)
9. [Examples](#examples)

---

## Basic Structure

Every JAML file follows this structure:

```yaml
name: Filter Name
description: Optional description
author: Your Name
dateCreated: 2025-01-01T00:00:00Z

deck: Red
stake: White

defaults:
  antes: [1, 2, 3, 4, 5, 6, 7, 8]
  packSlots: [0, 1, 2, 3, 4, 5]
  shopSlots: [0, 1, 2, 3, 4, 5]
  score: 1

must:
  # Required items

should:
  # Optional items (scored)

mustNot:
  # Banned items
```

---

## Metadata Fields

### Required
- **`name`** (string): Display name of the filter

### Optional
- **`description`** (string): Human-readable description
- **`author`** (string): Creator name
- **`dateCreated`** (string, ISO 8601): Creation timestamp
- **`deck`** (enum): Balatro deck to use
  - Options: `Red`, `Blue`, `Yellow`, `Green`, `Black`, `Magic`, `Nebula`, `Ghost`, `Abandoned`, `Checkered`, `Zodiac`, `Painted`, `Anaglyph`, `Plasma`, `Erratic`, `Challenge`
  - Default: `Red`
- **`stake`** (enum): Stake level
  - Options: `White`, `Red`, `Green`, `Black`, `Blue`, `Purple`, `Orange`, `Gold`
  - Default: `White`

---

## Clause Types

### `must`
Items that **MUST** appear in the seed. If any `must` clause fails, the seed is rejected.

```yaml
must:
  - joker: Blueprint
    antes: [1]
```

### `should`
Items that **SHOULD** appear for bonus scoring. Seeds are ranked by total score.

```yaml
should:
  - joker: Brainstorm
    antes: [1, 2]
    score: 50
```

### `mustNot`
Items that **MUST NOT** appear. If any `mustNot` clause matches, the seed is rejected.

```yaml
mustNot:
  - boss: TheWall
    antes: [1, 2]
```

---

## Item Types

### Jokers
- **`joker`**: Regular joker card
- **`legendaryJoker`**: Soul joker (legendary jokers)

```yaml
- joker: Blueprint
- joker: Brainstorm
- legendaryJoker: Perkeo
```

### Vouchers
```yaml
- voucher: Telescope
- voucher: Observatory
- voucher: Hieroglyph
```

### Consumables
- **`tarot`** or **`tarotCard`**: Tarot cards
- **`planet`** or **`planetCard`**: Planet cards
- **`spectral`** or **`spectralCard`**: Spectral cards

```yaml
- tarot: TheFool
- planet: Mercury
- spectral: Ankh
```

### Playing Cards
- **`playingCard`** or **`standardCard`**: Standard playing cards

```yaml
- playingCard:
    rank: A
    suit: Hearts
    enhancement: Mult
    seal: Gold
```

### Bosses and Tags
- **`boss`** or **`bossBlind`**: Boss blind
- **`tag`**: Tag (applies to small blind)
- **`smallBlindTag`**: Explicit small blind tag
- **`bigBlindTag`**: Explicit big blind tag

```yaml
- boss: TheWall
- tag: BossTag
```

### Special
- **`erraticRank`**: For Erratic deck - specific rank count
- **`erraticSuit`**: For Erratic deck - specific suit count
- **`event`**: Random event outcomes

```yaml
- erraticRank: Two
  min: 10
- event:
    eventType: LuckyMoney
    antes: [1, 2]
    rolls: [0, 1]
```

---

## Modifiers

### `antes`
Which antes (1-8) to check. Default: all antes (1-8).

```yaml
- joker: Blueprint
  antes: [1, 2, 3]
```

### `edition`
Required edition for jokers/vouchers.
- Options: `Foil`, `Holo`, `Polychrome`, `Negative`

```yaml
- legendaryJoker: Perkeo
  edition: Negative
```

### `sources`
Where the item can be found.
- Options: `shop`, `pack`, `tag`, `voucher`

```yaml
- joker: Blueprint
  sources: [shop, pack]
```

### `score`
Scoring weight for `should` clauses. Higher = more important.
- Default: `1`

```yaml
- joker: Brainstorm
  score: 100
```

### `label`
Custom label for display in results (doesn't affect search).

```yaml
- joker: Blueprint
  label: "First Blueprint"
```

### Playing Card Modifiers
- **`rank`**: `2`, `3`, `4`, `5`, `6`, `7`, `8`, `9`, `10`, `J`, `Q`, `K`, `A`
- **`suit`**: `Hearts`, `Diamonds`, `Clubs`, `Spades`
- **`enhancement`**: `Bonus`, `Mult`, `Wild`, `Glass`, `Steel`, `Stone`, `Lucky`, `Gold`
- **`seal`**: `Red`, `Blue`, `Gold`, `Purple`

### Stickers
For jokers with stickers (Erratic deck feature):

```yaml
- joker: WeeJoker
  stickers: [Eternal]
```

---

## Boolean Logic

### `and`
All nested clauses must match.

```yaml
must:
  - and:
      - joker: Blueprint
      - joker: Brainstorm
    antes: [1]
```

### `or`
Any nested clause can match.

```yaml
must:
  - or:
      - joker: Blueprint
      - joker: Brainstorm
    antes: [1]
```

---

## Shorthand Syntax

JAML supports convenient shorthand for common patterns.

### Type-as-Key
Instead of `type: Joker, value: Blueprint`, use:

```yaml
must:
  - joker: Blueprint
  - voucher: Telescope
  - legendaryJoker: Perkeo
```

### Plural Arrays
Multiple items of the same type:

```yaml
must:
  - jokers: [Blueprint, Brainstorm]
    antes: [1]
```

This expands to:
```yaml
must:
  - joker: Blueprint
    antes: [1]
  - joker: Brainstorm
    antes: [1]
```

---

## Defaults

Set default values for clauses to avoid repetition:

```yaml
defaults:
  antes: [1, 2, 3, 4, 5, 6, 7, 8]
  packSlots: [0, 1, 2, 3, 4, 5]
  shopSlots: [0, 1, 2, 3, 4, 5]
  score: 1

must:
  # Uses defaults.antes automatically
  - joker: Blueprint
  
  # Override with specific antes
  - joker: Brainstorm
    antes: [2, 3]
```

**Note**: Ante 1 automatically limits pack/shop slots to [0-3] (4 slots max).

---

## Examples

### Simple: Two Blueprints
```yaml
name: Two Blueprints
must:
  - joker: Blueprint
    antes: [1]
  - joker: Blueprint
    antes: [1]
```

### Observatory Build
```yaml
name: Observatory Telescope
must:
  - voucher: Telescope
    antes: [1, 2]
  - voucher: Observatory
    antes: [2, 3]
should:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1, 2]
    score: 100
```

### Negative Perkeo
```yaml
name: Negative Perkeo Early
should:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    score: 100
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [2]
    score: 96
```

### Copy Build
```yaml
name: Copy Build
must:
  - joker: Blueprint
    antes: [1]
  - joker: Brainstorm
    antes: [1, 2]
should:
  - joker: Showman
    antes: [1, 2]
    score: 50
```

### With Boolean Logic
```yaml
name: Blueprint or Brainstorm
must:
  - or:
      - joker: Blueprint
      - joker: Brainstorm
    antes: [1]
```

### Erratic Deck
```yaml
name: Erratic Wee Monday
deck: Erratic
stake: Black
must:
  - erraticRank: Two
    min: 10
  - joker: WeeJoker
    stickers: [Eternal]
    antes: [1]
```

---

## Common Patterns

### Multiple Copies
To require 2+ copies, list them separately:

```yaml
must:
  - joker: Blueprint
  - joker: Blueprint
```

### Early vs Late
Use `antes` to specify timing:

```yaml
should:
  - joker: Blueprint
    antes: [1]
    score: 100  # Early is better
  - joker: Blueprint
    antes: [2, 3]
    score: 50   # Later is okay
```

### Edition Requirements
```yaml
must:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
```

### Source Restrictions
```yaml
must:
  - joker: Blueprint
    sources: [shop]  # Must come from shop, not pack
    antes: [1]
```

---

## Validation Rules

1. **Item names** must match exactly (case-sensitive): `Blueprint` not `blueprint`
2. **Antes** must be 1-8
3. **Pack/shop slots** are 0-5 (ante 1 limited to 0-3)
4. **Scores** must be >= 0
5. **At least one clause** required in `must`, `should`, or `mustNot`

---

## Tips

- Use `defaults` to reduce repetition
- Use `should` with high scores for important but not required items
- Use `mustNot` to avoid bad bosses or unwanted items
- Combine `antes` with `score` to prioritize early items
- Use `label` to make results more readable
- Test filters with small seed counts first


