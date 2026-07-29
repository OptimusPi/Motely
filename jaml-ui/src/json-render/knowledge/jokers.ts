/**
 * Balatro Joker Knowledge Base
 * Extracted from the Master Encyclopedia for json-render reference components.
 */

export type JokerRarity = "Common" | "Uncommon" | "Rare" | "Legendary";
export type JokerCategory = "Copy" | "X-Mult" | "Flat" | "Economy" | "Retrigger" | "Utility";

export interface JokerInfo {
  name: string;
  rarity: JokerRarity;
  cost: number;
  effect: string;
  category: JokerCategory;
  synergies: string[];
  strategy: string;
  jamlKey: string;
}

export const JOKERS: Record<string, JokerInfo> = {
  // ── S-Tier: Copy & Retrigger ──
  Blueprint: {
    name: "Blueprint",
    rarity: "Rare",
    cost: 10,
    effect: "Copies the ability of the Joker card directly to its right.",
    category: "Copy",
    synergies: ["Baron", "Mime", "Triboulet", "Brainstorm"],
    strategy: "Place to the right of your highest-value joker. Works with any joker ability.",
    jamlKey: "joker: Blueprint",
  },
  Brainstorm: {
    name: "Brainstorm",
    rarity: "Rare",
    cost: 10,
    effect: "Copies the ability of the leftmost Joker currently held.",
    category: "Copy",
    synergies: ["Baron", "Mime", "Triboulet", "Blueprint"],
    strategy: "Place your highest-value joker in the leftmost slot for maximum value.",
    jamlKey: "joker: Brainstorm",
  },
  "Sock & Buskin": {
    name: "Sock & Buskin",
    rarity: "Uncommon",
    cost: 6,
    effect: "Retriggers all played Face Cards exactly once.",
    category: "Retrigger",
    synergies: ["Photograph", "Hanging Chad", "Triboulet", "Baron"],
    strategy: "Essential for face-card builds. Doubles all face card triggers.",
    jamlKey: "joker: Sock & Buskin",
  },
  "Hanging Chad": {
    name: "Hanging Chad",
    rarity: "Common",
    cost: 4,
    effect: "Retriggers the first played card scored in a hand exactly 2 additional times.",
    category: "Retrigger",
    synergies: ["Photograph", "Glass Card", "Red Seal", "Bloodstone"],
    strategy: "Place your highest-value card in the leftmost scoring position.",
    jamlKey: "joker: Hanging Chad",
  },
  Hack: {
    name: "Hack",
    rarity: "Uncommon",
    cost: 6,
    effect: "Retriggers all played 2s, 3s, 4s, and 5s exactly once.",
    category: "Retrigger",
    synergies: ["Wee Joker", "Ride the Bus", "Steel Card"],
    strategy: "Build a low-card deck. Each 2-5 retriggers for extra scoring.",
    jamlKey: "joker: Hack",
  },
  Mime: {
    name: "Mime",
    rarity: "Uncommon",
    cost: 6,
    effect: "Retriggers all card held-in-hand abilities (Steel, Gold, Baron, Baron).",
    category: "Retrigger",
    synergies: ["Baron", "Steel Card", "Gold Card", "Blueprint"],
    strategy: "The cornerstone of exponential scaling. Hold Steel Kings for (1.5)^4 per card.",
    jamlKey: "joker: Mime",
  },
  Dusk: {
    name: "Dusk",
    rarity: "Uncommon",
    cost: 6,
    effect: "Retriggers all played cards scored in the final hand of any round.",
    category: "Retrigger",
    synergies: ["Baron", "Triboulet", "Bloodstone", "The Idol"],
    strategy: "Save your best hand for last. All cards in final hand retrigger.",
    jamlKey: "joker: Dusk",
  },

  // ── X-Mult Jokers ──
  Baron: {
    name: "Baron",
    rarity: "Rare",
    cost: 8,
    effect: "Each King held in hand grants X1.5 Mult.",
    category: "X-Mult",
    synergies: ["Mime", "Steel Card", "Blueprint", "Brainstorm", "Sock & Buskin"],
    strategy: "Hold as many Steel Kings as possible. With Mime: (1.5)^4 per King. 8 Kings = 431,439x.",
    jamlKey: "joker: Baron",
  },
  Triboulet: {
    name: "Triboulet",
    rarity: "Legendary",
    cost: 20,
    effect: "Each King and Queen played and scored grants X2 Mult.",
    category: "X-Mult",
    synergies: ["Sock & Buskin", "Dusk", "Red Seal", "Blueprint"],
    strategy: "Play face-card heavy hands. Each King/Queen is a 2x multiplier.",
    jamlKey: "joker: Triboulet",
  },
  "The Idol": {
    name: "The Idol",
    rarity: "Uncommon",
    cost: 6,
    effect: "X2 Mult for each card scored matching a random Rank+Suit (changes every round).",
    category: "X-Mult",
    synergies: ["Ouija", "Sigil", "Death"],
    strategy: "Homogenize your deck to match the Idol's target. Use Ouija/Sigil to force match.",
    jamlKey: "joker: The Idol",
  },
  Bloodstone: {
    name: "Bloodstone",
    rarity: "Uncommon",
    cost: 6,
    effect: "1 in 2 chance for played Heart cards to grant X1.5 Mult when scored.",
    category: "X-Mult",
    synergies: ["Oops! All 6s", "The Sun", "Hearts suit builds"],
    strategy: "Convert deck to Hearts. With Oops! All 6s: 100% proc rate.",
    jamlKey: "joker: Bloodstone",
  },
  Vampire: {
    name: "Vampire",
    rarity: "Uncommon",
    cost: 6,
    effect: "Permanently gains X0.2 Mult for each enhanced card scored, stripping the enhancement.",
    category: "X-Mult",
    synergies: ["Midas Mask", "Gold Card", "Aura"],
    strategy: "Score Gold cards every round. Midas Mask paints them, Vampire consumes them for +X0.2 each.",
    jamlKey: "joker: Vampire",
  },
  Hologram: {
    name: "Hologram",
    rarity: "Uncommon",
    cost: 6,
    effect: "Gains X0.25 Mult every time a playing card is permanently added to the deck.",
    category: "X-Mult",
    synergies: ["Cryptid", "Holographic", "DNA"],
    strategy: "Add cards to deck via Cryptid or packs. Each addition is a permanent 0.25x multiplier.",
    jamlKey: "joker: Hologram",
  },
  Constellation: {
    name: "Constellation",
    rarity: "Uncommon",
    cost: 6,
    effect: "Gains X0.1 Mult every time a Planet card is used.",
    category: "X-Mult",
    synergies: ["Observatory", "Perkeo", "Planet Merchant"],
    strategy: "Use planets every round. Stack with Observatory for planet duplication.",
    jamlKey: "joker: Constellation",
  },
  Cavendish: {
    name: "Cavendish",
    rarity: "Common",
    cost: 4,
    effect: "X3 Mult. 1 in 1000 chance to self-destruct.",
    category: "X-Mult",
    synergies: ["Eternal", "Blueprint"],
    strategy: "High-risk, high-reward. Get Eternal edition to prevent destruction. Copy with Blueprint.",
    jamlKey: "joker: Cavendish",
  },

  // ── Flat Mult / Chip Scaling ──
  Stuntman: {
    name: "Stuntman",
    rarity: "Rare",
    cost: 8,
    effect: "+250 Chips, but permanently reduces hand size by 2.",
    category: "Flat",
    synergies: ["Plasma Deck", "Paint Brush", "Palette"],
    strategy: "On Plasma Deck: (Chips+Mult)/2 scoring makes flat chips incredibly valuable.",
    jamlKey: "joker: Stuntman",
  },
  Bull: {
    name: "Bull",
    rarity: "Uncommon",
    cost: 6,
    effect: "+2 Chips for every $1 currently held.",
    category: "Flat",
    synergies: ["Money Tree", "Seed Money", "Golden Joker", "Bootstraps"],
    strategy: "Hoard cash. With $1000, Bull gives +2000 Chips. Stack with interest vouchers.",
    jamlKey: "joker: Bull",
  },
  Bootstraps: {
    name: "Bootstraps",
    rarity: "Uncommon",
    cost: 6,
    effect: "+4 Mult for every $5 held in capital.",
    category: "Flat",
    synergies: ["Money Tree", "Seed Money", "Golden Joker", "Bull"],
    strategy: "Economic scaling. With $1000, Bootstraps gives +800 Mult.",
    jamlKey: "joker: Bootstraps",
  },
  "Wee Joker": {
    name: "Wee Joker",
    rarity: "Rare",
    cost: 8,
    effect: "Starts at +10 Chips. Gains +8 Chips every time a 2 is scored.",
    category: "Flat",
    synergies: ["Hack", "DNA", "Cryptid"],
    strategy: "Score 2s every round. With Hack, each 2 triggers twice for +16 Chips.",
    jamlKey: "joker: Wee Joker",
  },
  "Joker Stencil": {
    name: "Joker Stencil",
    rarity: "Uncommon",
    cost: 6,
    effect: "X1 Mult for each empty Joker slot (counts itself as empty).",
    category: "Flat",
    synergies: ["Blank", "Antimatter", "Ectoplasm"],
    strategy: "Deliberately leave joker slots empty. With Antimatter voucher: +1 slot, +1x multiplier.",
    jamlKey: "joker: Joker Stencil",
  },
  "Green Joker": {
    name: "Green Joker",
    rarity: "Common",
    cost: 4,
    effect: "+1 Mult per hand played, +12 Chips per discard remaining. Loses 1 Mult per discard used.",
    category: "Flat",
    synergies: ["Wasteful", "Recyclomancy", "Hit the Road"],
    strategy: "Save discards. With Recyclomancy (+2 discards), start each round with +24 Chips.",
    jamlKey: "joker: Green Joker",
  },
  "Ride the Bus": {
    name: "Ride the Bus",
    rarity: "Common",
    cost: 4,
    effect: "+1 Mult per consecutive hand played without scoring a Face Card. Resets to 0 on Face Card.",
    category: "Flat",
    synergies: ["Hack", "DNA", "Steel Card"],
    strategy: "Avoid face cards. Build a low-card deck with Hack for retriggered 2s-5s.",
    jamlKey: "joker: Ride the Bus",
  },

  // ── Economy ──
  "Golden Joker": {
    name: "Golden Joker",
    rarity: "Common",
    cost: 4,
    effect: "Earn $4 at end of round.",
    category: "Economy",
    synergies: ["Bull", "Bootstraps", "Money Tree", "Seed Money"],
    strategy: "Stack multiple copies for massive per-round income. $4 per copy, per round.",
    jamlKey: "joker: Golden Joker",
  },
  "Matador": {
    name: "Matador",
    rarity: "Uncommon",
    cost: 6,
    effect: "Earn $8 if played hand triggers the Boss Blind's ability.",
    category: "Economy",
    synergies: ["The Ox", "The Tooth", "The Arm"],
    strategy: "Intentionally trigger boss abilities for $8 payouts. High-risk, high-reward economy.",
    jamlKey: "joker: Matador",
  },
  "To the Moon": {
    name: "To the Moon",
    rarity: "Uncommon",
    cost: 6,
    effect: "Earn $1 extra interest for every $5 held at end of round.",
    category: "Economy",
    synergies: ["Money Tree", "Seed Money", "Golden Joker"],
    strategy: "Maximize interest cap. With Money Tree ($20 cap), hold $100+ for maximum returns.",
    jamlKey: "joker: To the Moon",
  },

  // ── Legendary Jokers (Soul-exclusive) ──
  Canio: {
    name: "Canio",
    rarity: "Legendary",
    cost: 20,
    effect: "When a played Face Card is destroyed, gains X1 Mult.",
    category: "X-Mult",
    synergies: ["Glass Card", "Justice", "Hologram"],
    strategy: "Play and destroy Glass face cards. Each destruction is a permanent 1x multiplier.",
    jamlKey: "joker: Canio",
  },
  Perkeo: {
    name: "Perkeo",
    rarity: "Legendary",
    cost: 20,
    effect: "At end of shop, duplicates 1 random held consumable with Negative edition.",
    category: "Utility",
    synergies: ["Observatory", "Pluto", "Crystal Ball", "Omen Globe"],
    strategy: "The infinite engine. Hold 1 Pluto, get 2 Plutos next round. Stack to 100+ Negative Plutos.",
    jamlKey: "joker: Perkeo",
  },
  Chicot: {
    name: "Chicot",
    rarity: "Legendary",
    cost: 20,
    effect: "Disables the effect of the current Boss Blind.",
    category: "Utility",
    synergies: ["The Manacle", "Blueprint", "Brainstorm"],
    strategy: "Essential for survival against counter bosses. Copy with Blueprint for permanent boss nullification.",
    jamlKey: "joker: Chicot",
  },
  Yorick: {
    name: "Yorick",
    rarity: "Legendary",
    cost: 20,
    effect: "Gains X1 Mult every 5 discards used.",
    category: "X-Mult",
    synergies: ["Wasteful", "Recyclomancy", "Hit the Road"],
    strategy: "Use discards aggressively. With Recyclomancy (+2 discards), scale faster.",
    jamlKey: "joker: Yorick",
  },
};

/** Get joker info by name. Returns undefined if not found. */
export function getJoker(name: string): JokerInfo | undefined {
  return JOKERS[name] ?? Object.values(JOKERS).find((j) => j.name === name);
}

/** Get all jokers in a category. */
export function getJokersByCategory(category: JokerCategory): JokerInfo[] {
  return Object.values(JOKERS).filter((j) => j.category === category);
}

/** Get jokers that synergize with a given joker. */
export function getSynergies(jokerName: string): JokerInfo[] {
  const joker = getJoker(jokerName);
  if (!joker) return [];
  return joker.synergies
    .map((name) => getJoker(name))
    .filter((j): j is JokerInfo => j !== undefined);
}
