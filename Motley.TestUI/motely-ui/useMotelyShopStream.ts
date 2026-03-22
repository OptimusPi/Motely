import { useCallback, useEffect, useRef, useState } from 'react'
import { boot, MotelyWasm } from 'motely-wasm'
import type { SeedAnalysisInfo } from './motelySeedAnalysis'

let wasmRuntime: Promise<{ instanceId: number }> | null = null

function ensureWasmRuntime(): Promise<{ instanceId: number }> {
  wasmRuntime ??= (async () => {
    await boot()
    const instanceId = MotelyWasm.MotelyWasmBackend.createInstance()
    return { instanceId }
  })()
  return wasmRuntime
}

export function useMotelyShopStream(
  seed: string,
  deck: string,
  stake: string,
  ante: number
) {
  const [rows, setRows] = useState<{ index: number; type: string; name: string }[]>([])
  const [streamError, setStreamError] = useState<string | null>(null)
  const [streamReady, setStreamReady] = useState(false)
  const [engineLoading, setEngineLoading] = useState(true)
  const [wasmVersion, setWasmVersion] = useState<string | null>(null)
  const [analysis, setAnalysis] = useState<SeedAnalysisInfo | null>(null)
  const [revealedInQueue, setRevealedInQueue] = useState(0)

  const analysisRef = useRef<SeedAnalysisInfo | null>(null)
  analysisRef.current = analysis

  const revealedRef = useRef(0)
  const analyzeGenRef = useRef(0)

  const runAnalyze = useCallback(async () => {
    const gen = ++analyzeGenRef.current
    setStreamError(null)
    setEngineLoading(true)
    setStreamReady(false)
    setAnalysis(null)
    setRows([])
    setRevealedInQueue(0)
    revealedRef.current = 0
    try {
      const { instanceId } = await ensureWasmRuntime()
      const [ver, json] = await Promise.all([
        MotelyWasm.MotelyWasmBackend.getVersion().catch(() => '?'),
        MotelyWasm.MotelyWasmBackend.analyzeSeed(
          instanceId,
          seed.trim() || 'BALATRO1',
          deck,
          stake
        ),
      ])
      if (gen !== analyzeGenRef.current) return
      setWasmVersion(ver)
      const result = JSON.parse(json) as SeedAnalysisInfo
      if (result.error) {
        throw new Error(result.error)
      }
      setAnalysis(result)
      setStreamReady(true)
    } catch (e: unknown) {
      if (gen !== analyzeGenRef.current) return
      setAnalysis(null)
      setStreamReady(false)
      setStreamError(e instanceof Error ? e.message : String(e))
    } finally {
      if (gen === analyzeGenRef.current) setEngineLoading(false)
    }
  }, [deck, seed, stake])

  useEffect(() => {
    void runAnalyze()
  }, [runAnalyze])

  useEffect(() => {
    setRows([])
    setRevealedInQueue(0)
    revealedRef.current = 0
  }, [ante])

  const queueForAnte = analysis?.antes.find((a) => a.ante === ante)?.shopQueue ?? null
  const queueLen = queueForAnte?.length ?? 0

  const pull = useCallback(
    (n: number) => {
      setStreamError(null)
      const a = analysisRef.current
      if (!a || n <= 0) return
      const q = a.antes.find((x) => x.ante === ante)?.shopQueue
      if (!q?.length) {
        setStreamError(`No shopQueue for ante ${ante} in this analyzeSeed snapshot.`)
        return
      }
      const start = revealedRef.current
      const slice = q.slice(start, start + n)
      if (slice.length === 0) return
      revealedRef.current = start + slice.length
      setRevealedInQueue(revealedRef.current)
      setRows((prev) => {
        const base = prev.length
        const add = slice.map((item, i) => ({
          index: base + i,
          type: item.id || 'ShopItem',
          name: item.name,
        }))
        return [...prev, ...add]
      })
    },
    [ante]
  )

  const resetStream = useCallback(() => {
    void runAnalyze()
  }, [runAnalyze])

  const copyDebug = useCallback(() => {
    const a = analysisRef.current
    if (!a) return
    const anteBlock = a.antes.find((x) => x.ante === ante)
    const payload = anteBlock ? { seed: a.seed, deck: a.deck, stake: a.stake, ante: anteBlock } : a
    void navigator.clipboard.writeText(JSON.stringify(payload, null, 2)).catch(() => {})
  }, [ante])

  return {
    rows,
    streamError,
    streamReady: streamReady && !engineLoading,
    engineLoading,
    cacheMeta:
      streamReady && wasmVersion ? { cursorCount: queueLen, wasmVersion, revealedInQueue } : null,
    resetStream,
    pull,
    copyDebug,
    copyDebugLabel: 'Copy ante JSON (Motely)',
  }
}
