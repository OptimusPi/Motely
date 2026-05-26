import { MOTELY_ITEM_FORMATS_BY_VALUE } from "./motelyItemFormats.js";

type MotelyItemCategoryName = "Standardcard" | "SpectralCard" | "TarotCard" | "PlanetCard" | "Joker" | "Invalid";

const CATEGORY_MAP: Record<MotelyItemCategoryName, MotelyRenderableCategory> = {
  Standardcard: "playing",
  SpectralCard: "spectral",
  TarotCard: "tarot",
  PlanetCard: "planet",
  Joker: "joker",
  Invalid: "unknown",
};

const RANKS = ["2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace"] as const;
const SUITS = ["Clubs", "Diamonds", "Hearts", "Spades"] as const;
const EDITIONS = ["Base", "Foil", "Holographic", "Polychrome", "Negative"] as const;
const SEALS = ["None", "Gold", "Red", "Blue", "Purple"] as const;

type RankName = (typeof RANKS)[number];
type SuitName = (typeof SUITS)[number];

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

export function motelyItemEditionName(input: MotelyItemInput): "Foil" | "Holographic" | "Polychrome" | "Negative" | null {
  if (input == null) return null;
  const val = typeof input === "number" ? input : input.edition;
  if (val == null) return null;
  const key = EDITIONS[val as keyof typeof EDITIONS];
  if (!key || key === "Base") return null;
  return key as "Foil" | "Holographic" | "Polychrome" | "Negative";
}

export function motelyItemSealName(input: MotelyItemInput): "Gold" | "Red" | "Blue" | "Purple" | null {
  if (input == null) return null;
  const val = typeof input === "number" ? null : input.seal;
  if (val == null) return null;
  const key = SEALS[val as keyof typeof SEALS];
  if (!key || key === "None") return null;
  return key as "Gold" | "Red" | "Blue" | "Purple";
}

export function motelyItemEnhancementName(input: MotelyItemInput): string | null {
  if (input == null) return null;
  const val = typeof input === "number" ? null : input.enhancement;
  if (val == null) return null;
  // Enhancements are not part of the current motely-wasm JS contract.
  return null;
}

export function motelyStandardcardRankName(input: MotelyItemInput): string | null {
  if (input == null) return null;
  if (motelyItemRenderCategory(input) !== "playing") return null;
  const val = typeof input === "number" ? resolveMotelyItemType(input) : input.rank;
  if (val == null) return null;
  if (typeof input === "number") return RANKS[val & 0xf] ?? null;
  return RANKS[val] as RankName | undefined ?? null;
}

export function motelyStandardcardSuitName(input: MotelyItemInput): "Clubs" | "Diamonds" | "Hearts" | "Spades" | null {
  if (input == null) return null;
  if (motelyItemRenderCategory(input) !== "playing") return null;
  const val = typeof input === "number" ? resolveMotelyItemType(input) : input.suit;
  if (val == null) return null;
  if (typeof input === "number") return SUITS[(val >> 4) & 0xf] ?? null;
  return SUITS[val] as SuitName | undefined ?? null;
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
