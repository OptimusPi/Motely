"use client";

import { useEffect, useState } from "react";

type WasmPhase = "idle" | "loading" | "ready" | "error";

/**
 * Local proof: loads the `motely` package (Bootsharp / NativeAOT-LLVM WASM) in the browser only.
 * Build artifacts first: from repo root, `npm run build:motely` in this app, or `npm run build` in `../Motely`.
 */
export function MotelyWasmStatus() {
  const [phase, setPhase] = useState<WasmPhase>("idle");
  const [bootMs, setBootMs] = useState<number | null>(null);
  const [detail, setDetail] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const started = performance.now();

    (async () => {
      setPhase("loading");
      setDetail(null);
      try {
        const mod = await import("motely/browser");
        const m = mod.default;
        await m.boot();
        const elapsed = Math.round(performance.now() - started);
        if (cancelled) return;

        const st = m.getStatus();
        if (st !== m.BootStatus.Booted) {
          throw new Error(
            `Unexpected BootStatus: ${st} (expected Booted=${m.BootStatus.Booted}).`
          );
        }

        setBootMs(elapsed);
        setPhase("ready");
        setDetail("NativeAOT-LLVM WASM booted; Motely.MotelyUI bindings are available.");
      } catch (e) {
        if (cancelled) return;
        const message = e instanceof Error ? e.message : String(e);
        setPhase("error");
        setBootMs(null);
        setDetail(message);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  function wasmLabel(): string {
    switch (phase) {
      case "idle":
        return "WASM: starting…";
      case "loading":
        return "WASM: booting…";
      case "ready":
        return `WASM: ready (${bootMs ?? "?"} ms)`;
      default:
        return "WASM: error";
    }
  }

  return (
    <div className="wasm-strip">
      <div className={`status-pill ${phase === "error" ? "error" : ""}`}>{wasmLabel()}</div>
      {detail ? (
        <p className="wasm-strip-detail">
          {phase === "error" ? (
            <>
              <strong>Fix:</strong> run <code>npm run build:motely</code> from this folder so{" "}
              <code>../Motely/dist/wasm</code> exists, then refresh. — {detail}
            </>
          ) : (
            detail
          )}
        </p>
      ) : null}
    </div>
  );
}
