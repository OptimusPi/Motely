import { useCallback, useEffect, useRef, useState } from 'react'
import type { Game } from '@balatrots/Game'
import { createTsGame } from '../engine-shop/tsBalatroGame'
import type { BalatroStreamStateJson } from '../engine-shop/streamCursors'
import type { ShopBenchRangeTimings, ShopDeck, ShopStake, ShopStreamRow } from './shopStreamShared'

function streamSnapshotMeta(g: Game): { cursorCount: number; generatedFirstPack: boolean } {
  const s = g.exportStreamState()
  return {
    cursorCount: Object.keys(s.nodes).length,
    generatedFirstPack: s.generatedFirstPack,
  }
}

function nowMs() {
  return typeof performance !== 'undefined' && performance.now ? performance.now() : Date.now()
}

function measureShopBenchRanges(next: () => void): ShopBenchRangeTimings {
  const t0 = nowMs()
  for (let i = 0; i < 100; i++) next()
  const t1 = nowMs()
  for (let i = 0; i < 9_900; i++) next()
  const t2 = nowMs()
  for (let i = 0; i < 2; i++) next()
  const t3 = nowMs()

  return {
    range0To99Ms: Math.round(t1 - t0),
    range100To9999Ms: Math.round(t2 - t1),
    range10000To10001Ms: Math.round(t3 - t2),
  }
}

export function useTsShopStream(seed: string, deck: ShopDeck, stake: ShopStake, ante: number) {
  const [rows, setRows] = useState<ShopStreamRow[]>([])
  const [streamError, setStreamError] = useState<string | null>(null)
  const [streamReady, setStreamReady] = useState(false)
  const [cacheMeta, setCacheMeta] = useState<{
    cursorCount: number
    generatedFirstPack: boolean
  } | null>(null)
  const [benchRangeTimings, setBenchRangeTimings] = useState<ShopBenchRangeTimings | null>(null)
  const [benchRunning, setBenchRunning] = useState(false)

  const gameRef = useRef<Game | null>(null)
  const rowsRef = useRef<ShopStreamRow[]>([])

  const resetStream = useCallback(() => {
    setStreamError(null)
    setBenchRangeTimings(null)
    try {
      const g = createTsGame(seed, deck, stake)
      gameRef.current = g
      rowsRef.current = []
      setRows([])
      setCacheMeta(streamSnapshotMeta(g))
      setStreamReady(true)
    } catch (e: unknown) {
      gameRef.current = null
      rowsRef.current = []
      setCacheMeta(null)
      setStreamReady(false)
      setStreamError(e instanceof Error ? e.message : String(e))
    }
  }, [deck, seed, stake])

  useEffect(() => {
    void resetStream()
  }, [resetStream])

  /** Match Motely hook: new ante = fresh list (same Game, different stream). */
  useEffect(() => {
    rowsRef.current = []
    setRows([])
    setBenchRangeTimings(null)
  }, [ante])

  const pull = useCallback(
    (n: number) => {
      setStreamError(null)
      const g = gameRef.current
      if (!g || n <= 0) return
      try {
        const base = rowsRef.current.length
        const add: ShopStreamRow[] = []
        for (let k = 0; k < n; k++) {
          const shop = g.nextShopItem(ante)
          add.push({
            index: base + k,
            type: String(shop.type),
            name: shop.item.getName(),
          })
        }
        rowsRef.current = [...rowsRef.current, ...add]
        setRows([...rowsRef.current])
        setCacheMeta(streamSnapshotMeta(g))
      } catch (e: unknown) {
        setStreamError(e instanceof Error ? e.message : String(e))
      }
    },
    [ante]
  )

  const copyDebug = useCallback(() => {
    const g = gameRef.current
    if (!g) return
    const json = g.exportStreamState() satisfies BalatroStreamStateJson
    void navigator.clipboard.writeText(JSON.stringify(json)).catch(() => { })
  }, [])

  const runBenchRanges = useCallback(async (): Promise<ShopBenchRangeTimings | null> => {
    setStreamError(null)
    setBenchRunning(true)
    try {
      const g = createTsGame(seed, deck, stake)
      const timings = measureShopBenchRanges(() => {
        g.nextShopItem(ante)
      })
      setBenchRangeTimings(timings)
      return timings
    } catch (e: unknown) {
      setStreamError(e instanceof Error ? e.message : String(e))
      return null
    } finally {
      setBenchRunning(false)
    }
  }, [ante, deck, seed, stake])

  return {
    rows,
    streamError,
    streamReady,
    engineLoading: false,
    cacheMeta,
    resetStream,
    pull,
    copyDebug,
    copyDebugLabel: 'Copy stream JSON',
    benchRangeTimings,
    benchRunning,
    runBenchRanges,
  }
}
