import { Fragment, useCallback, useMemo, useState } from 'react'
import {
  SHOP_DECKS,
  SHOP_STAKES,
  shopStreamStyles as s,
  type ShopBenchRangeTimings,
  type ShopDeck,
  type ShopStake,
} from './shopStreamShared'
import { useMotelyShopStream } from './useMotelyShopStream'
import { useTsShopStream } from './useTsShopStream'

type Props = Readonly<{ onBack: () => void }>

type BenchComparison = Readonly<{
  ts: ShopBenchRangeTimings
  motely: ShopBenchRangeTimings
}>

function formatMs(value: number | null | undefined) {
  return typeof value === 'number' ? `${value}ms` : '—'
}

function formatDelta(tsValue: number | null | undefined, motelyValue: number | null | undefined) {
  if (typeof tsValue !== 'number' || typeof motelyValue !== 'number') return '—'
  const delta = motelyValue - tsValue
  return `${delta > 0 ? '+' : ''}${delta}ms`
}

export function ShopStreamBenchCompare({ onBack }: Props) {
  const [seed, setSeed] = useState('ALEEB')
  const [deck, setDeck] = useState<ShopDeck>('Red')
  const [stake, setStake] = useState<ShopStake>('White')
  const [ante, setAnte] = useState(1)
  const [comparison, setComparison] = useState<BenchComparison | null>(null)
  const [running, setRunning] = useState(false)

  const ts = useTsShopStream(seed, deck, stake, ante)
  const motely = useMotelyShopStream(seed, deck, stake, ante)

  const canRun =
    ts.streamReady &&
    motely.streamReady &&
    !running &&
    !ts.benchRunning &&
    !motely.motelyBenchRunning

  const runComparison = useCallback(async () => {
    if (!canRun) return
    setRunning(true)
    setComparison(null)
    try {
      const tsTimings = await ts.runBenchRanges()
      const motelyTimings = motely.runBenchRanges()
      if (tsTimings && motelyTimings) {
        setComparison({ ts: tsTimings, motely: motelyTimings })
      }
    } finally {
      setRunning(false)
    }
  }, [canRun, motely, ts])

  const resetBoth = useCallback(() => {
    setComparison(null)
    ts.resetStream()
    motely.resetStream()
  }, [motely, ts])

  const tsTimings = comparison?.ts ?? ts.benchRangeTimings
  const motelyTimings = comparison?.motely ?? motely.benchRangeTimings
  const benchRows = useMemo(
    () => [
      {
        label: '0-99',
        ts: tsTimings?.range0To99Ms,
        motely: motelyTimings?.range0To99Ms,
      },
      {
        label: '100-9999',
        ts: tsTimings?.range100To9999Ms,
        motely: motelyTimings?.range100To9999Ms,
      },
      {
        label: '10000-10001',
        ts: tsTimings?.range10000To10001Ms,
        motely: motelyTimings?.range10000To10001Ms,
      },
    ],
    [motelyTimings, tsTimings]
  )

  const errorText = ts.streamError ?? motely.streamError

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

      <h1 style={s.h1}>Shop stream benchmark — TS vs Motely</h1>
      <p style={s.sub}>
        Exact comparison page for the existing TypeScript shop stream and the Motely WASM shop
        stream. Defaults to seed ALEEB, deck Red, stake White, ante 1.
      </p>

      <div style={s.row}>
        <span style={s.label}>Seed</span>
        <input
          style={s.input}
          value={seed}
          onChange={(e) => setSeed(e.target.value)}
          spellCheck={false}
        />
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
          style={s.btn}
          onClick={() => void runComparison()}
          disabled={!canRun}
        >
          {running ? 'Running…' : 'Run exact range benchmark'}
        </button>
        <button
          type="button"
          style={{ ...s.btn, ...s.btnMuted }}
          onClick={resetBoth}
          disabled={running}
        >
          Reset TS + Motely
        </button>
        <span style={s.label}>
          TS {ts.streamReady ? 'ready' : 'loading'} · Motely {motely.streamReady ? 'ready' : 'loading'}
        </span>
      </div>

      {errorText ? <p style={s.err}>{errorText}</p> : null}

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'minmax(120px, 160px) minmax(140px, 1fr) minmax(140px, 1fr) minmax(120px, 160px)',
          gap: 10,
          alignItems: 'center',
          border: '1px solid #222',
          borderRadius: 4,
          padding: 14,
          background: '#0a0a0e',
          maxWidth: 920,
        }}
      >
        <div style={{ color: '#888', fontSize: 11, textTransform: 'uppercase' }}>Range</div>
        <div style={{ color: '#888', fontSize: 11, textTransform: 'uppercase' }}>TypeScript</div>
        <div style={{ color: '#888', fontSize: 11, textTransform: 'uppercase' }}>Motely WASM</div>
        <div style={{ color: '#888', fontSize: 11, textTransform: 'uppercase' }}>Delta</div>
        {benchRows.map((row) => (
          <Fragment key={row.label}>
            <div style={{ color: '#ddd' }}>
              {row.label}
            </div>
            <div style={{ color: '#8fd4a8' }}>
              {formatMs(row.ts ?? null)}
            </div>
            <div style={{ color: '#8ac7ff' }}>
              {formatMs(row.motely ?? null)}
            </div>
            <div style={{ color: '#c4b8a8' }}>
              {formatDelta(row.ts ?? null, row.motely ?? null)}
            </div>
          </Fragment>
        ))}
      </div>

      <p style={s.meta}>
        TS benchmark uses a fresh game instance and drains exactly 100, then 9,900, then 2 shop items.
        Motely benchmark uses the live WASM context, silently drains the same ranges, then resets the
        shop stream.
      </p>
    </div>
  )
}
