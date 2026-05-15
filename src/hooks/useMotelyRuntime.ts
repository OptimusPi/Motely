"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import bootsharp from "motely-wasm";

export type MotelyRuntimeStatus = "idle" | "booting" | "ready" | "error";

export interface UseMotelyRuntimeState {
  status: MotelyRuntimeStatus;
  ready: boolean;
  error: string | null;
  fsReady: boolean;
  fsError: string | null;
  ensureReady: () => Promise<void>;
}

function currentStatus(): MotelyRuntimeStatus {
  switch (bootsharp.getStatus()) {
    case bootsharp.BootStatus.Booted:
      return "ready";
    case bootsharp.BootStatus.Booting:
      return "booting";
    default:
      return "idle";
  }
}

export function useMotelyRuntime(): UseMotelyRuntimeState {
  const [status, setStatus] = useState<MotelyRuntimeStatus>(() => currentStatus());
  const [error, setError] = useState<string | null>(null);
  const ensureReady = useCallback(async () => {
    try {
      setError(null);
      setStatus(currentStatus());
      if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
        setStatus("booting");
        await bootsharp.boot("/motely-wasm/bin");
      }
      setStatus(currentStatus());
    } catch (err) {
      setStatus("error");
      setError(err instanceof Error ? err.message : String(err));
      throw err;
    }
  }, []);

  return useMemo(
    () => ({
      status,
      ready: status === "ready",
      error,
      fsReady: false,
      fsError: null,
      ensureReady,
    }),
    [error, ensureReady, status],
  );
}

export function useMotelyRuntimeOwner(): void {
  const { ensureReady } = useMotelyRuntime();

  useEffect(() => {
    void ensureReady();
  }, [ensureReady]);
}
