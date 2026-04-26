/** Map MotelyItemType enum names to display-friendly strings */
export function getItemDisplayName(enumKey: string): string {
  // Convert PascalCase to spaced: "GreedyJoker" -> "Greedy Joker"
  // Handle special cases first
  const specials: Record<string, string> = {
    ChaostheClown: "Chaos the Clown",
    OopsAll6s: "Oops! All 6s",
    EightBall: "8 Ball",
    DNA: "DNA",
    MrBones: "Mr. Bones",
    ToDoList: "To Do List",
    Cloud9: "Cloud 9",
    SockAndBuskin: "Sock and Buskin",
    TheSoul: "The Soul",
    BlackHole: "Black Hole",
    PlanetX: "Planet X",
  };

  if (specials[enumKey]) return specials[enumKey];

  // Standard cards: C2 = 2 of Clubs, D10 = 10 of Diamonds, etc.
  const suitMap: Record<string, string> = { C: "♣", D: "♦", H: "♥", S: "♠" };
  const cardMatch = enumKey.match(/^([CDHS])([2-9JQKA]|10)$/);
  if (cardMatch) {
    const [, suit, rank] = cardMatch;
    const rankName = { J: "Jack", Q: "Queen", K: "King", A: "Ace" }[rank] ?? rank;
    return `${rankName} ${suitMap[suit]}`;
  }

  // General PascalCase split
  return enumKey.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
}

export type CardCategory = "joker" | "tarot" | "planet" | "spectral" | "playing" | "unknown";

const TAROT_NAMES = new Set([
  "TheFool", "TheMagician", "TheHighPriestess", "TheEmpress", "TheEmperor",
  "TheHierophant", "TheLovers", "TheChariot", "Justice", "TheHermit",
  "TheWheelOfFortune", "Strength", "TheHangedMan", "Death", "Temperance",
  "TheDevil", "TheTower", "TheStar", "TheMoon", "TheSun", "Judgement", "TheWorld",
]);

const PLANET_NAMES = new Set([
  "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn",
  "Uranus", "Neptune", "Pluto", "PlanetX", "Ceres", "Eris",
]);

const SPECTRAL_NAMES = new Set([
  "Familiar", "Grim", "Incantation", "Talisman", "Aura", "Wraith",
  "Sigil", "Ouija", "Ectoplasm", "Immolate", "Ankh", "DejaVu",
  "Hex", "Trance", "Medium", "Cryptid", "TheSoul", "BlackHole",
]);

export function getItemCategory(enumKey: string): CardCategory {
  if (/^[CDHS]([2-9JQKA]|10)$/.test(enumKey)) return "playing";
  if (TAROT_NAMES.has(enumKey)) return "tarot";
  if (PLANET_NAMES.has(enumKey)) return "planet";
  if (SPECTRAL_NAMES.has(enumKey)) return "spectral";
  // Everything else between the standard cards and Invalid is a joker
  return "joker";
}

export const CATEGORY_COLORS: Record<CardCategory, { bg: string; border: string; text: string }> = {
  joker: { bg: "bg-purple-900/30", border: "border-purple-500/50", text: "text-purple-200" },
  tarot: { bg: "bg-blue-900/30", border: "border-blue-500/50", text: "text-blue-200" },
  planet: { bg: "bg-amber-900/30", border: "border-amber-500/50", text: "text-amber-200" },
  spectral: { bg: "bg-cyan-900/30", border: "border-cyan-500/50", text: "text-cyan-200" },
  playing: { bg: "bg-neutral-100 dark:bg-neutral-800", border: "border-neutral-300 dark:border-neutral-600", text: "text-neutral-900 dark:text-neutral-100" },
  unknown: { bg: "bg-neutral-900/30", border: "border-neutral-500/50", text: "text-neutral-300" },
};

/** Suit color for standard cards */
export function getSuitColor(enumKey: string): string {
  if (enumKey.startsWith("H") || enumKey.startsWith("D")) return "text-red-500";
  return "text-neutral-900 dark:text-neutral-100";
}
