import { Motely } from "../motelyBoot.js";

// motely-wasm exports MotelyItemTypeCategory at runtime but its .d.ts hasn't
// caught up — cast unblocks the type check until motely-wasm regenerates types.
const M = Motely as unknown as Record<string, Record<string, number>>;

const CATEGORY_MAP: Record<number, MotelyRenderableCategory> = {
  [M.MotelyItemTypeCategory.Standardcard]: "playing",
  [M.MotelyItemTypeCategory.SpectralCard]: "spectral",
  [M.MotelyItemTypeCategory.TarotCard]: "tarot",
  [M.MotelyItemTypeCategory.PlanetCard]: "planet",
  [M.MotelyItemTypeCategory.Joker]: "joker",
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

function enumKey<T extends Record<string, unknown>>(e: T, value: number): string | null {
  const k = e[String(value)];
  return typeof k === "string" ? k : null;
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
  return enumKey(Motely.MotelyItemType as Record<string, unknown>, itemType) ?? `item#${itemType}`;
}

export function motelyItemCategory(itemType: number): MotelyRenderableCategory {
  const catValue = (itemType >> 12) & 0xf;
  return CATEGORY_MAP[catValue] ?? "unknown";
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
  if (input == null) return null;
  const val = typeof input === "number" ? input : input.edition;
  if (val == null) return null;
  const key = enumKey(Motely.MotelyItemEdition as Record<string, unknown>, val);
  if (!key || key === "None") return null;
  return key as "Foil" | "Holographic" | "Polychrome" | "Negative";
}

export function motelyItemSealName(input: MotelyItemInput): "Gold" | "Red" | "Blue" | "Purple" | null {
  if (input == null) return null;
  const val = typeof input === "number" ? null : input.seal;
  if (val == null) return null;
  const key = enumKey(Motely.MotelyItemSeal as Record<string, unknown>, val);
  if (!key || key === "None") return null;
  return key as "Gold" | "Red" | "Blue" | "Purple";
}

export function motelyItemEnhancementName(input: MotelyItemInput): string | null {
  if (input == null) return null;
  const val = typeof input === "number" ? null : input.enhancement;
  if (val == null) return null;
  const key = enumKey(Motely.MotelyItemEnhancement as Record<string, unknown>, val);
  if (!key || key === "None") return null;
  return key;
}

export function motelyStandardcardRankName(input: MotelyItemInput): string | null {
  if (input == null) return null;
  const val = typeof input === "number" ? null : input.rank;
  if (val == null) return null;
  return enumKey(Motely.MotelyStandardcardRank as Record<string, unknown>, val);
}

export function motelyStandardcardSuitName(input: MotelyItemInput): "Clubs" | "Diamonds" | "Hearts" | "Spades" | null {
  if (input == null) return null;
  const val = typeof input === "number" ? null : input.suit;
  if (val == null) return null;
  return enumKey(Motely.MotelyStandardcardSuit as Record<string, unknown>, val) as "Clubs" | "Diamonds" | "Hearts" | "Spades" | null;
}

export function decodeMotelyItemName(input: MotelyItemInput): string {
  return motelyItemTypeName(input);
}

export function decodeMotelyItem(input: MotelyItemInput): DecodedMotelyItem | null {
  const itemType = resolveMotelyItemType(input);
  if (itemType === null) return null;

  const enumKeyStr = enumKey(Motely.MotelyItemType as Record<string, unknown>, itemType) ?? `Unknown_${itemType}`;
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
