# Game Mechanics Documentation - Methodology & Sources

## How I Gathered Information

### Method 1: Code Analysis ✅ (Verified)
**What I did:**
- Searched codebase for specific mechanics
- Read source files directly
- Traced logic through code paths

**Sources Found:**

#### Finisher Bosses (VERIFIED ✅)
- **Source:** `MotelySingleSearchContext.Boss.cs:36-41`
- **Code Evidence:**
  ```csharp
  if (ante % 8 == 0)
  {
      // Finisher boss blind
      for (int i = 0; i < MotelyBossBlindExt.FinisherBossBlinds.Length; i++)
  ```
- **Conclusion:** ✅ VERIFIED - Finisher bosses only in Ante 8

#### Voucher Prerequisites (VERIFIED ✅)
- **Source:** `MotelySingleSearchContext.Vouchers.cs:143-156`
- **Source:** `IMPOSSIBLE_CONFIG_RULES.md` (already documented)
- **Code Evidence:** Prerequisite check logic exists
- **Conclusion:** ✅ VERIFIED - Upgrade vouchers need base vouchers

#### Ante 1 First Pack (VERIFIED ✅)
- **Source:** `MotelyJsonScoring.cs:567-571`
- **Source:** `IMPOSSIBLE_CONFIG_RULES.md` Rule 1
- **Conclusion:** ✅ VERIFIED - First pack always Buffoon with 2 jokers

#### Ante 3 Boss Blind (VERIFIED ✅)
- **Source:** `IMPOSSIBLE_CONFIG_RULES.md` Rule 2
- **Conclusion:** ✅ VERIFIED - Ante 3 always has Boss Tag, not skip tags

---

### Method 2: User Knowledge (NEEDS VERIFICATION ⚠️)
**What I did:**
- Used information you provided in conversation
- Documented it but marked as needing code verification

**Sources:**

#### Lucky Cat Unlock Requirement (NEEDS VERIFICATION ⚠️)
- **Source:** Your statement: "Lucky Cat does not show up in the game ever until the user actually gets a magic standard card"
- **What I documented:** Lucky Cat requires Gold Seal or Lucky Seal standard card
- **Code Search Results:**
  - Found `LuckyCat` enum references
  - Found `Gold Seal` references in test seeds
  - Found `CardLuckyMoney` and `CardLuckyMult` in `MotelyPrngKeys.cs`
  - **BUT:** Did NOT find explicit unlock logic in codebase
- **Status:** ⚠️ **NEEDS VERIFICATION** - Should check:
  1. Balatro source code (if available in `external/Balatro`)
  2. Game wiki/documentation
  3. Test seeds to see if Lucky Cat appears before magic cards

---

### Method 3: Test Seed Analysis (PARTIAL ✅)
**What I did:**
- Searched verified test seeds for patterns
- Found examples of Lucky Cat appearing in Ante 2+ (not Ante 1)
- Found examples of Gold Seal cards in Ante 1

**Sources:**
- `Motely.Tests/seeds/ALEEBOOO.verified.txt` - Lucky Cat in Ante 2 (slot 1, 18)
- `Motely.Tests/seeds/UNITTES.verified.txt` - Lucky Cat in Ante 2+ (slots 49, 6, 31)
- `Motely.Tests/seeds/1234567.verified.txt` - Lucky Cat in Ante 2+ (slots 30, 25)

**Pattern Observed:**
- Lucky Cat appears in Ante 2+ in all test seeds
- Gold Seal cards appear in Ante 1 in some seeds
- **But:** Correlation ≠ causation - need to verify unlock logic

---

## What Needs Verification

### High Priority ⚠️

1. **Lucky Cat Unlock Logic**
   - **Question:** Does Lucky Cat require a magic standard card (Gold/Lucky Seal)?
   - **Where to check:**
     - Balatro source code (if available)
     - Game wiki
     - Test by searching seeds with Lucky Cat in Ante 1 (should find none if rule is true)
   - **Current Status:** Documented based on user knowledge, needs code/wiki verification

2. **Other Locked Items**
   - **Question:** Are there other jokers/items that unlock based on conditions?
   - **Where to check:**
     - Balatro source code
     - Game wiki
     - Test seed patterns

3. **Magic Standard Card Definition**
   - **Question:** Is "magic standard card" = Gold Seal OR Lucky Seal? Or something else?
   - **Where to check:**
     - Balatro source code
     - Game wiki
     - Test seeds

---

## Recommended Verification Methods

### Method A: Source Code Analysis (BEST ✅)
**Balatro source code location (per user):**
- **Path:** `X:\BalatroSeedOracle\external\Balatro\*.lua`
- **Format:** Lua script files
- **What to search for:**
  1. `LuckyCat` or `luckycat` - find unlock conditions
  2. `unlock` or `locked` - find all unlock mechanics
  3. `magic` or `gold_seal` or `lucky_seal` - find magic card definitions
  4. `finisher` - verify finisher boss mechanics

**Steps:**
1. Search all `.lua` files for `LuckyCat`
2. Find unlock logic/conditions
3. Verify all documented mechanics
4. Document any other locked items found

### Method B: Wiki/Documentation (GOOD ✅)
**Balatro Wiki Resources:**
1. Search official Balatro wiki for "Lucky Cat" unlock requirements
2. Search for "locked items" or "unlock conditions"
3. Search for "magic standard card" or "gold seal" mechanics
4. Cross-reference with code findings

**Wiki URLs to check:**
- Official Balatro wiki (if exists)
- Fandom wiki
- Community wikis
- Reddit/Discord knowledge bases

### Method C: Test Seed Analysis (PARTIAL ✅)
**Using existing test seeds:**
1. Search all test seeds for Lucky Cat in Ante 1
2. If none found → supports unlock theory
3. Check if seeds with Lucky Cat also have magic cards in earlier antes

### Method D: Game Testing (SLOW ⚠️)
**Manual testing:**
1. Play game, try to get Lucky Cat without magic card
2. Verify unlock behavior
3. Document findings

---

## Current Documentation Status

| Mechanic | Source | Verification Status |
|----------|--------|-------------------|
| Finisher Bosses (Ante 8 only) | Code (`MotelySingleSearchContext.Boss.cs`) | ✅ VERIFIED |
| Voucher Prerequisites | Code (`MotelySingleSearchContext.Vouchers.cs`) | ✅ VERIFIED |
| Ante 1 First Pack (Buffoon) | Code (`MotelyJsonScoring.cs`) | ✅ VERIFIED |
| Ante 3 Boss Blind | Code/Logic | ✅ VERIFIED |
| Lucky Cat Unlock | User Knowledge | ⚠️ NEEDS VERIFICATION |
| Magic Card Definition | User Knowledge | ⚠️ NEEDS VERIFICATION |

---

## Next Steps

1. **Check Balatro Source Code** (if available)
   - Search for unlock logic
   - Verify Lucky Cat requirement
   - Find other locked items

2. **Search Game Wiki**
   - Official Balatro wiki
   - Community documentation
   - Reddit/Discord knowledge

3. **Analyze Test Seeds**
   - Pattern analysis for Lucky Cat
   - Correlation with magic cards
   - Statistical verification

4. **Update Documentation**
   - Mark verified items as ✅
   - Mark unverified items as ⚠️
   - Add code references where found

---

## How to Improve This Document

**If you have suggestions:**
1. Point me to Balatro source code location
2. Share wiki/documentation links
3. Provide test cases or examples
4. Share your knowledge of other locked items
5. Suggest better verification methods

**I'll update this document as we verify more mechanics!**

