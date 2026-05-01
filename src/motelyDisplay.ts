import { Motely } from "motely-wasm";



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
  return key === null ? `boss#${value}` : key;
}

export function motelyBossDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyVoucherDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyVoucher as Record<string, unknown>, value);
  return key === null ? `voucher#${value}` : key;
}

export function motelyVoucherDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyTagDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyTag as Record<string, unknown>, value);
  return key === null ? `tag#${value}` : key;
}

export function motelyTagDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyBoosterPackDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyBoosterPack as Record<string, unknown>, value);
  return key === null ? `pack#${value}` : key;
}

export function motelyBoosterPackDisplayNameFromKey(key: string): string {
  return `${key} Pack`;
}

export function motelyItemDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyItemDisplayNameFromValue(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyItemType as Record<string, unknown>, value & 0xffff);
  return key === null ? `item#${value}` : key;
}
