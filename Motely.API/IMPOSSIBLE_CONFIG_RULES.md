# Impossible Configuration Rules

These configurations are **impossible** in Balatro and will never return seed results. The AI must never generate these.

## Rule 1: First Buffoon Pack in Ante 1
**Impossible:** Non-joker items in the first Buffoon pack of Ante 1

**Why:**
- The first pack in Ante 1 is **always** a Buffoon pack
- It **always** contains exactly **2 jokers**
- It **always** costs **$4** (player starts with $4 on every deck)
- This is hardcoded game design - ensures player can always afford a joker

**Examples of IMPOSSIBLE configs:**
- ❌ `must: [{type: "Tarot", value: "TheFool", antes: [1], packSlots: [0]}]` - Tarot in first pack
- ❌ `must: [{type: "Voucher", value: "Overstock", antes: [1], packSlots: [0]}]` - Voucher in first pack
- ✅ `must: [{type: "Joker", value: "Blueprint", antes: [1], packSlots: [0]}]` - Joker in first pack (valid)

**Code Reference:** `MotelyJsonScoring.cs:567-571` - `generatedFirstPack: ante != 1`

---

## Rule 2: Skip Tags in Ante 3
**Impossible:** Skip reward tags (Negative Tag, Standard Tag, etc.) in Ante 3

**Why:**
- Ante 3 is **always** a Boss Blind
- Boss Blinds have a **Boss Tag**, not skip tags
- Skip tags only appear on Small Blind and Big Blind (antes 1-2, 4-8)
- Ante 3 structure: Small Blind → Big Blind → **Boss Blind** (Boss Tag only)

**Examples of IMPOSSIBLE configs:**
- ❌ `must: [{type: "Tag", value: "NegativeTag", antes: [3]}]` - Negative Tag in Ante 3
- ❌ `must: [{type: "SmallBlindTag", value: "NegativeTag", antes: [3]}]` - Skip tag in Ante 3 small blind
- ❌ `must: [{type: "BigBlindTag", value: "StandardTag", antes: [3]}]` - Skip tag in Ante 3 big blind
- ✅ `must: [{type: "Tag", value: "BossTag", antes: [3]}]` - Boss Tag in Ante 3 (valid)
- ✅ `must: [{type: "Tag", value: "NegativeTag", antes: [2]}]` - Negative Tag in Ante 2 (valid)

**Code Reference:** Verified seeds show Ante 3 always has "Boss Tag"

---

## Rule 3: Certain Tags in Ante 1
**Impossible:** These tags cannot appear in Ante 1 (game design)

**Disallowed Ante 1 Tags:**
- NegativeTag
- StandardTag
- MeteorTag
- BuffoonTag
- HandyTag
- GarbageTag
- **EtherealTag** ⚠️
- TopupTag
- OrbitalTag

**Why:**
- Game design prevents these tags from appearing in Ante 1
- Code resamples tags until a valid one is found

**Examples of IMPOSSIBLE configs:**
- ❌ `must: [{type: "Tag", value: "EtherealTag", antes: [1]}]` - Ethereal Tag in Ante 1
- ❌ `must: [{type: "Tag", value: "NegativeTag", antes: [1]}]` - Negative Tag in Ante 1
- ✅ `must: [{type: "Tag", value: "EtherealTag", antes: [2]}]` - Ethereal Tag in Ante 2+ (valid)

**Code Reference:** `MotelySingleSearchContext.Tags.cs:12-23` - `DisallowedAnteOneTags`

---

## Validation Strategy

### For AI System Prompt:
Add these rules to prevent impossible configs from being generated.

### For Unit Tests:
Test that these impossible configs are caught/rejected:
1. Non-joker in Ante 1 pack slot 0
2. Skip tag (Negative, Standard, etc.) in Ante 3
3. Disallowed tags (Ethereal, Negative, etc.) in Ante 1

### For Config Validator:
Consider adding validation in `MotelyJsonConfigValidator.cs` to catch these at config load time.

---

## Rule 4: Voucher Prerequisites
**Impossible:** Upgrade vouchers cannot appear until their base voucher is purchased

**Why:**
- Vouchers come in **base/upgrade pairs**
- **Odd-numbered vouchers** (1, 3, 5, 7...) are **upgrade vouchers**
- **Even-numbered vouchers** (0, 2, 4, 6...) are **base vouchers**
- Upgrade vouchers **require** their base voucher (voucher - 1) to be active first
- Game resamples vouchers until it finds one that doesn't need a prerequisite OR has its prerequisite unlocked

**Voucher Pairs:**
- `Overstock` (0) → `OverstockPlus` (1) ⚠️
- `ClearanceSale` (2) → `Liquidation` (3)
- `Hone` (4) → `GlowUp` (5)
- `RerollSurplus` (6) → `RerollGlut` (7)
- `CrystalBall` (8) → `OmenGlobe` (9)
- `Telescope` (10) → `Observatory` (11)
- `Grabber` (12) → `NachoTong` (13)
- `Wasteful` (14) → `Recyclomancy` (15)
- `TarotMerchant` (16) → `TarotTycoon` (17)
- `PlanetMerchant` (18) → `PlanetTycoon` (19)
- `SeedMoney` (20) → `MoneyTree` (21)
- `Blank` (22) → `Antimatter` (23)
- `MagicTrick` (24) → `Illusion` (25)
- `Hieroglyph` (26) → `Petroglyph` (27)
- `DirectorsCut` (28) → `Retcon` (29)
- `PaintBrush` (30) → `Palette` (31)

**Examples of IMPOSSIBLE configs:**
- ❌ `must: [{type: "Voucher", value: "OverstockPlus", antes: [1]}]` - Upgrade voucher in Ante 1 (base not purchased yet)
- ❌ `must: [{type: "Voucher", value: "Observatory", antes: [2]}]` - Observatory in Ante 2 without Telescope
- ✅ `must: [{type: "Voucher", value: "Overstock", antes: [1]}]` - Base voucher (valid)
- ✅ `must: [{type: "Voucher", value: "OverstockPlus", antes: [2]}]` - Upgrade voucher in Ante 2+ IF base was in Ante 1

**Code Reference:** `MotelySingleSearchContext.Vouchers.cs:38-59` - Prerequisite check: `((int)voucher & 1) == 1` means odd = upgrade, requires `voucher - 1` (base) to be active

**Important:** This is a **stateful** rule - upgrade vouchers can appear in later antes IF the base voucher was purchased earlier in the run.

---

## Additional Notes

- **Ante 0** (pre-run shop): Special case, has different rules
- **Ante 8**: Finisher bosses only (AmberAcorn, CeruleanBell, etc.)
- **Pack slots**: Ante 1 has 4 pack slots [0,1,2,3], Ante 2+ has 6 slots [0,1,2,3,4,5]


