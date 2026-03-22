import type {
  AnalysisResult,
  Card,
  HandType,
  JokerCard,
} from '../types'

/** JSON-safe snapshot pushed to Redis; Set → array at the edge. */
export type BalatroSyncPayload = Readonly<{
  gameSeed: string
  dealSeq: number
  reshuffleSeq: number
  deck: Card[]
  hand: Card[]
  selectedCardIds: string[]
  discardPile: Card[]
  handsRemaining: number
  discardsRemaining: number
  money: number
  ante: number
  round: number
  currentScore: number
  targetScore: number
  lastAnalysis: AnalysisResult | null
  jokers: JokerCard[]
  handLevels: Partial<Record<HandType, number>>
  showAnalysis: boolean
}>

export type BalatroRoomDocument = Readonly<{
  rev: number
  updatedAt: number
  payload: BalatroSyncPayload
}>
