# Balatro Game Mechanics for Seed Searching

This document explains how Balatro's random number generation (RNG) works and how seeds control game outcomes. Understanding these mechanics is crucial for creating effective JAML filters.

## Table of Contents

1. [Seeds and RNG](#seeds-and-rng)
2. [Antes and Progression](#antes-and-progression)
3. [Shop Mechanics](#shop-mechanics)
4. [Pack Mechanics](#pack-mechanics)
5. [Joker Generation](#joker-generation)
6. [Voucher Generation](#voucher-generation)
7. [Consumable Generation](#consumable-generation)
8. [Boss Blinds](#boss-blinds)
9. [Tags](#tags)
10. [Special Decks](#special-decks)

---

## Seeds and RNG

### What is a Seed?

A **seed** is a string (like `5SC1HR14`) that determines all random outcomes in a Balatro run. The same seed will always produce the same results:
- Same jokers in shops
- Same packs
- Same boss blinds
- Same consumables
- Same tags

### Seed Format

Balatro seeds are typically:
- **8 characters** long
- Mix of **letters and numbers**
- Case-insensitive (but displayed in uppercase)

Examples: `5SC1HR14`, `ABCD1234`, `WEEJOKER`

### RNG Sequence

Balatro uses a **deterministic RNG** based on the seed:
1. Seed is converted to a number
2. RNG state is initialized
3. Each random event consumes RNG values in a fixed order
4. Same seed = same sequence of random numbers

**Key Insight**: The order matters! If you want Blueprint in ante 1, the seed must generate it at that specific point in the RNG sequence.

---

## Antes and Progression

### Ante Structure

Balatro has **8 antes** (boss fights):
- **Ante 1**: First boss (easiest)
- **Antes 2-7**: Progressive difficulty
- **Ante 8**: Final boss

### Shop Availability

- **Ante 1**: 4 shop slots (0-3)
- **Antes 2-8**: 5 shop slots (0-4)

### Pack Availability

- **Ante 1**: 4 pack slots (0-3)
- **Antes 2-8**: 5 pack slots (0-4)

### Why Ante Matters

Items appearing **earlier** (lower antes) are more valuable:
- More time to use them
- More opportunities to build synergies
- Higher scores prioritize early items

**JAML Tip**: Use `antes: [1]` for "early" and `antes: [1, 2, 3]` for "first few antes".

---

## Shop Mechanics

### Shop Generation

Shops are generated at the start of each ante:
1. RNG determines which jokers appear
2. RNG determines which vouchers appear (if any)
3. RNG determines prices
4. RNG determines editions (Foil, Holo, Polychrome, Negative)

### Shop Slots

- **Slot 0**: Leftmost joker slot
- **Slots 1-4**: Additional joker slots
- **Slot 5**: Voucher slot (if available)

### Source Restriction

Use `sources: [shop]` to require items from shops only (not packs or tags).

---

## Pack Mechanics

### Pack Types

- **Standard Pack**: 5 random jokers
- **Jumbo Pack**: 10 random jokers (rare)
- **Mega Pack**: 15 random jokers (very rare)

### Pack Generation

Packs are generated when opened:
1. RNG determines pack type
2. RNG determines jokers in pack
3. RNG determines editions

### Pack Slots

- **Slot 0**: First pack slot
- **Slots 1-4**: Additional pack slots

### Source Restriction

Use `sources: [pack]` to require items from packs only.

---

## Joker Generation

### Rarity System

Jokers have 4 rarities:
- **Common** (61 jokers): Most frequent
- **Uncommon** (64 jokers): Less frequent
- **Rare** (20 jokers): Uncommon
- **Legendary** (5 jokers): Very rare

### Soul Jokers

**Soul Jokers** are legendary jokers that can appear:
- In shops (rare)
- From packs (very rare)
- From tags (BossTag, RareTag)

### Editions

Jokers can have editions:
- **Foil**: +$50 sell value
- **Holo**: +$3 mult
- **Polychrome**: +$50 mult
- **Negative**: Creates negative copy (powerful for some jokers)

### Generation Rules

1. RNG determines rarity
2. RNG selects joker from rarity pool
3. RNG determines edition (if applicable)
4. Same seed = same joker in same slot

**JAML Tip**: Use `edition: Negative` for powerful combos (e.g., Perkeo).

---

## Voucher Generation

### Voucher Types

- **Shop Vouchers**: Appear in shop slot 5
- **Tag Vouchers**: From VoucherTag
- **Deck Vouchers**: Some decks start with vouchers

### Voucher Rarity

Vouchers are **rare** and appear:
- In shops (ante 2+)
- From VoucherTag
- From specific deck starts

### Important Vouchers

- **Telescope**: Planet cards appear 1 ante earlier
- **Observatory**: Planet cards appear 2 antes earlier
- **Hieroglyph**: Creates random joker
- **Petroglyph**: Creates random consumable

**JAML Tip**: Telescope + Observatory is a powerful combo for planet builds.

---

## Consumable Generation

### Tarot Cards

22 tarot cards that modify playing cards:
- Generated from shops (TarotMerchant)
- Generated from packs (rare)
- Generated from tags

### Planet Cards

12 planet cards that modify playing cards:
- Generated from shops (PlanetMerchant)
- Generated from packs (rare)
- Telescope/Observatory affect availability

### Spectral Cards

18 spectral cards with powerful effects:
- Generated from shops (rare)
- Generated from packs (very rare)
- Generated from tags

### Generation Rules

1. RNG determines type (tarot/planet/spectral)
2. RNG selects specific card
3. Same seed = same consumable

---

## Boss Blinds

### Boss Types

- **Normal Bosses**: Appear in antes 1-7
- **Finisher Bosses**: Appear in ante 8 (5 variants)

### Boss Generation

Bosses are determined at the start of each ante:
1. RNG selects boss from available pool
2. Some bosses have minimum ante requirements
3. Same seed = same boss in same ante

### Notable Bosses

- **TheWall**: Appears in ante 2+, many players avoid
- **TheOx**: Appears in ante 6+, very difficult
- **ThePlant**: Appears in ante 4+, challenging

**JAML Tip**: Use `mustNot` to avoid difficult bosses.

---

## Tags

### Tag Types

Tags modify small blinds (first fight of each ante):
- **UncommonTag**: Guarantees uncommon joker
- **RareTag**: Guarantees rare joker
- **BossTag**: Guarantees boss joker
- **VoucherTag**: Guarantees voucher
- And many more...

### Tag Generation

Tags are generated at the start of each ante:
1. RNG determines if tag appears
2. RNG selects tag type
3. Same seed = same tags

### Tag Sources

Tags can provide jokers from special sources:
- `judgement`: Judgement tarot joker rolls
- `rareTag`: Rare Tag joker rolls
- `uncommonTag`: Uncommon Tag joker rolls

**JAML Tip**: Use `sources` to restrict where items come from.

---

## Special Decks

### Erratic Deck

- Starts with random joker
- Can have stickers (Eternal, Perishable, etc.)
- Uses `erraticRank` and `erraticSuit` for card filtering

### Other Decks

- **Magic**: Starts with CrystalBall voucher
- **Nebula**: Starts with Telescope voucher
- **Zodiac**: Starts with multiple vouchers
- **Ghost**: No starting hand
- And more...

**JAML Tip**: Specify `deck: Erratic` for Erratic-specific filters.

---

## Key Insights for JAML Generation

### 1. Timing Matters

Items in **ante 1** are most valuable. Use `antes: [1]` for "early" requests.

### 2. Multiple Copies

To require 2+ copies, list them separately:
```yaml
must:
  - joker: Blueprint
  - joker: Blueprint
```

### 3. Scoring Priority

Use `score` in `should` clauses to prioritize:
- Early items: `score: 100`
- Later items: `score: 50`

### 4. Source Restrictions

Use `sources` to control where items come from:
- `sources: [shop]` - Shop only
- `sources: [pack]` - Pack only
- `sources: [tag]` - Tag only

### 5. Edition Requirements

Some jokers are powerful with specific editions:
- Perkeo + Negative = very powerful
- Use `edition: Negative` for these combos

### 6. Boolean Logic

Use `or` for flexibility:
```yaml
must:
  - or:
      - joker: Blueprint
      - joker: Brainstorm
```

### 7. Avoid Bad Bosses

Use `mustNot` to avoid difficult bosses:
```yaml
mustNot:
  - boss: TheWall
```

---

## Common Patterns

### Observatory Build
- Telescope + Observatory for early planets
- Often paired with Perkeo (Negative)

### Copy Build
- Blueprint + Brainstorm + Showman
- Copies abilities of other jokers

### Money Build
- Baron + Golden Joker
- Generates money for more jokers

### Steel Build
- Steel Joker + Mime
- Generates steel cards

---

## Summary

1. **Seeds are deterministic**: Same seed = same results
2. **Order matters**: RNG sequence is fixed
3. **Early is better**: Ante 1 items are most valuable
4. **Sources matter**: Shop vs pack vs tag
5. **Editions matter**: Negative Perkeo is powerful
6. **Bosses matter**: Some are harder than others
7. **Decks matter**: Erratic has special mechanics

Use these insights to create effective JAML filters that find the seeds you want!


