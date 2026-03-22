'use client'

import { MotelyWasm } from 'motely-wasm'
import { bootMotelyEmbedded } from './motelyWasmBoot'

let wasmRuntime: Promise<{ instanceId: number }> | null = null

/** Shared WASM boot (consumer glue); keep fallbacks when calling sites can’t rely on it. */
export function ensureWasmRuntime(): Promise<{ instanceId: number }> {
  wasmRuntime ??= (async () => {
    await bootMotelyEmbedded()
    const instanceId = MotelyWasm.MotelyWasmBackend.createInstance()
    return { instanceId }
  })()
  return wasmRuntime
}
