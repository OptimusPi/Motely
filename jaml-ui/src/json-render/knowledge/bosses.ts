/**
 * Balatro Boss Blind Knowledge Base
 */

export type BossCategory = "Debuffer" | "Restrictor" | "Obfuscator" | "Scaler" | "Economic";

export interface BossInfo {
  name: string;
  jamlKey: string;
  category: BossCategory;
  effect: string;
  counters: string[];
  jamlAvoid: string;
  threatLevel: "Low" | "Medium" | "High" | "Lethal";
}

export const BOSSES: Record<string, BossInfo> = {
  // ── Debuffers ──
  "The Club": {
    name: "The Club",
    jamlKey: "boss: The Club",
    category: "Debuffer",
    effect: "Debuffs all Club cards.",
    counters: ["Non-Club decks", "Checkered Deck", "Chicot"],
    jamlAvoid: "mustNot: boss: The Club",
    threatLevel: "High",
  },
  "The Goad": {
    name: "The Goad",
    jamlKey: "boss: The Goad",
    category: "Debuffer",
    effect: "Debuffs all Spade cards.",
    counters: ["Non-Spade decks", "Checkered Deck", "Chicot"],
    jamlAvoid: "mustNot: boss: The Goad",
    threatLevel: "High",
  },
  "The Window": {
    name: "The Window",
    jamlKey: "boss: The Window",
    category: "Debuffer",
    effect: "Debuffs all Diamond cards.",
    counters: ["Non-Diamond decks", "Checkered Deck", "Chicot"],
    jamlAvoid: "mustNot: boss: The Window",
    threatLevel: "High",
  },
  "The Head": {
    name: "The Head",
    jamlKey: "boss: The Head",
    category: "Debuffer",
    effect: "Debuffs all Heart cards.",
    counters: ["Non-Heart decks", "Checkered Deck", "Chicot"],
    jamlAvoid: "mustNot: boss: The Head",
    threatLevel: "High",
  },
  "The Plant": {
    name: "The Plant",
    jamlKey: "boss: The Plant",
    category: "Debuffer",
    effect: "Debuffs all Face Cards (Jacks, Queens, Kings).",
    counters: ["Non-face card builds", "Chicot", "Director's Cut", "Retcon"],
    jamlAvoid: "mustNot: boss: The Plant",
    threatLevel: "Lethal",
  },
  "The Pillar": {
    name: "The Pillar",
    jamlKey: "boss: The Pillar",
    category: "Debuffer",
    effect: "Debuffs any playing card played previously in the current Ante.",
    counters: ["Diverse hand types", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Pillar",
    threatLevel: "Medium",
  },

  // ── Restrictors ──
  "The Psychic": {
    name: "The Psychic",
    jamlKey: "boss: The Psychic",
    category: "Restrictor",
    effect: "Every played hand must contain exactly 5 cards.",
    counters: ["5-card builds", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Psychic",
    threatLevel: "High",
  },
  "The Eye": {
    name: "The Eye",
    jamlKey: "boss: The Eye",
    category: "Restrictor",
    effect: "Disallows playing repeat hand types in the current round.",
    counters: ["Diverse hand types", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Eye",
    threatLevel: "Medium",
  },
  "The Mouth": {
    name: "The Mouth",
    jamlKey: "boss: The Mouth",
    category: "Restrictor",
    effect: "Restricts player to one specific hand type for the entire round.",
    counters: ["Versatile builds", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Mouth",
    threatLevel: "High",
  },
  "The Needle": {
    name: "The Needle",
    jamlKey: "boss: The Needle",
    category: "Restrictor",
    effect: "Restricts player to exactly 1 hand for the entire round.",
    counters: ["High-scoring single hand", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Needle",
    threatLevel: "High",
  },

  // ── Obfuscators ──
  "The House": {
    name: "The House",
    jamlKey: "boss: The House",
    category: "Obfuscator",
    effect: "First drawn hand is drawn face down.",
    counters: ["Memory", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The House",
    threatLevel: "Low",
  },
  "The Wheel": {
    name: "The Wheel",
    jamlKey: "boss: The Wheel",
    category: "Obfuscator",
    effect: "1 in 7 cards drawn face down. (2 in 7 with Oops! All 6s).",
    counters: ["Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Wheel",
    threatLevel: "Low",
  },
  "The Fish": {
    name: "The Fish",
    jamlKey: "boss: The Fish",
    category: "Obfuscator",
    effect: "All cards drawn face down after a hand is played.",
    counters: ["Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Fish",
    threatLevel: "Medium",
  },
  "The Mark": {
    name: "The Mark",
    jamlKey: "boss: The Mark",
    category: "Obfuscator",
    effect: "All Face Cards drawn face down.",
    counters: ["Non-face card builds", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Mark",
    threatLevel: "Medium",
  },

  // ── Scalers ──
  "The Wall": {
    name: "The Wall",
    jamlKey: "boss: The Wall",
    category: "Scaler",
    effect: "Extends required blind score to 4X the base Ante requirement.",
    counters: ["Exponential scaling", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Wall",
    threatLevel: "High",
  },
  "The Flint": {
    name: "The Flint",
    jamlKey: "boss: The Flint",
    category: "Scaler",
    effect: "Halves all base Chips and Mult for the played hand's base level.",
    counters: ["High flat mult/chip", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Flint",
    threatLevel: "High",
  },

  // ── Economic / Meta ──
  "The Ox": {
    name: "The Ox",
    jamlKey: "boss: The Ox",
    category: "Economic",
    effect: "Sets player's capital to $0 if they play their most frequently played hand.",
    counters: ["Diverse hand types", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Ox",
    threatLevel: "Medium",
  },
  "The Tooth": {
    name: "The Tooth",
    jamlKey: "boss: The Tooth",
    category: "Economic",
    effect: "Subtracts $1 from capital for every card played.",
    counters: ["High-card spam builds", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Tooth",
    threatLevel: "Medium",
  },
  "The Arm": {
    name: "The Arm",
    jamlKey: "boss: The Arm",
    category: "Economic",
    effect: "Permanently decreases level of played hand type by 1.",
    counters: ["Diverse hand types", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Arm",
    threatLevel: "High",
  },
  "The Hook": {
    name: "The Hook",
    jamlKey: "boss: The Hook",
    category: "Economic",
    effect: "Involuntarily discards 2 random cards after every play.",
    counters: ["Large hand size", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: The Hook",
    threatLevel: "Medium",
  },
  "The Manacle": {
    name: "The Manacle",
    jamlKey: "boss: The Manacle",
    category: "Restrictor",
    effect: "Temporarily reduces hand size by 1.",
    counters: ["Chicot exploit", "Paint Brush", "Palette"],
    jamlAvoid: "mustNot: boss: The Manacle",
    threatLevel: "Medium",
  },
  "The Water": {
    name: "The Water",
    jamlKey: "boss: The Water",
    category: "Restrictor",
    effect: "Removes all discards for the round.",
    counters: ["Chicot", "Retcon", "High-scoring hands"],
    jamlAvoid: "mustNot: boss: The Water",
    threatLevel: "High",
  },
};

// ── Finisher Blinds (Ante 8) ──
export const FINISHERS: BossInfo[] = [
  {
    name: "Cerulean Bell",
    jamlKey: "boss: Cerulean Bell",
    category: "Restrictor",
    effect: "Forces a random card to be permanently selected. Selling or discarding it triggers debuffs.",
    counters: ["Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: Cerulean Bell",
    threatLevel: "High",
  },
  {
    name: "Violet Vessel",
    jamlKey: "boss: Violet Vessel",
    category: "Scaler",
    effect: "Triples the baseline required score benchmark.",
    counters: ["Exponential scaling", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: Violet Vessel",
    threatLevel: "Lethal",
  },
  {
    name: "Crimson Heart",
    jamlKey: "boss: Crimson Heart",
    category: "Debuffer",
    effect: "Disables 1 random Joker after every hand played.",
    counters: ["Multiple jokers", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: Crimson Heart",
    threatLevel: "Lethal",
  },
  {
    name: "Verdant Leaf",
    jamlKey: "boss: Verdant Leaf",
    category: "Debuffer",
    effect: "Debuffs every card unless you sell at least 1 Joker.",
    counters: ["Cheap jokers to sell", "Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: Verdant Leaf",
    threatLevel: "High",
  },
  {
    name: "Amber Acorn",
    jamlKey: "boss: Amber Acorn",
    category: "Obfuscator",
    effect: "Shuffles all Jokers and flips them face down.",
    counters: ["Chicot", "Retcon"],
    jamlAvoid: "mustNot: boss: Amber Acorn",
    threatLevel: "High",
  },
];

export function getBoss(name: string): BossInfo | undefined {
  return BOSSES[name] ?? FINISHERS.find((b) => b.name === name);
}

export function getBossesByCategory(category: BossCategory): BossInfo[] {
  return [
    ...Object.values(BOSSES),
    ...FINISHERS,
  ].filter((b) => b.category === category);
}

export function getAllBosses(): BossInfo[] {
  return [...Object.values(BOSSES), ...FINISHERS];
}
