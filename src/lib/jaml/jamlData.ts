export const CLAUSE_TYPES = [
    'joker', 'soulJoker', 'voucher', 'tarotCard', 'planetCard',
    'spectralCard', 'standardCard', 'tag', 'boss', 'event'
];

export const ARRAY_KEYS = ['antes', 'tags', 'labels'];

export const JAML_KEYWORDS = [
    'must', 'should', 'mustNot', 'any', 'Any', ...CLAUSE_TYPES, ...ARRAY_KEYS
];
