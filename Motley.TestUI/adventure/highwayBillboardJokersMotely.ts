'use client'

import { MotelyWasm } from 'motely-wasm'
import {
  findJokerByDisplayName,
  type BalatroJokerCenter,
} from '../balatro/spriteAtlas/jokerRegistry'
import { ensureWasmRuntime } from '../motely-ui/motelyWasmRuntime'
import type { SeedAnalysisInfo } from '../motely-ui/motelySeedAnalysis'
import { loadHighwayBillboardJokers, type HighwayBillboardResult } from './highwayBillboardJokers'

const MAX_BILLBOARDS = 96

/**
 * Highway billboards: best-effort glue around Motely’s JS/WASM surface — not authoritative Balatro
 * semantics. Prefer `analyzeSeed` ante‑1 `shopQueue` order when it works, then top up from the TS
 * stream so the road stays full when the snapshot runs out.
 */
export async function loadHighwayBillboardJokersMotely(
  seed: string,
  deck: string,
  stake: string
): Promise<HighwayBillboardResult> {
  const ts = loadHighwayBillboardJokers(seed, deck, stake)

  try {
    const { instanceId } = await ensureWasmRuntime()
    const json = await MotelyWasm.MotelyWasmBackend.analyzeSeed(
      instanceId,
      seed.trim() || 'BALATRO1',
      deck,
      stake
    )
    const result = JSON.parse(json) as SeedAnalysisInfo
    if (result.error) {
      return {
        jokers: ts.jokers,
        note: ts.note ?? `Motely: ${result.error} (using TS stream).`,
      }
    }

    const ante1 = result.antes.find((a) => a.ante === 1)?.shopQueue ?? []
    const out: BalatroJokerCenter[] = []
    const seen = new Set<string>()

    for (const item of ante1) {
      if (out.length >= MAX_BILLBOARDS) break
      const c = findJokerByDisplayName(item.name)
      if (c && !seen.has(c.key)) {
        seen.add(c.key)
        out.push(c)
      }
    }

    for (const j of ts.jokers) {
      if (out.length >= MAX_BILLBOARDS) break
      if (!seen.has(j.key)) {
        seen.add(j.key)
        out.push(j)
      }
    }

    if (out.length === 0) {
      return ts
    }

    return {
      jokers: out,
      note: ante1.length > 0 ? null : ts.note,
    }
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e)
    return {
      jokers: ts.jokers,
      note: ts.note ? `${ts.note} (${msg})` : `Motely unavailable: ${msg} (TS stream).`,
    }
  }
}
