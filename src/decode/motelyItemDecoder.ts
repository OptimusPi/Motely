/**
 * Motely item decoder.
 *
 * MotelyItem.Value is a packed integer. The MotelyItemType enum
 * uses packed integers where the top nibble encodes category:
 *   0x1000 = Standardcard, 0x2000 = Spectral, 0x3000 = Tarot,
 *   0x4000 = Planet, 0x5000 = Joker, 0xF000 = Invalid
 */

import { Motely } from "motely-wasm";
import { getItemCategory, getItemDisplayName, type CardCategory } from "../utils/itemUtils.js";

// ─── Category from MotelyItemType integer ────────────────────────────────────

const CATEGORY_MASK = 0xf000;
const VALUE_TYPE_MASK = 0xffff;
const VALUE_SEAL_MASK = 0x70000;
const VALUE_ENHANCEMENT_MASK = 0x780000;
const VALUE_EDITION_MASK = 0x3800000;

const CATEGORY_TO_TYPE: Record<number, string> = {
  0x1000: "Standard card",
  0x2000: "Spectral",
  0x3000: "Tarot",
  0x4000: "Planet",
  0x5000: "Joker",
};

export type MotelyItemInput = number | MotelyRuntimeItem | null | undefined;

export interface MotelyRuntimeItem {
  type?: number;
  value?: number;
  edition?: number;
  seal?: number;
  enhancement?: number;
  suit?: number;
  rank?: number;
}

export type MotelyRenderableCategory = CardCategory | "unknown";

export interface DecodedMotelyItem {
  itemType: number;
  enumKey: string;
  displayName: string;
  category: MotelyRenderableCategory;
  edition: "Foil" | "Holographic" | "Polychrome" | "Negative" | null;
  seal: "Gold" | "Red" | "Blue" | "Purple" | null;
  enhancement: string | null;
  rank: string | null;
  suit: "Clubs" | "Diamonds" | "Hearts" | "Spades" | null;
}

export interface MotelyJamlCard {
  type: "joker" | "consumable" | "playing";
  card: {
    name: string;
    edition?: "Foil" | "Holographic" | "Polychrome" | "Negative";
    seal?: string;
    enhancements?: string[];
    rank?: string;
    suit?: string;
    scale?: number;
  };
}

interface DecodedMotelyItemBase {
  itemType: number;
  enumKey: string;
  displayName: string;
  category: MotelyRenderableCategory;
  rank: string | null;
  suit: "Clubs" | "Diamonds" | "Hearts" | "Spades" | null;
}

// ─── Reverse lookup: MotelyItemType integer → string name ────────────────────

const _itemTypeToName = new Map<number, string>();

function ensureItemTypeMap() {
  if (_itemTypeToName.size > 0) return;
  const e = Motely.MotelyItemType as Record<string, unknown>;
  for (const [key, val] of Object.entries(e)) {
    if (typeof val === "number" && typeof key === "string" && !/^\d+$/.test(key)) {
      _itemTypeToName.set(val, key);
    }
  }
}

function asRuntimeItem(input: MotelyItemInput): MotelyRuntimeItem | null {
  return input !== null && typeof input === "object" ? (input as MotelyRuntimeItem) : null;
}

function finiteNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function runtimeEnumName(
  enumObject: Record<string, unknown> | null | undefined,
  value: number | null,
): string | null {
  if (value === null || !enumObject || typeof enumObject !== "object") return null;
  const enumKey = enumObject[String(value)];
  return typeof enumKey === "string" && enumKey.length > 0 ? enumKey : null;
}

function parseStandardcardEnumKey(enumKey: string): { rank: string; suit: "Clubs" | "Diamonds" | "Hearts" | "Spades" } | null {
  const match = /^([CDHS])(10|[2-9JQKA])$/.exec(enumKey);
  if (!match) return null;

  const suitMap: Record<string, "Clubs" | "Diamonds" | "Hearts" | "Spades"> = {
    C: "Clubs",
    D: "Diamonds",
    H: "Hearts",
    S: "Spades",
  };

  const rankMap: Record<string, string> = {
    J: "Jack",
    Q: "Queen",
    K: "King",
    A: "Ace",
  };

  return {
    rank: rankMap[match[2]] ?? match[2],
    suit: suitMap[match[1]],
  };
}

function rankNameFromEnum(enumKey: string | null): string | null {
  if (!enumKey) return null;

  const rankMap: Record<string, string> = {
    Two: "2",
    Three: "3",
    Four: "4",
    Five: "5",
    Six: "6",
    Seven: "7",
    Eight: "8",
    Nine: "9",
    Ten: "10",
    Jack: "Jack",
    Queen: "Queen",
    King: "King",
    Ace: "Ace",
  };

  return rankMap[enumKey] ?? null;
}

function resolvePackedValue(input: MotelyItemInput): number | null {
  if (typeof input === "number") return Number.isFinite(input) ? input : null;
  const runtimeItem = asRuntimeItem(input);
  return finiteNumber(runtimeItem?.value);
}

function resolveEditionValue(input: MotelyItemInput): number | null {
  const runtimeItem = asRuntimeItem(input);
  const direct = finiteNumber(runtimeItem?.edition);
  if (direct !== null) return direct;

  const packedValue = resolvePackedValue(input);
  return packedValue !== null ? packedValue & VALUE_EDITION_MASK : null;
}

function resolveSealValue(input: MotelyItemInput): number | null {
  const runtimeItem = asRuntimeItem(input);
  const direct = finiteNumber(runtimeItem?.seal);
  if (direct !== null) return direct;

  const packedValue = resolvePackedValue(input);
  return packedValue !== null ? packedValue & VALUE_SEAL_MASK : null;
}

function resolveEnhancementValue(input: MotelyItemInput): number | null {
  const runtimeItem = asRuntimeItem(input);
  const direct = finiteNumber(runtimeItem?.enhancement);
  if (direct !== null) return direct;

  const packedValue = resolvePackedValue(input);
  return packedValue !== null ? packedValue & VALUE_ENHANCEMENT_MASK : null;
}

export function resolveMotelyItemType(input: MotelyItemInput): number | null {
  if (typeof input === "number") return Number.isFinite(input) ? (input & VALUE_TYPE_MASK) : null;

  const runtimeItem = asRuntimeItem(input);
  const directType = finiteNumber(runtimeItem?.type);
  if (directType !== null) return directType & VALUE_TYPE_MASK;

  const packedValue = finiteNumber(runtimeItem?.value);
  return packedValue !== null ? packedValue & VALUE_TYPE_MASK : null;
}

export function motelyItemRenderCategory(input: MotelyItemInput): MotelyRenderableCategory {
  const enumKey = motelyItemTypeName(input);
  return enumKey === "Unknown" ? "unknown" : getItemCategory(enumKey);
}

export function motelyItemEditionName(input: MotelyItemInput): "Foil" | "Holographic" | "Polychrome" | "Negative" | null {
  const enumKey = runtimeEnumName(Motely.MotelyItemEdition as Record<string, unknown>, resolveEditionValue(input));
  return enumKey === null || enumKey === "None" ? null : (enumKey as "Foil" | "Holographic" | "Polychrome" | "Negative");
}

export function motelyItemSealName(input: MotelyItemInput): "Gold" | "Red" | "Blue" | "Purple" | null {
  const enumKey = runtimeEnumName(Motely.MotelyItemSeal as Record<string, unknown>, resolveSealValue(input));
  return enumKey === null || enumKey === "None" ? null : (enumKey as "Gold" | "Red" | "Blue" | "Purple");
}

export function motelyItemEnhancementName(input: MotelyItemInput): string | null {
  const enumKey = runtimeEnumName(Motely.MotelyItemEnhancement as Record<string, unknown>, resolveEnhancementValue(input));
  return enumKey === null || enumKey === "None" ? null : enumKey;
}

export function motelyStandardcardSuitName(input: MotelyItemInput): "Clubs" | "Diamonds" | "Hearts" | "Spades" | null {
  const runtimeItem = asRuntimeItem(input);
  const directSuit = runtimeEnumName(Motely.MotelyStandardcardSuit as Record<string, unknown>, finiteNumber(runtimeItem?.suit));
  if (directSuit === "Clubs" || directSuit === "Diamonds" || directSuit === "Hearts" || directSuit === "Spades") {
    return directSuit;
  }

  const parsed = parseStandardcardEnumKey(motelyItemTypeName(input));
  return parsed?.suit ?? null;
}

export function motelyStandardcardRankName(input: MotelyItemInput): string | null {
  const runtimeItem = asRuntimeItem(input);
  const directRank = runtimeEnumName(Motely.MotelyStandardcardRank as Record<string, unknown>, finiteNumber(runtimeItem?.rank));
  const normalizedDirect = rankNameFromEnum(directRank);
  if (normalizedDirect !== null) return normalizedDirect;

  const parsed = parseStandardcardEnumKey(motelyItemTypeName(input));
  return parsed?.rank ?? null;
}

/** Get the enum key name for a MotelyItemType value. */
export function motelyItemTypeName(input: MotelyItemInput): string {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return "Unknown";

  ensureItemTypeMap();
  return _itemTypeToName.get(itemType) ?? "Unknown";
}

/** Get the category string for a MotelyItemType value. */
export function motelyItemCategory(input: MotelyItemInput): string {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return "Unknown";

  const renderCategory = motelyItemRenderCategory(itemType);
  if (renderCategory === "playing") return "Standard card";
  if (renderCategory === "spectral") return "Spectral";
  if (renderCategory === "tarot") return "Tarot";
  if (renderCategory === "planet") return "Planet";
  if (renderCategory === "joker") return "Joker";

  return CATEGORY_TO_TYPE[itemType & CATEGORY_MASK] ?? "Unknown";
}

/** Convert PascalCase enum key to display name. */
export function motelyItemDisplayName(input: MotelyItemInput): string {
  const enumKey = motelyItemTypeName(input);
  return enumKey === "Unknown" ? "Unknown" : getItemDisplayName(enumKey);
}

// ─── Module-level cache ──────────────────────────────────────────────────────

const _cache = new Map<number, string | null>();
const _decodedBaseCache = new Map<number, DecodedMotelyItemBase | null>();

export function decodeMotelyItem(input: MotelyItemInput): DecodedMotelyItem | null {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return null;

  let base = _decodedBaseCache.get(itemType) ?? null;
  if (!_decodedBaseCache.has(itemType)) {
    const enumKey = motelyItemTypeName(itemType);
    if (enumKey === "Unknown") {
      _decodedBaseCache.set(itemType, null);
      return null;
    }

    const category = motelyItemRenderCategory(itemType);
    const rank = motelyStandardcardRankName(itemType);
    const suit = motelyStandardcardSuitName(itemType);
    base = {
      itemType,
      enumKey,
      displayName: category === "playing" && rank && suit ? `${rank} of ${suit}` : getItemDisplayName(enumKey),
      category,
      rank,
      suit,
    };
    _decodedBaseCache.set(itemType, base);
  }

  if (!base) return null;

  const decoded: DecodedMotelyItem = {
    ...base,
    edition: motelyItemEditionName(input),
    seal: motelyItemSealName(input),
    enhancement: motelyItemEnhancementName(input),
  };

  return decoded;
}

export function decodeMotelyItemToJamlCard(input: MotelyItemInput, scale = 1): MotelyJamlCard | null {
  const decoded = decodeMotelyItem(input);
  if (!decoded || decoded.category === "unknown") return null;

  if (decoded.category === "playing") {
    if (!decoded.rank || !decoded.suit) return null;

    return {
      type: "playing",
      card: {
        name: `${decoded.rank} of ${decoded.suit}`,
        edition: decoded.edition ?? undefined,
        seal: decoded.seal ? `${decoded.seal} Seal` : undefined,
        enhancements: decoded.enhancement ? [decoded.enhancement] : undefined,
        rank: decoded.rank,
        suit: decoded.suit,
        scale,
      },
    };
  }

  return {
    type: decoded.category === "joker" ? "joker" : "consumable",
    card: {
      name: decoded.displayName,
      edition: decoded.edition ?? undefined,
      scale,
    },
  };
}

/**
 * Decode a MotelyItemType integer to a display name for sprite lookup.
 * Cached per value.
 */
export function decodeMotelyItemName(input: MotelyItemInput): string | null {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return null;
  if (_cache.has(itemType)) return _cache.get(itemType) ?? null;

  const category = motelyItemCategory(itemType);
  if (category === "Unknown") {
    _cache.set(itemType, null);
    return null;
  }

  const decoded = decodeMotelyItem(input);
  const name = decoded?.displayName ?? motelyItemDisplayName(itemType);
  _cache.set(itemType, name);
  return name;
}

/** Warm the cache for a batch of item type values. */
export function warmMotelyItemCache(itemTypes: readonly MotelyItemInput[]): void {
  for (const t of itemTypes) {
    decodeMotelyItemName(t);
    decodeMotelyItem(t);
  }
}

/** Number of unique items decoded so far. */
export function motelyItemCacheSize(): number {
  return _cache.size;
}
