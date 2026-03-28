"use client";

import { useEffect, useState } from "react";
import type MotelyModule from "motely-wasm";

type WasmPhase = "idle" | "loading" | "ready" | "error";

type MotelyResultEventHub = {
  subscribe: (handler: (seed: string, score: number) => void) => void;
  unsubscribe: (handler: (seed: string, score: number) => void) => void;
};

type MotelyProgressEventHub = {
  subscribe: (handler: (searched: bigint, found: bigint, elapsed: bigint) => void) => void;
  unsubscribe: (handler: (searched: bigint, found: bigint, elapsed: bigint) => void) => void;
};

type MotelyApi = {
  getVersion?: () => string;
  validateJaml?: (jaml: string) => string | null;
  runSearch?: (jaml: string, threads: number, batchCharCount: number, startBatch: number, endBatch: number) => string;
  onResult?: MotelyResultEventHub;
  onProgress?: MotelyProgressEventHub;
};

type BootResult = {
  default: typeof MotelyModule;
  api: MotelyApi;
};

let bootPromise: Promise<BootResult> | null = null;
let cachedApi: MotelyApi | null = null;

async function bootWasm(): Promise<MotelyApi> {
  if (cachedApi) return cachedApi;
  if (!bootPromise) {
    bootPromise = (async () => {
      const mod = await import("motely-wasm");
      await mod.default.boot();
      const root = (mod as unknown as { Motely?: { MotelyWasm?: MotelyApi } }).Motely;
      const api = root?.MotelyWasm;
      if (!api) throw new Error("MotelyWasm namespace not found");
      cachedApi = api;
      return { default: mod.default, api };
    })();
  }
  const result = await bootPromise;
  return result.api;
}

export function getWasmApi(): MotelyApi | null {
  return cachedApi;
}

export { bootWasm };

function runtimeBuildConfig(mod: typeof MotelyModule): { wasmEnableThreads?: boolean; wasmEnableSIMD?: boolean } {
  return (mod.dotnet as Record<string, unknown>)?.buildConfig as Record<string, boolean> ?? {};
}

export async function getWasmCapabilities() {
  const result = await bootPromise;
  if (!result) return null;
  const cfg = runtimeBuildConfig(result.default);
  return {
    version: typeof result.api.getVersion === "function" ? result.api.getVersion() : "?",
    simd: cfg.wasmEnableSIMD ?? false,
    threads: cfg.wasmEnableThreads ?? false,
  };
}

export function MotelyWasmStatus() {
  const [phase, setPhase] = useState<WasmPhase>("idle");
  const [detail, setDetail] = useState<string>("");

  useEffect(() => {
    setPhase("loading");
    bootWasm()
      .then(async (api) => {
        const version = typeof api.getVersion === "function" ? (api.getVersion() as string) : "?";
        const caps = await getWasmCapabilities();
        setPhase("ready");
        setDetail(
          `motely-wasm v${version} · ${caps?.simd ? "SIMD" : "No SIMD"} · ${caps?.threads ? "Threads" : "Single-thread"}`
        );
      })
      .catch((err) => {
        setPhase("error");
        setDetail(err instanceof Error ? err.message : "Boot failed");
      });
  }, []);

  return (
    <div className="wasm-strip">
      <div className={`status-pill ${phase === "error" ? "error" : ""}`}>
        {phase === "idle" && "WASM: starting..."}
        {phase === "loading" && "WASM: booting..."}
        {phase === "ready" && "WASM: ready"}
        {phase === "error" && "WASM: error"}
      </div>
      {detail ? <p className="wasm-strip-detail">{detail}</p> : null}
    </div>
  );
}
