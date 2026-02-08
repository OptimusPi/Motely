export const deckOptions = [
  'Red',
  'Blue',
  'Yellow',
  'Green',
  'Black',
  'Magic',
  'Nebula',
  'Ghost',
  'Abandoned',
  'Checkered',
  'Zodiac',
  'Painted',
  'Anaglyph',
  'Plasma',
  'Erratic',
  'Challenge'
]

export const stakeOptions = [
  'White',
  'Red',
  'Green',
  'Black',
  'Blue',
  'Purple',
  'Orange',
  'Gold'
]

export const clauseTypeOptions = [
  'Joker',
  'SoulJoker',
  'Voucher',
  'Tarot',
  'TarotCard',
  'Planet',
  'PlanetCard',
  'Spectral',
  'SpectralCard',
  'Tag',
  'Boss',
  'BossBlind',
  'PlayingCard',
  'StandardCard'
]

export const tagOptions = [
  'Coupon',
  'Top Up',
  'Lucky',
  'Skip',
  'Null',
  'Satellite',
  'Retro',
  'Meteor',
  'Economy',
  'Wild'
]

export const jokerOptions = [
  'Perkeo',
  'Blueprint',
  'Ankh',
  'Hanging Chad',
  'Burnt Joker',
  'Brainstorm',
  'DNA',
  'Egg',
  'Supernova',
  'Bull'
]

export const planetOptions = [
  'Mercury',
  'Venus',
  'Earth',
  'Mars',
  'Jupiter',
  'Saturn',
  'Uranus',
  'Neptune',
  'Pluto'
]

export const voucherOptions = [
  'Reboot',
  'Credit Card',
  'Co-op',
  'Blueprint Pack',
  'Tag Bag'
]

export const sealOptions = ['Red', 'Blue', 'Gold', 'Purple']
export const editionOptions = ['None', 'Foil', 'Holographic', 'Polychrome', 'Negative']
export const enhancementOptions = ['None', 'Bonus', 'Mult', 'Wild', 'Glass', 'Steel', 'Stone', 'Lucky', 'Gold']

export const playingCardRanks = ['2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K', 'A']
export const playingCardSuits = ['Hearts', 'Diamonds', 'Clubs', 'Spades']

export const valueSuggestionsMap = {
  Joker: jokerOptions,
  SoulJoker: jokerOptions,
  Voucher: voucherOptions,
  Tarot: ['The Fool', 'The Magician', 'The High Priestess', 'The Hierophant', 'The Lovers'],
  TarotCard: ['Fool', 'Magician', 'Priestess', 'Hierophant', 'Lovers'],
  Planet: planetOptions,
  PlanetCard: planetOptions,
  Spectral: ['Judgement', 'Perish', 'Aura', 'Hex'],
  SpectralCard: ['Judgement', 'Perish', 'Aura', 'Hex'],
  Tag: tagOptions,
  Boss: ['Verdant Leech', 'Blue Devil', 'Wall Street', 'Giant'],
  BossBlind: ['Verdant Leech', 'Blue Devil', 'Wall Street', 'Giant'],
  PlayingCard: playingCardRanks,
  StandardCard: playingCardRanks
}

export const anteOptions = [1, 2, 3, 4, 5, 6, 7, 8]
export const slotOptions = [0, 1, 2, 3, 4, 5]
export const sourceOptions = ['shop', 'pack', 'tag', 'voucher']
