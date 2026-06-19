/**
 * Sprite sheet position data for all Balatro game elements.
 * Extracted from Blueprint's const.ts — maps item names to {x, y} grid positions
 * within their respective sprite sheets.
 */

import { JAML_ASSET_FILES, resolveJamlAssetUrl, type JamlAssetKey } from "../assets.js";

export interface SpritePos {
  x: number;
  y: number;
}

export interface SpriteEntry {
  name: string;
  pos: SpritePos;
  animated?: boolean;
}

export interface SpriteSheetInfo {
  readonly asset: JamlAssetKey;
  readonly fileName: string;
  readonly columns: number;
  readonly rows: number;
  readonly src: string;
}

function defineSpriteSheet(asset: JamlAssetKey, columns: number, rows: number): SpriteSheetInfo {
  return {
    asset,
    fileName: JAML_ASSET_FILES[asset],
    columns,
    rows,
    get src() {
      return resolveJamlAssetUrl(asset);
    },
  };
}

/** Sprite sheet grid dimensions */
export const SPRITE_SHEETS = {
  jokers: defineSpriteSheet("jokers", 10, 16),
  tarots: defineSpriteSheet("tarots", 10, 6),
  deck: defineSpriteSheet("deck", 13, 4),
  enhancers: defineSpriteSheet("enhancers", 7, 5),
  editions: defineSpriteSheet("editions", 5, 1),
  stickers: defineSpriteSheet("stickers", 5, 3),
  blinds: defineSpriteSheet("blinds", 21, 31),
  vouchers: defineSpriteSheet("vouchers", 9, 4),
  tags: defineSpriteSheet("tags", 6, 5),
  boosters: defineSpriteSheet("boosters", 4, 9),
  stakes: defineSpriteSheet("stakes", 5, 2),
} as const;

export const JOKERS: SpriteEntry[] = [
  { name: "Joker", pos: { x: 0, y: 0 } }, { name: "Greedy Joker", pos: { x: 6, y: 1 } }, { name: "Lusty Joker", pos: { x: 7, y: 1 } }, { name: "Wrathful Joker", pos: { x: 8, y: 1 } }, { name: "Gluttonous Joker", pos: { x: 9, y: 1 } }, { name: "Jolly Joker", pos: { x: 2, y: 0 } }, { name: "Zany Joker", pos: { x: 3, y: 0 } }, { name: "Mad Joker", pos: { x: 4, y: 0 } }, { name: "Crazy Joker", pos: { x: 5, y: 0 } }, { name: "Droll Joker", pos: { x: 6, y: 0 } }, { name: "Sly Joker", pos: { x: 0, y: 14 } }, { name: "Wily Joker", pos: { x: 1, y: 14 } }, { name: "Clever Joker", pos: { x: 2, y: 14 } }, { name: "Devious Joker", pos: { x: 3, y: 14 } }, { name: "Crafty Joker", pos: { x: 4, y: 14 } }, { name: "Half Joker", pos: { x: 7, y: 0 } }, { name: "Joker Stencil", pos: { x: 2, y: 5 } }, { name: "Four Fingers", pos: { x: 6, y: 6 } }, { name: "Mime", pos: { x: 4, y: 1 } }, { name: "Credit Card", pos: { x: 5, y: 1 } }, { name: "Ceremonial Dagger", pos: { x: 5, y: 5 } }, { name: "Banner", pos: { x: 1, y: 2 } }, { name: "Mystic Summit", pos: { x: 2, y: 2 } }, { name: "Marble Joker", pos: { x: 3, y: 2 } }, { name: "Loyalty Card", pos: { x: 4, y: 2 } }, { name: "8 Ball", pos: { x: 0, y: 5 } }, { name: "Misprint", pos: { x: 6, y: 2 } }, { name: "Dusk", pos: { x: 4, y: 7 } }, { name: "Raised Fist", pos: { x: 8, y: 2 } }, { name: "Chaos the Clown", pos: { x: 1, y: 0 } }, { name: "Fibonacci", pos: { x: 1, y: 5 } }, { name: "Steel Joker", pos: { x: 7, y: 2 } }, { name: "Scary Face", pos: { x: 2, y: 3 } }, { name: "Abstract Joker", pos: { x: 3, y: 3 } }, { name: "Delayed Gratification", pos: { x: 4, y: 3 } }, { name: "Hack", pos: { x: 5, y: 2 } }, { name: "Pareidolia", pos: { x: 6, y: 3 } }, { name: "Gros Michel", pos: { x: 7, y: 6 } }, { name: "Even Steven", pos: { x: 8, y: 3 } }, { name: "Odd Todd", pos: { x: 9, y: 3 } }, { name: "Scholar", pos: { x: 3, y: 6 } }, { name: "Business Card", pos: { x: 1, y: 4 } }, { name: "Supernova", pos: { x: 2, y: 4 } }, { name: "Ride the Bus", pos: { x: 1, y: 6 } }, { name: "Space Joker", pos: { x: 3, y: 5 } }, { name: "Egg", pos: { x: 0, y: 10 } }, { name: "Burglar", pos: { x: 1, y: 10 } }, { name: "Blackboard", pos: { x: 2, y: 10 } }, { name: "Runner", pos: { x: 3, y: 10 } }, { name: "Ice Cream", pos: { x: 4, y: 10 } }, { name: "DNA", pos: { x: 5, y: 10 } }, { name: "Splash", pos: { x: 6, y: 10 } }, { name: "Blue Joker", pos: { x: 7, y: 10 } }, { name: "Sixth Sense", pos: { x: 8, y: 10 } }, { name: "Constellation", pos: { x: 9, y: 10 } }, { name: "Hiker", pos: { x: 0, y: 11 } }, { name: "Faceless Joker", pos: { x: 1, y: 11 } }, { name: "Green Joker", pos: { x: 2, y: 11 } }, { name: "Superposition", pos: { x: 3, y: 11 } }, { name: "To Do List", pos: { x: 4, y: 11 } }, { name: "Cavendish", pos: { x: 5, y: 11 } }, { name: "Card Sharp", pos: { x: 6, y: 11 } }, { name: "Red Card", pos: { x: 7, y: 11 } }, { name: "Madness", pos: { x: 8, y: 11 } }, { name: "Square Joker", pos: { x: 9, y: 11 } }, { name: "Seance", pos: { x: 0, y: 12 } }, { name: "Riff-raff", pos: { x: 1, y: 12 } }, { name: "Vampire", pos: { x: 2, y: 12 } }, { name: "Shortcut", pos: { x: 3, y: 12 } }, { name: "Hologram", pos: { x: 4, y: 12 } }, { name: "Vagabond", pos: { x: 5, y: 12 } }, { name: "Baron", pos: { x: 6, y: 12 } }, { name: "Cloud 9", pos: { x: 7, y: 12 } }, { name: "Rocket", pos: { x: 8, y: 12 } }, { name: "Obelisk", pos: { x: 9, y: 12 } }, { name: "Midas Mask", pos: { x: 0, y: 13 } }, { name: "Luchador", pos: { x: 1, y: 13 } }, { name: "Photograph", pos: { x: 2, y: 13 } }, { name: "Gift Card", pos: { x: 3, y: 13 } }, { name: "Turtle Bean", pos: { x: 4, y: 13 } }, { name: "Erosion", pos: { x: 5, y: 13 } }, { name: "Reserved Parking", pos: { x: 6, y: 13 } }, { name: "Mail In Rebate", pos: { x: 7, y: 13 } }, { name: "To the Moon", pos: { x: 8, y: 13 } }, { name: "Hallucination", pos: { x: 9, y: 13 } }, { name: "Fortune Teller", pos: { x: 7, y: 5 } }, { name: "Juggler", pos: { x: 0, y: 1 } }, { name: "Drunkard", pos: { x: 1, y: 1 } }, { name: "Stone Joker", pos: { x: 9, y: 0 } }, { name: "Golden Joker", pos: { x: 9, y: 2 } }, { name: "Lucky Cat", pos: { x: 5, y: 14 } }, { name: "Baseball Card", pos: { x: 6, y: 14 } }, { name: "Bull", pos: { x: 7, y: 14 } }, { name: "Diet Cola", pos: { x: 8, y: 14 } }, { name: "Trading Card", pos: { x: 9, y: 14 } }, { name: "Flash Card", pos: { x: 0, y: 15 } }, { name: "Popcorn", pos: { x: 1, y: 15 } }, { name: "Spare Trousers", pos: { x: 4, y: 15 } }, { name: "Ancient Joker", pos: { x: 7, y: 15 } }, { name: "Ramen", pos: { x: 2, y: 15 } }, { name: "Walkie Talkie", pos: { x: 8, y: 15 } }, { name: "Seltzer", pos: { x: 3, y: 15 } }, { name: "Castle", pos: { x: 9, y: 15 } }, { name: "Smiley Face", pos: { x: 6, y: 15 } }, { name: "Campfire", pos: { x: 5, y: 15 } }, { name: "Golden Ticket", pos: { x: 5, y: 3 } }, { name: "Mr. Bones", pos: { x: 3, y: 4 } }, { name: "Acrobat", pos: { x: 2, y: 1 } }, { name: "Sock and Buskin", pos: { x: 3, y: 1 } }, { name: "Swashbuckler", pos: { x: 9, y: 5 } }, { name: "Troubadour", pos: { x: 0, y: 2 } }, { name: "Certificate", pos: { x: 8, y: 8 } }, { name: "Smeared Joker", pos: { x: 4, y: 6 } }, { name: "Throwback", pos: { x: 5, y: 7 } }, { name: "Hanging Chad", pos: { x: 9, y: 6 } }, { name: "Rough Gem", pos: { x: 9, y: 7 } }, { name: "Bloodstone", pos: { x: 0, y: 8 } }, { name: "Arrowhead", pos: { x: 1, y: 8 } }, { name: "Onyx Agate", pos: { x: 2, y: 8 } }, { name: "Glass Joker", pos: { x: 1, y: 3 } }, { name: "Showman", pos: { x: 6, y: 5 } }, { name: "Flower Pot", pos: { x: 0, y: 6 } }, { name: "Blueprint", pos: { x: 0, y: 3 } }, { name: "Wee Joker", pos: { x: 0, y: 4 } }, { name: "Merry Andy", pos: { x: 8, y: 0 } }, { name: "Oops! All 6s", pos: { x: 5, y: 6 } }, { name: "The Idol", pos: { x: 6, y: 7 } }, { name: "Seeing Double", pos: { x: 4, y: 4 } }, { name: "Matador", pos: { x: 4, y: 5 } }, { name: "Hit the Road", pos: { x: 8, y: 5 } }, { name: "The Duo", pos: { x: 5, y: 4 } }, { name: "The Trio", pos: { x: 6, y: 4 } }, { name: "The Family", pos: { x: 7, y: 4 } }, { name: "The Order", pos: { x: 8, y: 4 } }, { name: "The Tribe", pos: { x: 9, y: 4 } }, { name: "Stuntman", pos: { x: 8, y: 6 } }, { name: "Invisible Joker", pos: { x: 1, y: 7 } }, { name: "Brainstorm", pos: { x: 7, y: 7 } }, { name: "Satellite", pos: { x: 8, y: 7 } }, { name: "Shoot the Moon", pos: { x: 2, y: 6 } }, { name: "Drivers License", pos: { x: 0, y: 7 } }, { name: "Cartomancer", pos: { x: 7, y: 3 } }, { name: "Astronomer", pos: { x: 2, y: 7 } }, { name: "Burnt Joker", pos: { x: 3, y: 7 } }, { name: "Bootstraps", pos: { x: 9, y: 8 } }, { name: "Canio", pos: { x: 3, y: 8 } }, { name: "Triboulet", pos: { x: 4, y: 8 } }, { name: "Yorick", pos: { x: 5, y: 8 } }, { name: "Chicot", pos: { x: 6, y: 8 } }, { name: "Perkeo", pos: { x: 7, y: 8 } },
];

export const JOKER_FACES: SpriteEntry[] = [
  { name: "Hologram", pos: { x: 2, y: 9 }, animated: true },
  { name: "Canio", pos: { x: 3, y: 9 }, animated: true },
  { name: "Triboulet", pos: { x: 4, y: 9 }, animated: true },
  { name: "Yorick", pos: { x: 5, y: 9 }, animated: true },
  { name: "Chicot", pos: { x: 6, y: 9 }, animated: true },
  { name: "Perkeo", pos: { x: 7, y: 9 }, animated: true },
];

export const CONSUMABLE_FACES: SpriteEntry[] = [
  { name: "The Soul", pos: { x: 0, y: 1 }, animated: true },
];

export const TAROTS_AND_PLANETS: SpriteEntry[] = [
  { name: "The Fool", pos: { x: 0, y: 0 } }, { name: "The Magician", pos: { x: 1, y: 0 } }, { name: "The High Priestess", pos: { x: 2, y: 0 } }, { name: "The Empress", pos: { x: 3, y: 0 } }, { name: "The Emperor", pos: { x: 4, y: 0 } }, { name: "The Hierophant", pos: { x: 5, y: 0 } }, { name: "The Lovers", pos: { x: 6, y: 0 } }, { name: "The Chariot", pos: { x: 7, y: 0 } }, { name: "Justice", pos: { x: 8, y: 0 } }, { name: "The Hermit", pos: { x: 9, y: 0 } }, { name: "The Wheel of Fortune", pos: { x: 0, y: 1 } }, { name: "Strength", pos: { x: 1, y: 1 } }, { name: "The Hanged Man", pos: { x: 2, y: 1 } }, { name: "Death", pos: { x: 3, y: 1 } }, { name: "Temperance", pos: { x: 4, y: 1 } }, { name: "The Devil", pos: { x: 5, y: 1 } }, { name: "The Tower", pos: { x: 6, y: 1 } }, { name: "The Star", pos: { x: 7, y: 1 } }, { name: "The Moon", pos: { x: 8, y: 1 } }, { name: "The Sun", pos: { x: 9, y: 1 } }, { name: "Judgement", pos: { x: 0, y: 2 } }, { name: "The World", pos: { x: 1, y: 2 } }, { name: "Mercury", pos: { x: 0, y: 3 } }, { name: "Venus", pos: { x: 1, y: 3 } }, { name: "Earth", pos: { x: 2, y: 3 } }, { name: "Mars", pos: { x: 3, y: 3 } }, { name: "Jupiter", pos: { x: 4, y: 3 } }, { name: "Saturn", pos: { x: 5, y: 3 } }, { name: "Uranus", pos: { x: 6, y: 3 } }, { name: "Neptune", pos: { x: 7, y: 3 } }, { name: "Pluto", pos: { x: 8, y: 3 } }, { name: "Planet X", pos: { x: 9, y: 2 } }, { name: "Ceres", pos: { x: 8, y: 2 } }, { name: "Eris", pos: { x: 3, y: 2 } }, { name: "Familiar", pos: { x: 0, y: 4 } }, { name: "Grim", pos: { x: 1, y: 4 } }, { name: "Incantation", pos: { x: 2, y: 4 } }, { name: "Talisman", pos: { x: 3, y: 4 } }, { name: "Aura", pos: { x: 4, y: 4 } }, { name: "Wraith", pos: { x: 5, y: 4 } }, { name: "Sigil", pos: { x: 6, y: 4 } }, { name: "Ouija", pos: { x: 7, y: 4 } }, { name: "Ectoplasm", pos: { x: 8, y: 4 } }, { name: "Immolate", pos: { x: 9, y: 4 } }, { name: "Ankh", pos: { x: 0, y: 5 } }, { name: "Deja Vu", pos: { x: 1, y: 5 } }, { name: "Hex", pos: { x: 2, y: 5 } }, { name: "Trance", pos: { x: 3, y: 5 } }, { name: "Medium", pos: { x: 4, y: 5 } }, { name: "Cryptid", pos: { x: 5, y: 5 } }, { name: "The Soul", pos: { x: 2, y: 2 } }, { name: "Black Hole", pos: { x: 9, y: 3 } },
];

export const TAGS: SpriteEntry[] = [
  { name: "Uncommon Tag", pos: { x: 0, y: 0 } }, { name: "Rare Tag", pos: { x: 1, y: 0 } }, { name: "Negative Tag", pos: { x: 2, y: 0 } }, { name: "Foil Tag", pos: { x: 3, y: 0 } }, { name: "Holographic Tag", pos: { x: 0, y: 1 } }, { name: "Polychrome Tag", pos: { x: 1, y: 1 } }, { name: "Investment Tag", pos: { x: 2, y: 1 } }, { name: "Voucher Tag", pos: { x: 3, y: 1 } }, { name: "Boss Tag", pos: { x: 0, y: 2 } }, { name: "Standard Tag", pos: { x: 1, y: 2 } }, { name: "Charm Tag", pos: { x: 2, y: 2 } }, { name: "Meteor Tag", pos: { x: 3, y: 2 } }, { name: "Buffoon Tag", pos: { x: 4, y: 2 } }, { name: "Handy Tag", pos: { x: 1, y: 3 } }, { name: "Garbage Tag", pos: { x: 2, y: 3 } }, { name: "Ethereal Tag", pos: { x: 3, y: 3 } }, { name: "Coupon Tag", pos: { x: 4, y: 0 } }, { name: "Double Tag", pos: { x: 5, y: 0 } }, { name: "Juggle Tag", pos: { x: 5, y: 1 } }, { name: "D6 Tag", pos: { x: 5, y: 3 } }, { name: "Top-up Tag", pos: { x: 4, y: 1 } }, { name: "Speed Tag", pos: { x: 0, y: 3 } }, { name: "Orbital Tag", pos: { x: 5, y: 2 } }, { name: "Economy Tag", pos: { x: 4, y: 3 } },
];

export const VOUCHERS: SpriteEntry[] = [
  { name: "Overstock", pos: { x: 0, y: 0 } }, { name: "Clearance Sale", pos: { x: 3, y: 0 } }, { name: "Hone", pos: { x: 4, y: 0 } }, { name: "Reroll Surplus", pos: { x: 0, y: 2 } }, { name: "Crystal Ball", pos: { x: 2, y: 2 } }, { name: "Telescope", pos: { x: 3, y: 2 } }, { name: "Grabber", pos: { x: 5, y: 0 } }, { name: "Wasteful", pos: { x: 6, y: 0 } }, { name: "Tarot Merchant", pos: { x: 1, y: 0 } }, { name: "Planet Merchant", pos: { x: 2, y: 0 } }, { name: "Seed Money", pos: { x: 1, y: 2 } }, { name: "Blank", pos: { x: 7, y: 0 } }, { name: "Magic Trick", pos: { x: 4, y: 2 } }, { name: "Hieroglyph", pos: { x: 5, y: 2 } }, { name: "Director's Cut", pos: { x: 6, y: 2 } }, { name: "Paint Brush", pos: { x: 7, y: 2 } }, { name: "Overstock Plus", pos: { x: 0, y: 1 } }, { name: "Liquidation", pos: { x: 3, y: 1 } }, { name: "Glow Up", pos: { x: 4, y: 1 } }, { name: "Reroll Glut", pos: { x: 0, y: 3 } }, { name: "Omen Globe", pos: { x: 2, y: 3 } }, { name: "Observatory", pos: { x: 3, y: 3 } }, { name: "Nacho Tong", pos: { x: 5, y: 1 } }, { name: "Recyclomancy", pos: { x: 6, y: 1 } }, { name: "Tarot Tycoon", pos: { x: 1, y: 1 } }, { name: "Planet Tycoon", pos: { x: 2, y: 1 } }, { name: "Money Tree", pos: { x: 1, y: 3 } }, { name: "Antimatter", pos: { x: 7, y: 1 } }, { name: "Illusion", pos: { x: 4, y: 3 } }, { name: "Petroglyph", pos: { x: 5, y: 3 } }, { name: "Retcon", pos: { x: 6, y: 3 } }, { name: "Palette", pos: { x: 7, y: 3 } },
];

export const BOSSES: SpriteEntry[] = [
  { name: "Small Blind", pos: { x: 0, y: 0 } }, { name: "Big Blind", pos: { x: 0, y: 1 } }, { name: "The Ox", pos: { x: 0, y: 2 } }, { name: "The Hook", pos: { x: 0, y: 7 } }, { name: "The Mouth", pos: { x: 0, y: 18 } }, { name: "The Fish", pos: { x: 0, y: 5 } }, { name: "The Club", pos: { x: 0, y: 4 } }, { name: "The Manacle", pos: { x: 0, y: 8 } }, { name: "The Tooth", pos: { x: 0, y: 22 } }, { name: "The Wall", pos: { x: 0, y: 9 } }, { name: "The House", pos: { x: 0, y: 3 } }, { name: "The Mark", pos: { x: 0, y: 23 } }, { name: "Cerulean Bell", pos: { x: 0, y: 26 } }, { name: "The Wheel", pos: { x: 0, y: 10 } }, { name: "The Arm", pos: { x: 0, y: 11 } }, { name: "The Psychic", pos: { x: 0, y: 12 } }, { name: "The Goad", pos: { x: 0, y: 13 } }, { name: "The Water", pos: { x: 0, y: 14 } }, { name: "The Eye", pos: { x: 0, y: 17 } }, { name: "The Plant", pos: { x: 0, y: 19 } }, { name: "The Needle", pos: { x: 0, y: 20 } }, { name: "The Head", pos: { x: 0, y: 21 } }, { name: "Verdant Leaf", pos: { x: 0, y: 28 } }, { name: "Violet Vessel", pos: { x: 0, y: 29 } }, { name: "The Window", pos: { x: 0, y: 6 } }, { name: "The Serpent", pos: { x: 0, y: 15 } }, { name: "The Pillar", pos: { x: 0, y: 16 } }, { name: "The Flint", pos: { x: 0, y: 24 } }, { name: "Amber Acorn", pos: { x: 0, y: 27 } }, { name: "Crimson Heart", pos: { x: 0, y: 25 } },
];

export const BOOSTER_PACKS: SpriteEntry[] = [
  { name: "Arcana Pack", pos: { x: 0, y: 0 } }, { name: "Celestial Pack", pos: { x: 0, y: 1 } }, { name: "Jumbo Arcana Pack", pos: { x: 0, y: 2 } }, { name: "Mega Arcana Pack", pos: { x: 2, y: 2 } }, { name: "Jumbo Celestial Pack", pos: { x: 0, y: 3 } }, { name: "Mega Celestial Pack", pos: { x: 2, y: 3 } }, { name: "Spectral Pack", pos: { x: 0, y: 4 } }, { name: "Jumbo Spectral Pack", pos: { x: 2, y: 4 } }, { name: "Mega Spectral Pack", pos: { x: 3, y: 4 } }, { name: "Standard Pack", pos: { x: 0, y: 6 } }, { name: "Jumbo Standard Pack", pos: { x: 0, y: 7 } }, { name: "Mega Standard Pack", pos: { x: 2, y: 7 } }, { name: "Buffoon Pack", pos: { x: 0, y: 8 } }, { name: "Jumbo Buffoon Pack", pos: { x: 2, y: 8 } }, { name: "Mega Buffoon Pack", pos: { x: 3, y: 8 } },
];

export const EDITION_MAP: Record<string, number> = {
  Foil: 1,
  Holographic: 2,
  Polychrome: 3,
};

export const STICKER_MAP: Record<string, SpritePos> = {
  Eternal: { x: 0, y: 0 },
  Perishable: { x: 0, y: 2 },
  Rental: { x: 1, y: 2 },
};

export const RANK_MAP: Record<string, number> = {
  "2": 0, "3": 1, "4": 2, "5": 3, "6": 4, "7": 5, "8": 6, "9": 7, "10": 8, Jack: 9, Queen: 10, King: 11, Ace: 12,
};

export const SUIT_MAP: Record<string, number> = {
  Hearts: 0, Clubs: 1, Diamonds: 2, Spades: 3,
};

export const ENHANCER_MAP: Record<string, SpritePos> = {
  Bonus: { x: 1, y: 1 }, Mult: { x: 2, y: 1 }, Wild: { x: 3, y: 1 }, Glass: { x: 5, y: 1 }, Steel: { x: 6, y: 1 }, Stone: { x: 5, y: 0 }, Gold: { x: 6, y: 0 }, Lucky: { x: 4, y: 1 },
};

export const SEAL_MAP: Record<string, SpritePos> = {
  "Gold Seal": { x: 2, y: 0 }, "Purple Seal": { x: 4, y: 4 }, "Red Seal": { x: 5, y: 4 }, "Blue Seal": { x: 6, y: 4 },
  Gold: { x: 2, y: 0 }, Purple: { x: 4, y: 4 }, Red: { x: 5, y: 4 }, Blue: { x: 6, y: 4 },
};

/** Build fast lookup maps */
const jokerMap = new Map(JOKERS.map((j) => [j.name, j]));
const jokerFaceMap = new Map(JOKER_FACES.map((j) => [j.name, j]));
const consumableMap = new Map(TAROTS_AND_PLANETS.map((t) => [t.name, t]));
const consumableFaceMap = new Map(CONSUMABLE_FACES.map((c) => [c.name, c]));
const tagMap = new Map(TAGS.map((t) => [t.name, t]));
const voucherMap = new Map(VOUCHERS.map((v) => [v.name, v]));
const bossMap = new Map(BOSSES.map((b) => [b.name, b]));
const boosterMap = new Map(BOOSTER_PACKS.map((b) => [b.name, b]));

export function findJoker(name: string) { return jokerMap.get(name); }
export function findJokerFace(name: string) { return jokerFaceMap.get(name); }
export function findConsumable(name: string) { return consumableMap.get(name); }
export function findConsumableFace(name: string) { return consumableFaceMap.get(name); }
export function findTag(name: string) { return tagMap.get(name); }
export function findVoucher(name: string) { return voucherMap.get(name); }
export function findBoss(name: string) { return bossMap.get(name); }
export function findBooster(name: string) { return boosterMap.get(name); }
