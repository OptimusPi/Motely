import { Motely } from 'motely-wasm';
import {
    CLAUSE_TYPE_KEYS,
    SOURCE_KEYS,
} from '../jaml/jamlSchema.js';

type RuntimeEnum = Record<string, string | number>;
type MotelyRuntimeEnums = typeof Motely & Record<string, RuntimeEnum>;

const MotelyEnums = Motely as MotelyRuntimeEnums;

function enumOptionKeys(enumObject: RuntimeEnum): string[] {
    return Object.keys(enumObject).filter(k => isNaN(Number(k)));
}

// UI options derived from motely-wasm directly
export const DECK_OPTIONS = enumOptionKeys(Motely.MotelyDeck);
export const STAKE_OPTIONS = enumOptionKeys(Motely.MotelyStake);

export const ANTE_OPTIONS = [1, 2, 3, 4, 5, 6, 7, 8];
export const SLOT_OPTIONS = [1, 2, 3, 4, 5];

export const RANK_OPTIONS = enumOptionKeys(MotelyEnums.MotelyStandardcardRank);
export const SUIT_OPTIONS = enumOptionKeys(MotelyEnums.MotelyStandardcardSuit);
export const ENHANCEMENT_OPTIONS = enumOptionKeys(MotelyEnums.MotelyItemEnhancement).filter(k => k !== "None");
export const EDITION_OPTIONS = enumOptionKeys(MotelyEnums.MotelyItemEdition).filter(k => k !== "None");
export const SEAL_OPTIONS = enumOptionKeys(MotelyEnums.MotelyItemSeal).filter(k => k !== "None");

export const CLAUSE_TYPES = [...CLAUSE_TYPE_KEYS];

export const SOURCE_OPTIONS = [...SOURCE_KEYS];
