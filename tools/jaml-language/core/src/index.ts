export const JAML_LANGUAGE_ID = "jaml";
export const JUMMY_LANGUAGE_ID = "jummy";

export const JAML_ROOT_KEYS = [
  "id",
  "name",
  "author",
  "dateCreated",
  "description",
  "deck",
  "stake",
  "defaults",
  "must",
  "should",
  "mustNot",
  "aesthetics",
  "hashtags",
  "seeds",
] as const;

export const CLAUSE_TYPE_KEYS = [
  "joker",
  "jokers",
  "commonJoker",
  "commonJokers",
  "uncommonJoker",
  "uncommonJokers",
  "rareJoker",
  "rareJokers",
  "mixedJoker",
  "mixedJokers",
  "soulJoker",
  "legendaryJoker",
  "voucher",
  "vouchers",
  "tarot",
  "tarotCard",
  "spectral",
  "spectralCard",
  "planet",
  "planetCard",
  "boss",
  "tag",
  "smallBlindTag",
  "bigBlindTag",
  "standardCard",
  "erraticRank",
  "erraticSuit",
  "erraticCard",
  "startingDraw",
  "event",
  "eventType",
  "luckyMoney",
  "luckyMult",
  "misprintMult",
  "wheelOfFortune",
  "cavendishExtinct",
  "grosMichelExtinct",
] as const;

export const CLAUSE_PROPERTY_KEYS = [
  "type",
  "value",
  "or",
  "and",
  "clauses",
  "label",
  "score",
  "min",
  "max",
  "edition",
  "stickers",
  "seal",
  "enhancement",
  "rank",
  "suit",
  "rolls",
  "soulEditionRolls",
  "soulCardOnly",
  "mode",
  "antes",
  "shopItems",
  "boosterPacks",
  "minShopSlot",
  "maxShopSlot",
  "minPackSlot",
  "maxPackSlot",
  "sources",
] as const;

export const CLAUSE_KEYS = [...CLAUSE_TYPE_KEYS, ...CLAUSE_PROPERTY_KEYS] as const;

export function looksLikeJson(text: string): boolean {
  const t = text.trimStart();
  return t.startsWith("{") || t.startsWith("[");
}

export function looksLikeJummy(text: string): boolean {
  const t = text.toLowerCase();
  return t.includes("jummy:") || t.includes("what:") || t.includes("where:");
}

export function unknownRootKeys(root: Record<string, unknown>): string[] {
  const allowed = new Set<string>(JAML_ROOT_KEYS as readonly string[]);
  return Object.keys(root).filter((k) => !allowed.has(k));
}