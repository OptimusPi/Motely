# JAML Genie Brain - Comprehensive Balatro Knowledge Base

This document contains exhaustive knowledge about Balatro game mechanics, items, pools, and seed searching strategies. This knowledge is injected into the AI's context to make it "super smart" about generating JAML filters.

## Table of Contents
1. [Item Mappings](#item-mappings)
2. [Game Mechanics](#game-mechanics)
3. [Probability & Rarity](#probability--rarity)
4. [Synergies & Combinations](#synergies--combinations)
5. [Voucher Effects](#voucher-effects)
6. [Shop Queue Mechanics](#shop-queue-mechanics)
7. [Seed Searching Strategy](#seed-searching-strategy)
8. [Common Patterns](#common-patterns)
9. [Impossible Configs](#impossible-configs)
10. [Advanced Edge Cases](#advanced-edge-cases)

---

## Item Mappings

### Jokers (Complete List)

#### Common Jokers (61 total)
Joker, GreedyJoker, LustyJoker, WrathfulJoker, GluttonousJoker, JollyJoker, ZanyJoker, MadJoker, CrazyJoker, DrollJoker, SlyJoker, WilyJoker, CleverJoker, DeviousJoker, CraftyJoker, HalfJoker, CreditCard, Banner, MysticSummit, EightBall, Misprint, RaisedFist, ChaostheClown, ScaryFace, AbstractJoker, DelayedGratification, GrosMichel, EvenSteven, OddTodd, Scholar, BusinessCard, Supernova, RideTheBus, Egg, Runner, IceCream, Splash, BlueJoker, FacelessJoker, GreenJoker, Superposition, ToDoList, Cavendish, RedCard, SquareJoker, RiffRaff, Photograph, ReservedParking, MailInRebate, Hallucination, FortuneTeller, Juggler, Drunkard, GoldenJoker, Popcorn, WalkieTalkie, SmileyFace, GoldenTicket, Swashbuckler, HangingChad, ShootTheMoon

#### Uncommon Jokers (64 total)
JokerStencil, FourFingers, Mime, CeremonialDagger, MarbleJoker, LoyaltyCard, Dusk, Fibonacci, SteelJoker, Hack, Pareidolia, SpaceJoker, Burglar, Blackboard, SixthSense, Constellation, Hiker, CardSharp, Madness, Seance, Vampire, Shortcut, Hologram, Cloud9, Rocket, MidasMask, Luchador, GiftCard, TurtleBean, Erosion, ToTheMoon, StoneJoker, LuckyCat, Bull, DietCola, TradingCard, FlashCard, SpareTrousers, Ramen, Seltzer, Castle, MrBones, Acrobat, SockAndBuskin, Troubadour, Certificate, SmearedJoker, Throwback, RoughGem, Bloodstone, Arrowhead, OnyxAgate, GlassJoker, Showman, FlowerPot, MerryAndy, OopsAll6s, TheIdol, SeeingDouble, Matador, Satellite, Cartomancer, Astronomer, Bootstraps

#### Rare Jokers (20 total)
DNA, Vagabond, Baron, Obelisk, BaseballCard, AncientJoker, Campfire, Blueprint, WeeJoker, HitTheRoad, TheDuo, TheTrio, TheFamily, TheOrder, TheTribe, Stuntman, InvisibleJoker, Brainstorm, DriversLicense, BurntJoker

#### Legendary Jokers (5 total)
Canio, Triboulet, Yorick, Chicot, Perkeo

### Vouchers (36 total)
Overstock, OverstockPlus, ClearanceSale, Liquidation, Hone, GlowUp, RerollSurplus, RerollGlut, CrystalBall, OmenGlobe, Telescope, Observatory, Grabber, NachoTong, Wasteful, Recyclomancy, TarotMerchant, TarotTycoon, PlanetMerchant, PlanetTycoon, SeedMoney, MoneyTree, Blank, Antimatter, MagicTrick, Illusion, Hieroglyph, Petroglyph, DirectorsCut, Retcon, PaintBrush, Palette

### Tarot Cards (22 total)
TheFool, TheMagician, TheHighPriestess, TheEmpress, TheEmperor, TheHierophant, TheLovers, TheChariot, Justice, TheHermit, TheWheelOfFortune, Strength, TheHangedMan, Death, Temperance, TheDevil, TheTower, TheStar, TheMoon, TheSun, Judgement, TheWorld

### Spectral Cards (23 total)
Familiar, Grim, Incantation, Talisman, Aura, Wraith, Sigil, Ouija, Ectoplasm, Immolate, Ankh, DejaVu, Hex, Trance, Medium, Cryptid, Soul, BlackHole

### Planet Cards (12 total)
Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto, PlanetX, Ceres, Eris

### Tags (28 total)
UncommonTag, RareTag, NegativeTag, FoilTag, HolographicTag, PolychromeTag, InvestmentTag, VoucherTag, BossTag, StandardTag, CharmTag, MeteorTag, BuffoonTag, HandyTag, GarbageTag, EtherealTag, CouponTag, DoubleTag, JuggleTag, D6Tag, TopupTag, SpeedTag, OrbitalTag, EconomyTag

**Special Tag Mechanics:**
- **CouponTag**: Small blind tag reward - if player takes small blind skip reward and gets CouponTag, ALL items in the next shop are FREE (extremely powerful economy strategy)

### Boss Blinds

#### Finisher Bosses (5 total)
AmberAcorn, CeruleanBell, CrimsonHeart, VerdantLeaf, VioletVessel

#### Normal Bosses (23 total)
TheArm (min Ante 2), TheClub, TheEye (min Ante 3), TheFish (min Ante 2), TheFlint (min Ante 2), TheGoad, TheHead, TheHook, TheHouse (min Ante 2), TheManacle, TheMark (min Ante 2), TheMouth (min Ante 2), TheNeedle (min Ante 2), TheOx (min Ante 6), ThePillar, ThePlant (min Ante 4), ThePsychic, TheSerpent (min Ante 5), TheTooth (min Ante 3), TheWall (min Ante 2), TheWater (min Ante 2), TheWheel (min Ante 2), TheWindow

### Booster Packs

#### Pack Types
- Arcana (Tarot cards): Normal (3 cards), Jumbo (5 cards), Mega (5 cards, 2 choices)
- Celestial (Planet cards): Normal (3 cards), Jumbo (5 cards), Mega (5 cards, 2 choices)
- Standard (Jokers): Normal (3 cards), Jumbo (5 cards), Mega (5 cards, 2 choices)
- Buffoon (Jokers): Normal (2 cards), Jumbo (4 cards), Mega (4 cards, 2 choices)
- Spectral (Spectral cards): Normal (2 cards), Jumbo (4 cards), Mega (4 cards, 2 choices)

#### Pack Weights (for seed generation)
- Arcana: 4.0 (Normal), 2.0 (Jumbo), 0.5 (Mega)
- Celestial: 4.0 (Normal), 2.0 (Jumbo), 0.5 (Mega)
- Standard: 4.0 (Normal), 2.0 (Jumbo), 0.5 (Mega)
- Buffoon: 1.2 (Normal), 0.6 (Jumbo), 0.15 (Mega)
- Spectral: 0.6 (Normal), 0.3 (Jumbo), 0.07 (Mega)

---

## Game Mechanics

### Ante Structure
- **Ante 1**: First shop pack is ALWAYS a Buffoon pack (2 jokers, costs $4). Cannot have non-joker items in pack slot 0.
- **Ante 2**: Can have any pack type. Skip tags become available.
- **Ante 3**: ALWAYS a Boss Blind. Only BossTag is available (no Skip tags like NegativeTag, StandardTag, etc.).
- **Antes 4-8**: Normal blinds, all pack types and tags available.

### Tag Availability by Ante
- **Ante 1**: Cannot have NegativeTag, StandardTag, MeteorTag, BuffoonTag, HandyTag, GarbageTag, EtherealTag, TopupTag, OrbitalTag
- **Ante 2+**: EtherealTag becomes available
- **Ante 3**: Only BossTag (Boss Blind)
- **Antes 2, 4-8**: All skip tags available (NegativeTag, StandardTag, etc.)

**Special Tag Notes:**
- **CouponTag**: Available as small blind tag reward (Antes 2, 4-8). When player takes small blind skip reward and gets CouponTag, ALL items in the next shop become FREE. This is an extremely powerful economy strategy - consider adding to "should" clauses for economy builds.
- **NegativeTag**: Gives negative edition to the NEXT PLAIN SHOP SLOTS JOKER only (not pack jokers!). Only affects ONE joker. For both jokers to be negative via NegativeTag, they'd need to be: both in shop slots next to each other, Anaglyph deck (DoubleTag at end of ante), NegativeTag in SmallBlindTag - very restrictive!

### Deck Defaults
- **Magic Deck**: Starts with CrystalBall voucher
- **Nebula Deck**: Starts with Telescope voucher
- **Zodiac Deck**: Starts with TarotMerchant, PlanetMerchant, and Overstock vouchers
- **Other Decks**: No default vouchers

### Editions
Valid editions: None, Foil, Holographic, Polychrome, Negative

### Seals
Valid seals: None, Gold, Red, Blue, Purple

### Enhancements
Valid enhancements: None, Bonus, Mult, Wild, Glass, Steel, Stone, Gold, Lucky

---

## Probability & Rarity

### Joker Rarity Probabilities
When generating jokers from packs or shops:
- **Rare Jokers**: 5% chance (probability > 0.95)
- **Uncommon Jokers**: 25% chance (probability > 0.7 and <= 0.95)
- **Common Jokers**: 70% chance (probability <= 0.7)
- **Legendary Jokers**: Extremely rare, typically only from specific sources (vouchers, tags, or special packs)

**Strategic Implications:**
- Requesting rare/legendary jokers in early antes (1-2) significantly reduces seed availability
- Common jokers are much easier to find - prefer them for "must" clauses
- Use "should" clauses for rare jokers to increase search flexibility

### Shop Item Type Rates (Base Rates)
Shop slots have weighted probabilities for item types:
- **Jokers**: 20.0 (most common shop item)
- **Tarot Cards**: 4.0 (base rate)
- **Planet Cards**: 4.0 (base rate)
- **Playing Cards**: 0.0 (only with MagicTrick voucher)
- **Spectral Cards**: 0.0 (base), 2.0 (Ghost deck only)

**Voucher Modifications:**
- TarotMerchant: Tarot rate increases to 9.6
- TarotTycoon: Tarot rate increases to 32.0
- PlanetMerchant: Planet rate increases to 9.6
- PlanetTycoon: Planet rate increases to 32.0
- MagicTrick: Playing card rate increases to 4.0

**Strategic Implications:**
- Shop searches are more reliable for jokers (20.0 rate) than tarots/planets (4.0 base)
- With TarotTycoon/PlanetTycoon, tarots/planets become MORE common than jokers in shops
- Consider voucher availability when searching for tarot/planet cards

### Pack Probability Weights
Pack weights determine how often each pack type appears:
- **Standard/Arcana/Celestial**: 4.0 (Normal), 2.0 (Jumbo), 0.5 (Mega) - Most common
- **Buffoon**: 1.2 (Normal), 0.6 (Jumbo), 0.15 (Mega) - Less common
- **Spectral**: 0.6 (Normal), 0.3 (Jumbo), 0.07 (Mega) - Rare

**Strategic Implications:**
- Standard packs are 3.3x more common than Buffoon packs
- Spectral packs are very rare - avoid requiring them unless necessary
- Mega packs are rare but offer 2 choices - good for flexibility

### Edition Probabilities
Editions are randomly applied to jokers:
- **None**: Most common (default)
- **Foil**: Uncommon
- **Holographic**: Rare
- **Polychrome**: Very rare
- **Negative**: Rare

**Strategic Implications:**
- Requiring specific editions (especially Polychrome/Negative) dramatically reduces seed availability
- Use "should" clauses for edition preferences, not "must" clauses
- Consider using tags (FoilTag, HolographicTag, PolychromeTag) instead of requiring editions directly

---

## Synergies & Combinations

### Power Synergy Groups
These jokers work exceptionally well together:

**Economy Synergy:**
- GoldenTicket + BusinessCard + ReservedParking + MailInRebate + Rocket (money generation jokers)
- CouponTag (small blind tag) - makes all items FREE in next shop (powerful economy)
- Temperance (tarot) + any joker selling (max $50 per sell)
- TheHermit (tarot) + money sources (doubles money, max $20)
- StandardPack + StandardCard with Gold Seal (+$3 when scored - pack economy source)
- **HangingChad + Gold Seal** - HangingChad triggers first card 2 extra times, so Gold Seal (+$3) becomes $9! (Common joker, powerful economy multiplier)

**Scoring Synergy:**
- HangingChad + Photograph (both score multipliers)
- WeeJoker + any low-value joker (WeeJoker doubles low jokers)
- Baron + any joker (Baron doubles all jokers)

**Copy Joker Mechanics (Separate, Not Synergistic):**
- Blueprint: Copies the effect of the last joker played
- Brainstorm: Copies a random joker's effect
- Note: Blueprint and Brainstorm don't synergize with each other - they're just two different flavors of copy jokers

**Re-Trigger Synergy (Powerful Multiplier):**
- **HangingChad** (COMMON joker) - Triggers the first (leftmost) card in the played hand 2 extra times
  - Normal: Card triggers once
  - With HangingChad: Card triggers 3 times total (1 normal + 2 extra)
  - Example: Gold Seal (+$3 when scored) → $9 with HangingChad! (3x multiplier)
  - Works with ANY "on score" effect: Gold Seal, Foil edition (+$3), Lucky enhancement, etc.
  - Extremely powerful for economy builds - Common joker that triples "on score" money effects

**Blueprint + HangingChad Re-Trigger Combo (EXTREMELY POWERFUL):**
- **Blueprint** (to the LEFT of HangingChad) copies HangingChad's "2 extra triggers" effect
- Result: Blueprint retriggers HangingChad's effect, adding 2 MORE triggers!
- Example with Gold Seal Standard Card:
  - Normal: $3 (1 trigger)
  - With HangingChad: $9 (1 normal + 2 extra = 3 triggers)
  - With Blueprint (left) + HangingChad: $15 total!
    - $3 (normal) + $6 (HangingChad's 2 extra) + $6 (Blueprint copying HangingChad's 2 extra) = $15
- This is an EXTREMELY powerful economy combo - Blueprint position matters (must be LEFT of HangingChad)
- Works with any "on score" effect - Blueprint amplifies HangingChad's re-trigger effect

**Copy/Clone Synergy:**
- Blueprint + any powerful joker (Blueprint copies last joker played - position matters!)
- Brainstorm + any powerful joker (Brainstorm copies random joker)
- Photograph + any scoring joker (Photograph copies scoring joker)

**Blueprint Position Synergy (Critical!):**
- Blueprint copies the joker to its RIGHT (the last joker played before it)
- **Blueprint + HangingChad**: If Blueprint is LEFT of HangingChad, Blueprint copies HangingChad's "2 extra triggers" effect
  - This creates a multiplicative re-trigger combo (see Re-Trigger Synergy section)
  - Gold Seal: $3 → $9 (HangingChad) → $15 (Blueprint copying HangingChad)
- Position matters! Blueprint must be LEFT of the joker it copies

**Edition Synergy:**
- Polychrome + any mult joker (Polychrome doubles mult)
- Negative + any joker with negative effect (Negative reverses effects)
- Foil + any joker (Foil gives +$3 when scored)

### Anti-Synergy (Avoid Together)
These combinations are counterproductive:

- Showman + any joker that needs to be sold (Showman prevents selling)
- BurntJoker + any joker you want to keep (BurntJoker destroys jokers)
- Madness + any joker you want to keep (Madness destroys jokers randomly)

### Legendary Synergies
Legendary jokers have unique synergies:

- **Perkeo**: Works with consumables (tarots, planets, spectrals) - copies last consumed
- **Canio**: Works with face cards (J, Q, K) - gives mult per face card
- **Triboulet**: Works with scoring - gives chips per $1 spent
- **Yorick**: Works with destroyed jokers - gives mult per destroyed joker
- **Chicot**: Works with scoring - gives mult per hand played

**Strategic Implications:**
- When users request legendary jokers, consider adding synergistic items to "should" clauses
- Perkeo searches benefit from tarot/planet/spectral availability
- Canio searches benefit from face card availability

---

## Voucher Effects

### Economy Vouchers
- **SeedMoney**: Start with $3 extra
- **MoneyTree**: +$1 at start of each round
- **TarotMerchant**: Tarot cards appear 2.4x more often in shops (4.0 → 9.6)
- **TarotTycoon**: Tarot cards appear 8x more often in shops (4.0 → 32.0)
- **PlanetMerchant**: Planet cards appear 2.4x more often in shops (4.0 → 9.6)
- **PlanetTycoon**: Planet cards appear 8x more often in shops (4.0 → 32.0)

### Pack Vouchers
- **Overstock**: +1 pack per shop
- **OverstockPlus**: +2 packs per shop
- **ClearanceSale**: Packs cost $1 less
- **Liquidation**: Packs cost $2 less
- **BuffoonTag**: Guarantees Buffoon pack in shop (if available)

### Reroll Vouchers
- **RerollSurplus**: +1 reroll per shop
- **RerollGlut**: +2 rerolls per shop

### Deck-Specific Vouchers
- **CrystalBall**: Magic deck default - shows next pack contents
- **Telescope**: Nebula deck default - shows next celestial pack contents
- **TarotMerchant + PlanetMerchant + Overstock**: Zodiac deck defaults

### Special Effect Vouchers
- **MagicTrick**: Playing cards appear in shops (rate 4.0)
- **Blank**: Start with Blank joker (can be transformed)
- **Antimatter**: Start with Antimatter joker (negative mult)
- **Hone**: Start with Hone joker (mult per hand)
- **GlowUp**: Start with GlowUp joker (mult per round)

**Strategic Implications:**
- Voucher searches are deterministic (always appear if in seed)
- Deck-specific vouchers are guaranteed for those decks
- Economy vouchers significantly improve shop availability for tarots/planets
- Consider voucher availability when searching for shop items

---

## Shop Queue Mechanics

### Shop Queue Structure
Each ante has a shop queue that determines what items appear:
- Queue is generated deterministically from seed
- Items appear in order from the queue
- Queue length varies by ante and deck

### Shop Slot Types
Shops have multiple slots that can contain:
- Jokers (most common, rate 20.0)
- Tarot cards (rate 4.0 base, modified by vouchers)
- Planet cards (rate 4.0 base, modified by vouchers)
- Playing cards (rate 0.0 base, requires MagicTrick voucher)
- Spectral cards (rate 0.0 base, Ghost deck only, rate 2.0)

### Pack Availability
Packs appear in shops based on:
- Pack weights (Standard/Arcana/Celestial most common)
- Buffoon packs less common
- Spectral packs very rare
- Vouchers can modify pack availability (Overstock, BuffoonTag)

**Strategic Implications:**
- Shop searches are more reliable than pack searches (jokers have 20.0 rate vs pack weights)
- Early antes (1-2) have smaller shop queues - easier to predict
- Later antes have larger queues - more flexibility but harder to predict specific items
- Voucher effects significantly modify shop rates - consider them in searches

---

## Advanced Edge Cases

### Conditional Unlocks (Locked Items)

**LuckyCat Unlock Requirement:**
- **LuckyCat** does NOT appear in the game until player obtains a **Lucky enhancement** card
- **CRITICAL:** If LuckyCat appears BEFORE player obtains Lucky enhancement, it gets **RE-ROLLED** into a different Uncommon joker!
- **CRITICAL:** Pack slot 0 in Ante 1 is ALWAYS Buffoon pack (jokers only) - cannot contain playing cards!
- **CRITICAL:** "Seeing" a pack doesn't re-roll - only when you OPEN it! Player can choose pack order!
- Lucky enhancement comes from Standard packs (enhanced cards appear in Standard packs)
- After Lucky enhancement is obtained, LuckyCat becomes available
- **Search Strategy for LuckyCat in Ante 1:**
  - Must require Lucky enhancement standard card in Standard pack (pack slot 1+) - NOT pack slot 0!
  - LuckyCat can be in Buffoon pack (slot 0) - player opens Standard pack FIRST, then Buffoon pack
  - Example:
    ```yaml
    must:
      # Lucky enhancement in Standard pack (pack slot 1) - player opens this FIRST
      - playingCard:
          enhancement: Lucky
        antes: [1]
        sources:
          packSlots: [1]  # Standard pack - can have playing cards
      # LuckyCat in Buffoon pack (slot 0) or other packs (slots 2, 3)
      # Player sees Buffoon pack but opens Standard pack FIRST, then Buffoon pack
      - joker: LuckyCat
        antes: [1]
        sources:
          packSlots: [0, 2, 3]  # Can be in Buffoon pack slot 0 (player opens Standard pack FIRST)
    ```
- **Why:** 
  - Buffoon pack = jokers only (can't have playing cards)
  - Standard pack = can have playing cards with enhancements
  - Player has agency - can choose which pack to open first
  - "Seeing" doesn't re-roll - only opening does!

### Erratic Deck Mechanics
Erratic deck has special mechanics:
- Cards have random ranks/suits (erraticRank, erraticSuit)
- Cannot search for specific ranks/suits in Erratic deck
- Must use erraticRank/erraticSuit filters instead
- Example: `{ type: "erraticRank", value: "Two", min: 10 }` for Erratic deck

### Ghost Deck Mechanics
Ghost deck has special mechanics:
- Spectral cards appear in shops (rate 2.0)
- Cannot get spectral cards from packs in other decks
- Spectral cards are more common in Ghost deck shops

### Stake Effects
Stakes modify difficulty but don't affect item availability:
- White: Base difficulty
- Red: +1 difficulty
- Green: +1 difficulty, +$1 per ante
- Black: +2 difficulty
- Blue: +2 difficulty, +$2 per ante
- Purple: +3 difficulty
- Orange: +3 difficulty, +$3 per ante
- Gold: +4 difficulty

**Strategic Implications:**
- Stake doesn't affect seed generation - same items appear regardless
- Higher stakes just make blinds harder, not items rarer
- Don't restrict stake in searches unless user specifically requests it

### Boss Blind Mechanics
- Ante 3 is ALWAYS a boss blind
- Boss blinds have specific mechanics (TheWheel, TheHook, etc.)
- Finisher bosses (AmberAcorn, etc.) only appear in final ante
- Normal bosses have minimum antes (TheOx requires Ante 6+)

**Strategic Implications:**
- Cannot search for skip tags in Ante 3 (boss blind only)
- Boss-specific searches should focus on Ante 3 or later antes
- Finisher boss searches should focus on final ante

### Pack Slot Mechanics
- Pack slot 0 is the FIRST pack in a shop
- Ante 1 slot 0 is ALWAYS a Buffoon pack (2 jokers)
- Other slots can be any pack type
- Pack slots are numbered 0, 1, 2, etc.

**Strategic Implications:**
- Ante 1 slot 0 searches MUST be jokers (Buffoon pack)
- Other slots can search for any pack type
- Pack slot searches are more restrictive than general pack searches

---

## Seed Searching Strategy

### Early Economy Focus
When users request "econ" or "economy", prioritize:
- **Jokers**: GoldenTicket, BusinessCard, ReservedParking, MailInRebate, Rocket (focus Antes 1-3)
- **Tags**: CouponTag (small blind tag - makes all items FREE in next shop - extremely powerful economy)
- **Tarot Cards**: Temperance (sell Jokers, max $50), TheFool (creates last Tarot/Planet), TheHermit (doubles money, max $20)
- **Playing Cards**: Gold Seal (+$3 when scored)
- **Pack + Card Combination**: StandardPack containing StandardCard with Gold Seal (+$3 when scored - pack economy source)
- **Vouchers**: SeedMoney, MoneyTree, TarotMerchant, PlanetMerchant

**Smart Economy Strategy:**
- Use "should" clauses with scores for economy items (don't require them all)
- Focus on Antes 1-3 for economy (early game is when money matters most)
- Consider voucher availability (TarotMerchant/PlanetMerchant make tarots/planets more common)
- Gold Seal on playing cards is reliable economy (appears in shops)
- StandardPack + StandardCard with Gold Seal is a pack-based economy source (+$3 when scored)
- **HangingChad + Gold Seal** is an extremely powerful combo - Common joker that triples Gold Seal money ($3 → $9)
- **Blueprint + HangingChad + Gold Seal** is EXTREMELY powerful - Blueprint (left) copies HangingChad's re-trigger, making Gold Seal $15! ($3 → $9 → $15)
- CouponTag is extremely powerful economy (makes entire shop FREE) - consider adding to "should" clauses for economy builds
- ReservedParking and MailInRebate are common jokers that generate money - good for early economy

### Ante-Specific Strategies
- **Ante 1**: Focus on jokers from Buffoon pack (2 jokers, $4 cost). Cannot request non-jokers in pack slot 0.
- **Ante 2**: Good for skip tags and economy items
- **Ante 3**: Boss Blind only - focus on BossTag, cannot use skip tags
- **Antes 4-8**: Full flexibility for all items and tags

### Rarity Considerations
- **Common Jokers**: Most abundant (70% chance), easier to find - use for "must" clauses
- **Uncommon Jokers**: Moderate rarity (25% chance) - can use in "must" but prefer "should"
- **Rare Jokers**: Harder to find (5% chance) - prefer "should" clauses, wider antes (1-8)
- **Legendary Jokers**: Very rare (<1% chance) - ALWAYS use "should" clauses, entire run (1-8)

**Smart Rarity Strategy:**
- Use "must" for common jokers only
- Use "should" for uncommon+ jokers with scores
- Request rare/legendary jokers across entire run (antes: [1,2,3,4,5,6,7,8])
- Consider multiple "should" clauses for rare jokers (increases chances)

### Pack Slot Rules
- **Slot 0 (First Pack)**: In Ante 1, MUST be Buffoon pack (2 jokers). Cannot be non-joker items.
  - **Buffoon pack = ONLY jokers** (cannot contain playing cards, tarots, planets, etc.)
  - **Standard pack = Can contain playing cards** (with enhancements, seals, etc.)
- **Other Slots**: Can be any pack type (Standard, Arcana, Celestial, etc.)
- **Player Agency**: Player can choose which pack to open first - "seeing" a pack doesn't trigger re-rolls, only opening does!

---

## Common Patterns

### Slang Translations
- "blurry face joker" → SmearedJoker
- "face chad" → HangingChad + Photograph (both jokers)
- "dice" → OopsAll6s
- "wee" → WeeJoker
- "bus" → RideTheBus
- "blueprint" → Blueprint
- "brain" → Brainstorm
- "econ"/"economy" → Money sources (see Early Economy Focus)

### Common Card Name Fixes
- "Lucky Cat" → LuckyCat
- "Oops All Six" → OopsAll6s
- "Oops All 6s" → OopsAll6s
- "Score by Chad" → ScoreByChad

### Typo Fixes
- "Auntie One" → "Ante 1"
- "Auntie [number]" → "Ante [number]" (but preserve "Antimatter")
- "Anti-[number]" → "Ante [number]" (but preserve "anti-one" for exclusions)

### Exclusion Patterns
- "anti-one [item]" → mustNot: [{ type: "Joker", value: "[item]" }]
- "no [item]" → mustNot: [{ type: "Joker", value: "[item]" }]
- "without [item]" → mustNot: [{ type: "Joker", value: "[item]" }]

---

## Impossible Configs

### NEVER Generate These (They Will Never Return Seeds)

1. **Non-joker items in Ante 1 pack slot 0**
   - ❌ Tarot cards in Ante 1 pack slot 0
   - ❌ Spectral cards in Ante 1 pack slot 0
   - ❌ Planet cards in Ante 1 pack slot 0
   - ✅ Jokers in Ante 1 pack slot 0 (valid - Buffoon pack)

2. **Skip tags in Ante 3**
   - ❌ NegativeTag in Ante 3
   - ❌ StandardTag in Ante 3
   - ❌ MeteorTag in Ante 3
   - ❌ BuffoonTag in Ante 3
   - ❌ HandyTag in Ante 3
   - ❌ GarbageTag in Ante 3
   - ❌ EtherealTag in Ante 3
   - ❌ TopupTag in Ante 3
   - ❌ OrbitalTag in Ante 3
   - ✅ BossTag in Ante 3 (valid - Boss Blind)

3. **Invalid tags in Ante 1**
   - ❌ NegativeTag in Ante 1
   - ❌ StandardTag in Ante 1
   - ❌ MeteorTag in Ante 1
   - ❌ BuffoonTag in Ante 1
   - ❌ HandyTag in Ante 1
   - ❌ GarbageTag in Ante 1
   - ❌ EtherealTag in Ante 1
   - ❌ TopupTag in Ante 1
   - ❌ OrbitalTag in Ante 1

### Valid Patterns

1. **Jokers in Ante 1 pack slot 0** ✅
   - Example: `{ type: "Joker", value: "Blueprint", antes: [1], packSlots: [0] }`

2. **Skip tags in Antes 2, 4-8** ✅
   - Example: `{ type: "Tag", value: "NegativeTag", antes: [2, 4, 5, 6, 7, 8] }`

3. **EtherealTag in Ante 2+** ✅
   - Example: `{ type: "Tag", value: "EtherealTag", antes: [2, 3, 4, 5, 6, 7, 8] }`

4. **BossTag in Ante 3** ✅
   - Example: `{ type: "Tag", value: "BossTag", antes: [3] }`

---

## Advanced Strategies

### Multi-Ante Requirements
When users request items "early" or "in the first few antes":
- Use `antes: [1, 2, 3]` for early game focus
- Use `antes: [1, 2, 3, 4]` for first half of run
- Use `antes: [1, 2, 3, 4, 5, 6, 7, 8]` for entire run

### Scoring Columns
Always ensure at least one `should` clause exists for scoring columns:
- Default: `{ type: "Joker", value: "Egg", score: 1 }`
- Economy: Add money sources with scores (GoldenTicket: 2, BusinessCard: 2, etc.)
- Synergy: Add complementary jokers with scores (Blueprint: 3 if Brainstorm requested, etc.)

**Smart Scoring Strategy:**
- Use higher scores (2-5) for items that synergize with "must" clauses
- Use lower scores (1-2) for general good items
- Add multiple "should" clauses to increase seed flexibility
- Consider rarity when assigning scores (rare items get higher scores)

### Edition Requests
When users request specific editions:
- "Negative [joker]" → `{ type: "Joker", value: "[joker]", edition: "Negative" }`
- "Foil [joker]" → `{ type: "Joker", value: "[joker]", edition: "Foil" }`
- "Poly [joker]" → `{ type: "Joker", value: "[joker]", edition: "Polychrome" }`

### Sticker Requests
When users request stickers:
- "Eternal [joker]" → `{ type: "Joker", value: "[joker]", stickers: ["Eternal"] }`
- "Rental [joker]" → `{ type: "Joker", value: "[joker]", stickers: ["Rental"] }`
- "Perishable [joker]" → `{ type: "Joker", value: "[joker]", stickers: ["Perishable"] }`

---

## JSON Schema Reference

### Filter Clause Structure
```json
{
  "type": "Joker" | "SoulJoker" | "Voucher" | "Tarot" | "Planet" | "Spectral" | "Tag" | "Boss" | "PlayingCard",
  "value": "Blueprint",  // Item name (enum value)
  "values": ["Blueprint", "Brainstorm"],  // Multiple items (OR)
  "edition": "None" | "Foil" | "Holographic" | "Polychrome" | "Negative",
  "seal": "None" | "Gold" | "Red" | "Blue" | "Purple",
  "enhancement": "None" | "Bonus" | "Mult" | "Wild" | "Glass" | "Steel" | "Stone" | "Gold" | "Lucky",
  "stickers": ["Eternal"] | ["Rental"] | ["Perishable"],
  "antes": [1, 2, 3],  // Which antes to check
  "packSlots": [0, 1],  // Which pack slots to check (0 = first pack)
  "score": 1,  // For should clauses (scoring weight)
  "label": "Blueprint (economy)",  // Human-readable label
  "clauses": []  // Nested AND/OR groups
}
```

### Config Structure
```json
{
  "name": "Filter Name",
  "description": "Filter description",
  "author": "JamlGenie",
  "deck": "Red" | "Blue" | "Yellow" | "Green" | "Black" | "Magic" | "Nebula" | "Ghost" | "Abandoned" | "Checkered" | "Zodiac" | "Painted" | "Anaglyph" | "Plasma" | "Erratic",
  "stake": "White" | "Red" | "Green" | "Black" | "Blue" | "Purple" | "Orange" | "Gold",
  "must": [],  // Required items (ALL must be found)
  "should": [],  // Preferred items (scoring)
  "mustNot": []  // Excluded items (NONE can be present)
}
```

---

## End of Brain Document

This knowledge base should be referenced when generating JAML filters to ensure:
1. Valid item names (exact enum values)
2. Valid antes and pack slots
3. Valid tags per ante
4. Proper exclusion handling
5. Economy-focused searches
6. Impossible config avoidance

Remember: The AI should always validate against these rules before generating JAML filters.

---

## Smart Generation Guidelines

### When to Use "must" vs "should"
- **Use "must"**: Common jokers, early antes (1-3), core build requirements
- **Use "should"**: Rare/legendary jokers, late antes (4-8), nice-to-have items, synergies

### When to Use Wide Antes
- **Narrow antes (1-3)**: Early game focus, economy builds, common jokers
- **Wide antes (1-8)**: Rare/legendary jokers, flexible builds, late-game items

### When to Use Pack Slots
- **Pack slot 0**: Ante 1 only (always Buffoon pack - jokers only)
- **Other pack slots**: When user specifically requests pack position
- **No pack slots**: More flexible (allows shop OR pack)

### When to Use Tags
- **Edition tags**: When user wants specific edition but doesn't care which joker
- **Rarity tags**: When user wants rarity but doesn't care which specific joker
- **Skip tags**: When user wants to skip specific antes (Antes 2, 4-8 only)
- **CouponTag**: When user wants economy - makes entire shop FREE (Antes 2, 4-8 only, small blind tag reward)

### Probability-Aware Generation
- **High probability items**: Use "must" clauses, narrow antes
- **Low probability items**: Use "should" clauses, wide antes, multiple options
- **Very rare items**: Always use "should", always use wide antes (1-8), add multiple alternatives

### Synergy-Aware Generation
- When user requests a joker, consider adding synergistic jokers to "should" clauses
- When user requests economy, add multiple economy sources to "should" clauses
- When user requests "on score" effects (Gold Seal, Foil edition), consider adding HangingChad to "should" clauses (triples the effect!)
- When user requests legendary joker, add its synergies to "should" clauses

### Voucher-Aware Generation
- Consider deck defaults when generating (Magic=CrystalBall, Nebula=Telescope, Zodiac=TarotMerchant+PlanetMerchant+Overstock)
- When searching for tarots/planets, consider voucher availability (TarotMerchant/PlanetMerchant increase rates)
- Vouchers are deterministic - if in seed, they always appear

### Edge Case Handling
- Erratic deck: Use erraticRank/erraticSuit, not regular rank/suit
- Ghost deck: Spectral cards available in shops
- Ante 3: Boss blind only, no skip tags
- Ante 1 slot 0: Always Buffoon pack (jokers only)
- **Conditional unlocks**: Some jokers require unlock conditions (e.g., LuckyCat requires Lucky enhancement first)


