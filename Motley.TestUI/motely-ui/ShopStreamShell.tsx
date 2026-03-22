import type { ShopDeck, ShopStake, ShopStreamRow } from './shopStreamShared'
import { SHOP_DECKS, SHOP_STAKES, shopStreamStyles as s } from './shopStreamShared'

export type ShopStreamCacheMetaTs = Readonly<{
  cursorCount: number
  generatedFirstPack: boolean
}>

export type ShopStreamCacheMetaMotely = Readonly<{
  cursorCount: number
  wasmVersion: string
  revealedInQueue: number
}>

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
}: Props) {
  const controlsDisabled = !streamReady || engineLoading

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
        <span style={s.label}>
          pulled {rows.length}
          {streamReady ? ` · next index ${rows.length}` : ''}
        </span>
        <button
          type="button"
          style={{ ...s.btn, ...s.btnMuted }}
          onClick={() => void copyDebug()}
          disabled={!streamReady}
        >
          {copyDebugLabel}
        </button>
      </div>

      {cacheMetaTs && streamReady && (
        <p style={{ ...s.meta, marginTop: 0, marginBottom: 12 }}>
          RNG cache: <code style={{ color: '#666' }}>{cacheMetaTs.cursorCount}</code> queue cursors
          (each a <code style={{ color: '#666' }}>number</code> / double) · first pack flag{' '}
          <code style={{ color: '#666' }}>{String(cacheMetaTs.generatedFirstPack)}</code>
        </p>
      )}

      {cacheMetaMotely && streamReady && (
        <p style={{ ...s.meta, marginTop: 0, marginBottom: 12 }}>
          Motely <code style={{ color: '#666' }}>{cacheMetaMotely.wasmVersion}</code> · ante shop
          snapshot <code style={{ color: '#666' }}>{cacheMetaMotely.cursorCount}</code> slots ·
          revealed{' '}
          <code style={{ color: '#666' }}>
            {cacheMetaMotely.revealedInQueue}/{cacheMetaMotely.cursorCount}
          </code>{' '}
          (C# <code style={{ color: '#666' }}>analyzeSeed</code> — fixed queue per ante, not an
          infinite stream)
        </p>
      )}

      {streamError && <p style={s.err}>{streamError}</p>}

      {rows.length > 0 && (
        <ul style={s.shop}>
          {rows.map((r) => (
            <li key={r.index}>
              <span style={{ color: '#6a7a8a' }}>#{r.index}</span>{' '}
              <code style={{ color: '#5a6a7a' }}>{r.type}</code>{' '}
              <span style={{ color: '#9a9a9a' }}>{r.name}</span>
            </li>
          ))}
        </ul>
      )}

      <p style={s.meta}>{footerNote}</p>
    </div>
  )
}
