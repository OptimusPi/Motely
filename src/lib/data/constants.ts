import { Motely } from 'motely-wasm';
import {
    CLAUSE_TYPE_KEYS,
    SOURCE_KEYS,
} from '../jaml/jamlSchema.js';

// UI options derived from motely-wasm directly
export const DECK_OPTIONS = Object.keys(Motely.MotelyDeck).filter(k => isNaN(Number(k)));
export const STAKE_OPTIONS = Object.keys(Motely.MotelyStake).filter(k => isNaN(Number(k)));

export const ANTE_OPTIONS = [1, 2, 3, 4, 5, 6, 7, 8];
export const SLOT_OPTIONS = [1, 2, 3, 4, 5];

export const RANK_OPTIONS = Object.keys(Motely.MotelyStandardcardRank).filter(k => isNaN(Number(k)));
export const SUIT_OPTIONS = Object.keys(Motely.MotelyStandardcardSuit).filter(k => isNaN(Number(k)));
export const ENHANCEMENT_OPTIONS = Object.keys(Motely.MotelyItemEnhancement).filter(k => isNaN(Number(k)) && k !== "None");
export const EDITION_OPTIONS = Object.keys(Motely.MotelyItemEdition).filter(k => isNaN(Number(k)) && k !== "None");
export const SEAL_OPTIONS = Object.keys(Motely.MotelyItemSeal).filter(k => isNaN(Number(k)) && k !== "None");

export const CLAUSE_TYPES = [...CLAUSE_TYPE_KEYS];

export const SOURCE_OPTIONS = [...SOURCE_KEYS];
