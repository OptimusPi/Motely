"use client";

/* eslint-disable @typescript-eslint/no-explicit-any */
import { useEffect, useState } from "react";

type WasmPhase = "idle" | "loading" | "ready" | "error";

let bootPromise: Promise<unknown> | null = null;
let cachedApi: Record<string, unknown> | null = null;

async function bootWasm(): Promise<Record<string, unknown>> {
  if (cachedApi) return cachedApi;
  if (!bootPromise) {
    bootPromise = (async () => {
      const mod = await import(/* webpackIgnore: true */ "/motely-framework/index.mjs" as any);
      await (mod.boot ?? mod.default?.boot)();
      const root = mod.Motely as Record<string, Record<string, unknown>> | undefined;
      const api = (mod.MotelyWasm ?? root?.MotelyWasm ?? root?.Executors?.MotelyWasm) as Record<string, unknown> | undefined;
      if (!api) throw new Error("MotelyWasm namespace not found");
      cachedApi = api;
      return api;
    })();
  }
  return bootPromise as Promise<Record<string, unknown>>;
}

export function getWasmApi(): Record<string, unknown> | null {
  return cachedApi;
}

export { bootWasm };

export function MotelyWasmStatus() {
  const [phase, setPhase] = useState<WasmPhase>("idle");
  const [detail, setDetail] = useState<string>("");

  useEffect(() => {
    setPhase("loading");
    bootWasm()
      .then((api) => {
        const version = typeof api.getVersion === "function" ? (api.getVersion() as string) : "?";
        setPhase("ready");
        setDetail(`motely-wasm v${version}`);
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
