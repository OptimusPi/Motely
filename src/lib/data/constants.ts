import {
  CLAUSE_TYPE_KEYS,
  SOURCE_KEYS,
  DECK_VALUES,
  STAKE_VALUES,
  EDITION_VALUES,
  SEAL_VALUES,
  ENHANCEMENT_VALUES,
} from '../jaml/jamlSchema.js';

export const DECK_OPTIONS       = DECK_VALUES as string[];
export const STAKE_OPTIONS      = STAKE_VALUES as string[];
export const EDITION_OPTIONS    = EDITION_VALUES as string[];
export const SEAL_OPTIONS       = SEAL_VALUES as string[];
export const ENHANCEMENT_OPTIONS = ENHANCEMENT_VALUES as string[];

export const ANTE_OPTIONS  = [1, 2, 3, 4, 5, 6, 7, 8];
export const SLOT_OPTIONS  = [1, 2, 3, 4, 5];

export const RANK_OPTIONS = ["Ace", "King", "Queen", "Jack", "Ten", "Nine", "Eight", "Seven", "Six", "Five", "Four", "Three", "Two"];
export const SUIT_OPTIONS = ["Spades", "Hearts", "Clubs", "Diamonds"];

export const CLAUSE_TYPES   = [...CLAUSE_TYPE_KEYS];
export const SOURCE_OPTIONS = [...SOURCE_KEYS];
