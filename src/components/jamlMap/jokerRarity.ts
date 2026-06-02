// Joker rarity classification.
//
// motely-wasm 19.x no longer exports a `MotelyJokerRarity` enum — it ships
// per-rarity joker-NAME enums (MotelyJokerCommon / MotelyJokerUncommon /
// MotelyJokerRare) instead, which are a different concept entirely. The rarity
// *tier* is a UI concern, so it's defined here. Values are opaque tags (used
// only for switch/equality and clause-key mapping), never compared against an
// engine value.
export enum MotelyJokerRarity {
  Common,
  Uncommon,
  Rare,
  Legendary,
}
