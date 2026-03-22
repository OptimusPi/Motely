import { useCallback, useEffect, useRef, useState } from 'react'
import type { Game } from '@balatrots/Game'
import { createTsGame } from '../engine-shop/tsBalatroGame'
import type { BalatroStreamStateJson } from '../engine-shop/streamCursors'
import type { ShopDeck, ShopStake, ShopStreamRow } from './shopStreamShared'

function streamSnapshotMeta(g: Game): { cursorCount: number; generatedFirstPack: boolean } {
  const s = g.exportStreamState()
  return {
    cursorCount: Object.keys(s.nodes).length,
    generatedFirstPack: s.generatedFirstPack,
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

  const gameRef = useRef<Game | null>(null)

  const resetStream = useCallback(() => {
    setStreamError(null)
    try {
      const g = createTsGame(seed, deck, stake)
      gameRef.current = g
      setRows([])
      setCacheMeta(streamSnapshotMeta(g))
      setStreamReady(true)
    } catch (e: unknown) {
      gameRef.current = null
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
    setRows([])
  }, [ante])

  const pull = useCallback(
    (n: number) => {
      setStreamError(null)
      const g = gameRef.current
      if (!g || n <= 0) return
      try {
        setRows((prev) => {
          const base = prev.length
          const add: ShopStreamRow[] = []
          for (let k = 0; k < n; k++) {
            const shop = g.nextShopItem(ante)
            add.push({
              index: base + k,
              type: String(shop.type),
              name: shop.item.getName(),
            })
          }
          return [...prev, ...add]
        })
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
    void navigator.clipboard.writeText(JSON.stringify(json)).catch(() => {})
  }, [])

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
  }
}
