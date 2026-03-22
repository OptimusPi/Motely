import { useBalatroStore } from '../store/balatroStore'
import type { BalatroSyncPayload } from './balatroSyncTypes'

type BalatroState = ReturnType<typeof useBalatroStore.getState>

export function payloadFromStore(s: BalatroState): BalatroSyncPayload {
  return {
    gameSeed: s.gameSeed,
    dealSeq: s.dealSeq,
    reshuffleSeq: s.reshuffleSeq,
    deck: s.deck,
    hand: s.hand,
    selectedCardIds: [...s.selectedCards],
    discardPile: s.discardPile,
    handsRemaining: s.handsRemaining,
    discardsRemaining: s.discardsRemaining,
    money: s.money,
    ante: s.ante,
    round: s.round,
    currentScore: s.currentScore,
    targetScore: s.targetScore,
    lastAnalysis: s.lastAnalysis,
    jokers: s.jokers,
    handLevels: s.handLevels,
    showAnalysis: s.showAnalysis,
  }
}

export function mergePayloadIntoStore(p: BalatroSyncPayload): Partial<BalatroState> {
  return {
    gameSeed: p.gameSeed,
    dealSeq: p.dealSeq,
    reshuffleSeq: p.reshuffleSeq,
    deck: p.deck,
    hand: p.hand,
    selectedCards: new Set(p.selectedCardIds),
    discardPile: p.discardPile,
    handsRemaining: p.handsRemaining,
    discardsRemaining: p.discardsRemaining,
    money: p.money,
    ante: p.ante,
    round: p.round,
    currentScore: p.currentScore,
    targetScore: p.targetScore,
    lastAnalysis: p.lastAnalysis,
    jokers: p.jokers,
    handLevels: p.handLevels,
    showAnalysis: p.showAnalysis,
  }
}
