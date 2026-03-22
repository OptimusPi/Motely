import { useEffect, useState, useCallback } from 'react'

interface GameState {
  seed: string
  playerCount: number
  createdAt: number
  lastUpdated: number
  cards?: Array<{
    suit: 'hearts' | 'clubs' | 'diamonds' | 'spades'
    rank: '2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '10' | 'J' | 'Q' | 'K' | 'A'
  }>
}

export function useSeedGame(seed: string, pollInterval: number = 5000) {
  const [state, setState] = useState<GameState | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Initial fetch + setup
  useEffect(() => {
    const fetchState = async () => {
      try {
        setLoading(true)
        const res = await fetch(`/api/seed/${seed}/state`)
        if (!res.ok) throw new Error(`HTTP ${res.status}`)
        const data = (await res.json()) as GameState
        setState(data)
        setError(null)
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Unknown error'
        setError(message)
        console.error(`[useSeedGame] fetch error for seed=${seed}:`, message)
      } finally {
        setLoading(false)
      }
    }

    fetchState()

    // Optional: polling for multiplayer sync
    if (pollInterval > 0) {
      const interval = setInterval(fetchState, pollInterval)
      return () => clearInterval(interval)
    }
  }, [seed, pollInterval])

  const updateState = useCallback(
    async (updates: Partial<GameState>) => {
      if (!state) return
      try {
        const res = await fetch(`/api/seed/${seed}/state`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(updates),
        })
        if (!res.ok) throw new Error(`HTTP ${res.status}`)
        const data = (await res.json()) as GameState
        setState(data)
        return data
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Unknown error'
        setError(message)
        console.error(`[useSeedGame] update error for seed=${seed}:`, message)
      }
    },
    [seed, state]
  )

  return { state, loading, error, updateState }
}
