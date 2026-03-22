import { create } from 'zustand'

/** Lightweight readout for the highway scene overlay (analyzer stub). */
export const useAdventureHudStore = create<{
  scroll: number
  speed: number
  setDrive: (scroll: number, speed: number) => void
}>((set) => ({
  scroll: 0,
  speed: 0,
  setDrive: (scroll, speed) => set({ scroll, speed }),
}))
