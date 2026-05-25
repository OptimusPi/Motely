import {
  MotelyDeck,
  MotelyStake,
  MotelyItemEdition,
  MotelyItemSeal,
  MotelyItemEnhancement,
} from 'motely-wasm/motely/enums';

const enumNames = (e: Record<string, string | number>): readonly string[] =>
  Object.keys(e).filter(k => isNaN(Number(k)));

export const DECK_VALUES   = enumNames(MotelyDeck);
export const STAKE_VALUES  = enumNames(MotelyStake);
export const EDITION_VALUES = enumNames(MotelyItemEdition);
export const SEAL_VALUES   = enumNames(MotelyItemSeal);
export const ENHANCEMENT_VALUES = enumNames(MotelyItemEnhancement);

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

export const SOURCE_KEYS: readonly string[] = [
  'shopItems', 'boosterPacks', 'minShopItem', 'maxShopItem',
  'tags', 'requireMega', 'charmTag', 'etherealTag',
  'judgement', 'rareTag', 'uncommonTag', 'wraith', 'soulCard',
  'arcanaPacks', 'spectralPacks', 'riffRaff',
  'purpleSealOrEightBall', 'emperor', 'sixthSense', 'seance',
  'certificate', 'incantation', 'familiar', 'grim', 'deckDraw',
  'uncommonShopJokers', 'rareShopJokers', 'commonShopJokers', 'allShopJokers',
];

export const PROPERTY_KEYS: readonly string[] = CLAUSE_TYPE_KEYS;

export const METADATA_KEYS: readonly string[] = [
  'id', 'name', 'description', 'author', 'dateCreated',
  'deck', 'stake', 'seeds', 'hashtags', 'aesthetics', 'defaults',
];

export const SECTION_KEYS: readonly string[] = ['must', 'should', 'mustNot'];

export const ALL_JAML_KEYWORDS: string[] = [
  ...SECTION_KEYS, ...METADATA_KEYS, ...CLAUSE_TYPE_KEYS, ...SOURCE_KEYS,
];

export function getValidValuesForKey(key: string): readonly string[] | null {
  if (key === 'deck') return DECK_VALUES;
  if (key === 'stake') return STAKE_VALUES;
  if (key === 'edition') return EDITION_VALUES;
  if (key === 'seal') return SEAL_VALUES;
  if (key === 'enhancement') return ENHANCEMENT_VALUES;
  return null;
}

export function isInvalidValueForProp(value: string, prop: string): boolean {
  const valid = getValidValuesForKey(prop);
  if (!valid) return false;
  return !valid.includes(value);
}

export function getAvailablePropsForType(): readonly string[] {
  return PROPERTY_KEYS;
}

export function isInvalidPropForType(): boolean {
  return false;
}

export interface ValidationState {
  errors: string[];
  warnings: string[];
}
