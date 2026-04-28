import { Motely } from "motely-wasm";

/**
 * Display-name utilities — thin wrappers over motely-wasm runtime enums.
 * No hand-maintained lookup tables. The enum IS the source of truth.
 */

function spaceSplit(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
}

function runtimeEnumKey(
  enumObject: Record<string, unknown> | null | undefined,
  value: number,
): string | null {
  if (!enumObject || typeof enumObject !== "object") return null;
  const key = enumObject[String(value)];
  return typeof key === "string" && key.length > 0 ? key : null;
}

// ─── Public API (same signatures as before, zero hand-rolled tables) ────────

export function motelyBossDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyBossBlind as Record<string, unknown>, value & 0xff);
  return key === null ? `boss#${value}` : spaceSplit(key);
}

export function motelyBossDisplayNameFromKey(key: string): string {
  return spaceSplit(key);
}

export function motelyVoucherDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyVoucher as Record<string, unknown>, value);
  return key === null ? `voucher#${value}` : spaceSplit(key);
}

export function motelyVoucherDisplayNameFromKey(key: string): string {
  return spaceSplit(key);
}

export function motelyTagDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyTag as Record<string, unknown>, value);
  return key === null ? `tag#${value}` : spaceSplit(key);
}

export function motelyTagDisplayNameFromKey(key: string): string {
  return spaceSplit(key);
}

export function motelyBoosterPackDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyBoosterPack as Record<string, unknown>, value);
  return key === null ? `pack#${value}` : spaceSplit(key);
}

export function motelyBoosterPackDisplayNameFromKey(key: string): string {
  return `${spaceSplit(key)} Pack`;
}

export function motelyItemDisplayNameFromKey(key: string): string {
  return spaceSplit(key);
}

export function motelyItemDisplayNameFromValue(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyItemType as Record<string, unknown>, value & 0xffff);
  return key === null ? `item#${value}` : spaceSplit(key);
}
