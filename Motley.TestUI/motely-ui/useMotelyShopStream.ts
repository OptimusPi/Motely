import { useCallback, useEffect, useRef, useState } from 'react'
import * as motelyWasmMod from 'motely-wasm'
import { MotelyWasm } from 'motely-wasm'
import type {
  MotelyBenchSizes,
  MotelyBenchTwoK,
  ShopBenchRangeTimings,
  ShopStreamCacheMetaMotely,
  ShopStreamRow,
} from './shopStreamShared'

function getBoot(): () => Promise<unknown> {
  const m = motelyWasmMod as Record<string, unknown>
  if (typeof m.boot === 'function') return m.boot as () => Promise<unknown>
  const d = m.default as Record<string, unknown> | undefined
  if (d && typeof d.boot === 'function') return d.boot as () => Promise<unknown>
  throw new Error('motely-wasm: expected boot() on module or default.boot')
}

let wasmBoot: Promise<void> | null = null

function ensureWasmBooted(): Promise<void> {
  wasmBoot ??= getBoot()().then(() => undefined)
  return wasmBoot
}

function disposeInteropSession(s: unknown) {
  if (s === null || s === undefined) return
  const d = s as { dispose?: () => void }
  if (typeof d.dispose !== 'function') return
  try {
    d.dispose()
  } catch {
    /* host may already tear down */
  }
}

/** <code>ShopItemDto</code> from WASM: same as analyzeSeed shop queue (camelCase JSON). */
function shopItemDtoToRow(dto: unknown): Pick<ShopStreamRow, 'type' | 'name' | 'value'> {
  const o = dto as Record<string, unknown>
  const id = o.id ?? o.Id
  const name = o.name ?? o.Name
  const rawVal = o.value ?? o.Value
  const typeStr = typeof id === 'string' && id.length > 0 ? id : 'ShopItem'
  const nameStr =
    typeof name === 'string' && name.length > 0 ? name : typeStr
  const value =
    typeof rawVal === 'number' && Number.isFinite(rawVal) ? rawVal : undefined
  return value !== undefined
    ? { type: typeStr, name: nameStr, value }
    : { type: typeStr, name: nameStr }
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

export function useMotelyShopStream(
  seed: string,
  deck: string,
  stake: string,
  ante: number
) {
  const [rows, setRows] = useState<ShopStreamRow[]>([])
  const [streamError, setStreamError] = useState<string | null>(null)
  const [streamReady, setStreamReady] = useState(false)
  const [engineLoading, setEngineLoading] = useState(true)
  const [wasmVersion, setWasmVersion] = useState<string | null>(null)
  const [lastPullMs, setLastPullMs] = useState<number | null>(null)
  const [motelyBenchTwoK, setMotelyBenchTwoK] = useState<MotelyBenchTwoK | null>(null)
  const [motelyBenchSizes, setMotelyBenchSizes] = useState<MotelyBenchSizes | null>(null)
  const [benchRangeTimings, setBenchRangeTimings] = useState<ShopBenchRangeTimings | null>(null)
  const [motelyBenchRunning, setMotelyBenchRunning] = useState(false)

  const sessionRef = useRef<ReturnType<
    typeof MotelyWasm.MotelyBrowserApi.createSingleSearchContext
  > | null>(null)
  const rowsRef = useRef<ShopStreamRow[]>([])
  const openGenRef = useRef(0)

  useEffect(() => {
    const gen = ++openGenRef.current
    setStreamError(null)
    setEngineLoading(true)
    setStreamReady(false)
    rowsRef.current = []
    setRows([])
    setLastPullMs(null)
    setMotelyBenchTwoK(null)
    setMotelyBenchSizes(null)
    setBenchRangeTimings(null)
    disposeInteropSession(sessionRef.current)
    sessionRef.current = null

    void (async () => {
      try {
        await ensureWasmBooted()
        if (gen !== openGenRef.current) return
        const ctx = MotelyWasm.MotelyBrowserApi.createSingleSearchContext(
          seed.trim() || 'BALATRO1',
          deck,
          stake
        )
        sessionRef.current = ctx
        const ver = await Promise.resolve(MotelyWasm.MotelyBrowserApi.getVersion())
        if (gen !== openGenRef.current) return
        setWasmVersion(ver)
        setStreamReady(true)
      } catch (e: unknown) {
        if (gen !== openGenRef.current) return
        setStreamReady(false)
        setStreamError(e instanceof Error ? e.message : String(e))
      } finally {
        if (gen === openGenRef.current) setEngineLoading(false)
      }
    })()
  }, [deck, seed, stake])

  useEffect(() => {
    if (!streamReady) return
    const ctx = sessionRef.current
    if (!ctx) return
    rowsRef.current = []
    setRows([])
    setStreamError(null)
    setLastPullMs(null)
    setMotelyBenchTwoK(null)
    setMotelyBenchSizes(null)
    setBenchRangeTimings(null)
    try {
      ctx.beginShopStream(ante)
    } catch (e: unknown) {
      setStreamError(e instanceof Error ? e.message : String(e))
    }
  }, [ante, streamReady])

  const pull = useCallback((n: number) => {
    setStreamError(null)
    const ctx = sessionRef.current
    if (!ctx || n <= 0) return
    const t0 =
      typeof performance !== 'undefined' && performance.now ? performance.now() : Date.now()
    try {
      const base = rowsRef.current.length
      const add: ShopStreamRow[] = []
      for (let k = 0; k < n; k++) {
        const dto = ctx.getNextShopItem() as unknown
        add.push({ index: base + k, ...shopItemDtoToRow(dto) })
      }
      rowsRef.current = [...rowsRef.current, ...add]
      setRows([...rowsRef.current])
      const t1 =
        typeof performance !== 'undefined' && performance.now ? performance.now() : Date.now()
      setLastPullMs(Math.round(t1 - t0))
    } catch (e: unknown) {
      setStreamError(e instanceof Error ? e.message : String(e))
    }
  }, [])

  const resetStream = useCallback(() => {
    const gen = ++openGenRef.current
    setStreamError(null)
    setEngineLoading(true)
    setStreamReady(false)
    rowsRef.current = []
    setRows([])
    setLastPullMs(null)
    setMotelyBenchTwoK(null)
    setMotelyBenchSizes(null)
    disposeInteropSession(sessionRef.current)
    sessionRef.current = null

    void (async () => {
      try {
        await ensureWasmBooted()
        if (gen !== openGenRef.current) return
        const ctx = MotelyWasm.MotelyBrowserApi.createSingleSearchContext(
          seed.trim() || 'BALATRO1',
          deck,
          stake
        )
        sessionRef.current = ctx
        const ver = await Promise.resolve(MotelyWasm.MotelyBrowserApi.getVersion())
        if (gen !== openGenRef.current) return
        setWasmVersion(ver)
        setStreamReady(true)
      } catch (e: unknown) {
        if (gen !== openGenRef.current) return
        setStreamReady(false)
        setStreamError(e instanceof Error ? e.message : String(e))
      } finally {
        if (gen === openGenRef.current) setEngineLoading(false)
      }
    })()
  }, [deck, seed, stake])

  const benchLockRef = useRef(false)

  const resetShopStreamAfterSilentBench = useCallback(() => {
    const ctx = sessionRef.current
    if (!ctx) return
    ctx.beginShopStream(ante)
    rowsRef.current = []
    setRows([])
  }, [ante])

  const runMotelyBenchTwoK = useCallback(() => {
    const ctx = sessionRef.current
    if (!ctx || benchLockRef.current) return
    benchLockRef.current = true
    setMotelyBenchRunning(true)
    setStreamError(null)
    setMotelyBenchSizes(null)
    setBenchRangeTimings(null)
    try {
      const now =
        typeof performance !== 'undefined' && performance.now ? performance.now : Date.now
      const t0 = now()
      for (let i = 0; i < 1000; i++) ctx.getNextShopItem()
      const t1 = now()
      for (let i = 0; i < 1000; i++) ctx.getNextShopItem()
      const t2 = now()
      setMotelyBenchTwoK({
        first1kMs: Math.round(t1 - t0),
        second1kMs: Math.round(t2 - t1),
      })
      resetShopStreamAfterSilentBench()
    } catch (e: unknown) {
      setStreamError(e instanceof Error ? e.message : String(e))
    } finally {
      benchLockRef.current = false
      setMotelyBenchRunning(false)
    }
  }, [resetShopStreamAfterSilentBench])

  const runMotelyBenchSizes = useCallback(() => {
    const ctx = sessionRef.current
    if (!ctx || benchLockRef.current) return
    benchLockRef.current = true
    setMotelyBenchRunning(true)
    setStreamError(null)
    setMotelyBenchTwoK(null)
    setBenchRangeTimings(null)
    try {
      const now =
        typeof performance !== 'undefined' && performance.now ? performance.now : Date.now
      const timeN = (n: number) => {
        const t0 = now()
        for (let i = 0; i < n; i++) ctx.getNextShopItem()
        return Math.round(now() - t0)
      }
      setMotelyBenchSizes({
        k1: timeN(1_000),
        k10: timeN(10_000),
        k100: timeN(100_000),
        k1m: timeN(1_000_000),
      })
      resetShopStreamAfterSilentBench()
    } catch (e: unknown) {
      setStreamError(e instanceof Error ? e.message : String(e))
    } finally {
      benchLockRef.current = false
      setMotelyBenchRunning(false)
    }
  }, [resetShopStreamAfterSilentBench])

  const runBenchRanges = useCallback((): ShopBenchRangeTimings | null => {
    const ctx = sessionRef.current
    if (!ctx || benchLockRef.current) return null
    benchLockRef.current = true
    setMotelyBenchRunning(true)
    setStreamError(null)
    setMotelyBenchTwoK(null)
    setMotelyBenchSizes(null)
    try {
      const timings = measureShopBenchRanges(() => {
        ctx.getNextShopItem()
      })
      setBenchRangeTimings(timings)
      resetShopStreamAfterSilentBench()
      return timings
    } catch (e: unknown) {
      setStreamError(e instanceof Error ? e.message : String(e))
      return null
    } finally {
      benchLockRef.current = false
      setMotelyBenchRunning(false)
    }
  }, [resetShopStreamAfterSilentBench])

  const copyDebug = useCallback(() => {
    const ctx = sessionRef.current
    const payload = {
      seed: seed.trim() || 'BALATRO1',
      deck,
      stake,
      ante,
      rowCount: rows.length,
      rows,
      hasSession: Boolean(ctx),
    }
    void navigator.clipboard.writeText(JSON.stringify(payload, null, 2)).catch(() => { })
  }, [ante, deck, rows, seed, stake])

  const cacheMeta: ShopStreamCacheMetaMotely | null =
    streamReady && wasmVersion
      ? {
        wasmVersion,
        revealedInQueue: rows.length,
        infiniteShopStream: true,
        lastPullMs,
      }
      : null

  return {
    rows,
    streamError,
    streamReady: streamReady && !engineLoading,
    engineLoading,
    cacheMeta,
    resetStream,
    pull,
    copyDebug,
    copyDebugLabel: 'Copy stream JSON (Motely)',
    motelyBenchTwoK,
    motelyBenchSizes,
    benchRangeTimings,
    motelyBenchRunning,
    runMotelyBenchTwoK,
    runMotelyBenchSizes,
    runBenchRanges,
  }
}
