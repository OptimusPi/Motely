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

/** Look up sprite data by name. Accepts display names ("Icy Joker"), enum keys ("IcyJoker"), and "Joker | Name" prefixed forms. */
export function getSpriteData(name: string): SpriteData | null {
  return ITEM_MAP.get(normalize(stripPrefix(name))) ?? ITEM_MAP.get(normalize(name)) ?? null;
}
