import { create } from 'zustand'

/** Highway billboard jokers: deterministic list from shared Balatro <code>gameSeed</code> (Motely + TS shop). */
type State = {
  billboardLoading: boolean
  billboardError: string | null
  billboardJokerCount: number
  billboardNote: string | null
}

export const useAdventureBillboardStatusStore = create<State>(() => ({
  billboardLoading: false,
  billboardError: null,
  billboardJokerCount: 0,
  billboardNote: null,
}))

export function setAdventureBillboardStatus(partial: Partial<State>): void {
  useAdventureBillboardStatusStore.setState(partial)
}
