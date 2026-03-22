// Balatro Card & Game Types

export type Suit = 'hearts' | 'diamonds' | 'clubs' | 'spades'
export type Rank = 'A' | '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '10' | 'J' | 'Q' | 'K'

export interface Card {
  id: string
  suit: Suit
  rank: Rank
  chips: number
  enhancement?: CardEnhancement
  seal?: CardSeal
  edition?: CardEdition
}

export type CardEnhancement =
  | 'bonus'
  | 'mult'
  | 'wild'
  | 'glass'
  | 'steel'
  | 'stone'
  | 'gold'
  | 'lucky'

export type CardSeal = 'gold' | 'red' | 'blue' | 'purple'

export type CardEdition = 'foil' | 'holographic' | 'polychrome' | 'negative'

export type HandType =
  | 'high_card'
  | 'pair'
  | 'two_pair'
  | 'three_of_a_kind'
  | 'straight'
  | 'flush'
  | 'full_house'
  | 'four_of_a_kind'
  | 'straight_flush'
  | 'royal_flush'
  | 'five_of_a_kind'
  | 'flush_house'
  | 'flush_five'

export interface HandScore {
  type: HandType
  name: string
  baseChips: number
  baseMult: number
  level: number
}

export interface JokerCard {
  id: string
  name: string
  description: string
  rarity: 'common' | 'uncommon' | 'rare' | 'legendary'
  effect: JokerEffect
  edition?: CardEdition
}

export interface JokerEffect {
  type: 'add_mult' | 'add_chips' | 'mult_mult' | 'retrigger' | 'special'
  value?: number
  condition?: string
}

export interface AnalysisResult {
  hand: HandScore
  scoringCards: Card[]
  totalChips: number
  totalMult: number
  finalScore: number
  breakdown: ScoreBreakdown[]
}

export interface ScoreBreakdown {
  source: string
  chips?: number
  mult?: number
  xMult?: number
  description: string
}

// Chip values for each rank
export const RANK_CHIPS: Record<Rank, number> = {
  A: 11,
  K: 10,
  Q: 10,
  J: 10,
  '10': 10,
  '9': 9,
  '8': 8,
  '7': 7,
  '6': 6,
  '5': 5,
  '4': 4,
  '3': 3,
  '2': 2,
}

// Rank order for straights (A can be high or low)
export const RANK_ORDER: Rank[] = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K']

// Base hand scores (chips, mult)
export const BASE_HANDS: Record<HandType, { chips: number; mult: number; name: string }> = {
  high_card: { chips: 5, mult: 1, name: 'High Card' },
  pair: { chips: 10, mult: 2, name: 'Pair' },
  two_pair: { chips: 20, mult: 2, name: 'Two Pair' },
  three_of_a_kind: { chips: 30, mult: 3, name: 'Three of a Kind' },
  straight: { chips: 30, mult: 4, name: 'Straight' },
  flush: { chips: 35, mult: 4, name: 'Flush' },
  full_house: { chips: 40, mult: 4, name: 'Full House' },
  four_of_a_kind: { chips: 60, mult: 7, name: 'Four of a Kind' },
  straight_flush: { chips: 100, mult: 8, name: 'Straight Flush' },
  royal_flush: { chips: 100, mult: 8, name: 'Royal Flush' },
  five_of_a_kind: { chips: 120, mult: 12, name: 'Five of a Kind' },
  flush_house: { chips: 140, mult: 14, name: 'Flush House' },
  flush_five: { chips: 160, mult: 16, name: 'Flush Five' },
}

// Suit colors for rendering
export const SUIT_COLORS: Record<Suit, string> = {
  hearts: '#e74c3c',
  diamonds: '#3498db',
  clubs: '#27ae60',
  spades: '#2c3e50',
}

// Suit symbols
export const SUIT_SYMBOLS: Record<Suit, string> = {
  hearts: '♥',
  diamonds: '♦',
  clubs: '♣',
  spades: '♠',
}
