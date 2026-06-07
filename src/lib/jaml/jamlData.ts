export const CLAUSE_TYPE_KEYS: readonly string[] = [
  'joker', 'jokers',
  'commonJoker', 'commonJokers',
  'uncommonJoker', 'uncommonJokers',
  'rareJoker', 'rareJokers',
  'legendaryJoker', 'legendaryJokers',
  'voucher', 'vouchers',
  'tarotCard', 'tarotCards',
  'spectralCard', 'spectralCards',
  'planetCard',
  'boss', 'tag', 'smallBlindTag', 'bigBlindTag',
  'standardCard', 'standardCards',
  'erraticRank', 'erraticSuit', 'erraticCard',
  'startingDraw', 'event',
  'luckyMoney', 'luckyMult', 'misprintMult',
  'wheelOfFortune', 'cavendishExtinct', 'grosMichelExtinct',
  'spaceLevelup', 'businessPayout', 'bloodstoneTrigger',
  'parkingPayout', 'glassDestroy', 'wheelStaysFlipped',
  'and', 'or', 'clauses',
];

export const CLAUSE_TYPES = [...CLAUSE_TYPE_KEYS];

export const ARRAY_KEYS = ['antes', 'tags', 'labels'];

export const JAML_KEYWORDS = [
  'must', 'should', 'mustNot', 'any', 'Any', ...CLAUSE_TYPES, ...ARRAY_KEYS,
];
