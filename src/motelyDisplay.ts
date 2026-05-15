import { Motely } from "./motelyBoot.js";
import { MOTELY_ITEM_FORMATS_BY_VALUE } from "./decode/motelyItemFormats.js";

type RuntimeEnum = Record<string, string | number>;
type MotelyRuntimeEnums = typeof Motely & Record<string, RuntimeEnum>;

const MotelyEnums = Motely as MotelyRuntimeEnums;

function runtimeEnumKey(
  enumObject: Record<string, unknown>,
  value: number,
): string | null {
  const key = enumObject[String(value)];
  return typeof key === "string" && key.length > 0 ? key : null;
}

export function motelyBossDisplayName(value: number): string {
  const key = runtimeEnumKey(MotelyEnums.MotelyBossBlind, value & 0xff);
  return key ?? `boss#${value}`;
}

export function motelyBossDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyVoucherDisplayName(value: number): string {
  const key = runtimeEnumKey(MotelyEnums.MotelyVoucher, value);
  return key ?? `voucher#${value}`;
}

export function motelyVoucherDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyTagDisplayName(value: number): string {
  const key = runtimeEnumKey(MotelyEnums.MotelyTag, value);
  return key ?? `tag#${value}`;
}

export function motelyTagDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyBoosterPackDisplayName(value: number): string {
  const key = runtimeEnumKey(MotelyEnums.MotelyBoosterPack, value);
  return key ?? `pack#${value}`;
}

export function motelyBoosterPackDisplayNameFromKey(key: string): string {
  return `${key} Pack`;
}

export function motelyItemDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyItemDisplayNameFromValue(value: number): string {
  const itemType = value & 0xffff;
  return MOTELY_ITEM_FORMATS_BY_VALUE[itemType as keyof typeof MOTELY_ITEM_FORMATS_BY_VALUE]?.displayName ?? `item#${value}`;
}
