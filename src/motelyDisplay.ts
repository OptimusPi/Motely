import { Motely } from "./motelyBoot.js";

function runtimeEnumKey(
  enumObject: Record<string, unknown>,
  value: number,
): string | null {
  const key = enumObject[String(value)];
  return typeof key === "string" && key.length > 0 ? key : null;
}

export function motelyBossDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyBossBlind as Record<string, unknown>, value & 0xff);
  return key ?? `boss#${value}`;
}

export function motelyBossDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyVoucherDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyVoucher as Record<string, unknown>, value);
  return key ?? `voucher#${value}`;
}

export function motelyVoucherDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyTagDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyTag as Record<string, unknown>, value);
  return key ?? `tag#${value}`;
}

export function motelyTagDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyBoosterPackDisplayName(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyBoosterPack as Record<string, unknown>, value);
  return key ?? `pack#${value}`;
}

export function motelyBoosterPackDisplayNameFromKey(key: string): string {
  return `${key} Pack`;
}

export function motelyItemDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyItemDisplayNameFromValue(value: number): string {
  const key = runtimeEnumKey(Motely.MotelyItemType as Record<string, unknown>, value & 0xffff);
  return key ?? `item#${value}`;
}
