\# Balatro Master Encyclopedia: Complete Items, Effects, and Synergies

This document is a comprehensive, production-grade reference manual containing the complete taxonomy, mechanical effects, and synergistic equations of Balatro's entities. It is formatted specifically as a high-density system prompt addition or training set context file for Natural Language-to-JAML (Jimbo's Ante Markup Language) translation engines, supporting the development of \`motely-wasm\` backends, \`jaml-ui\` interfaces, and the \`mcp.seedfinder.app/mcp\` server.

\---

\#\# SECTION 1: SYSTEM PARAMETERS — DECKS & STAKES

\#\#\# 1.1 The 15 Standard Decks  
A core input vector in JAML is the \`deck:\` root key. Mapping incorrect names or attempting to route unviable starting parameters will result in search failures. Below is the strict mechanical dictionary for all playable decks:

1\. \*\*Red Deck\*\* (\`Red\`):  
   \- \*\*Effect\*\*: Grants \+1 Discard per round.  
   \- \*\*JAML/Strategy\*\*: Excellent baseline for high-consistency discard strategies. Amplifies the value of Jokers that trigger upon discarding (e.g., \*Hit the Road\*, \*Yorick\*).  
2\. \*\*Blue Deck\*\* (\`Blue\`):  
   \- \*\*Effect\*\*: Grants \+1 Hand per round.  
   \- \*\*JAML/Strategy\*\*: Provides a stable scoring buffer. Ideal for hands that require deep cycling but require a final play.  
3\. \*\*Yellow Deck\*\* (\`Yellow\`):  
   \- \*\*Effect\*\*: Starts with \+$10 starting capital (Starts at $14 instead of $4).  
   \- \*\*JAML/Strategy\*\*: Drastically reduces the time needed to hit the standard $25 interest cap. Highly synergistic with early economic scaling Jokers (\*Golden Joker\*, \*Bootstraps\*, \*Bull\*).  
4\. \*\*Green Deck\*\* (\`Green\`):  
   \- \*\*Effect\*\*: Interest is entirely disabled. Earns $2 per played hand remaining and $1 per discard remaining at the end of each round.  
   \- \*\*JAML/Strategy\*\*: Inverts the economic model. Demands that JAML search filters prioritize immediate flat-scoring Jokers to minimize played hands and save discards, maximizing economic payout.  
5\. \*\*Black Deck\*\* (\`Black\`):  
   \- \*\*Effect\*\*: Grants \+1 Joker slot, but reduces played hands per round by 1\.  
   \- \*\*JAML/Strategy\*\*: High-ceiling, high-risk. Early game is extremely punishing due to the hand reduction. JAML filters should look for immediate, early-game flat chip or mult support to survive Ante 1 and 2\.  
6\. \*\*Magic Deck\*\* (\`Magic\`):  
   \- \*\*Effect\*\*: Starts the run with the \*\*Crystal Ball\*\* voucher (+1 consumable slot) and two copies of \*\*The Fool\*\* tarot card.  
   \- \*\*JAML/Strategy\*\*: Ideal for duplicating early spectral transformations or high-value tarots (such as \*Death\* or \*The Hermit\*).  
7\. \*\*Nebula Deck\*\* (\`Nebula\`):  
   \- \*\*Effect\*\*: Starts the run with the \*\*Telescope\*\* voucher (Celestial packs always contain the Planet card for your most played poker hand), but permanently reduces consumable slots by 1\.  
   \- \*\*JAML/Strategy\*\*: Restricts holding space for Tarots/Spectrals, forcing immediate commits. Highly synergistic with rapid planetary scaling.  
8\. \*\*Zodiac Deck\*\* (\`Zodiac\`):  
   \- \*\*Effect\*\*: Starts the run with \*\*Tarot Merchant\*\*, \*\*Planet Merchant\*\*, and \*\*Overstock\*\* vouchers pre-equipped in the meta-state.  
   \- \*\*JAML/Strategy\*\*: Vastly increases shop variety and rate of consumable spawns. Synergizes with economy-heavy seeds to abuse rolling shop streams.  
9\. \*\*Ghost Deck\*\* (\`Ghost\`):  
   \- \*\*Effect\*\*: Spectral cards can appear directly in standard shops. Starts the run with a \*\*Hex\*\* card (applies Polychrome to a random Joker, destroying all other Jokers).  
   \- \*\*JAML/Strategy\*\*: The ultimate environment for early Polychrome S-tier Jokers and deck modification. JAML filters should look for high-value early shop spectral items (\*Cryptid\*, \*Ectoplasm\*, \*Ankh\*).  
10\. \*\*Checkered Deck\*\* (\`Checkered\`):  
    \- \*\*Effect\*\*: Starting deck is restructured to contain exactly 26 Spades and 26 Hearts (eliminating Diamonds and Clubs).  
    \- \*\*JAML/Strategy\*\*: Simplifies flush and flush-five building to a statistical near-certainty. Strongly synergistic with \*Bloodstone\*, \*The Idol\*, and \*Wrathful/Lusty Joker\*.  
11\. \*\*Painted Deck\*\* (\`Painted\`):  
    \- \*\*Effect\*\*: Grants \+2 Hand Size, but permanently reduces Joker slots by 1\.  
    \- \*\*JAML/Strategy\*\*: Maximizes "held-in-hand" scaling triggers (\*Baron\*, \*Mime\*, Steel cards) and complex straights. JAML must actively account for the 4-Joker slot limit.  
12\. \*\*Anaglyph Deck\*\* (\`Anaglyph\`):  
    \- \*\*Effect\*\*: Grants a \*\*Double Tag\*\* every time a Boss Blind is defeated.  
    \- \*\*JAML/Strategy\*\*: Allows players to stack dozens of Double Tags to redeem them simultaneously on a single, high-value blind skip Tag (e.g., converting 15 Double Tags into 15 Negative Jokers or 15 Mega Arcana Packs).  
13\. \*\*Plasma Deck\*\* (\`Plasma\`):  
    \- \*\*Effect\*\*: Scores are calculated by balancing Chips and Multipliers: \`Score \= ((Chips \+ Mult) / 2)^2\`. Base blind size requirements are permanently doubled across all Antes.  
    \- \*\*JAML/Strategy\*\*: Fundamentally alters scoring mechanics. Flat additions (+Chips or \+Mult) are mathematically balanced, making cards like \*Stuntman\* or \*Bull\* insanely powerful in early game, scaling exponentially later.  
14\. \*\*Abandoned Deck\*\* (\`Abandoned\`):  
    \- \*\*Effect\*\*: Starting deck contains exactly 40 cards, having had all 12 face cards (Jacks, Queens, Kings) permanently removed.  
    \- \*\*JAML/Strategy\*\*: Increases the probability of drawing low-card straights and pairs. Renders face-card Jokers (\*Baron\*, \*Triboulet\*, \*Sock & Buskin\*, \*Photograph\*) completely inert.  
15\. \*\*Erratic Deck\*\* (\`Erratic\`):  
    \- \*\*Effect\*\*: Starting deck consists of 52 cards, but all suits and ranks are completely randomized at seed initialization.  
    \- \*\*JAML/Strategy\*\*: \*\*The only deck where \`ErraticSuit\` and \`ErraticRank\` JAML rules are valid\*\*. Used to find seeds starting with high-density ranks (e.g., 18 Aces) or highly skewed suits.

\---

\#\#\# 1.2 The 8 Stake Difficulties  
Difficulty stakes modify variables, shop costs, and item tags within the seed finder's procedural matrix.

1\. \*\*White Stake\*\* (\`White\`): Base configuration. No modifications.  
2\. \*\*Red Stake\*\* (\`Red\`): Small Blinds provide absolutely no monetary reward on victory. Skips are encouraged to secure early-game Tags.  
3\. \*\*Green Stake\*\* (\`Green\`): Required score scaling accelerates for each successive Ante. Requires geometric scaling engines by Ante 3\.  
4\. \*\*Black Stake\*\* (\`Black\`): \*\*Eternal Jokers\*\* are introduced to the shop pool (30% chance of spawning on any Joker). Eternal Jokers cannot be sold or destroyed, permanently blocking self-destruct synergies like \*Madness\* or \*Ceremonial Dagger\*.  
5\. \*\*Blue Stake\*\* (\`Blue\`): Player's baseline discard pool is reduced by 1\. Severely impacts discard economies (\*Merry Andy\*, \*Hit the Road\*).  
6\. \*\*Purple Stake\*\* (\`Purple\`): Required scoring accelerates exponentially. Flat additions become unviable past Ante 4\.  
7\. \*\*Orange Stake\*\* (\`Orange\`): \*\*Perishable Jokers\*\* are introduced to the shop pool (30% chance). Perishable Jokers are permanently debuffed and rendered completely inert after 5 rounds. This blocks long-term scaling engines (\*Wee Joker\*, \*Constellation\*) if they spawn with the modifier.  
8\. \*\*Gold Stake\*\* (\`Gold\`): \*\*Rental Jokers\*\* are introduced to the shop pool (30% chance). Rental Jokers cost $3 per round to maintain in the inventory. Enforces massive economic stress. JAML search queries must prioritize robust early interest-generating engines.

\#\# SECTION 2: THE CONSUMABLE COMPENDIUM

Consumables are spawned via Arcana, Celestial, and Spectral booster packs, or through specialized shop generation pools. Standardizing their JAML representations is critical for precise filter matching.

\#\#\# 2.1 The 22 Tarot Cards (Arcana Pool)  
Tarot cards are primarily used for economy generation, deck modification, and card enhancement.

1\. \*\*The Fool\*\* (\`tarot: The Fool\`):  
   \- \*\*Effect\*\*: Spawns the last Tarot or Planet card used during the run (excluding \*The Fool\* itself).  
   \- \*\*Strategy\*\*: Crucial for duplicating high-value deck-fixing consumables like \*Death\* or \*Strength\*.  
2\. \*\*The Magician\*\* (\`tarot: The Magician\`):  
   \- \*\*Effect\*\*: Enhances up to 2 selected playing cards in hand to \*\*Lucky Cards\*\*.  
   \- \*\*Strategy\*\*: Triggers probability-based Jokers. Essential enabler for \*Lucky Cat\* and \*Bloodstone\* setups.  
3\. \*\*The High Priestess\*\* (\`tarot: The High Priestess\`):  
   \- \*\*Effect\*\*: Spawns up to 2 random Planet cards matching any unlocked hands.  
   \- \*\*Strategy\*\*: Accelerates early-game level progression.  
4\. \*\*The Empress\*\* (\`tarot: The Empress\`):  
   \- \*\*Effect\*\*: Enhances up to 2 selected playing cards in hand to \*\*Mult Cards\*\* (+4 Mult when scored).  
   \- \*\*Strategy\*\*: Provides highly reliable early-game flat mult.  
5\. \*\*The Emperor\*\* (\`tarot: The Emperor\`):  
   \- \*\*Effect\*\*: Spawns up to 2 random Tarot cards directly into the consumable slots.  
   \- \*\*Strategy\*\*: Excellent utility and action-economy generator.  
6\. \*\*The Hierophant\*\* (\`tarot: The Hierophant\`):  
   \- \*\*Effect\*\*: Enhances up to 2 selected playing cards in hand to \*\*Bonus Cards\*\* (+30 Chips when scored).  
   \- \*\*Strategy\*\*: Strong flat-chip scoring aid, especially on high-mult low-chip hands.  
7\. \*\*The Lovers\*\* (\`tarot: The Lovers\`):  
   \- \*\*Effect\*\*: Enhances 1 selected playing card in hand to a \*\*Wild Card\*\* (can fit any suit).  
   \- \*\*Strategy\*\*: Vital for flush/flush-five consistency prior to complete deck homogenization.  
8\. \*\*The Chariot\*\* (\`tarot: The Chariot\`):  
   \- \*\*Effect\*\*: Enhances 1 selected playing card in hand to a \*\*Steel Card\*\* (X1.5 Mult while held in hand).  
   \- \*\*Strategy\*\*: \*\*The cornerstone of late-game exponential scaling\*\* when combined with \*Mime\* and \*Baron\*.  
9\. \*\*Justice\*\* (\`tarot: Justice\`):  
   \- \*\*Effect\*\*: Enhances 1 selected playing card in hand to a \*\*Glass Card\*\* (X2 Mult when scored, 1 in 4 chance to destroy card upon scoring).  
   \- \*\*Strategy\*\*: Ideal for burst scoring to defeat high-blind benchmarks. Pair with \*Oops\! All 6s\* to control destruction rates.  
10\. \*\*The Hermit\*\* (\`tarot: The Hermit\`):  
    \- \*\*Effect\*\*: Doubles current player capital (Max payout of $20).  
    \- \*\*Strategy\*\*: The absolute premier early-game economy card.  
11\. \*\*The Wheel of Fortune\*\* (\`tarot: The Wheel of Fortune\`):  
    \- \*\*Effect\*\*: 1 in 4 chance to add a random Edition (Foil, Holographic, Polychrome) to a random Joker currently in the inventory.  
    \- \*\*Strategy\*\*: Highly speculative. Often used as a benchmark for testing RNG seed streams.  
12\. \*\*Strength\*\* (\`tarot: Strength\`):  
    \- \*\*Effect\*\*: Increases the rank of up to 2 selected playing cards in hand by exactly 1 (e.g., Queens become Kings, Aces become 2s).  
    \- \*\*Strategy\*\*: Vital for deck-fixing towards Kings (for \*Baron\* / \*Triboulet\*) or Aces.  
13\. \*\*The Hanged Man\*\* (\`tarot: The Hanged Man\`):  
    \- \*\*Effect\*\*: Permanently destroys up to 2 selected playing cards from the deck.  
    \- \*\*Strategy\*\*: Primary deck-culling mechanism to eliminate low-value cards and optimize draw consistency.  
14\. \*\*Death\*\* (\`tarot: Death\`):  
    \- \*\*Effect\*\*: Converts the leftmost selected card into an exact duplicate of the rightmost selected card in hand.  
    \- \*\*Strategy\*\*: \*\*The definitive deck-homogenization tool\*\*. Retains all seals, editions, and enhancements.  
15\. \*\*Temperance\*\* (\`tarot: Temperance\`):  
    \- \*\*Effect\*\*: Grants cash equal to the total sell value of all Jokers currently owned (Max payout of $50).  
    \- \*\*Strategy\*\*: Highly synergistic with high-rarity or highly modified Joker builds.  
16\. \*\*The Devil\*\* (\`tarot: The Devil\`):  
    \- \*\*Effect\*\*: Enhances 1 selected playing card in hand to a \*\*Gold Card\*\* (Earns $3 if held in hand at the end of a round).  
    \- \*\*Strategy\*\*: Critical long-term economic support. Feeds \*Vampire\* when paired with \*Midas Mask\*.  
17\. \*\*The Tower\*\* (\`tarot: The Tower\`):  
    \- \*\*Effect\*\*: Enhances 1 selected playing card to a \*\*Stone Card\*\* (+50 Chips, scores without rank or suit).  
    \- \*\*Strategy\*\*: Synergizes with flat-chip builds. Bypasses suit debuffs and hand restrictions.  
18\. \*\*The Star\*\* (\`tarot: The Star\`):  
    \- \*\*Effect\*\*: Converts up to 3 selected playing cards in hand to \*\*Diamonds\*\*.  
    \- \*\*Strategy\*\*: Direct flush-building aid.  
19\. \*\*The Moon\*\* (\`tarot: The Moon\`):  
    \- \*\*Effect\*\*: Converts up to 3 selected playing cards in hand to \*\*Clubs\*\*.  
    \- \*\*Strategy\*\*: Direct flush-building aid.  
20\. \*\*The Sun\*\* (\`tarot: The Sun\`):  
    \- \*\*Effect\*\*: Converts up to 3 selected playing cards in hand to \*\*Hearts\*\*.  
    \- \*\*Strategy\*\*: Highly synergistic with \*Bloodstone\* probability runs.  
21\. \*\*Judgement\*\* (\`tarot: Judgement\`):  
    \- \*\*Effect\*\*: Spawns a random Joker card directly into any empty slot in the inventory.  
    \- \*\*Strategy\*\*: Useful early-game gamble to secure scoring or utility pieces.  
22\. \*\*The World\*\* (\`tarot: The World\`):  
    \- \*\*Effect\*\*: Converts up to 3 selected playing cards in hand to \*\*Spades\*\*.  
    \- \*\*Strategy\*\*: Direct flush-building aid.

\---

\#\#\# 2.2 The 12 Planet Cards (Celestial Pool)  
Planet cards scale the base Chips and Multiplier parameters of specific poker hand shapes.

1\. \*\*Mercury\*\* (\`planet: Mercury\`): Upgrades \*\*Pair\*\* (+15 Chips, \+1 Mult).  
2\. \*\*Venus\*\* (\`planet: Venus\`): Upgrades \*\*Three of a Kind\*\* (+20 Chips, \+2 Mult).  
3\. \*\*Earth\*\* (\`planet: Earth\`): Upgrades \*\*Full House\*\* (+25 Chips, \+2 Mult).  
4\. \*\*Mars\*\* (\`planet: Mars\`): Upgrades \*\*Four of a Kind\*\* (+30 Chips, \+3 Mult).  
5\. \*\*Jupiter\*\* (\`planet: Jupiter\`): Upgrades \*\*Flush\*\* (+15 Chips, \+2 Mult).  
6\. \*\*Saturn\*\* (\`planet: Saturn\`): Upgrades \*\*Straight\*\* (+30 Chips, \+4 Mult). Highly dominant early game.  
7\. \*\*Uranus\*\* (\`planet: Uranus\`): Upgrades \*\*Two Pair\*\* (+20 Chips, \+1 Mult).  
8\. \*\*Neptune\*\* (\`planet: Neptune\`): Upgrades \*\*Straight Flush\*\* (+40 Chips, \+4 Mult).  
9\. \*\*Pluto\*\* (\`planet: Pluto\`): Upgrades \*\*High Card\*\* (+10 Chips, \+1 Mult). The core scaling metric for endless high-card builds.  
10\. \*\*Planet X\*\* (\`planet: Planet X\`): Upgrades \*\*Five of a Kind\*\* (+35 Chips, \+3 Mult). (Secret hand, unlocked by playing 5 cards of the same rank).  
11\. \*\*Ceres\*\* (\`planet: Ceres\`): Upgrades \*\*Flush House\*\* (+40 Chips, \+4 Mult). (Secret hand, unlocked by playing a Full House where all cards share a suit).  
12\. \*\*Eris\*\* (\`planet: Eris\`): Upgrades \*\*Flush Five\*\* (+50 Chips, \+3 Mult). (The highest scaling secret hand, unlocked by playing 5 cards of identical rank and suit).

\---

\#\#\# 2.3 The 18 Spectral Cards (Spectral Pool)  
Spectral cards represent high-risk, high-reward procedural interventions, often carrying significant structural penalties such as reduced hand size or total capital depletion.

1\. \*\*Familiar\*\* (\`spectral: Familiar\`): Destroys 1 random card in hand, spawns 3 random Enhanced \*\*Face Cards\*\*.  
2\. \*\*Grim\*\* (\`spectral: Grim\`): Destroys 1 random card in hand, spawns 2 random Enhanced \*\*Aces\*\*.  
3\. \*\*Incantation\*\* (\`spectral: Incantation\`): Destroys 1 random card in hand, spawns 4 random Enhanced \*\*Numbered Cards\*\*.  
4\. \*\*Talisman\*\* (\`spectral: Talisman\`): Applies a \*\*Gold Seal\*\* to 1 selected card in hand. (Gold sealed cards grant $3 upon being played).  
5\. \*\*Aura\*\* (\`spectral: Aura\`): Applies a random Edition (Foil, Holographic, Polychrome) to 1 selected card in hand.  
6\. \*\*Deja Vu\*\* (\`spectral: Deja Vu\`): Applies a \*\*Red Seal\*\* to 1 selected card in hand. (Red sealed cards retrigger their played or held effects exactly once). \*\*Highest priority modifier for endless builds\*\*.  
7\. \*\*Trance\*\* (\`spectral: Trance\`): Applies a \*\*Blue Seal\*\* to 1 selected card in hand. (Blue sealed cards generate a random Planet card if held in hand at round's end).  
8\. \*\*Medium\*\* (\`spectral: Medium\`): Applies a \*\*Purple Seal\*\* to 1 selected card in hand. (Purple sealed cards generate a random Tarot card upon discard). Excellent for action-economy and deck-fixing.  
9\. \*\*Ouija\*\* (\`spectral: Ouija\`): Converts all cards in hand to a single random Rank, but permanently reduces player's hand size by 1\.  
10\. \*\*Sigil\*\* (\`spectral: Sigil\`): Converts all cards in hand to a single random Suit.  
11\. \*\*Cryptid\*\* (\`spectral: Cryptid\`): Generates exactly 2 permanent copies of a selected card in hand, retaining all enhancements, editions, and seals.  
12\. \*\*Wraith\*\* (\`spectral: Wraith\`): Spawns a random \*\*Rare Joker\*\* but sets player's total capital to exactly $0.  
13\. \*\*Immolate\*\* (\`spectral: Immolate\`): Permanently destroys 5 random cards in hand, immediately granting $20. Excellent deck-culling and economy tool.  
14\. \*\*Ankh\*\* (\`spectral: Ankh\`): Creates an exact duplicate of a random Joker in your inventory, but destroys all other Jokers currently held.  
15\. \*\*Hex\*\* (\`spectral: Hex\`): Applies the \*\*Polychrome\*\* edition (X1.5 Mult) to a random Joker in your inventory, but destroys all other Jokers currently held.  
16\. \*\*Ectoplasm\*\* (\`spectral: Ectoplasm\`): Applies the \*\*Negative\*\* edition (+1 Joker slot) to a random Joker in your inventory, but permanently reduces player's hand size by 1\.  
17\. \*\*The Soul\*\* (\`spectral: The Soul\`): Has a fixed \*\*0.3% spawn chance\*\* within Spectral or Arcana Packs. Spawns a random \*\*Legendary Joker\*\* (Canio, Triboulet, Yorick, Chicot, Perkeo).  
18\. \*\*Black Hole\*\* (\`spectral: Black Hole\`): Has a fixed \*\*0.3% spawn chance\*\* within Spectral or Celestial Packs. Instantly upgrades every single poker hand level by exactly 1\.

\#\# SECTION 3: VOUCHER TIER MATRIX

There are exactly \*\*32 Vouchers\*\* in Balatro, split into 16 Tier-1 Vouchers and 16 corresponding Tier-2 Vouchers. Buying a Tier-1 Voucher is a hard programmatic gateway required to unlock its Tier-2 variant in future shops.

| Tier-1 Voucher JAML Key | Tier-1 Effect | Tier-2 Voucher JAML Key | Tier-2 Effect (Requires Tier-1) | Strategic Implication |  
| :--- | :--- | :--- | :--- | :--- |  
| \`Overstock\` | \+1 card slot in shops. | \`Overstock Plus\` | Adds another \+1 card slot in shops (total of 5 slots). | Increases overall rate of finding Jokers and consumables. |  
| \`Clearance Sale\` | All shop items cost 25% less. | \`Liquidation\` | All shop items cost 50% less. | Vital for gold/rental stake mitigation and reroll-heavy runs. |  
| \`Hone\` | Foil, Holographic, and Polychrome cards appear 2x more often. | \`Glow\` | Foil, Holographic, and Polychrome cards appear 4x more often. | Accelerates the rate of finding Negative and Polychrome scaling elements. |  
| \`Reroll Surplus\` | Shop rerolls cost $2 less. | \`Reroll Glut\` | Shop rerolls cost $2 less (stacked, making rerolls extremely cheap). | Enables aggressive deep-shop hunting for S-tier synergies. |  
| \`Crystal Ball\` | \+1 maximum consumable card slot. | \`Omen Globe\` | Spectral cards can appear in Arcana packs. | Crucial for holding \*Death\* and \*Steel\* cards; enables \*The Soul\* spawns in Tarot packs. |  
| \`Telescope\` | Celestial Packs always contain planet for most played hand. | \`Observatory\` | Planet cards held in consumable slots grant X1.5 Mult to their respective hand. | \*\*Provides infinite, compounding X1.5 multipliers\*\* via \*Perkeo\* duplication. |  
| \`Grabber\` | Permanently grants \+1 Hand per round. | \`Nacho Tong\` | Permanently grants \+1 Hand per round (total of \+2). | Extends scoring opportunities and survival rate. |  
| \`Wasteful\` | Permanently grants \+1 Discard per round. | \`Recyclomancy\` | Permanently grants \+1 Discard per round (total of \+2). | Powers discard scaling Jokers (\*Hit the Road\*, \*Yorick\*, \*Castle\*). |  
| \`Tarot Merchant\` | Tarot cards appear 2x more frequently in the shop. | \`Tarot Tycoon\` | Tarot cards appear 4x more frequently in the shop. | Enhances deck-fixing and duplication rates. |  
| \`Planet Merchant\` | Planet cards appear 2x more frequently in the shop. | \`Planet Tycoon\` | Planet cards appear 4x more frequently in the shop. | Facilitates fast, flat planetary scoring levelups. |  
| \`Director's Cut\` | Allows the player to reroll the Boss Blind once per Ante for $10. | \`Retcon\` | Allows the player to reroll the Boss Blind infinitely per Ante (starts at $10). | \*\*Essential protection against counter Boss Blinds\*\* (e.g., rerolling \*The Plant\*). |  
| \`Paint Brush\` | Grants \+1 Hand Size. | \`Palette\` | Grants \+1 Hand Size (total of \+2). | \*\*Directly multiplies held-in-hand multiplier mechanics\*\*. |  
| \`Magic Trick\` | Standard playing cards can appear in shops. | \`Illusion\` | Playing cards purchased in shops can have Enhancements, Editions, or Seals. | Eliminates reliance on Standard Packs for deck-fixing. |  
| \`Blank\` | \*\*Has absolutely no mechanical effect\*\* (pure progression gateway). | \`Antimatter\` | Grants \+1 Joker slot in the active inventory. | \*\*The single highest-value general voucher in the game\*\*. |  
| \`Seed Money\` | Raises the maximum interest cap to $10 (reached at $50). | \`Money Tree\` | Raises the maximum interest cap to $20 (reached at $100). | Supports late-game gold hoarding and \*Bootstraps\* / \*Bull\* scaling. |  
| \`Hieroglyph\` | Permanently decreases current Ante by 1, but subtracts 1 Hand. | \`Petroglyph\` | Permanently decreases current Ante by 1, but subtracts 1 Discard. | \*\*Grants an additional Ante of shop rerolls\*\* and scaling time. |

\---

\#\# SECTION 4: BOSS BLIND TAXONOMY & DISRUPTION MAPPING

There are exactly \*\*28 Boss Blinds\*\* in Balatro. To route seeds successfully, JAML configurations utilize \`mustNot\` clauses to avoid blinds that disable the player's core strategy.

\#\#\# 4.1 The Phoenician Name Meanings  
The game's 28 Boss Blinds are named after specific, historically mapped concepts derived from the Phoenician alphabet and celestial elements.  
\- \*Alef\* maps to \*\*The Ox\*\*  
\- \*Bet\* maps to \*\*The House\*\*  
\- \*Gimel\* maps to \*\*The Club\*\*  
\- \*Dalet\* maps to \*\*The Goad\*\*  
\- \*He\* maps to \*\*The Window\*\*  
\- \*Vav\* maps to \*\*The Head\*\*  
\- \*Zayin\* maps to \*\*The Wheel\*\*  
\- \*Het\* maps to \*\*The Wall\*\*  
\- \*Tet\* maps to \*\*The Flint\*\*  
\- \*Yod\* maps to \*\*The Pillar\*\*  
\- \*Kaf\* maps to \*\*The Eye\*\*  
\- \*Lamed\* maps to \*\*The Plant\*\*  
\- \*Mem\* maps to \*\*The Water\*\*  
\- \*Nun\* maps to \*\*The Needle\*\*  
\- \*Samekh\* maps to \*\*The Head\*\*  
\- \*Ayin\* maps to \*\*The Fish\*\*  
\- \*Pe\* maps to \*\*The Mouth\*\*  
\- \*Tsadi\* maps to \*\*The Tooth\*\*  
\- \*Qof\* maps to \*\*The Mark\*\*  
\- \*Resh\* maps to \*\*The Arm\*\*  
\- \*Shin\* maps to \*\*The Psychic\*\*  
\- \*Tav\* maps to \*\*The Hook\*\*

\#\#\# 4.2 Mechanical Disruption Categories

\#\#\#\# Category A: The Debuffers (Disables cards)  
\- \*\*The Club\*\* (\`boss: The Club\`): Debuffs all Club cards.  
\- \*\*The Goad\*\* (\`boss: The Goad\`): Debuffs all Spade cards.  
\- \*\*The Window\*\* (\`boss: The Window\`): Debuffs all Diamond cards.  
\- \*\*The Head\*\* (\`boss: The Head\`): Debuffs all Heart cards.  
\- \*\*The Plant\*\* (\`boss: The Plant\`): Debuffs all Face Cards (Jacks, Queens, Kings). \*\*Absolute build-killer for Baron, Triboulet, and Photograph runs\*\*.  
\- \*\*The Pillar\*\* (\`boss: The Pillar\`): Debuffs any playing card played previously in the current Ante.

\#\#\#\# Category B: The Restrictors (Bans optimal play patterns)  
\- \*\*The Psychic\*\* (\`boss: The Psychic\`): Every played hand must contain exactly 5 cards. Prevents playing isolated high-card or pair builds.  
\- \*\*The Eye\*\* (\`boss: The Eye\`): Disallows playing repeat hand types in the current round.  
\- \*\*The Mouth\*\* (\`boss: The Mouth\`): Restricts the player to playing only one specific hand type for the entire round (set by the first hand played).  
\- \*\*The Needle\*\* (\`boss: The Needle\`): Restricts the player to playing exactly 1 hand for the entire round.

\#\#\#\# Category C: The Obfuscators (Hides information)  
\- \*\*The House\*\* (\`boss: The House\`): The first drawn hand is drawn face down.  
\- \*\*The Wheel\*\* (\`boss: The Wheel\`): 1 in 7 cards are drawn face down. (Oops\! All 6s increases this to 2 in 7).  
\- \*\*The Fish\*\* (\`boss: The Fish\`): Draws all cards face down after a hand is played.  
\- \*\*The Mark\*\* (\`boss: The Mark\`): Draws all Face Cards face down.

\#\#\#\# Category D: The Scalers (Elevates score caps)  
\- \*\*The Wall\*\* (\`boss: The Wall\`): Extends the required blind scoring benchmark to 4X the base Ante requirement.  
\- \*\*The Flint\*\* (\`boss: The Flint\`): Halves all base Chips and Multipliers for the played hand's base level (highly punitive early game).

\#\#\#\# Category E: Economic / Meta Disrupters (Long-term structural penalties)  
\- \*\*The Ox\*\* (\`boss: The Ox\`): Sets the player's capital to exactly $0 if they play their most frequently played poker hand.  
\- \*\*The Tooth\*\* (\`boss: The Tooth\`): Subtracts $1 from the player's capital for every card played (penalizes high-card spamming).  
\- \*\*The Arm\*\* (\`boss: The Arm\`): Permanently decreases the level of the played hand type by 1\.  
\- \*\*The Hook\*\* (\`boss: The Hook\`): Involuntarily discards 2 random cards from hand after every play.  
\- \*\*The Manacle\*\* (\`boss: The Manacle\`): Temporarily reduces the player's hand size by 1\.  
\- \*\*The Water\*\* (\`boss: The Water\`): Removes all discards for the round.

\#\#\# 4.3 Finisher Blinds (Ante 8 Showdowns)  
The concluding Boss of Ante 8 is guaranteed to be one of five "Finisher Blinds":  
1\. \*\*Cerulean Bell\*\* (\`boss: Cerulean Bell\`): Forces a random card in hand to be permanently selected; selling it or discarding it triggers debuffs.  
2\. \*\*Violet Vessel\*\* (\`boss: Violet Vessel\`): Triples the baseline required score benchmark.  
3\. \*\*Crimson Heart\*\* (\`boss: Crimson Heart\`): Disables 1 random Joker card in your inventory after every hand played.  
4\. \*\*Verdant Leaf\*\* (\`boss: Verdant Leaf\`): Debuffs every single card in your deck unless you sell at least 1 Joker card.  
5\. \*\*Amber Acorn\*\* (\`boss: Amber Amber\`): Shuffles all Jokers in the inventory and flips them face down, hiding their identities and trigger positions.

\#\# SECTION 5: JOKER DICTIONARY & ENTITY NORMALIZATION

To prevent failed search combinations, this dictionary lists the most crucial Jokers, their rarities, internal naming conventions, and JAML strategic classifications.

\#\#\# 5.1 Naming Normalization Key  
\- \*Stencil Joker\* $  
ightarrow$ \*\*\`Joker Stencil\`\*\* (Uncommon, $6)  
\- \*Eight Ball\* $  
ightarrow$ \*\*\`8 Ball\`\*\* (Common, $5)  
\- \*Caino\* $  
ightarrow$ \*\*\`Canio\`\*\* (Legendary, Soul-exclusive)  
\- \*Abstract\* $  
ightarrow$ \*\*\`Abstract Joker\`\*\* (Common, $4)  
\- \*Blueprint\* $  
ightarrow$ \*\*\`Blueprint\`\*\* (Rare, $10)  
\- \*Brainstorm\* $  
ightarrow$ \*\*\`Brainstorm\`\*\* (Rare, $10)

\#\#\# 5.2 Category A: The Copy & Retrigger Foundations (S-Tier)  
1\. \*\*Blueprint\*\* (\`joker: Blueprint\`): Rare. Copies the ability of the Joker card directly to its right. \*\*The most flexible JAML query target\*\*.  
2\. \*\*Brainstorm\*\* (\`joker: Brainstorm\`): Rare. Copies the ability of the leftmost Joker currently held in the inventory.  
3\. \*\*Sock & Buskin\*\* (\`joker: Sock & Buskin\`): Uncommon. Retriggers all played Face Cards exactly once.  
4\. \*\*Hanging Chad\*\* (\`joker: Hanging Chad\`): Common. Retriggers the first played card scored in a hand exactly 2 additional times.  
5\. \*\*Hack\*\* (\`joker: Hack\`): Uncommon. Retriggers all played 2s, 3s, 4s, and 5s exactly once.  
6\. \*\*Mime\*\* (\`joker: Mime\`): Uncommon. Retriggers all card held-in-hand abilities (Steel enhancements, Gold enhancements, Baron Kings, Shoot the Moon Queens).  
7\. \*\*Dusk\*\* (\`joker: Dusk\`): Uncommon. Retriggers all played cards scored in the final played hand of any round.

\#\#\# 5.3 Category B: Exponential Multipliers (X-Mult)  
1\. \*\*Baron\*\* (\`joker: Baron\`): Rare. Each King card held in the player's hand during scoring grants an independent X1.5 Mult.  
2\. \*\*Triboulet\*\* (\`joker: Triboulet\`): Legendary. Each King and Queen played and scored grants an independent X2 Mult.  
3\. \*\*The Idol\*\* (\`joker: The Idol\`): Uncommon. Grants X2 Mult for each card scored that matches a randomly assigned Rank and Suit (changes every round, only pulls from cards in player's current deck).  
4\. \*\*Bloodstone\*\* (\`joker: Bloodstone\`): Uncommon. 1 in 2 chance for played Heart cards to grant X1.5 Mult when scored. (Oops\! All 6s makes this 100% consistent).  
5\. \*\*Vampire\*\* (\`joker: Vampire\`): Uncommon. Permanently gains X0.2 Mult for each card enhancement scored (e.g., Gold, Steel), stripping the card of its enhancement.  
6\. \*\*Hologram\*\* (\`joker: Hologram\`): Uncommon. Gains X0.25 Mult every time a playing card is permanently added to the deck.  
7\. \*\*Constellation\*\* (\`joker: Constellation\`): Uncommon. Gains X0.1 Mult every time a Planet card is used.  
8\. \*\*Cavitational Cavendish\*\* (\`joker: Cavendish\`): Common. Unlocked by destroying a \*Gros Michel\*. Grants X3 Mult; has a 1 in 1000 chance to self-destruct.

\#\#\# 5.4 Category C: Flat Multipliers & Chip Scaling  
1\. \*\*Stuntman\*\* (\`joker: Stuntman\`): Rare. Grants \+250 Chips, but permanently reduces player's hand size by 2\.  
2\. \*\*Bull\*\* (\`joker: Bull\`): Uncommon. Grants \+2 Chips for every $1 currently held in player's capital. (Unbounded scaling).  
3\. \*\*Bootstraps\*\* (\`joker: Bootstraps\`): Uncommon. Grants \+4 Mult for every $5 held in player's capital.  
4\. \*\*Wee Joker\*\* (\`joker: Wee Joker\`): Rare. Starts at \+10 Chips. Permanently gains \+8 Chips every time a 2 is scored.  
5\. \*\*Joker Stencil\*\* (\`joker: Joker Stencil\`): Uncommon. X1 Mult for each empty Joker slot (Stencil itself is counted as an empty slot).  
6\. \*\*Green Joker\*\* (\`joker: Green Joker\`): Common. Gains \+1 Mult per hand played, \+12 Chips per discard remaining, but loses 1 Mult per discard used.  
7\. \*\*Ride the Bus\*\* (\`joker: Ride the Bus\`): Common. Gains \+1 Mult per consecutive hand played without scoring a Face Card (resets to zero if a Face Card is scored).

\---

\#\# SECTION 6: THE SYNERGY BIBLE — META COMBINATIONS & MATHEMATICS

To find true "God Seeds," JAML filters must look for compound systems where cards multiply one another rather than add linearly.

\#\#\# 6.1 The Zenith: Baron \+ Mime Held-in-Hand Exponential Loop  
The ultimate mathematical engine in Balatro is constructed using \*\*Steel Kings held in hand\*\* paired with \*\*Baron\*\* and \*\*Mime\*\*.

\#\#\#\# The Math  
\- \*\*Base Steel Enhancement\*\*: Holds a permanent X1.5 multiplier while in hand.  
\- \*\*Baron\*\*: Adds an independent X1.5 multiplier for every King held in hand.  
\- \*\*Mime\*\*: Forces all held abilities to trigger a second time.

If a player holds exactly one \*\*Steel King\*\* in hand:  
\- The card triggers naturally: \*\*Steel\*\* (X1.5) \+ \*\*Baron\*\* (X1.5).  
\- \*\*Mime\*\* forces a retrigger: \*\*Steel\*\* (X1.5) \+ \*\*Baron\*\* (X1.5).  
This single card produces:  
\`Score Mult \= 1.5 \* 1.5 \* 1.5 \* 1.5 \= (1.5)^4 \= X5.0625\`

If the player has cloned Kings and expanded their hand size to hold \*\*8 Steel Kings\*\* simultaneously:  
\`Total Mult \= (1.5)^(8 \* 4\) \= (1.5)^32 \= X431,439\`

If the player copies Baron or Mime using \*\*Blueprint\*\* or \*\*Brainstorm\*\*, the exponent escalates into the thousands:  
\`Total Mult \= (1.5)^(Kings \* Triggers \* Copy-stacking)\`  
This exponential loop easily bypasses standard mathematical limits, triggering scores past $10^{100}$ (Scientific Notation) and eventually hitting the floating-point infinity limit (\*\*naneinf\*\*).

\---

\#\#\# 6.2 The Photochad Retrigger  
A highly efficient early-game setup that utilizes two cheap Common Jokers: \*\*Photograph\*\* and \*\*Hanging Chad\*\*.

\- \*\*Photograph\*\*: Grants an X2 Multiplier strictly to the first face card scored in a played hand.  
\- \*\*Hanging Chad\*\*: Retriggers the first played card scored in a hand exactly 2 additional times.

When a Face Card (e.g., King of Hearts) is placed in the leftmost scoring slot of a played hand:  
1\. \*\*Trigger 1\*\*: Card scores $  
ightarrow$ triggers Photograph (X2).  
2\. \*\*Trigger 2 (Hanging Chad)\*\*: Card retriggers $  
ightarrow$ triggers Photograph again (X2).  
3\. \*\*Trigger 3 (Hanging Chad)\*\*: Card retriggers $  
ightarrow$ triggers Photograph a third time (X2).  
\`Total Mult \= 2 \* 2 \* 2 \= X8 Mult\` on a single Face Card.  
\- \*\*Scaling\*\*: If the card is enhanced to \*\*Glass\*\* (X2 on scoring), the Glass effect also triggers 3 times, scaling the card's output to \`X64 Mult\`. If the card possesses a \*\*Red Seal\*\*, it retriggers again, scaling to \`X128\` (or \`X256\` with Glass).

\---

\#\#\# 6.3 The Observatory Infinite (Perkeo Stack)  
\*\*Perkeo\*\* is a Legendary Joker that duplicates 1 random held consumable at the end of the shop, applying a \*\*Negative\*\* edition so it doesn't take up inventory space.

\- \*\*Observatory Voucher\*\*: Held planet cards grant X1.5 Mult to their respective hand.

\#\#\#\# Execution  
1\. The player plays \*\*High Card\*\* as their dominant hand.  
2\. The player holds exactly one \*\*Pluto\*\* planet card.  
3\. \*\*Perkeo\*\* duplicates the Pluto card every single round, creating Negative Plutos.  
4\. Over 10 rounds, the player accumulates 10 Negative Plutos.  
5\. Because the cards are Negative, they sit in the consumable inventory indefinitely.  
6\. When the player plays High Card, the Observatory voucher triggers for every held Pluto:  
\`Total Mult \= (1.5)^10 \= X57.66 Mult\`  
Over a full run, a player can hold \*\*100+ Negative Plutos\*\*, achieving \`(1.5)^100\` $ pprox$ \`X400,000,000,000\` Mult on High Card plays. This completely bypasses the need for high-tier hand builds.

\---

\#\#\# 6.4 The Vampire \+ Midas Mask Economic Scaling  
\- \*\*Midas Mask\*\*: Automatically paints any played Face Card with a \*\*Gold Enhancement\*\* upon being played.  
\- \*\*Vampire\*\*: Strips enhancements from scored cards to permanently gain \+X0.2 Mult.

\#\#\#\# Loop  
1\. The player plays a hand containing Face Cards.  
2\. Upon play, \*\*Midas Mask\*\* paints the Face Cards to Gold.  
3\. Upon scoring, \*\*Vampire\*\* consumes the Gold enhancement, immediately gaining permanent XMult scaling (e.g., scoring 5 Face Cards grants \`+X1.0\` Mult permanently in a single hand).  
4\. The cards remain in the deck as standard cards, ready to be painted Gold again next round.  
5\. This sustains both high XMult scaling and economy (from any Gold cards held at round's end).

\---

\#\#\# 6.5 The Chicot \+ Manacle Hand Size Exploit  
In normal play, entering "negative hand-size" is impossible or highly punitive. However, if a player possesses \*\*multiple copies of Chicot\*\* (typically cloned via \*Ankh\* or \*Blueprint\*):

1\. The player fights \*\*The Manacle\*\* Boss Blind (-1 Hand Size).  
2\. Each active \*\*Chicot\*\* on the board attempts to disable the Boss Blind's ability separately.  
3\. Due to a computational state-reconciliation anomaly in the game's code, instead of simply resetting hand-size to baseline, each Chicot applies a \`+1 Hand Size\` modifier.  
4\. If the player has 5 Chicots, they exit the Boss Blind with a permanent, compounded increase to hand-size, expanding their maximum capacity to hold Steel cards.  
