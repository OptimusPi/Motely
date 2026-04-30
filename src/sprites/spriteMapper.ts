import {
  JOKERS, JOKER_FACES, TAROTS_AND_PLANETS, CONSUMABLE_FACES,
  VOUCHERS, BOSSES, TAGS, BOOSTER_PACKS,
  type SpriteEntry,
} from "./spriteData.js";

export interface SpritePos {
  x: number;
  y: number;
}

export type SpriteSheetType =
  | "Jokers" | "Tarots" | "Vouchers" | "Boosters"
  | "Enhancers" | "Editions" | "BlindChips" | "tags"
  | "Stakes" | "Decks";

import type { JamlAssetKey } from '../assets.js';

export const SHEET_META: Record<SpriteSheetType, { cols: number; rows: number; assetKey: JamlAssetKey }> = {
  Jokers:    { cols: 10, rows: 16, assetKey: 'jokers' },
  Tarots:    { cols: 10, rows: 6,  assetKey: 'tarots' },
  Vouchers:  { cols: 9,  rows: 4,  assetKey: 'vouchers' },
  Boosters:  { cols: 4,  rows: 9,  assetKey: 'boosters' },
  BlindChips:{ cols: 21, rows: 31, assetKey: 'blinds' },
  tags:      { cols: 6,  rows: 5,  assetKey: 'tags' },
  Enhancers: { cols: 7,  rows: 5,  assetKey: 'enhancers' },
  Editions:  { cols: 5,  rows: 1,  assetKey: 'editions' },
  Stakes:    { cols: 5,  rows: 2,  assetKey: 'stakes' },
  Decks:     { cols: 13, rows: 4,  assetKey: 'deck' },
};

export interface SpriteData {
  pos: SpritePos;
  type: SpriteSheetType;
}

const normalize = (name: string): string => name.toLowerCase().replace(/\s+/g, "");
const stripPrefix = (name: string): string =>
  name.replace(/^(Joker|Tarot|Planet|Voucher|Pack|Edition|Tag) [|:] /i, "").trim();

const ITEM_MAP = new Map<string, SpriteData>();

function registerAll(items: SpriteEntry[], type: SpriteSheetType) {
  for (const item of items) {
    if (!item.name || !item.pos) continue;
    ITEM_MAP.set(normalize(item.name), { pos: item.pos, type });
  }
}

registerAll(JOKERS, "Jokers");
registerAll(JOKER_FACES, "Jokers");
registerAll(TAROTS_AND_PLANETS, "Tarots");
registerAll(CONSUMABLE_FACES ?? [], "Tarots");
registerAll(VOUCHERS, "Vouchers");
registerAll(BOSSES, "BlindChips");
registerAll(TAGS, "tags");
registerAll(BOOSTER_PACKS ?? [], "Boosters");

/**
 * Per-sheet mystery/back card positions for unknown items.
 * Used as fallback when an item name can't be resolved.
 */
export const MYSTERY_SPRITES: Partial<Record<SpriteSheetType, SpritePos>> = {
  Jokers:     { x: 9, y: 9 },   // grey card back (legendary face row)
  Tarots:     { x: 4, y: 2 },   // blank consumable
  Vouchers:   { x: 8, y: 2 },   // mystery voucher
  Boosters:   { x: 0, y: 5 },   // grey empty pack
  tags:       { x: 3, y: 4 },   // grey ? tag
  BlindChips: { x: 0, y: 30 },  // grey ? blind
};

/** Get the mystery/fallback sprite for a given sheet type. */
export function getMysterySprite(sheet: SpriteSheetType): SpriteData {
  return { pos: MYSTERY_SPRITES[sheet] ?? { x: 0, y: 0 }, type: sheet };
}

/** Look up sprite data by name. Accepts display names ("Icy Joker"), enum keys ("IcyJoker"), and "Joker | Name" prefixed forms. */
export function getSpriteData(name: string): SpriteData | null {
  return ITEM_MAP.get(normalize(stripPrefix(name))) ?? ITEM_MAP.get(normalize(name)) ?? null;
}

/**
 * Wildcard "Any" sprites from the Enhancers sheet.
 * Used in the JAML visual editor when a clause value is "Any" (e.g. `legendaryJoker: Any`).
 */
export const WILDCARD_SPRITES = {
  /** Grey silhouette — Enhancers row 3 col 5. For generic "Any" items. */
  anySilhouette: { pos: { x: 5, y: 3 }, type: "Enhancers" as SpriteSheetType },
  /** Grey ? circle — Enhancers row 3 col 6. For "boss: Any" etc. */
  anyMystery:    { pos: { x: 6, y: 3 }, type: "Enhancers" as SpriteSheetType },
  /** Red X debuff — Editions row 0 col 4. For mustNot zone indicator. */
  mustNotDebuff: { pos: { x: 4, y: 0 }, type: "Editions" as SpriteSheetType },
  /** The Soul card — Tarots row 2 col 2. Base for "legendaryJoker: Any" (they spawn from Soul). */
  anyLegendary:  { pos: { x: 2, y: 2 }, type: "Tarots" as SpriteSheetType },
  /** Blank spectral back — Tarots row 2 col 5. For "spectralCard: Any". */
  anySpectral:   { pos: { x: 5, y: 2 }, type: "Tarots" as SpriteSheetType },
  /** Blank tarot back — Tarots row 2 col 6. For "tarotCard: Any". */
  anyTarot:      { pos: { x: 6, y: 2 }, type: "Tarots" as SpriteSheetType },
  /** Blank planet back — Tarots row 2 col 7. For "planetCard: Any". */
  anyPlanet:     { pos: { x: 7, y: 2 }, type: "Tarots" as SpriteSheetType },
} as const;

/** Look up sprite data by name, falling back to the mystery card for the given sheet if not found. */
export function getSpriteDataOrMystery(name: string, fallbackSheet: SpriteSheetType = "Jokers"): SpriteData {
  return getSpriteData(name) ?? getMysterySprite(fallbackSheet);
}
