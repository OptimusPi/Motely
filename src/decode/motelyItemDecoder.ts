/**
 * Motely item decoder — thin wrapper over motely-wasm runtime enums.
 * No hand-rolled bitmask tables. The WASM enum IS the source of truth.
 *
 * Consumers must call `setMotelyEnums(Motely)` once after `await bootsharp.boot()`
 * for runtime enum lookups to resolve. Before that, decoders return placeholder
 * strings rather than throwing.
 */

import type { Motely as MotelyEnumsType } from "motely-wasm";

let _motely: typeof MotelyEnumsType | null = null;
let _categoryMap: Record<number, MotelyRenderableCategory> | null = null;

export function setMotelyEnums(motely: typeof MotelyEnumsType): void {
  _motely = motely;
  _categoryMap = null;
}

function getCategoryMap(): Record<number, MotelyRenderableCategory> {
  if (_categoryMap) return _categoryMap;
  if (!_motely) return {};
  _categoryMap = {
    [_motely.MotelyItemTypeCategory.Standardcard]: "playing",
    [_motely.MotelyItemTypeCategory.SpectralCard]: "spectral",
    [_motely.MotelyItemTypeCategory.TarotCard]: "tarot",
    [_motely.MotelyItemTypeCategory.PlanetCard]: "planet",
    [_motely.MotelyItemTypeCategory.Joker]: "joker",
  };
  return _categoryMap;
}

// ─── Re-export types that consumers depend on ────────────────────────────────

export type CardCategory = "joker" | "consumable" | "playing" | "spectral" | "tarot" | "planet";
export type MotelyRenderableCategory = CardCategory | "unknown";

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

// ─── Helpers using motely-wasm runtime enums directly ────────────────────────

function enumKey<T extends Record<string, unknown>>(e: T, value: number): string | null {
  const k = e[String(value)];
  return typeof k === "string" ? k : null;
}

function spaceSplit(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
}

// ─── Core decoder ────────────────────────────────────────────────────────────

function resolvePackedValue(input: MotelyItemInput): number | null {
  if (input == null) return null;
  if (typeof input === "number") return Number.isFinite(input) ? input : null;
  return input.value ?? input.type ?? null;
}

export function resolveMotelyItemType(input: MotelyItemInput): number | null {
  const val = resolvePackedValue(input);
  return val !== null ? val & 0xffff : null;
}

export function motelyItemTypeName(input: MotelyItemInput): string {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return "Unknown";
  if (!_motely) return `item#${itemType}`;
  return enumKey(_motely.MotelyItemType as Record<string, unknown>, itemType) ?? `item#${itemType}`;
}

export function motelyItemCategory(itemType: number): MotelyRenderableCategory {
  const catValue = (itemType >> 12) & 0xf;
  return getCategoryMap()[catValue] ?? "unknown";
}

export function motelyItemRenderCategory(input: MotelyItemInput): MotelyRenderableCategory {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return "unknown";
  return motelyItemCategory(itemType);
}

export function motelyItemDisplayName(input: MotelyItemInput): string {
  return spaceSplit(motelyItemTypeName(input));
}

export function motelyItemEditionName(input: MotelyItemInput): "Foil" | "Holographic" | "Polychrome" | "Negative" | null {
  if (input == null || !_motely) return null;
  const val = typeof input === "number" ? input : (input as MotelyRuntimeItem).edition;
  if (val == null) return null;
  const key = enumKey(_motely.MotelyItemEdition as Record<string, unknown>, typeof val === "number" ? val : 0);
  if (!key || key === "None") return null;
  return key as "Foil" | "Holographic" | "Polychrome" | "Negative";
}

export function motelyItemSealName(input: MotelyItemInput): "Gold" | "Red" | "Blue" | "Purple" | null {
  if (input == null || !_motely) return null;
  const val = typeof input === "number" ? null : (input as MotelyRuntimeItem).seal;
  if (val == null) return null;
  const key = enumKey(_motely.MotelyItemSeal as Record<string, unknown>, val);
  if (!key || key === "None") return null;
  return key as "Gold" | "Red" | "Blue" | "Purple";
}

export function motelyItemEnhancementName(input: MotelyItemInput): string | null {
  if (input == null || !_motely) return null;
  const val = typeof input === "number" ? null : (input as MotelyRuntimeItem).enhancement;
  if (val == null) return null;
  const key = enumKey(_motely.MotelyItemEnhancement as Record<string, unknown>, val);
  if (!key || key === "None") return null;
  return key;
}

export function motelyStandardcardRankName(input: MotelyItemInput): string | null {
  if (input == null || !_motely) return null;
  const val = typeof input === "number" ? null : (input as MotelyRuntimeItem).rank;
  if (val == null) return null;
  return enumKey(_motely.MotelyStandardcardRank as Record<string, unknown>, val);
}

export function motelyStandardcardSuitName(input: MotelyItemInput): "Clubs" | "Diamonds" | "Hearts" | "Spades" | null {
  if (input == null || !_motely) return null;
  const val = typeof input === "number" ? null : (input as MotelyRuntimeItem).suit;
  if (val == null) return null;
  return enumKey(_motely.MotelyStandardcardSuit as Record<string, unknown>, val) as "Clubs" | "Diamonds" | "Hearts" | "Spades" | null;
}

export function decodeMotelyItemName(input: MotelyItemInput): string {
  return motelyItemTypeName(input);
}

export function decodeMotelyItem(input: MotelyItemInput): DecodedMotelyItem | null {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return null;

  const enumKeyStr = _motely
    ? enumKey(_motely.MotelyItemType as Record<string, unknown>, itemType) ?? `Unknown_${itemType}`
    : `Unknown_${itemType}`;
  const category = motelyItemCategory(itemType);
  const displayName = spaceSplit(enumKeyStr);

  return {
    itemType,
    enumKey: enumKeyStr,
    displayName,
    category,
    edition: motelyItemEditionName(input),
    seal: motelyItemSealName(input),
    enhancement: motelyItemEnhancementName(input),
    rank: motelyStandardcardRankName(input),
    suit: motelyStandardcardSuitName(input),
  };
}

export function decodeMotelyItemToJamlCard(input: MotelyItemInput, scale?: number): MotelyJamlCard | null {
  const decoded = decodeMotelyItem(input);
  if (!decoded) return null;

  const type: "joker" | "consumable" | "playing" =
    decoded.category === "joker" ? "joker"
    : decoded.category === "playing" ? "playing"
    : "consumable";

  return {
    type,
    card: {
      name: decoded.displayName,
      edition: decoded.edition ?? undefined,
      seal: decoded.seal ?? undefined,
      enhancements: decoded.enhancement ? [decoded.enhancement] : undefined,
      rank: decoded.rank ?? undefined,
      suit: decoded.suit ?? undefined,
      scale,
    },
  };
}

export function warmMotelyItemCache(): void { /* no-op */ }
export function motelyItemCacheSize(): number { return 0; }
