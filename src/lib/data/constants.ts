import {
    CLAUSE_TYPE_KEYS,
    DECK_VALUES,
    EDITION_VALUES,
    ENHANCEMENT_VALUES,
    RANK_VALUES,
    SEAL_VALUES,
    SOURCE_KEYS,
    STAKE_VALUES,
    SUIT_VALUES,
} from '../jaml/jamlSchema.js';

// UI options derived from the shipped JAML schema wherever possible.
export const DECK_OPTIONS = [...DECK_VALUES];
export const STAKE_OPTIONS = [...STAKE_VALUES];

export const ANTE_OPTIONS = [1, 2, 3, 4, 5, 6, 7, 8];
export const SLOT_OPTIONS = [1, 2, 3, 4, 5];

export const RANK_OPTIONS = [...RANK_VALUES];
export const SUIT_OPTIONS = [...SUIT_VALUES];
export const ENHANCEMENT_OPTIONS = [...ENHANCEMENT_VALUES];
export const EDITION_OPTIONS = [...EDITION_VALUES];
export const SEAL_OPTIONS = [...SEAL_VALUES];

export const CLAUSE_TYPES = [...CLAUSE_TYPE_KEYS];

export const SOURCE_OPTIONS = [...SOURCE_KEYS];
