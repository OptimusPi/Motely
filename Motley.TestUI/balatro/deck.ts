// Deck management and card utilities
import { Card, Suit, Rank, RANK_CHIPS } from './types'

const SUITS: Suit[] = ['hearts', 'diamonds', 'clubs', 'spades']
const RANKS: Rank[] = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K']

let cardIdCounter = 0

export function resetCardIdCounter(): void {
  cardIdCounter = 0
}

/** FNV-1a–style 32-bit hash for stable numeric seeds from strings. */
export function hashSeedString(s: string): number {
  let h = 2166136261 >>> 0
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i)
    h = Math.imul(h, 16777619) >>> 0
  }
  return h >>> 0
}

export function createCard(suit: Suit, rank: Rank): Card {
  return {
    id: `card-${cardIdCounter++}`,
    suit,
    rank,
    chips: RANK_CHIPS[rank],
  }
}

export function createStandardDeck(): Card[] {
  const deck: Card[] = []
  for (const suit of SUITS) {
    for (const rank of RANKS) {
      deck.push(createCard(suit, rank))
    }
  }
  return deck
}

export function shuffleDeck<T>(deck: T[]): T[] {
  const shuffled = [...deck]
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]]
  }
  return shuffled
}

/** Fisher–Yates with deterministic RNG derived from `seedString` + optional `salt`. */
export function shuffleDeckSeeded<T>(deck: T[], seedString: string, salt = ''): T[] {
  let state = hashSeedString(`${seedString}\0${salt}`) >>> 0
  const rnd = () => {
    state = (state + 0x6d2b79f5) >>> 0
    let t = state
    t = Math.imul(t ^ (t >>> 15), t | 1)
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296
  }
  const shuffled = [...deck]
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(rnd() * (i + 1))
    ;[shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]]
  }
  return shuffled
}

export function drawCards(deck: Card[], count: number): { drawn: Card[]; remaining: Card[] } {
  const drawn = deck.slice(0, count)
  const remaining = deck.slice(count)
  return { drawn, remaining }
}

export function getRankValue(rank: Rank): number {
  const index = RANKS.indexOf(rank)
  return index === 0 ? 14 : index + 1 // A is high (14) for comparison
}

export function getRankValueLow(rank: Rank): number {
  const index = RANKS.indexOf(rank)
  return index + 1 // A is 1 for low straights
}

export function sortByRank(cards: Card[]): Card[] {
  return [...cards].sort((a, b) => getRankValue(b.rank) - getRankValue(a.rank))
}

export function sortBySuit(cards: Card[]): Card[] {
  const suitOrder: Record<Suit, number> = { spades: 0, hearts: 1, diamonds: 2, clubs: 3 }
  return [...cards].sort((a, b) => {
    const suitDiff = suitOrder[a.suit] - suitOrder[b.suit]
    if (suitDiff !== 0) return suitDiff
    return getRankValue(b.rank) - getRankValue(a.rank)
  })
}

export function groupByRank(cards: Card[]): Map<Rank, Card[]> {
  const groups = new Map<Rank, Card[]>()
  for (const card of cards) {
    const existing = groups.get(card.rank) || []
    existing.push(card)
    groups.set(card.rank, existing)
  }
  return groups
}

export function groupBySuit(cards: Card[]): Map<Suit, Card[]> {
  const groups = new Map<Suit, Card[]>()
  for (const card of cards) {
    const existing = groups.get(card.suit) || []
    existing.push(card)
    groups.set(card.suit, existing)
  }
  return groups
}
