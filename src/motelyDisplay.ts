import type { Motely as MotelyEnumsType } from "motely-wasm";

// Setter pattern: consumers boot motely-wasm and call `setMotelyEnums(Motely)`
// once after boot. Display functions degrade gracefully (return placeholder
// strings) before the setter is called rather than throwing.
let _motely: typeof MotelyEnumsType | null = null;

export function setMotelyEnums(motely: typeof MotelyEnumsType): void {
  _motely = motely;
}

function runtimeEnumKey(
  enumObject: Record<string, unknown> | null | undefined,
  value: number,
): string | null {
  if (!enumObject || typeof enumObject !== "object") return null;
  const key = enumObject[String(value)];
  return typeof key === "string" && key.length > 0 ? key : null;
}

// ─── Public API ──────────────────────────────────────────────────────────────

export function motelyBossDisplayName(value: number): string {
  if (!_motely) return `boss#${value}`;
  const key = runtimeEnumKey(_motely.MotelyBossBlind as Record<string, unknown>, value & 0xff);
  return key === null ? `boss#${value}` : key;
}

export function motelyBossDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyVoucherDisplayName(value: number): string {
  if (!_motely) return `voucher#${value}`;
  const key = runtimeEnumKey(_motely.MotelyVoucher as Record<string, unknown>, value);
  return key === null ? `voucher#${value}` : key;
}

export function motelyVoucherDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyTagDisplayName(value: number): string {
  if (!_motely) return `tag#${value}`;
  const key = runtimeEnumKey(_motely.MotelyTag as Record<string, unknown>, value);
  return key === null ? `tag#${value}` : key;
}

export function motelyTagDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyBoosterPackDisplayName(value: number): string {
  if (!_motely) return `pack#${value}`;
  const key = runtimeEnumKey(_motely.MotelyBoosterPack as Record<string, unknown>, value);
  return key === null ? `pack#${value}` : key;
}

export function motelyBoosterPackDisplayNameFromKey(key: string): string {
  return `${key} Pack`;
}

export function motelyItemDisplayNameFromKey(key: string): string {
  return key;
}

export function motelyItemDisplayNameFromValue(value: number): string {
  if (!_motely) return `item#${value}`;
  const key = runtimeEnumKey(_motely.MotelyItemType as Record<string, unknown>, value & 0xffff);
  return key === null ? `item#${value}` : key;
}
