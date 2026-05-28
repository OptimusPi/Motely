import { MOTELY_ITEM_FORMATS_BY_VALUE } from "./motelyItemFormats.js";
import {
  MotelyItemEdition,
  MotelyItemSeal,
  MotelyItemEnhancement,
  MotelyStandardcardRank,
  MotelyStandardcardSuit,
} from "motely-wasm";

type MotelyItemCategoryName = "Standardcard" | "SpectralCard" | "TarotCard" | "PlanetCard" | "Joker" | "Invalid";

const CATEGORY_MAP: Record<MotelyItemCategoryName, MotelyRenderableCategory> = {
  Standardcard: "playing",
  SpectralCard: "spectral",
  TarotCard: "tarot",
  PlanetCard: "planet",
  Joker: "joker",
  Invalid: "unknown",
};

const EDITIONS: Record<MotelyItemEdition, string> = {
  [MotelyItemEdition.None]: "Base",
  [MotelyItemEdition.Foil]: "Foil",
  [MotelyItemEdition.Holographic]: "Holographic",
  [MotelyItemEdition.Polychrome]: "Polychrome",
  [MotelyItemEdition.Negative]: "Negative",
};

const SEALS: Record<MotelyItemSeal, string> = {
  [MotelyItemSeal.None]: "None",
  [MotelyItemSeal.Gold]: "Gold",
  [MotelyItemSeal.Red]: "Red",
  [MotelyItemSeal.Blue]: "Blue",
  [MotelyItemSeal.Purple]: "Purple",
};

const ENHANCEMENTS: Record<MotelyItemEnhancement, string> = {
  [MotelyItemEnhancement.None]: "None",
  [MotelyItemEnhancement.Bonus]: "Bonus",
  [MotelyItemEnhancement.Mult]: "Mult",
  [MotelyItemEnhancement.Wild]: "Wild",
  [MotelyItemEnhancement.Glass]: "Glass",
  [MotelyItemEnhancement.Steel]: "Steel",
  [MotelyItemEnhancement.Stone]: "Stone",
  [MotelyItemEnhancement.Gold]: "Gold",
  [MotelyItemEnhancement.Lucky]: "Lucky",
};

const RANKS: Record<MotelyStandardcardRank, string> = {
  [MotelyStandardcardRank.Two]: "2",
  [MotelyStandardcardRank.Three]: "3",
  [MotelyStandardcardRank.Four]: "4",
  [MotelyStandardcardRank.Five]: "5",
  [MotelyStandardcardRank.Six]: "6",
  [MotelyStandardcardRank.Seven]: "7",
  [MotelyStandardcardRank.Eight]: "8",
  [MotelyStandardcardRank.Nine]: "9",
  [MotelyStandardcardRank.Ten]: "10",
  [MotelyStandardcardRank.Jack]: "Jack",
  [MotelyStandardcardRank.Queen]: "Queen",
  [MotelyStandardcardRank.King]: "King",
  [MotelyStandardcardRank.Ace]: "Ace",
};

const SUITS: Record<MotelyStandardcardSuit, "Clubs" | "Diamonds" | "Hearts" | "Spades"> = {
  [MotelyStandardcardSuit.Clubs]: "Clubs",
  [MotelyStandardcardSuit.Diamonds]: "Diamonds",
  [MotelyStandardcardSuit.Hearts]: "Hearts",
  [MotelyStandardcardSuit.Spades]: "Spades",
};


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
  isEternal: boolean;
  isPerishable: boolean;
  isRental: boolean;
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
    isEternal?: boolean;
    isPerishable?: boolean;
    isRental?: boolean;
    scale?: number;
  };
}

function itemFormat(itemType: number) {
  return MOTELY_ITEM_FORMATS_BY_VALUE[itemType as keyof typeof MOTELY_ITEM_FORMATS_BY_VALUE];
}

function spaceSplit(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
}

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
  return itemFormat(itemType)?.enumName ?? `item#${itemType}`;
}

export function motelyItemCategory(itemType: number): MotelyRenderableCategory {
  const category = itemFormat(itemType)?.category as MotelyItemCategoryName | undefined;
  return category ? CATEGORY_MAP[category] ?? "unknown" : "unknown";
}

export function motelyItemRenderCategory(input: MotelyItemInput): MotelyRenderableCategory {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return "unknown";
  return motelyItemCategory(itemType);
}

export function motelyItemDisplayName(input: MotelyItemInput): string {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return "Unknown";
  return itemFormat(itemType)?.displayName ?? spaceSplit(motelyItemTypeName(input));
}

import { Motely } from "motely-wasm";

function isStickerSet(input: MotelyItemInput, bitOffset: number): boolean {
  const val = resolvePackedValue(input);
  if (val === null) return false;
  return (val & (1 << bitOffset)) !== 0;
}

export function motelyItemEditionName(input: MotelyItemInput): "Foil" | "Holographic" | "Polychrome" | "Negative" | null {
  if (input == null) return null;
  const val = typeof input === "number" ? Motely.decodeItemEdition(input) : input.edition;
  if (val == null || val === MotelyItemEdition.None) return null;
  return EDITIONS[val] as "Foil" | "Holographic" | "Polychrome" | "Negative";
}

export function motelyItemSealName(input: MotelyItemInput): "Gold" | "Red" | "Blue" | "Purple" | null {
  if (input == null) return null;
  const val = typeof input === "number" ? Motely.decodeItemSeal(input) : input.seal;
  if (val == null || val === MotelyItemSeal.None) return null;
  return SEALS[val] as "Gold" | "Red" | "Blue" | "Purple";
}

export function motelyItemEnhancementName(input: MotelyItemInput): string | null {
  if (input == null) return null;
  const val = typeof input === "number" ? Motely.decodeItemEnhancement(input) : input.enhancement;
  if (val == null || val === MotelyItemEnhancement.None) return null;
  return ENHANCEMENTS[val] ?? null;
}


export function motelyStandardcardRankName(input: MotelyItemInput): string | null {
  if (input == null) return null;
  if (motelyItemRenderCategory(input) !== "playing") return null;
  const val = typeof input === "number" ? Motely.decodeStandardcardRank(input) : input.rank;
  if (val == null) return null;
  return RANKS[val] ?? null;
}

export function motelyStandardcardSuitName(input: MotelyItemInput): "Clubs" | "Diamonds" | "Hearts" | "Spades" | null {
  if (input == null) return null;
  if (motelyItemRenderCategory(input) !== "playing") return null;
  const val = typeof input === "number" ? Motely.decodeStandardcardSuit(input) : input.suit;
  if (val == null) return null;
  return SUITS[val] ?? null;
}

export function decodeMotelyItemName(input: MotelyItemInput): string {
  return motelyItemTypeName(input);
}

export function decodeMotelyItem(input: MotelyItemInput): DecodedMotelyItem | null {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return null;

  const format = itemFormat(itemType);
  const enumKeyStr = format?.enumName ?? `Unknown_${itemType}`;
  const category = motelyItemCategory(itemType);
  const displayName = format?.displayName ?? spaceSplit(enumKeyStr);

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
    isEternal: typeof input === "number" ? Motely.isEternal(input) : isStickerSet(input, 30),
    isPerishable: typeof input === "number" ? Motely.isPerishable(input) : isStickerSet(input, 31),
    isRental: typeof input === "number" ? Motely.isRental(input) : isStickerSet(input, 29),
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
      isEternal: decoded.isEternal,
      isPerishable: decoded.isPerishable,
      isRental: decoded.isRental,
      scale,
    },
  };
}

export function warmMotelyItemCache(): void { /* no-op */ }
export function motelyItemCacheSize(): number { return 0; }
