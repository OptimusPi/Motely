/**
 * Balatro Deck & Stake Knowledge Base
 */

export interface DeckInfo {
  name: string;
  jamlKey: string;
  effect: string;
  strategy: string;
  synergies: string[];
  difficulty: "Easy" | "Medium" | "Hard";
}

export const DECKS: Record<string, DeckInfo> = {
  Red: {
    name: "Red Deck",
    jamlKey: "Red",
    effect: "Grants +1 Discard per round.",
    strategy:
      "Excellent baseline for high-consistency discard strategies. Amplifies Hit the Road, Yorick, and Castle.",
    synergies: ["Yorick", "Hit the Road", "Castle", "Green Joker"],
    difficulty: "Easy",
  },
  Blue: {
    name: "Blue Deck",
    jamlKey: "Blue",
    effect: "Grants +1 Hand per round.",
    strategy:
      "Provides stable scoring buffer. Ideal for hands that require deep cycling but need a final play.",
    synergies: ["Grabber", "Nacho Tong", "Dusk"],
    difficulty: "Easy",
  },
  Yellow: {
    name: "Yellow Deck",
    jamlKey: "Yellow",
    effect: "Starts with +$10 (total $14 instead of $4).",
    strategy:
      "Drastically reduces time to hit $25 interest cap. Highly synergistic with early economic scaling jokers.",
    synergies: ["Golden Joker", "Bootstraps", "Bull", "To the Moon"],
    difficulty: "Easy",
  },
  Green: {
    name: "Green Deck",
    jamlKey: "Green",
    effect:
      "Interest disabled. Earns $2 per played hand remaining and $1 per discard remaining at end of round.",
    strategy:
      "Inverts economic model. Minimize played hands and save discards for maximum payout. Prioritize flat-scoring jokers.",
    synergies: ["Stuntman", "Bull", "Bootstraps", "Green Joker"],
    difficulty: "Hard",
  },
  Black: {
    name: "Black Deck",
    jamlKey: "Black",
    effect: "Grants +1 Joker slot, but reduces played hands by 1.",
    strategy:
      "High-ceiling, high-risk. Early game is extremely punishing. Look for immediate flat chip/mult support to survive Ante 1-2.",
    synergies: ["Stuntman", "Joker Stencil", "Wee Joker"],
    difficulty: "Hard",
  },
  Magic: {
    name: "Magic Deck",
    jamlKey: "Magic",
    effect:
      "Starts with Crystal Ball voucher (+1 consumable slot) and two copies of The Fool tarot.",
    strategy:
      "Ideal for duplicating early spectral transformations or high-value tarots (Death, The Hermit).",
    synergies: ["The Fool", "Death", "The Hermit", "Crystal Ball"],
    difficulty: "Medium",
  },
  Nebula: {
    name: "Nebula Deck",
    jamlKey: "Nebula",
    effect:
      "Starts with Telescope voucher (Celestial packs always contain planet for most played hand), but -1 consumable slot.",
    strategy:
      "Restricts holding space for Tarots/Spectrals. Forces immediate commits. Synergizes with rapid planetary scaling.",
    synergies: ["Constellation", "Observatory", "Perkeo", "Pluto"],
    difficulty: "Medium",
  },
  Zodiac: {
    name: "Zodiac Deck",
    jamlKey: "Zodiac",
    effect:
      "Starts with Tarot Merchant, Planet Merchant, and Overstock vouchers pre-equipped.",
    strategy:
      "Vastly increases shop variety and consumable spawn rate. Synergizes with economy-heavy seeds for reroll abuse.",
    synergies: ["Reroll Surplus", "Reroll Glut", "Clearance Sale", "Liquidation"],
    difficulty: "Easy",
  },
  Ghost: {
    name: "Ghost Deck",
    jamlKey: "Ghost",
    effect:
      "Spectral cards can appear in standard shops. Starts with Hex card (applies Polychrome to random Joker, destroys others).",
    strategy:
      "Ultimate environment for early Polychrome S-tier jokers. Look for Cryptid, Ectoplasm, Ankh in shops.",
    synergies: ["Hex", "Ankh", "Ectoplasm", "Cryptid", "The Soul"],
    difficulty: "Medium",
  },
  Checkered: {
    name: "Checkered Deck",
    jamlKey: "Checkered",
    effect:
      "Starting deck contains exactly 26 Spades and 26 Hearts (no Diamonds or Clubs).",
    strategy:
      "Simplifies flush and flush-five building to near-certainty. Synergizes with Bloodstone, The Idol, and Wrathful/Lusty Joker.",
    synergies: ["Bloodstone", "The Idol", "The Sun", "The Star"],
    difficulty: "Medium",
  },
  Painted: {
    name: "Painted Deck",
    jamlKey: "Painted",
    effect: "Grants +2 Hand Size, but permanently reduces Joker slots by 1.",
    strategy:
      "Maximizes held-in-hand scaling triggers (Baron, Mime, Steel cards). Must account for 4-joker slot limit.",
    synergies: ["Baron", "Mime", "Steel Card", "Joker Stencil"],
    difficulty: "Hard",
  },
  Anaglyph: {
    name: "Anaglyph Deck",
    jamlKey: "Anaglyph",
    effect: "Grants a Double Tag every time a Boss Blind is defeated.",
    strategy:
      "Stack dozens of Double Tags, redeem them simultaneously on a high-value blind skip Tag for massive value.",
    synergies: ["Double Tag", "Negative Joker", "Mega Arcana Pack"],
    difficulty: "Medium",
  },
  Plasma: {
    name: "Plasma Deck",
    jamlKey: "Plasma",
    effect:
      "Score = ((Chips + Mult) / 2)^2. Base blind requirements permanently doubled.",
    strategy:
      "Fundamentally alters scoring. Flat additions (+Chips or +Mult) are mathematically balanced. Stuntman and Bull are insanely powerful.",
    synergies: ["Stuntman", "Bull", "Bootstraps", "Green Joker"],
    difficulty: "Hard",
  },
  Abandoned: {
    name: "Abandoned Deck",
    jamlKey: "Abandoned",
    effect:
      "Starting deck contains exactly 40 cards (all 12 face cards removed).",
    strategy:
      "Increases probability of low-card straights and pairs. Baron, Triboulet, Photograph are completely inert.",
    synergies: ["Hack", "Ride the Bus", "Wee Joker", "DNA"],
    difficulty: "Hard",
  },
  Erratic: {
    name: "Erratic Deck",
    jamlKey: "Erratic",
    effect:
      "Starting deck: 52 cards, but all suits and ranks are completely randomized at seed initialization.",
    strategy:
      "The only deck where ErraticSuit and ErraticRank JAML rules are valid. Find seeds with high-density ranks (e.g., 18 Aces).",
    synergies: ["The Idol", "Bloodstone", "DNA", "Death"],
    difficulty: "Medium",
  },
};

export interface StakeInfo {
  name: string;
  jamlKey: string;
  effect: string;
  strategy: string;
  difficulty: "Easy" | "Medium" | "Hard" | "Expert";
}

export const STAKES: StakeInfo[] = [
  {
    name: "White Stake",
    jamlKey: "White",
    effect: "Base configuration. No modifications.",
    strategy: "Standard play. Use for learning or testing new strategies.",
    difficulty: "Easy",
  },
  {
    name: "Red Stake",
    jamlKey: "Red",
    effect: "Small Blinds provide no monetary reward on victory.",
    strategy: "Skips are encouraged to secure early-game Tags.",
    difficulty: "Easy",
  },
  {
    name: "Green Stake",
    jamlKey: "Green",
    effect: "Required score scaling accelerates for each successive Ante.",
    strategy: "Requires geometric scaling engines by Ante 3.",
    difficulty: "Medium",
  },
  {
    name: "Black Stake",
    jamlKey: "Black",
    effect: "Eternal Jokers introduced (30% spawn chance). Cannot be sold or destroyed.",
    strategy: "Blocks self-destruct synergies (Madness, Ceremonial Dagger). Avoid these jokers.",
    difficulty: "Hard",
  },
  {
    name: "Blue Stake",
    jamlKey: "Blue",
    effect: "Player's baseline discard pool reduced by 1.",
    strategy: "Severely impacts discard economies (Merry Andy, Hit the Road, Yorick).",
    difficulty: "Medium",
  },
  {
    name: "Purple Stake",
    jamlKey: "Purple",
    effect: "Required scoring accelerates exponentially.",
    strategy: "Flat additions become unviable past Ante 4. Need X-Mult or exponential engines.",
    difficulty: "Hard",
  },
  {
    name: "Orange Stake",
    jamlKey: "Orange",
    effect: "Perishable Jokers introduced (30% chance). Debuffed after 5 rounds.",
    strategy: "Blocks long-term scaling engines (Wee Joker, Constellation). Look for immediate impact.",
    difficulty: "Expert",
  },
  {
    name: "Gold Stake",
    jamlKey: "Gold",
    effect: "Rental Jokers introduced (30% chance). Cost $3 per round to maintain.",
    strategy: "Massive economic stress. Prioritize interest-generating engines and cheap builds.",
    difficulty: "Expert",
  },
];

export function getDeck(name: string): DeckInfo | undefined {
  return DECKS[name] ?? Object.values(DECKS).find((d) => d.name === name);
}

export function getStake(name: string): StakeInfo | undefined {
  return STAKES.find(
    (s) => s.name === name || s.jamlKey === name
  );
}
