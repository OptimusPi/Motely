import {
    CLAUSE_TYPE_KEYS,
    SOURCE_KEYS,
} from '../jaml/jamlSchema.js';

export const DECK_OPTIONS = [
    "Red", "Blue", "Yellow", "Green", "Black", "Magic", "Nebula", "Ghost",
    "Abandoned", "Checkered", "Zodiac", "Painted", "Anaglyph", "Plasma", "Erratic",
];
export const STAKE_OPTIONS = ["White", "Red", "Green", "Black", "Blue", "Purple", "Orange", "Gold"];

export const ANTE_OPTIONS = [1, 2, 3, 4, 5, 6, 7, 8];
export const SLOT_OPTIONS = [1, 2, 3, 4, 5];

export const RANK_OPTIONS = ["Ace", "King", "Queen", "Jack", "Ten", "Nine", "Eight", "Seven", "Six", "Five", "Four", "Three", "Two"];
export const SUIT_OPTIONS = ["Spades", "Hearts", "Clubs", "Diamonds"];
export const ENHANCEMENT_OPTIONS = ["Lucky", "Bonus", "Mult", "Glass", "Steel", "Stone", "Gold", "Wild"];
export const EDITION_OPTIONS = ["Foil", "Holographic", "Polychrome", "Negative"];
export const SEAL_OPTIONS = ["Gold", "Red", "Blue", "Purple"];

export const CLAUSE_TYPES = [...CLAUSE_TYPE_KEYS];

export const SOURCE_OPTIONS = [...SOURCE_KEYS];
