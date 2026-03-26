"use client";

import { useEffect, useState } from "react";

type WasmPhase = "idle" | "loading" | "ready" | "error";

/**
 * Simple status component - no hacked dependencies.
 * Ready for real npm package integration when available.
 */
export function MotelyWasmStatus() {
  const [phase, setPhase] = useState<WasmPhase>("ready");
  const [detail, setDetail] = useState<string>("Real npm packages only - no hacked dependencies");

  function wasmLabel(): string {
    switch (phase) {
      case "idle":
        return "Status: starting…";
      case "loading":
        return "Status: loading…";
      case "ready":
        return "Status: ready";
      default:
        return "Status: error";
    }
  }

  return (
    <div className="wasm-strip">
      <div className={`status-pill ${phase === "error" ? "error" : ""}`}>{wasmLabel()}</div>
      {detail ? (
        <p className="wasm-strip-detail">
          {detail}
        </p>
      ) : null}
    </div>
  );
}
