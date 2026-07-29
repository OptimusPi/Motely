/**
 * Balatro Synergy Knowledge Base
 * Meta combinations and their mathematical breakdowns.
 */

export interface SynergyInfo {
  name: string;
  jokers: string[];
  description: string;
  math: string;
  setup: string[];
  difficulty: "Easy" | "Medium" | "Hard" | "Legendary";
  bossCounters: string[];
  tags: string[];
}

export const SYNERGIES: SynergyInfo[] = [
  {
    name: "Baron + Mime Exponential Loop",
    jokers: ["Baron", "Mime"],
    description:
      "The ultimate mathematical engine. Hold Steel Kings in hand. Each King grants X1.5 from Steel and X1.5 from Baron. Mime retriggers both.",
    math: "1 Steel King = (1.5)^4 = X5.06. 8 Kings = (1.5)^32 = X431,439. With Blueprint/Brainstorm: exponents in thousands.",
    setup: [
      "Find Baron and Mime",
      "Get The Chariot (Steel) or find Steel-enhanced Kings",
      "Hold as many Steel Kings as possible in hand",
      "Copy with Blueprint or Brainstorm for additional multipliers",
    ],
    difficulty: "Hard",
    bossCounters: ["The Plant", "The Manacle", "The Psychic"],
    tags: ["endless", "steel", "x-mult", "s-tier"],
  },
  {
    name: "Photochad Retrigger",
    jokers: ["Photograph", "Hanging Chad"],
    description:
      "Cheap, efficient early-game burst. Photograph gives X2 on first face card. Hanging Chad retriggers that card 2 more times.",
    math: "X2 × X2 × X2 = X8 Mult on first face card. With Glass: 8 × 2 = X16. With Red Seal: X32.",
    setup: [
      "Get Photograph (Common) and Hanging Chad (Common)",
      "Place face card in leftmost scoring position",
      "Enhance face card to Glass for additional X2",
      "Add Red Seal for another retrigger",
    ],
    difficulty: "Easy",
    bossCounters: ["The Plant", "The Flint"],
    tags: ["early-game", "burst", "cheap", "face-cards"],
  },
  {
    name: "Observatory Infinite (Perkeo Stack)",
    jokers: ["Perkeo"],
    description:
      "Perkeo duplicates 1 held consumable at end of shop with Negative edition. Stack Negative Plutos for infinite X1.5 multipliers.",
    math: "10 Negative Plutos = (1.5)^10 = X57.66. 100 Plutos = (1.5)^100 ≈ X400 billion. Requires Observatory voucher.",
    setup: [
      "Get Perkeo (Legendary)",
      "Buy Observatory voucher (Telescope → Observatory)",
      "Hold exactly 1 Pluto planet card",
      "Perkeo duplicates it every round",
      "Play High Card as your dominant hand type",
    ],
    difficulty: "Legendary",
    bossCounters: ["The Ox", "The Flint"],
    tags: ["endless", "consumable", "x-mult", "legendary"],
  },
  {
    name: "Vampire + Midas Mask Economy",
    jokers: ["Vampire", "Midas Mask"],
    description:
      "Midas Mask paints played Face Cards Gold. Vampire consumes the Gold enhancement for +X0.2 Mult permanently. Cards remain for repainting.",
    math: "5 Face Cards per round = +X1.0 Mult per round. Sustainable infinite scaling.",
    setup: [
      "Get Vampire (Uncommon) and Midas Mask (Uncommon)",
      "Play hands with Face Cards every round",
      "Midas paints them Gold, Vampire consumes for scaling",
      "Cards stay in deck for next round's painting",
    ],
    difficulty: "Medium",
    bossCounters: ["The Plant", "The Ox"],
    tags: ["economy", "sustainable", "x-mult", "mid-game"],
  },
  {
    name: "Bull + Bootstraps Economic Scaling",
    jokers: ["Bull", "Bootstraps"],
    description:
      "Bull gives +2 Chips per $1 held. Bootstraps gives +4 Mult per $5 held. Both scale with cash hoarding.",
    math: "$1000: Bull = +2000 Chips, Bootstraps = +800 Mult. Stack with interest vouchers for $20+ per round.",
    setup: [
      "Get Bull and Bootstraps (both Uncommon)",
      "Buy Seed Money → Money Tree vouchers ($20 interest cap)",
      "Hoard cash, never spend unless necessary",
      "Add Golden Joker for +$4 per round per copy",
    ],
    difficulty: "Easy",
    bossCounters: ["The Ox", "The Tooth"],
    tags: ["economy", "flat", "chips", "mult", "easy"],
  },
  {
    name: "Plasma Deck + Stuntman",
    jokers: ["Stuntman"],
    description:
      "Plasma Deck scores by ((Chips + Mult) / 2)^2. Flat chip additions become exponentially powerful. Stuntman gives +250 Chips.",
    math: "Stuntman alone: base 250 chips. With 50 mult: ((250+50)/2)^2 = 22,500. With 100 mult: ((250+100)/2)^2 = 30,625.",
    setup: [
      "Select Plasma Deck",
      "Get Stuntman (Rare) as early as possible",
      "Add any flat mult (Bootstraps, Green Joker)",
      "Avoid X-Mult jokers (wasted on Plasma's formula)",
    ],
    difficulty: "Medium",
    bossCounters: ["The Flint", "The Wall"],
    tags: ["plasma", "chips", "exponential", "deck-specific"],
  },
  {
    name: "Chicot + Manacle Hand Size Exploit",
    jokers: ["Chicot"],
    description:
      "Multiple Chicots (cloned via Ankh/Blueprint) vs The Manacle Boss. Each Chicot applies +1 Hand Size instead of just nullifying.",
    math: "5 Chicots vs Manacle: +5 permanent hand size. 10 Chicots: +10 hand size.",
    setup: [
      "Get Chicot (Legendary)",
      "Clone with Ankh or Blueprint",
      "Fight The Manacle Boss Blind",
      "Each Chicot triggers +1 Hand Size independently",
    ],
    difficulty: "Legendary",
    bossCounters: ["The Manacle"], // this is the boss you WANT to fight
    tags: ["exploit", "hand-size", "legendary", "niche"],
  },
  {
    name: "Canio Glass Destruction Loop",
    jokers: ["Canio"],
    description:
      "Canio gains X1 Mult when a played Face Card is destroyed. Glass cards destroy themselves 1 in 4 times when scored.",
    math: "Each Glass face card destruction: +X1 Mult. With 20 destroyed face cards: X20 Mult. With Oops! All 6s: destruction rate increases.",
    setup: [
      "Get Canio (Legendary)",
      "Enhance face cards to Glass (Justice tarot)",
      "Play and destroy Glass face cards repeatedly",
      "Add Oops! All 6s for higher destruction rate",
    ],
    difficulty: "Hard",
    bossCounters: ["The Plant", "The Flint"],
    tags: ["destruction", "x-mult", "legendary", "face-cards"],
  },
];

/** Find synergies that involve a specific joker. */
export function findSynergies(jokerName: string): SynergyInfo[] {
  return SYNERGIES.filter((s) =>
    s.jokers.some((j) => j.toLowerCase() === jokerName.toLowerCase())
  );
}

/** Find synergies by tag. */
export function findSynergiesByTag(tag: string): SynergyInfo[] {
  return SYNERGIES.filter((s) =>
    s.tags.some((t) => t.toLowerCase() === tag.toLowerCase())
  );
}

/** Get recommended synergies for a set of jokers. */
export function getRecommendedSynergies(
  jokerNames: string[]
): SynergyInfo[] {
  const normalized = jokerNames.map((n) => n.toLowerCase());
  return SYNERGIES.filter((s) =>
    s.jokers.some((j) => normalized.includes(j.toLowerCase()))
  ).sort((a, b) => {
    const aMatches = a.jokers.filter((j) => normalized.includes(j.toLowerCase())).length;
    const bMatches = b.jokers.filter((j) => normalized.includes(j.toLowerCase())).length;
    return bMatches - aMatches; // most matches first
  });
}
