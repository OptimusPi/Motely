/** Display-name helpers for packed Motely enum values. */

import {
  MotelyBoosterPack,
  MotelyBossBlind,
  MotelyTag,
  MotelyVoucher,
  MotelyDeck,
  MotelyStake,
} from "motely-wasm";

export function splitCamelCase(key: string): string {
  return key.replace(/([A-Z])/g, " $1").trim();
}

export function packDisplayName(pack: MotelyBoosterPack): string {
  return splitCamelCase(MotelyBoosterPack[pack]);
}

export function bossDisplayName(boss: MotelyBossBlind): string {
  return splitCamelCase(MotelyBossBlind[boss]);
}

export function tagDisplayName(tag: MotelyTag): string {
  return splitCamelCase(MotelyTag[tag]);
}

export function voucherDisplayName(voucher: MotelyVoucher): string {
  return splitCamelCase(MotelyVoucher[voucher]);
}

export function deckDisplayName(deck: MotelyDeck): string {
  return splitCamelCase(MotelyDeck[deck]);
}

export function stakeDisplayName(stake: MotelyStake): string {
  return splitCamelCase(MotelyStake[stake]);
}
