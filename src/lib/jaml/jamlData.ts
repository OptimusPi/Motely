import { CLAUSE_TYPE_KEYS } from './jamlSchema.js';

export const CLAUSE_TYPES = [...CLAUSE_TYPE_KEYS];

export const ARRAY_KEYS = ['antes', 'tags', 'labels'];

export const JAML_KEYWORDS = [
    'must', 'should', 'mustNot', 'any', 'Any', ...CLAUSE_TYPES, ...ARRAY_KEYS
];
