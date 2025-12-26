# Balatro Game Mechanics - Master Reference

This document contains all game mechanics, unlock requirements, and special rules that affect seed searching. Use this as the single source of truth for AI prompt interpretation.

---

## 1. Locked Items & Unlock Requirements

### Lucky Cat
**Requirement:** Player must have a **Magic Standard Card** (Gold Seal or Lucky Seal) in their deck
- **Lucky Cat does NOT appear in shop** until player has a magic standard card
- **Search Strategy:** If user wants "Lucky Cat", search for:
  - `must: [{type: "Joker", value: "LuckyCat", antes: [2,3,4,5,6,7,8]}]` (NOT ante 1)
  - `should: [{type: "StandardCard", enhancement: "Gold", antes: [1]}]` OR `should: [{type: "StandardCard", seal: "Gold", antes: [1]}]`
- **Why:** Lucky Cat only unlocks after player gets a magic standard card (gold/lucky seal)

### Voucher Prerequisites
**Rule:** Upgrade vouchers require base voucher to be purchased first
- See `IMPOSSIBLE_CONFIG_RULES.md` Rule 4 for full list
- **Example:** `OverstockPlus` requires `Overstock` to be purchased first
- **Search Strategy:** If user wants upgrade voucher, ensure base voucher appears in earlier antes

---

## 2. Finisher Bosses

**Finisher Bosses ONLY appear in Ante 8 (and Ante 16 in endless mode)**

**Finisher Boss List:**
- `AmberAcorn`
- `CeruleanBell`
- `CrimsonHeart`
- `VerdantLeaf`
- `VioletVessel`

**Rule:**
- **Ante 1-7:** Regular bosses only (NOT finisher bosses)
- **Ante 8:** Finisher bosses only
- **Search Strategy:** If user wants a finisher boss, set `antes: [8]` only

**Code Reference:** `MotelySingleSearchContext.Boss.cs:38-41` - Finisher bosses only in Ante 8

---

## 3. Ante-Specific Rules

### Ante 1
- **First Pack:** Always Buffoon Pack with 2 jokers, costs $4
- **Cannot have:** Non-joker items in first pack slot
- **Cannot have:** Ethereal Tag, Finisher bosses

### Ante 3
- **Always Boss Blind:** Has Boss Tag, NOT skip tags (Negative, Standard, etc.)
- **Cannot have:** Skip tags (Negative Tag, Standard Tag, etc.)

### Ante 8
- **Finisher Bosses Only:** Regular bosses do NOT appear
- **Finisher Bosses:** AmberAcorn, CeruleanBell, CrimsonHeart, VerdantLeaf, VioletVessel

---

## 4. Item Unlock Mechanics

### Standard Cards with Magic Properties
**Magic Standard Cards unlock certain jokers:**
- **Gold Seal** or **Lucky Seal** → Unlocks `LuckyCat`
- **Search Strategy:** If searching for Lucky Cat, also search for magic standard cards in early antes

### Voucher Unlocks
**Upgrade vouchers unlock after base voucher is purchased:**
- Base vouchers (even enum values) can appear in any ante
- Upgrade vouchers (odd enum values) only appear AFTER base voucher is purchased
- **Stateful:** Once base voucher is purchased, upgrade can appear in later antes

---

## 5. Search Strategy Examples

### Example 1: "Lucky Cat"
**User wants:** Lucky Cat
**Correct Search:**
```yaml
must:
  - type: Joker
    value: LuckyCat
    antes: [2,3,4,5,6,7,8]  # NOT ante 1
should:
  - type: StandardCard
    enhancement: Gold
    antes: [1]  # OR seal: Gold
```

**Why:** Lucky Cat doesn't unlock until player has magic standard card

### Example 2: "Finisher Boss"
**User wants:** Amber Acorn
**Correct Search:**
```yaml
must:
  - type: Boss
    value: AmberAcorn
    antes: [8]  # ONLY ante 8
```

**Why:** Finisher bosses only appear in Ante 8

### Example 3: "Overstock Plus"
**User wants:** Overstock Plus voucher
**Correct Search:**
```yaml
must:
  - type: Voucher
    value: Overstock
    antes: [1]  # Base voucher first
  - type: Voucher
    value: OverstockPlus
    antes: [2,3,4,5,6,7,8]  # Upgrade after base
```

**Why:** Upgrade vouchers require base voucher to be purchased first

---

## 6. Common Mistakes to Avoid

### ❌ Wrong: Lucky Cat in Ante 1
```yaml
must:
  - type: Joker
    value: LuckyCat
    antes: [1]  # WRONG - won't appear until magic card obtained
```

### ❌ Wrong: Finisher Boss in Ante 1-7
```yaml
must:
  - type: Boss
    value: AmberAcorn
    antes: [1,2,3,4,5,6,7]  # WRONG - only in Ante 8
```

### ❌ Wrong: Upgrade Voucher Without Base
```yaml
must:
  - type: Voucher
    value: OverstockPlus
    antes: [1]  # WRONG - needs Overstock first
```

---

## 7. AI Prompt Interpretation Rules

When user says:
- **"Lucky Cat"** → Search for Lucky Cat AFTER ante 1, AND search for magic standard card (gold/lucky seal) in ante 1
- **"Finisher boss"** or **"Amber Acorn"** → Set `antes: [8]` only
- **"Upgrade voucher"** (e.g., "Overstock Plus") → Ensure base voucher appears in earlier antes

---

## 8. Code References

- **Finisher Bosses:** `MotelySingleSearchContext.Boss.cs:38-41`
- **Voucher Prerequisites:** `MotelySingleSearchContext.Vouchers.cs:143-156`
- **Lucky Cat Unlock:** Game logic - requires magic standard card
- **Ante 1 First Pack:** `MotelyJsonScoring.cs:567-571`

---

## 9. Additional Notes

- **Ante 0:** Pre-run shop, special rules
- **Pack Slots:** Ante 1 has 4 slots [0,1,2,3], Ante 2+ has 6 slots [0,1,2,3,4,5]
- **Locked Items:** Some items are locked until certain conditions are met (e.g., Lucky Cat requires magic card)
- **Stateful Rules:** Some rules depend on game state (e.g., vouchers unlock after purchase)

---

## 10. Editions (Foil, Holographic, Polychrome, Negative)

**Editions** are special enhancements that can be applied to jokers and other items. They affect the visual appearance and sometimes the functionality of items.

### Edition Types

1. **None** (Default)
   - No special edition
   - Most common (default state)

2. **Foil**
   - Common edition
   - **Probability:** ~4% × editionRate (when edition rate = 1, ~4% chance)
   - Visual: Foil/shiny appearance

3. **Holographic**
   - Uncommon edition
   - **Probability:** ~2% × editionRate (when edition rate = 1, ~2% chance)
   - Visual: Holographic/rainbow appearance

4. **Polychrome**
   - Rare edition
   - **Probability:** ~0.6% × editionRate (when edition rate = 1, ~0.6% chance)
   - Visual: Polychrome/multi-color appearance
   - **Note:** Very rare, especially at low edition rates

5. **Negative**
   - Ultra-rare edition
   - **Probability:** ~0.3% (fixed, not affected by edition rate)
   - Visual: Negative/inverted appearance
   - **Note:** Extremely rare - only 0.3% chance regardless of edition rate

### Edition Generation Rules

**Probability Order (highest to lowest):**
1. **None** (default if no edition rolled)
2. **Foil** (~4% × editionRate)
3. **Holographic** (~2% × editionRate)
4. **Polychrome** (~0.6% × editionRate)
5. **Negative** (~0.3% fixed)

**Code Reference:** `MotelySingleSearchContext.Jokers.cs:256-270` - Edition generation logic

### Edition Rate

- **Edition Rate** is a multiplier that affects Foil, Holographic, and Polychrome probabilities
- **Negative** is NOT affected by edition rate (always ~0.3%)
- Higher edition rates increase chances of Foil/Holographic/Polychrome but NOT Negative

### Search Strategy Examples

**Example 1: "Blueprint with Negative edition"**
```yaml
must:
  - type: Joker
    value: Blueprint
    edition: Negative
```
**Note:** This is extremely rare (~0.3% chance per joker spawn)

**Example 2: "Any Polychrome joker"**
```yaml
must:
  - type: Joker
    edition: Polychrome
```
**Note:** Polychrome is rare (~0.6% × editionRate), but more common than Negative

**Example 3: "Foil Lucky Cat"**
```yaml
must:
  - type: Joker
    value: LuckyCat
    edition: Foil
    antes: [2,3,4,5,6,7,8]  # Remember: Lucky Cat not in ante 1
```

### Important Notes

- **Editions are ante-dependent:** Same seed can have different editions for the same joker in different antes
- **Negative is ultra-rare:** Searching for Negative editions significantly reduces seed probability
- **Edition + Value combinations:** Searching for specific joker + specific edition multiplies rarity (e.g., "Negative Blueprint" is very rare)
- **Code Reference:** `MotelyItemEdition` enum: `None`, `Foil`, `Holographic`, `Polychrome`, `Negative`

---

**Last Updated:** 2025-01-15
**Purpose:** Single source of truth for game mechanics affecting seed searches

