'use client'

import { Motely } from 'motely-wasm'

const api = Motely.Executors.MotelyWasm

let bootPromise: Promise<void> | null = null
let analyzeInstanceId: number | null = null

async function bootEmbedded(): Promise<void> {
  const { default: mod } = await import('motely-wasm')
  await mod.boot()
}

/** Boots the LLVM WASM bundle once (embedded dotnet when `root: null`). */
export function ensureWasmRuntime(): Promise<void> {
  bootPromise ??= bootEmbedded()
  return bootPromise
}

/** Shared `createInstance` id for `analyzeSeed` (ante snapshots, highway billboards). */
export async function ensureAnalyzeInstance(): Promise<number> {
  await ensureWasmRuntime()
  if (analyzeInstanceId == null) {
    const id = api.createInstance()
    analyzeInstanceId = id
  }
  return analyzeInstanceId
}

export type MotelySearchContext = {
  beginShopStream(ante: number): void
  getNextShopItem(): unknown
  dispose(): void
}

/**
 * Session-scoped shop stream; maps numeric session interop to the object API the UI expects.
 */
export function createMotelySearchContext(seed: string, deck: string, stake: string): MotelySearchContext {
  const sessionId = api.createSingleSearchContext(seed, deck, stake)
  return {
    beginShopStream(ante: number) {
      api.beginShopStream(sessionId, ante)
    },
    getNextShopItem() {
      return JSON.parse(api.getNextShopItemJson(sessionId)) as unknown
    },
    dispose() {
      api.disposeSingleSearchContext(sessionId)
    },
  }
}

export function getMotelyVersion(): string {
  return api.getVersion()
}

export async function analyzeSeedJson(seed: string, deck: string, stake: string): Promise<string> {
  const instanceId = await ensureAnalyzeInstance()
  return api.analyzeSeed(instanceId, seed, deck, stake)
}
