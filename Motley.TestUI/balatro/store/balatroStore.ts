import { create } from 'zustand'
import type { Card, JokerCard, HandType, AnalysisResult } from '../types'
import { createStandardDeck, shuffleDeckSeeded, drawCards, resetCardIdCounter } from '../deck'
import { analyzeHand } from '../handAnalyzer'

function readInitialSeed(): string {
  if (typeof window === 'undefined') return 'BALATRO1'
  const q = new URLSearchParams(window.location.search).get('seed')
  const s = q?.trim()
  return s && s.length > 0 ? s : 'BALATRO1'
}

export type InitGameOpts = { seed?: string; newRun?: boolean }

interface BalatroState {
  // Deck management
  deck: Card[]
  hand: Card[]
  selectedCards: Set<string>
  discardPile: Card[]

  // Game state
  handsRemaining: number
  discardsRemaining: number
  money: number
  ante: number
  round: number

  // Score tracking
  currentScore: number
  targetScore: number
  lastAnalysis: AnalysisResult | null

  // Jokers
  jokers: JokerCard[]

  // Hand levels (upgrades)
  handLevels: Partial<Record<HandType, number>>

  // UI state
  isAnimating: boolean
  showAnalysis: boolean

  /** String seed; same seed + same deal sequence ⇒ same deck order. */
  gameSeed: string
  /** Increments each initGame (opening hands after rounds use higher indices). */
  dealSeq: number
  /** Count of discard-pile reshuffles this run (salted into shuffle). */
  reshuffleSeq: number

  // Actions
  initGame: (opts?: InitGameOpts) => void
  drawHand: (count?: number) => void
  selectCard: (cardId: string) => void
  deselectCard: (cardId: string) => void
  toggleCardSelection: (cardId: string) => void
  clearSelection: () => void
  playHand: () => void
  discardSelected: () => void
  analyzeSelection: () => void
  setShowAnalysis: (show: boolean) => void
  addJoker: (joker: JokerCard) => void
  removeJoker: (jokerId: string) => void
  upgradeHand: (handType: HandType) => void
  nextRound: () => void
}

const INITIAL_HANDS = 4
const INITIAL_DISCARDS = 3
const HAND_SIZE = 8

export const useBalatroStore = create<BalatroState>((set, get) => ({
  // Initial state
  deck: [],
  hand: [],
  selectedCards: new Set(),
  discardPile: [],
  handsRemaining: INITIAL_HANDS,
  discardsRemaining: INITIAL_DISCARDS,
  money: 0,
  ante: 1,
  round: 1,
  currentScore: 0,
  targetScore: 300,
  lastAnalysis: null,
  jokers: [],
  handLevels: {},
  isAnimating: false,
  showAnalysis: true,
  gameSeed: readInitialSeed(),
  dealSeq: -1,
  reshuffleSeq: 0,

  initGame: (opts) => {
    const prev = get()
    const seed = opts?.seed ?? prev.gameSeed
    if (opts?.seed !== undefined && typeof window !== 'undefined') {
      try {
        const u = new URL(window.location.href)
        u.searchParams.set('seed', seed)
        window.history.replaceState({}, '', `${u.pathname}${u.search}${u.hash}`)
      } catch {
        /* ignore */
      }
    }

    let dealSeq = prev.dealSeq
    if (opts?.newRun === true || opts?.seed !== undefined) {
      dealSeq = -1
    }
    dealSeq += 1

    resetCardIdCounter()
    const deck = shuffleDeckSeeded(createStandardDeck(), seed, `:deal:${dealSeq}`)
    const { drawn, remaining } = drawCards(deck, HAND_SIZE)

    set({
      gameSeed: seed,
      dealSeq,
      reshuffleSeq: 0,
      deck: remaining,
      hand: drawn,
      selectedCards: new Set(),
      discardPile: [],
      handsRemaining: INITIAL_HANDS,
      discardsRemaining: INITIAL_DISCARDS,
      currentScore: 0,
      targetScore: 300,
      lastAnalysis: null,
      isAnimating: false,
    })
  },

  drawHand: (count = HAND_SIZE) => {
    const { deck, hand, discardPile, gameSeed, reshuffleSeq } = get()
    let currentDeck = [...deck]
    let nextReshuffle = reshuffleSeq

    if (currentDeck.length < count) {
      nextReshuffle += 1
      currentDeck = shuffleDeckSeeded(
        [...currentDeck, ...discardPile],
        gameSeed,
        `:reshuffle:${nextReshuffle}`
      )
      set({ discardPile: [] })
    }

    const { drawn, remaining } = drawCards(currentDeck, count - hand.length)

    set({
      deck: remaining,
      hand: [...hand, ...drawn],
      selectedCards: new Set(),
      reshuffleSeq: nextReshuffle,
    })
  },

  selectCard: (cardId) => {
    const { selectedCards } = get()
    if (selectedCards.size >= 5) return // Max 5 cards

    const newSelection = new Set(selectedCards)
    newSelection.add(cardId)
    set({ selectedCards: newSelection })

    // Auto-analyze when selection changes
    get().analyzeSelection()
  },

  deselectCard: (cardId) => {
    const { selectedCards } = get()
    const newSelection = new Set(selectedCards)
    newSelection.delete(cardId)
    set({ selectedCards: newSelection })

    get().analyzeSelection()
  },

  toggleCardSelection: (cardId) => {
    const { selectedCards } = get()
    if (selectedCards.has(cardId)) {
      get().deselectCard(cardId)
    } else {
      get().selectCard(cardId)
    }
  },

  clearSelection: () => {
    set({ selectedCards: new Set(), lastAnalysis: null })
  },

  playHand: () => {
    const { hand, selectedCards, handsRemaining, currentScore, handLevels, discardPile } = get()

    if (selectedCards.size === 0 || handsRemaining <= 0) return

    const selectedCardsArray = hand.filter((c) => selectedCards.has(c.id))
    const analysis = analyzeHand(selectedCardsArray, handLevels)

    // Remove played cards from hand
    const remainingHand = hand.filter((c) => !selectedCards.has(c.id))

    set({
      hand: remainingHand,
      selectedCards: new Set(),
      discardPile: [...discardPile, ...selectedCardsArray],
      handsRemaining: handsRemaining - 1,
      currentScore: currentScore + analysis.finalScore,
      lastAnalysis: analysis,
      isAnimating: true,
    })

    // Draw back up
    setTimeout(() => {
      get().drawHand()
      set({ isAnimating: false })
    }, 1000)
  },

  discardSelected: () => {
    const { hand, selectedCards, discardsRemaining, discardPile } = get()

    if (selectedCards.size === 0 || discardsRemaining <= 0) return

    const selectedCardsArray = hand.filter((c) => selectedCards.has(c.id))
    const remainingHand = hand.filter((c) => !selectedCards.has(c.id))

    set({
      hand: remainingHand,
      selectedCards: new Set(),
      discardPile: [...discardPile, ...selectedCardsArray],
      discardsRemaining: discardsRemaining - 1,
      lastAnalysis: null,
    })

    // Draw back up
    setTimeout(() => {
      get().drawHand()
    }, 500)
  },

  analyzeSelection: () => {
    const { hand, selectedCards, handLevels } = get()

    if (selectedCards.size === 0) {
      set({ lastAnalysis: null })
      return
    }

    const selectedCardsArray = hand.filter((c) => selectedCards.has(c.id))
    const analysis = analyzeHand(selectedCardsArray, handLevels)
    set({ lastAnalysis: analysis })
  },

  setShowAnalysis: (show) => {
    set({ showAnalysis: show })
  },

  addJoker: (joker) => {
    const { jokers } = get()
    if (jokers.length >= 5) return // Max 5 jokers
    set({ jokers: [...jokers, joker] })
  },

  removeJoker: (jokerId) => {
    const { jokers } = get()
    set({ jokers: jokers.filter((j) => j.id !== jokerId) })
  },

  upgradeHand: (handType) => {
    const { handLevels } = get()
    const currentLevel = handLevels[handType] ?? 1
    set({
      handLevels: { ...handLevels, [handType]: currentLevel + 1 },
    })
  },

  nextRound: () => {
    const { ante, round, currentScore, targetScore } = get()

    if (currentScore >= targetScore) {
      // Won the round
      const newAnte = round % 3 === 0 ? ante + 1 : ante
      const newTarget = Math.floor(targetScore * 1.5)

      set({
        round: round + 1,
        ante: newAnte,
        targetScore: newTarget,
        currentScore: 0,
      })

      get().initGame()
    }
  },
}))
