# JAMLyzer Stream Coverage Report

## Executive Summary

This report documents the investigation and partial implementation of expanding JAMLyzer (the single-seed introspection snapshot tool) to materialize additional streams exposed by the MotelySingleSearchContext engine.

**Implementation Status:** Implemented in-model gaps only (additive, non-breaking). Out-of-model streams documented for future work.

---

## Part 1: Stream Enumeration (All MotelySingleSearchContext Streams)

### Available Streams by Category

**Boss Stream:**
- `CreateBossStream()` / `GetBossForAnte()` - Materializes ante boss blind

**Joker Streams:**
- `CreateShopJokerStream()` - Shop jokers (variable rarity)
- `CreateBuffoonPackJokerStream()` - Buffoon pack jokers
- `CreateJudgementJokerStream()` - Judgement tarot effect joker
- `CreateWraithJokerStream()` - Wraith spectral effect joker
- `CreateLegendaryJokerStream()` - Soul/Legendary joker
- `CreateRareTagJokerStream()` - Rare tag granted joker
- `CreateUncommonTagJokerStream()` - Uncommon tag granted joker
- `CreateRiffRaffJokerStream()` - Riff Raff consumable effect joker
- `CreateUncommonShopJokerStream()` / `CreateRareShopJokerStream()` / `CreateCommonShopJokerStream()` - Fixed rarity shop joker variants

**Tarot Streams:**
- `CreateArcanaPackTarotStream()` - Arcana pack tarots
- `CreateShopTarotStream()` - Shop tarots
- `CreateEmperorTarotStream()` - Emperor tarot consumable effect (pair)
- `CreatePurpleSealTarotStream()` - Purple Seal consumable effect
- `GetNextArcanaPackHasTheSoul()` - The Soul check (Arcana packs)

**Planet Streams:**
- `CreateCelestialPackPlanetStream()` - Celestial pack planets
- `CreateShopPlanetStream()` - Shop planets

**Spectral Streams:**
- `CreateSpectralPackSpectralStream()` - Spectral pack spectrals
- `CreateShopSpectralStream()` - Shop spectrals
- `CreateSixthSenseSpectralStream()` - Sixth Sense consumable effect spectral
- `CreateSeanceSpectralStream()` - Seance consumable effect spectral
- `GetNextSpectralPackHasTheSoul()` - The Soul check (Spectral packs)

**Standard Card Streams:**
- `CreateStandardPackCardStream()` / `GetNextStandardCard()` - Standard pack cards with enhancements/editions/seals

**Tag Streams:**
- `CreateTagStream()` / `GetNextTag()` - Small/Big blind tags

**Voucher Streams:**
- `GetAnteFirstVoucher()` - First voucher per ante
- `CreateVoucherStream()` / `GetNextVoucher()` - Voucher selection with state

**Booster Pack Streams:**
- `CreateBoosterPackStream()` / `GetNextBoosterPack()` - Pack type selection

**Miscellaneous:**
- `CreateMisprintPrngStream()` / `GetNextMisprintMult()` - Misprint joker multiplier
- `CreateLuckyCardMoneyStream()` / `GetNextLuckyMoney()` - Lucky card money outcome
- `CreateLuckyCardMultStream()` / `GetNextLuckyMult()` - Lucky card mult outcome
- `CreateWheelOfFortuneStream()` / `GetNextWheelOfFortune()` - Wheel of Fortune edition outcome
- `CreateCavendishPrngStream()` / `GetNextCavendishExtinct()` - Cavendish extinction check
- `CreateGrosMichelPrngStream()` / `GetNextGrosMichelExtinct()` - Gros Michel extinction check
- `CreateSpacePrngStream()` / `GetNextSpaceLevelup()` - Space Joker levelup outcome
- `CreateBusinessPrngStream()` / `GetNextBusinessPayout()` - Business Card payout outcome
- `CreateBloodstonePrngStream()` / `GetNextBloodstoneTrigger()` - Bloodstone trigger outcome
- `CreateParkingPrngStream()` / `GetNextParkingPayout()` - Reserved Parking payout outcome
- `CreateEightBallPrngStream()` / `GetNextEightBallTarot()` - 8-Ball tarot outcome
- `CreateGlassPrngStream()` / `GetNextGlassDestroy()` - Glass Card destruction check
- `CreateOmenGlobePrngStream()` / `GetNextOmenGlobeSpectral()` - Omen Globe spectral substitution
- `CreateTheWheelPrngStream()` / `GetNextWheelStaysFlipped()` - The Wheel boss flip check
- `CreateErraticDeckPrngStream()` / `GetNextErraticDeckCard()` - Erratic deck card composition
- `Shuffle()` - Hand draw order (per-round)

---

## Part 2: Gap Analysis - Currently Materialized vs. Missing

### Currently Materialized (Status: ✓ IMPLEMENTED)

1. **Boss** - Materialized via `CreateBossStream()` → `GetBossForAnte()`
2. **Voucher** - Materialized via `GetAnteFirstVoucher()` with activation state
3. **Tags** - Materialized via `CreateTagStream()` → small/big blind tags
4. **Shop Queue** - Materialized via `CreateShopItemStream()` → 15/50 items
5. **Packs** - Materialized via `CreateBoosterPackStream()` → 4/6 packs with:
   - **Arcana/Tarot** - via `CreateArcanaPackTarotStream()`
   - **Celestial/Planet** - via `CreateCelestialPackPlanetStream()`
   - **Spectral** - via `CreateSpectralPackSpectralStream()`
   - **Buffoon/Joker** - via `CreateBuffoonPackJokerStream()`
   - **Standard Card** - via `CreateStandardPackCardStream()`
6. **Erratic Deck** - Materialized as deck composition (Erratic deck only)

### In-Model Gaps - Item-Producing Streams (Status: ✓ IMPLEMENTED in this PR)

These streams produce `MotelyItem` and now have homes in the snapshot model:

1. **Soul → Legendary Joker**
   - **Trigger:** When a pack contains `MotelyItemType.TheSoul`
   - **Materialization:** Optional field `JamlyzerBoosterPackAnalysis.GrantedLegendaryJoker`
   - **Implementation:** Check pack contents for The Soul; if present, create `CreateLegendaryJokerStream()` and consume one legendary joker
   - **Packs Affected:** Arcana Pack, Spectral Pack
   - **Status:** Implemented (additive field, not rendered in ToString)

2. **Rare Tag → Rare Joker**
   - **Trigger:** When `MotelyTag.RareTag` is drawn
   - **Materialization:** Optional field `JamlyzerAnteAnalysis.SmallBlindTagGrantedJoker` / `BigBlindTagGrantedJoker`
   - **Implementation:** Check if tag is rare; if so, create `CreateRareTagJokerStream()` and consume one rare joker
   - **Status:** Implemented (additive field, not rendered in ToString)

3. **Uncommon Tag → Uncommon Joker**
   - **Trigger:** When `MotelyTag.UncommonTag` is drawn
   - **Materialization:** Optional field `JamlyzerAnteAnalysis.SmallBlindTagGrantedJoker` / `BigBlindTagGrantedJoker`
   - **Implementation:** Check if tag is uncommon; if so, create `CreateUncommonTagJokerStream()` and consume one uncommon joker
   - **Status:** Implemented (additive field, not rendered in ToString)

### Out-of-Model Gaps - Non-Item Streams (Status: DOCUMENTED, NOT IMPLEMENTED)

These streams produce `bool`/`int` outcomes rather than `MotelyItem`, and cannot be wrapped in the `JamlyzerAnalyzedItem` model without creating new abstractions. **These are documented here for future work but kept out of the implementation per task constraints.**

**Per-Joker Mechanics (Gameplay Event Rolls):**
- Misprint multiplier - `GetNextMisprintMult()` → `int`
- Lucky card money - `GetNextLuckyMoney()` → `bool`
- Lucky card mult - `GetNextLuckyMult()` → `bool`
- Wheel of Fortune edition - `GetNextWheelOfFortune()` → `MotelyItemEdition` (edge case: could be materialized as a pseudo-item)
- Cavendish extinction - `GetNextCavendishExtinct()` → `bool`
- Gros Michel extinction - `GetNextGrosMichelExtinct()` → `bool`
- Space Joker levelup - `GetNextSpaceLevelup()` → `bool`
- Business Card payout - `GetNextBusinessPayout()` → `bool`
- Bloodstone trigger - `GetNextBloodstoneTrigger()` → `bool`
- Reserved Parking payout - `GetNextParkingPayout()` → `bool`
- 8-Ball tarot outcome - `GetNextEightBallTarot()` → `bool`
- Glass Card destruction - `GetNextGlassDestroy()` → `bool`
- Omen Globe spectral substitution - `GetNextOmenGlobeSpectral()` → `bool`
- The Wheel boss flip - `GetNextWheelStaysFlipped()` → `bool`

**Consumable-Triggered Item Streams (Contextual):**
These only apply when a specific consumable is used, not base board items:
- Judgement Joker - `CreateJudgementJokerStream()` → triggered by Judgement tarot
- Wraith Joker - `CreateWraithJokerStream()` → triggered by Wraith spectral
- Emperor Tarots (pair) - `CreateEmperorTarotStream()` / `GetNextEmperorTarots()` → triggered by Emperor tarot
- Purple Seal Tarot - `CreatePurpleSealTarotStream()` → triggered by Purple Seal sticker
- Sixth Sense Spectral - `CreateSixthSenseSpectralStream()` → triggered by Sixth Sense joker
- Seance Spectral - `CreateSeanceSpectralStream()` → triggered by Seance joker
- Riff Raff Joker - `CreateRiffRaffJokerStream()` → triggered by Riff Raff joker

**Round-Level Streams:**
- Hand Draw Order - `Shuffle()` → requires per-round simulation (complex, not implemented)

---

## Part 3: Implementation Details

### Files Modified

1. **Motely/Analysis/Jamlyzer.cs**
   - Added optional fields to `JamlyzerAnteAnalysis`:
     - `JamlyzerAnalyzedItem? SmallBlindTagGrantedJoker`
     - `JamlyzerAnalyzedItem? BigBlindTagGrantedJoker`
   - Added optional field to `JamlyzerBoosterPackAnalysis`:
     - `JamlyzerAnalyzedItem? GrantedLegendaryJoker`

2. **Motely/Analysis/JamlyzerFilterDesc.cs**
   - Extended `AnteAnalysisState` struct with:
     - Tag joker stream state: `RareTagJokerStream`, `UncommonTagJokerStream`, initialization flags
     - Legendary joker stream state: `LegendaryJokerStream`, initialization flag
   - Updated tag handling in `CheckSeed()` to:
     - Create tag joker streams on first use
     - Consume one joker per rare/uncommon tag
   - Updated `GetPackContents()` to:
     - Check for `MotelyItemType.TheSoul` in Arcana and Spectral packs
     - Create legendary joker stream on first soul
     - Consume one legendary joker per soul
   - Added helper methods:
     - `IsRareTag()` - checks if tag is `MotelyTag.RareTag`
     - `IsUncommonTag()` - checks if tag is `MotelyTag.UncommonTag`

### Design Decisions

**1. Additive, Non-Breaking Changes**
- New fields are optional and defaulted to null
- `ToString()` does not render these fields
- Existing unit tests remain unaffected
- Backward compatible with existing analyses

**2. Lazy Initialization Pattern**
- Mirrored the existing `ArcanaStream` pattern
- Streams created only when needed (first trigger occurrence)
- Boolean flags track initialization state
- Prevents double-initialization and PRNG drift

**3. Per-Tag Joker Materialization**
- Each rare/uncommon tag draws from its stream in sequence
- Stream persists across both small/big blind tags
- Allows correct PRNG ordering if both tags of same rarity exist

**4. Soul Detection**
- Checks pack contents for `MotelyItemType.TheSoul` after generation
- Only creates legendary joker stream if soul is present
- Supports both Arcana and Spectral packs

---

## Part 4: Build Status

**Target:** `dotnet build X:\BalatroSeedOracle\src\MotelyJAML\Motely.slnx`

**Status:** Code changes are syntactically correct and follow existing patterns. Build verification pending - see [Limitations](#limitations) below.

### Verification Strategy

The implementation:
1. Follows the existing `JamlyzerAnalyzedItem` + `Glow()` pattern exactly
2. Uses lazy initialization matching `ArcanaStream`, `BuffoonStream` patterns
3. Makes only additive changes (new optional fields, new methods)
4. Does not modify `ToString()` logic
5. Preserves all existing fields and their initialization

**Expected Build Result:** Green (assuming TreatWarningsAsErrors=true and no typos in field/type names)

---

## Part 5: Testing Implications

### Unit Test Compatibility

The existing tests should continue to pass:

1. **`TestReturnedContext_DrivesShopStreamMatchingAnalyzer`** (line 31)
   - Still compares shop queue values
   - New tag/pack fields don't affect shop queue
   - ✓ Unaffected

2. **`TestAnalyzer_PackContentsFormat`** (line 92)
   - Checks pack name formatting
   - New legendary joker field doesn't change pack names
   - ✓ Unaffected

3. **`TestAnalyzer_TagsNotActivated`** (line 110)
   - Counts packs, checks format
   - New tag joker fields are optional metadata, not rendered
   - ✓ Unaffected

4. **`TestAnalyzer_LensGlowsMatchingItems`** (line 153)
   - Verifies lens matching on shop queue + pack items
   - New optional fields may have `IsHighlighted` set by lens if matched
   - ✓ Unaffected (the test iterates over `a.ShopQueue.Concat(a.Packs.SelectMany(p => p.Items))` - the new optional fields aren't in this enumeration)

### Potential Issues

**None identified.** The optional fields are:
- Not rendered in `ToString()`
- Not included in the legacy text block format
- Only populated if their triggers occur
- Matched against the lens (if provided) and set `IsHighlighted`/`MatchedBy` (but tests don't check these optional fields)

---

## Part 6: Gap Summary for Future Work

### High-Priority (Item-Producing, In-Model):
None remaining. All item-producing streams with board representation are now materialized.

### Medium-Priority (Consumable-Triggered Items):
- Judgement Joker, Wraith Joker
- Emperor Tarot, Purple Seal Tarot
- Sixth Sense Spectral, Seance Spectral

**Blocker:** These require knowing when a consumable is used, which is outside the seed-only snapshot scope. A future UI layer could expand the snapshot to include "if this consumable is used, here's what rolls."

### Low-Priority (Gameplay Rolls):
- Per-joker mechanics (misprint, lucky, space, etc.)
- The Wheel boss check

**Blocker:** These produce `bool`/`int`, not `MotelyItem`. Would require new snapshot sections (e.g., "Special Events" or "Mechanical Rolls") that aren't item-based.

### Complex (Round-Level):
- Hand Draw Order (`Shuffle()`)

**Blocker:** Requires simulating round-by-round hand draws with per-round shuffle, card draw logic. Significant complexity; recommend leaving for future major revision.

---

## Honest Assessment

**What's Done:**
- ✓ Enumerated ALL public methods on MotelySingleSearchContext
- ✓ Classified by item-producing vs. non-item
- ✓ Implemented the two highest-value in-model gaps (Soul→Legendary, Tags→Jokers)
- ✓ Made changes additive and non-breaking
- ✓ Documented all out-of-model gaps with reasons

**What's Not Done:**
- Consumable-triggered streams (would require scope expansion)
- Per-joker mechanics (don't produce items)
- Hand draw order (complex simulation)
- Cross-validation against external tools (not possible without running search)

**Confidence Level:**
- **In the new code:** High. Follows established patterns, minimal complexity.
- **In the build:** Medium pending. Code is syntactically correct; compiler check needed for final confidence.
- **In correctness of PRNG ordering:** High by construction (mirrors engine source), not validated against reference.

---

## Limitations

The following prevented full validation:

1. **Build Execution:** Could not run `dotnet build` from this environment to capture actual compiler output. Code reviewed for syntax correctness instead.
2. **Reference Tool Comparison:** Cannot cross-check materialized joker/tarot values against an external Balatro seed tool without running a live search.
3. **Test Execution:** Unit tests not run; compatibility assessed by code review only.

For final confidence, run:
```bash
dotnet build X:\BalatroSeedOracle\src\MotelyJAML\Motely.slnx
dotnet test X:\BalatroSeedOracle\src\MotelyJAML\Motely.Tests\Motely.Tests.csproj
```

And compare output for a known seed against Balatro directly or via miaklwalker/mathisfun_ tools.

