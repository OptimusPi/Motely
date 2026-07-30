// Single canonical form per value. No case tolerance, no abbreviations.
// Value strings match the keys used by RANK_MAP / SUIT_MAP / ENHANCER_MAP /
// SEAL_MAP / EDITION_MAP so no aliasing/normalization is needed at render time.

export const CardSuit = {
  Hearts: 'Hearts',
  Diamonds: 'Diamonds',
  Clubs: 'Clubs',
  Spades: 'Spades',
} as const
export type CardSuit = typeof CardSuit[keyof typeof CardSuit]

export const CardRank = {
  Two: '2',
  Three: '3',
  Four: '4',
  Five: '5',
  Six: '6',
  Seven: '7',
  Eight: '8',
  Nine: '9',
  Ten: '10',
  Jack: 'Jack',
  Queen: 'Queen',
  King: 'King',
  Ace: 'Ace',
} as const
export type CardRank = typeof CardRank[keyof typeof CardRank]

export const CardEnhancement = {
  Bonus: 'Bonus',
  Mult: 'Mult',
  Wild: 'Wild',
  Glass: 'Glass',
  Steel: 'Steel',
  Stone: 'Stone',
  Gold: 'Gold',
  Lucky: 'Lucky',
} as const
export type CardEnhancement = typeof CardEnhancement[keyof typeof CardEnhancement]

export const CardSeal = {
  Gold: 'Gold',
  Red: 'Red',
  Blue: 'Blue',
  Purple: 'Purple',
} as const
export type CardSeal = typeof CardSeal[keyof typeof CardSeal]

export const CardEdition = {
  Foil: 'Foil',
  Holographic: 'Holographic',
  Polychrome: 'Polychrome',
  Negative: 'Negative',
} as const
export type CardEdition = typeof CardEdition[keyof typeof CardEdition]
