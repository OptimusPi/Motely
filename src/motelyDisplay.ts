import { Motely } from "motely-wasm";
import { getItemDisplayName } from "./utils/itemUtils.js";

type MotelyLabelEntry<K extends string = string> = Readonly<{
  key: K;
  label: string;
}>;

type EntryKey<T extends readonly MotelyLabelEntry[]> = T[number]["key"];

interface MotelyLabelLookup<K extends string> {
  readonly keyToLabel: ReadonlyMap<K, string>;
  readonly labelToKey: ReadonlyMap<string, K>;
}

const BOSS_ENTRIES = [
  { key: "AmberAcorn", label: "Amber Acorn" },
  { key: "CeruleanBell", label: "Cerulean Bell" },
  { key: "CrimsonHeart", label: "Crimson Heart" },
  { key: "VerdantLeaf", label: "Verdant Leaf" },
  { key: "VioletVessel", label: "Violet Vessel" },
  { key: "TheArm", label: "The Arm" },
  { key: "TheClub", label: "The Club" },
  { key: "TheEye", label: "The Eye" },
  { key: "TheFish", label: "The Fish" },
  { key: "TheFlint", label: "The Flint" },
  { key: "TheGoad", label: "The Goad" },
  { key: "TheHead", label: "The Head" },
  { key: "TheHook", label: "The Hook" },
  { key: "TheHouse", label: "The House" },
  { key: "TheManacle", label: "The Manacle" },
  { key: "TheMark", label: "The Mark" },
  { key: "TheMouth", label: "The Mouth" },
  { key: "TheNeedle", label: "The Needle" },
  { key: "TheOx", label: "The Ox" },
  { key: "ThePillar", label: "The Pillar" },
  { key: "ThePlant", label: "The Plant" },
  { key: "ThePsychic", label: "The Psychic" },
  { key: "TheSerpent", label: "The Serpent" },
  { key: "TheTooth", label: "The Tooth" },
  { key: "TheWall", label: "The Wall" },
  { key: "TheWater", label: "The Water" },
  { key: "TheWheel", label: "The Wheel" },
  { key: "TheWindow", label: "The Window" },
] as const satisfies readonly MotelyLabelEntry[];

const VOUCHER_ENTRIES = [
  { key: "Overstock", label: "Overstock" },
  { key: "OverstockPlus", label: "Overstock Plus" },
  { key: "ClearanceSale", label: "Clearance Sale" },
  { key: "Liquidation", label: "Liquidation" },
  { key: "Hone", label: "Hone" },
  { key: "GlowUp", label: "Glow Up" },
  { key: "RerollSurplus", label: "Reroll Surplus" },
  { key: "RerollGlut", label: "Reroll Glut" },
  { key: "CrystalBall", label: "Crystal Ball" },
  { key: "OmenGlobe", label: "Omen Globe" },
  { key: "Telescope", label: "Telescope" },
  { key: "Observatory", label: "Observatory" },
  { key: "Grabber", label: "Grabber" },
  { key: "NachoTong", label: "Nacho Tong" },
  { key: "Wasteful", label: "Wasteful" },
  { key: "Recyclomancy", label: "Recyclomancy" },
  { key: "TarotMerchant", label: "Tarot Merchant" },
  { key: "TarotTycoon", label: "Tarot Tycoon" },
  { key: "PlanetMerchant", label: "Planet Merchant" },
  { key: "PlanetTycoon", label: "Planet Tycoon" },
  { key: "SeedMoney", label: "Seed Money" },
  { key: "MoneyTree", label: "Money Tree" },
  { key: "Blank", label: "Blank" },
  { key: "Antimatter", label: "Antimatter" },
  { key: "MagicTrick", label: "Magic Trick" },
  { key: "Illusion", label: "Illusion" },
  { key: "Hieroglyph", label: "Hieroglyph" },
  { key: "Petroglyph", label: "Petroglyph" },
  { key: "DirectorsCut", label: "Director's Cut" },
  { key: "Retcon", label: "Retcon" },
  { key: "PaintBrush", label: "Paint Brush" },
  { key: "Palette", label: "Palette" },
] as const satisfies readonly MotelyLabelEntry[];

const TAG_ENTRIES = [
  { key: "UncommonTag", label: "Uncommon Tag" },
  { key: "RareTag", label: "Rare Tag" },
  { key: "NegativeTag", label: "Negative Tag" },
  { key: "FoilTag", label: "Foil Tag" },
  { key: "HolographicTag", label: "Holographic Tag" },
  { key: "PolychromeTag", label: "Polychrome Tag" },
  { key: "InvestmentTag", label: "Investment Tag" },
  { key: "VoucherTag", label: "Voucher Tag" },
  { key: "BossTag", label: "Boss Tag" },
  { key: "StandardTag", label: "Standard Tag" },
  { key: "CharmTag", label: "Charm Tag" },
  { key: "MeteorTag", label: "Meteor Tag" },
  { key: "BuffoonTag", label: "Buffoon Tag" },
  { key: "HandyTag", label: "Handy Tag" },
  { key: "GarbageTag", label: "Garbage Tag" },
  { key: "EtherealTag", label: "Ethereal Tag" },
  { key: "CouponTag", label: "Coupon Tag" },
  { key: "DoubleTag", label: "Double Tag" },
  { key: "JuggleTag", label: "Juggle Tag" },
  { key: "D6Tag", label: "D6 Tag" },
  { key: "TopupTag", label: "Top-up Tag" },
  { key: "SpeedTag", label: "Speed Tag" },
  { key: "OrbitalTag", label: "Orbital Tag" },
  { key: "EconomyTag", label: "Economy Tag" },
] as const satisfies readonly MotelyLabelEntry[];

const BOOSTER_PACK_ENTRIES = [
  { key: "Arcana", label: "Arcana Pack" },
  { key: "JumboArcana", label: "Jumbo Arcana Pack" },
  { key: "MegaArcana", label: "Mega Arcana Pack" },
  { key: "Celestial", label: "Celestial Pack" },
  { key: "JumboCelestial", label: "Jumbo Celestial Pack" },
  { key: "MegaCelestial", label: "Mega Celestial Pack" },
  { key: "Standard", label: "Standard Pack" },
  { key: "JumboStandard", label: "Jumbo Standard Pack" },
  { key: "MegaStandard", label: "Mega Standard Pack" },
  { key: "Buffoon", label: "Buffoon Pack" },
  { key: "JumboBuffoon", label: "Jumbo Buffoon Pack" },
  { key: "MegaBuffoon", label: "Mega Buffoon Pack" },
  { key: "Spectral", label: "Spectral Pack" },
  { key: "JumboSpectral", label: "Jumbo Spectral Pack" },
  { key: "MegaSpectral", label: "Mega Spectral Pack" },
] as const satisfies readonly MotelyLabelEntry[];

const BOSS_VALUE_MASK = 0xff;
const ITEM_VALUE_MASK = 0xffff;

export const MOTELY_DISPLAY_SCHEMA = {
  bosses: BOSS_ENTRIES,
  vouchers: VOUCHER_ENTRIES,
  tags: TAG_ENTRIES,
  boosterPacks: BOOSTER_PACK_ENTRIES,
} as const;

export type MotelyDisplaySchema = typeof MOTELY_DISPLAY_SCHEMA;
export type MotelyBossKey = EntryKey<typeof BOSS_ENTRIES>;
export type MotelyVoucherKey = EntryKey<typeof VOUCHER_ENTRIES>;
export type MotelyTagKey = EntryKey<typeof TAG_ENTRIES>;
export type MotelyBoosterPackKey = EntryKey<typeof BOOSTER_PACK_ENTRIES>;

function createLabelLookup<T extends readonly MotelyLabelEntry[]>(entries: T): MotelyLabelLookup<EntryKey<T>> {
  return {
    keyToLabel: new Map(entries.map((entry) => [entry.key, entry.label])) as ReadonlyMap<EntryKey<T>, string>,
    labelToKey: new Map(entries.map((entry) => [entry.label, entry.key])) as ReadonlyMap<string, EntryKey<T>>,
  };
}

const bossLookup = createLabelLookup(BOSS_ENTRIES);
const voucherLookup = createLabelLookup(VOUCHER_ENTRIES);
const tagLookup = createLabelLookup(TAG_ENTRIES);
const boosterPackLookup = createLabelLookup(BOOSTER_PACK_ENTRIES);

function spaceSplit(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
}

function displayNameFromKey<K extends string>(lookup: MotelyLabelLookup<K>, key: string, fallback: string): string {
  return lookup.keyToLabel.get(key as K) ?? fallback;
}

function keyFromDisplayName<K extends string>(lookup: MotelyLabelLookup<K>, label: string): K | null {
  return lookup.labelToKey.get(label) ?? null;
}

function runtimeEnumKey(
  enumObject: Record<string, unknown> | null | undefined,
  value: number,
): string | null {
  if (!enumObject || typeof enumObject !== "object") return null;
  const key = enumObject[String(value)];
  return typeof key === "string" && key.length > 0 ? key : null;
}

export function motelyBossDisplayNameFromKey(key: string): string {
  return displayNameFromKey(bossLookup, key, spaceSplit(key));
}

export function motelyVoucherDisplayNameFromKey(key: string): string {
  return displayNameFromKey(voucherLookup, key, spaceSplit(key));
}

export function motelyTagDisplayNameFromKey(key: string): string {
  return displayNameFromKey(tagLookup, key, spaceSplit(key));
}

export function motelyBoosterPackDisplayNameFromKey(key: string): string {
  return displayNameFromKey(boosterPackLookup, key, `${spaceSplit(key)} Pack`);
}

export function motelyItemDisplayNameFromKey(key: string): string {
  return getItemDisplayName(key);
}

export function motelyBossDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyBossBlind as Record<string, unknown>, value & BOSS_VALUE_MASK);
  return key === null ? `boss#${value}` : motelyBossDisplayNameFromKey(key);
}

export function motelyVoucherDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyVoucher as Record<string, unknown>, value);
  return key === null ? `voucher#${value}` : motelyVoucherDisplayNameFromKey(key);
}

export function motelyTagDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyTag as Record<string, unknown>, value);
  return key === null ? `tag#${value}` : motelyTagDisplayNameFromKey(key);
}

export function motelyBoosterPackDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyBoosterPack as Record<string, unknown>, value);
  return key === null ? `pack#${value}` : motelyBoosterPackDisplayNameFromKey(key);
}

export function motelyItemDisplayNameFromValue(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyItemType as Record<string, unknown>, value & ITEM_VALUE_MASK);
  return key === null ? `item#${value}` : motelyItemDisplayNameFromKey(key);
}

export function motelyBossKeyFromDisplayName(label: string): MotelyBossKey | null {
  return keyFromDisplayName(bossLookup, label);
}

export function motelyVoucherKeyFromDisplayName(label: string): MotelyVoucherKey | null {
  return keyFromDisplayName(voucherLookup, label);
}

export function motelyTagKeyFromDisplayName(label: string): MotelyTagKey | null {
  return keyFromDisplayName(tagLookup, label);
}

export function motelyBoosterPackKeyFromDisplayName(label: string): MotelyBoosterPackKey | null {
  return keyFromDisplayName(boosterPackLookup, label);
}
