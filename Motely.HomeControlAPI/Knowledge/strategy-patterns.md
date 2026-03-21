# Balatro Strategy Patterns for Seed Searching

This document describes common Balatro build strategies and how to search for seeds that enable them. Each pattern includes the JAML filter structure and rationale.

## Table of Contents

1. [Observatory Build](#observatory-build)
2. [Copy Build](#copy-build)
3. [Negative Perkeo Build](#negative-perkeo-build)
4. [Money Build](#money-build)
5. [Steel Build](#steel-build)
6. [Wee Joker Build](#wee-joker-build)
7. [Hieroglyph Build](#hieroglyph-build)
8. [Hybrid Builds](#hybrid-builds)

---

## Observatory Build

### Description

Uses Telescope + Observatory vouchers to get planet cards 2-3 antes earlier than normal. Often paired with Perkeo (Negative) for consumable generation.

### Why It Works

- Planet cards are powerful but appear late
- Telescope makes them appear 1 ante earlier
- Observatory makes them appear 2 antes earlier
- Combined: planets appear 2-3 antes earlier
- Perkeo (Negative) generates more consumables

### JAML Pattern

```yaml
name: Observatory Build
must:
  - voucher: Telescope
    antes: [1, 2]
  - voucher: Observatory
    antes: [2, 3]
should:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    score: 100
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [2]
    score: 96
  - joker: Blueprint
    antes: [1, 2, 3]
    score: 1
```

### Variations

**Early Observatory**:
```yaml
must:
  - voucher: Telescope
    antes: [1]
  - voucher: Observatory
    antes: [2]
```

**Late Observatory** (still viable):
```yaml
must:
  - voucher: Telescope
    antes: [1, 2, 3]
  - voucher: Observatory
    antes: [2, 3, 4]
```

### User Prompts

- "observatory telescope combo"
- "telescope observatory perkeo negative"
- "observatory build early"

---

## Copy Build

### Description

Uses Blueprint + Brainstorm + Showman to copy abilities of other jokers. Extremely powerful when combined with strong jokers.

### Why It Works

- Blueprint copies joker to the right
- Brainstorm copies joker to the left
- Showman copies random joker
- Together: massive ability multiplication

### JAML Pattern

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
  - joker: Mime
    antes: [1, 2]
    score: 30
```

### Variations

**Double Blueprint**:
```yaml
must:
  - joker: Blueprint
    antes: [1]
  - joker: Blueprint
    antes: [1]
```

**Flexible Copy** (Blueprint OR Brainstorm):
```yaml
must:
  - or:
      - joker: Blueprint
      - joker: Brainstorm
    antes: [1]
```

### User Prompts

- "blueprint and brainstorm together"
- "copy build"
- "blueprint brainstorm showman combo"
- "2 blueprints in ante 1"

---

## Negative Perkeo Build

### Description

Uses Perkeo (Negative edition) to generate negative copies of consumables. Extremely powerful for generating resources.

### Why It Works

- Perkeo creates negative copy of random consumable
- Negative edition makes it happen every round
- Generates tarots, planets, spectrals
- Pairs well with Telescope/Observatory

### JAML Pattern

```yaml
name: Negative Perkeo
should:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    score: 100
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [2]
    score: 96
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [3]
    score: 95
```

### Variations

**Early Perkeo** (highest priority):
```yaml
should:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    score: 100
```

**Perkeo with Observatory**:
```yaml
must:
  - voucher: Telescope
    antes: [1, 2]
  - voucher: Observatory
    antes: [2, 3]
should:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    score: 100
```

### User Prompts

- "negative perkeo early"
- "perkeo negative in first 3 antes"
- "negative perkeo telescope observatory"

---

## Money Build

### Description

Uses Baron + Golden Joker to generate money. Money enables buying more jokers and vouchers.

### Why It Works

- Baron gains +3 Mult per Gold Joker
- Golden Joker generates money
- More money = more jokers = more power

### JAML Pattern

```yaml
name: Money Build
must:
  - joker: Baron
    antes: [1, 2]
should:
  - joker: GoldenJoker
    antes: [1]
    score: 50
  - joker: GoldenJoker
    antes: [2]
    score: 30
```

### Variations

**Baron Priority**:
```yaml
must:
  - joker: Baron
    antes: [1]
should:
  - joker: GoldenJoker
    antes: [1, 2]
    score: 50
```

### User Prompts

- "baron with golden joker"
- "money build"
- "baron gold stack"

---

## Steel Build

### Description

Uses Steel Joker + Mime to generate steel cards. Steel cards are powerful but rare.

### Why It Works

- Steel Joker generates steel cards
- Mime copies Steel Joker's ability
- More steel cards = more power

### JAML Pattern

```yaml
name: Steel Build
must:
  - joker: SteelJoker
    antes: [1, 2]
should:
  - joker: Mime
    antes: [1, 2]
    score: 50
```

### Variations

**Early Steel**:
```yaml
must:
  - joker: SteelJoker
    antes: [1]
should:
  - joker: Mime
    antes: [1]
    score: 50
```

### User Prompts

- "steel joker with mime"
- "steel build"
- "steel joker early"

---

## Wee Joker Build

### Description

Uses Wee Joker (often with Eternal sticker) in Erratic deck. Wee Joker gains +4 Mult per Wee Card.

### Why It Works

- Erratic deck has many Wee Cards
- Wee Joker scales with Wee Cards
- Eternal sticker prevents destruction
- Very powerful in Erratic deck

### JAML Pattern

```yaml
name: Wee Joker Erratic
deck: Erratic
must:
  - joker: WeeJoker
    stickers: [Eternal]
    antes: [1]
should:
  - erraticRank: Two
    score: 1
```

### Variations

**Wee Joker Priority**:
```yaml
deck: Erratic
must:
  - joker: WeeJoker
    antes: [1]
should:
  - joker: WeeJoker
    stickers: [Eternal]
    antes: [1]
    score: 100
```

### User Prompts

- "wee joker erratic deck"
- "wee joker with eternal sticker"
- "wee joker erratic"

---

## Hieroglyph Build

### Description

Uses Hieroglyph + Petroglyph vouchers to generate random jokers and consumables.

### Why It Works

- Hieroglyph creates random joker
- Petroglyph creates random consumable
- Together: lots of resources

### JAML Pattern

```yaml
name: Hieroglyph Build
must:
  - voucher: Hieroglyph
    antes: [1, 2, 3]
  - voucher: Petroglyph
    antes: [1, 2, 3]
```

### Variations

**Early Hieroglyph**:
```yaml
must:
  - voucher: Hieroglyph
    antes: [1, 2]
  - voucher: Petroglyph
    antes: [1, 2]
```

### User Prompts

- "hieroglyph and petroglyph"
- "hieroglyph petroglyph combo"
- "glyph build"

---

## Hybrid Builds

### Observatory + Copy

Combines Observatory build with Copy build for maximum power.

```yaml
name: Observatory Copy
must:
  - voucher: Telescope
    antes: [1, 2]
  - voucher: Observatory
    antes: [2, 3]
  - joker: Blueprint
    antes: [1]
should:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    score: 100
  - joker: Brainstorm
    antes: [1, 2]
    score: 50
```

### Copy + Money

Combines Copy build with Money build.

```yaml
name: Copy Money
must:
  - joker: Blueprint
    antes: [1]
  - joker: Baron
    antes: [1, 2]
should:
  - joker: Brainstorm
    antes: [1, 2]
    score: 50
  - joker: GoldenJoker
    antes: [1, 2]
    score: 30
```

---

## Common User Request Patterns

### "Early" Requests

Users often want items "early" (ante 1):
- Use `antes: [1]` for must clauses
- Use `antes: [1]` with high score for should clauses

### "Multiple" Requests

Users want 2+ copies:
- List items separately in must/should
- Each copy is a separate clause

### "Combo" Requests

Users want multiple items together:
- Use `must` for required combos
- Use `should` with scores for optional combos

### "Avoid" Requests

Users want to avoid bad bosses:
- Use `mustNot` with boss name
- Specify antes if needed

---

## Scoring Guidelines

### High Priority (score: 100)
- Items in ante 1
- Core build pieces
- Legendary jokers with editions

### Medium Priority (score: 50-80)
- Items in antes 2-3
- Supporting pieces
- Synergy enablers

### Low Priority (score: 1-30)
- Items in antes 4+
- Nice-to-haves
- Filler items

---

## Tips for AI Generation

1. **Recognize build names**: "observatory build" = Telescope + Observatory
2. **Understand "early"**: Usually means ante 1
3. **Handle "multiple"**: 2+ copies = list separately
4. **Use "should" for flexibility**: Not everything needs to be "must"
5. **Score appropriately**: Early = high score, late = low score
6. **Consider synergies**: Perkeo + Observatory, Blueprint + Brainstorm
7. **Respect deck requirements**: Erratic deck needs `deck: Erratic`

---

## Summary

Common patterns:
- **Observatory**: Telescope + Observatory + Perkeo
- **Copy**: Blueprint + Brainstorm + Showman
- **Negative Perkeo**: Perkeo (Negative) early
- **Money**: Baron + Golden Joker
- **Steel**: Steel Joker + Mime
- **Wee Joker**: Wee Joker (Eternal) in Erratic
- **Hieroglyph**: Hieroglyph + Petroglyph

Use these patterns to understand user requests and generate appropriate JAML filters!


