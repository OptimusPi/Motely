// Joker rarity *tier* — a UI presentation concept.
//
// motely-wasm ships per-rarity joker-NAME enums (MotelyJokerCommon /
// MotelyJokerUncommon / MotelyJokerRare) but no rarity-tier enum, so the tier
// itself is defined here. These are opaque UI tags (used for switch/equality and
// clause-key mapping), never compared against an engine value. The membership
// that decides which tier a joker belongs to is read straight from those engine
// enums — see `getJokerRarity` in JokerPicker.
export enum JokerRarityTier {
  Common,
  Uncommon,
  Rare,
  Legendary,
}
