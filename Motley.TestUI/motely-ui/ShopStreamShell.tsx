import { useCallback, useEffect, useRef, useState, type UIEvent } from 'react'
import type {
  MotelyBenchSizes,
  MotelyBenchTwoK,
  ShopDeck,
  ShopStake,
  ShopStreamCacheMetaMotely,
  ShopStreamRow,
} from './shopStreamShared'
import { SHOP_DECKS, SHOP_STAKES, shopStreamStyles as s } from './shopStreamShared'

export type ShopStreamCacheMetaTs = Readonly<{
  cursorCount: number
  generatedFirstPack: boolean
}>

export type { ShopStreamCacheMetaMotely }

const SHOP_ROW_PX = 22
const SHOP_OVERSCAN = 10
const INFINITE_COOLDOWN_MS = 350

type Props = Readonly<{
  onBack: () => void
  title: string
  subtitle: string
  footerNote: string
  seed: string
  setSeed: (v: string) => void
  deck: ShopDeck
  setDeck: (v: ShopDeck) => void
  stake: ShopStake
  setStake: (v: ShopStake) => void
  ante: number
  setAnte: (v: number) => void
  rows: ShopStreamRow[]
  streamError: string | null
  streamReady: boolean
  engineLoading: boolean
  cacheMetaTs: ShopStreamCacheMetaTs | null
  cacheMetaMotely: ShopStreamCacheMetaMotely | null
  resetStream: () => void
  pull: (n: number) => void
  copyDebug: () => void
  copyDebugLabel: string
  /** When true, scrolling near the bottom pulls more items (same for TS + Motely). */
  infiniteScroll?: boolean
  infiniteScrollChunkSize?: number
  motelyBenchTwoK?: MotelyBenchTwoK | null
  motelyBenchSizes?: MotelyBenchSizes | null
  motelyBenchRunning?: boolean
  onMotelyBenchTwoK?: () => void
  onMotelyBenchSizes?: () => void
}>

export function ShopStreamShell({
  onBack,
  title,
  subtitle,
  footerNote,
  seed,
  setSeed,
  deck,
  setDeck,
  stake,
  setStake,
  ante,
  setAnte,
  rows,
  streamError,
  streamReady,
  engineLoading,
  cacheMetaTs,
  cacheMetaMotely,
  resetStream,
  pull,
  copyDebug,
  copyDebugLabel,
  infiniteScroll = true,
  infiniteScrollChunkSize = 100,
  motelyBenchTwoK = null,
  motelyBenchSizes = null,
  motelyBenchRunning = false,
  onMotelyBenchTwoK,
  onMotelyBenchSizes,
}: Props) {
  const controlsDisabled = !streamReady || engineLoading
  const scrollRef = useRef<HTMLDivElement>(null)
  const sentinelRef = useRef<HTMLDivElement>(null)
  const [scrollTop, setScrollTop] = useState(0)
  const [viewportH, setViewportH] = useState(400)
  const lastInfiniteMs = useRef(0)

  const onScroll = useCallback((e: UIEvent<HTMLDivElement>) => {
    setScrollTop(e.currentTarget.scrollTop)
  }, [])

  useEffect(() => {
    const el = scrollRef.current
    if (!el) return
    const ro = new ResizeObserver(() => {
      setViewportH(Math.max(120, el.clientHeight))
    })
    ro.observe(el)
    setViewportH(Math.max(120, el.clientHeight))
    return () => ro.disconnect()
  }, [])

  const first = Math.max(0, Math.floor(scrollTop / SHOP_ROW_PX) - SHOP_OVERSCAN)
  const visCount = Math.ceil(viewportH / SHOP_ROW_PX) + 2 * SHOP_OVERSCAN
  const last = Math.min(rows.length, first + visCount)
  const padTop = first * SHOP_ROW_PX
  const padBottom = Math.max(0, (rows.length - last) * SHOP_ROW_PX)

  useEffect(() => {
    if (!infiniteScroll || controlsDisabled || motelyBenchRunning) return
    const root = scrollRef.current
    const target = sentinelRef.current
    if (!root || !target) return

    const io = new IntersectionObserver(
      (entries) => {
        const hit = entries.some((e) => e.isIntersecting)
        if (!hit) return
        if (rows.length === 0) return
        if (root.scrollHeight <= root.clientHeight + 32) return
        const now = Date.now()
        if (now - lastInfiniteMs.current < INFINITE_COOLDOWN_MS) return
        lastInfiniteMs.current = now
        pull(Math.max(1, infiniteScrollChunkSize))
      },
      { root, rootMargin: '320px', threshold: 0 }
    )
    io.observe(target)
    return () => io.disconnect()
  }, [
    infiniteScroll,
    infiniteScrollChunkSize,
    controlsDisabled,
    motelyBenchRunning,
    pull,
    rows.length,
  ])

  const showMotelyBench = Boolean(onMotelyBenchTwoK && onMotelyBenchSizes)

  return (
    <div style={s.root}>
      <button
        type="button"
        onClick={onBack}
        style={{
          ...s.btn,
          background: '#222',
          borderColor: '#444',
          color: '#aaa',
          marginBottom: 16,
        }}
      >
        ← Back
      </button>

      <h1 style={s.h1}>{title}</h1>
      <p style={s.sub}>{subtitle}</p>

      {engineLoading && (
        <p style={{ ...s.meta, marginTop: 0, color: '#7a8a9a' }}>
          Loading C# / .NET WASM (first time can take a few seconds)…
        </p>
      )}

      <div style={s.row}>
        <span style={s.label}>Seed</span>
        <input
          style={s.input}
          value={seed}
          onChange={(e) => setSeed(e.target.value)}
          spellCheck={false}
        />
      </div>
      <div style={s.row}>
        <span style={s.label}>Deck</span>
        <select style={s.select} value={deck} onChange={(e) => setDeck(e.target.value as ShopDeck)}>
          {SHOP_DECKS.map((d) => (
            <option key={d} value={d}>
              {d}
            </option>
          ))}
        </select>
        <span style={s.label}>Stake</span>
        <select
          style={s.select}
          value={stake}
          onChange={(e) => setStake(e.target.value as ShopStake)}
        >
          {SHOP_STAKES.map((st) => (
            <option key={st} value={st}>
              {st}
            </option>
          ))}
        </select>
        <span style={s.label}>Ante</span>
        <input
          style={{ ...s.input, width: 72, minWidth: 72 }}
          type="number"
          min={1}
          max={99}
          value={ante}
          onChange={(e) => setAnte(Math.max(1, Math.min(99, Number(e.target.value) || 1)))}
        />
      </div>

      <div style={s.row}>
        <button
          type="button"
          style={{ ...s.btn, ...s.btnMuted }}
          onClick={resetStream}
          disabled={engineLoading}
        >
          Reset stream
        </button>
        <button type="button" style={s.btn} onClick={() => pull(25)} disabled={controlsDisabled}>
          +25 items
        </button>
        <button type="button" style={s.btn} onClick={() => pull(100)} disabled={controlsDisabled}>
          +100 items
        </button>
        <button type="button" style={s.btn} onClick={() => pull(500)} disabled={controlsDisabled}>
          +500 items
        </button>
        <button type="button" style={s.btn} onClick={() => pull(1000)} disabled={controlsDisabled}>
          +1K items
        </button>
        <span style={s.label}>
          pulled {rows.length}
          {streamReady ? ` · next index ${rows.length}` : ''}
        </span>
        <button
          type="button"
          style={{ ...s.btn, ...s.btnMuted }}
          onClick={() => {
            copyDebug()
          }}
          disabled={!streamReady}
        >
          {copyDebugLabel}
        </button>
      </div>

      {showMotelyBench && (
        <div style={{ ...s.row, alignItems: 'flex-start' }}>
          <span style={s.label}>WASM bench (silent drain, then stream reset)</span>
          <button
            type="button"
            style={{ ...s.btn, ...s.btnMuted }}
            onClick={() => onMotelyBenchTwoK?.()}
            disabled={controlsDisabled || motelyBenchRunning}
          >
            Time 1K + 1K (0–999 → 1000–1999)
          </button>
          <button
            type="button"
            style={s.btn}
            onClick={() => onMotelyBenchSizes?.()}
            disabled={controlsDisabled || motelyBenchRunning}
          >
            Time 1K → 10K → 100K → 1M
          </button>
          {motelyBenchRunning && <span style={s.label}>Running…</span>}
        </div>
      )}

      {(motelyBenchTwoK || motelyBenchSizes) && (
        <p style={{ ...s.meta, marginTop: 0, marginBottom: 12 }}>
          {motelyBenchTwoK && (
            <>
              Two 1K slices: first{' '}
              <code style={{ color: '#666' }}>{motelyBenchTwoK.first1kMs}ms</code> (items 0–999) ·
              second{' '}
              <code style={{ color: '#666' }}>{motelyBenchTwoK.second1kMs}ms</code> (1000–1999)
              <br />
            </>
          )}
          {motelyBenchSizes && (
            <>
              Batch drains: 1K{' '}
              <code style={{ color: '#666' }}>{motelyBenchSizes.k1}ms</code> · 10K{' '}
              <code style={{ color: '#666' }}>{motelyBenchSizes.k10}ms</code> · 100K{' '}
              <code style={{ color: '#666' }}>{motelyBenchSizes.k100}ms</code> · 1M{' '}
              <code style={{ color: '#666' }}>{motelyBenchSizes.k1m}ms</code>
            </>
          )}
        </p>
      )}

      {cacheMetaTs && streamReady && (
        <p style={{ ...s.meta, marginTop: 0, marginBottom: 12 }}>
          RNG cache: <code style={{ color: '#666' }}>{cacheMetaTs.cursorCount}</code> queue cursors
          (each a <code style={{ color: '#666' }}>number</code> / double) · first pack flag{' '}
          <code style={{ color: '#666' }}>{String(cacheMetaTs.generatedFirstPack)}</code>
        </p>
      )}

      {cacheMetaMotely && streamReady && (
        <p style={{ ...s.meta, marginTop: 0, marginBottom: 12 }}>
          Motely <code style={{ color: '#666' }}>{cacheMetaMotely.wasmVersion}</code> · live shop
          stream for ante <code style={{ color: '#666' }}>{ante}</code> (
          <code style={{ color: '#666' }}>BeginShopStream</code> +{' '}
          <code style={{ color: '#666' }}>GetNextShopItem</code>, same stack as{' '}
          <code style={{ color: '#666' }}>SeedRouterTests</code>) · pulled{' '}
          <code style={{ color: '#666' }}>{cacheMetaMotely.revealedInQueue}</code>
          {cacheMetaMotely.lastPullMs != null && cacheMetaMotely.lastPullMs >= 0 ? (
            <>
              {' '}
              · last +N: <code style={{ color: '#666' }}>{cacheMetaMotely.lastPullMs}ms</code>
            </>
          ) : null}
        </p>
      )}

      {infiniteScroll && streamReady && !engineLoading && (
        <p style={{ ...s.meta, marginTop: 0, marginBottom: 8 }}>
          Infinite scroll: virtualized list — scroll down to load +{infiniteScrollChunkSize} near the
          end (same behavior TS + Motely).
        </p>
      )}

      {streamError && <p style={s.err}>{streamError}</p>}

      <div ref={scrollRef} style={s.shopScroll} onScroll={onScroll}>
        {rows.length === 0 ? (
          <p style={{ ...s.meta, margin: '12px 16px' }}>
            No rows yet — use +N or scroll the empty pane (infinite scroll pulls when the sentinel
            hits the viewport).
          </p>
        ) : null}
        <div style={{ height: padTop }} aria-hidden />
        <ul style={{ ...s.shop, listStylePosition: 'outside' }}>
          {rows.slice(first, last).map((r) => (
            <li
              key={r.index}
              style={{
                height: SHOP_ROW_PX,
                minHeight: SHOP_ROW_PX,
                lineHeight: `${SHOP_ROW_PX}px`,
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              }}
            >
              <span style={{ color: '#6a7a8a' }}>#{r.index}</span>{' '}
              <code style={{ color: '#5a6a7a' }}>{r.type}</code>{' '}
              <span style={{ color: '#9a9a9a' }}>{r.name}</span>
              {r.value !== undefined ? (
                <>
                  {' '}
                  <code style={{ color: '#4a6a7a' }} title="packed MotelyItem bits">
                    {r.value}
                  </code>
                </>
              ) : null}
            </li>
          ))}
        </ul>
        <div style={{ height: padBottom }} aria-hidden />
        <div
          ref={sentinelRef}
          style={{
            height: 1,
            width: '100%',
            flexShrink: 0,
            pointerEvents: 'none',
          }}
        />
      </div>

      <p style={s.meta}>{footerNote}</p>
    </div>
  )
}
