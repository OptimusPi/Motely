"use client";

import { useCallback, useEffect, useMemo, useSyncExternalStore } from "react";
import {
  ensureMotelyReady,
  getMotelyRuntimeSnapshot,
  subscribeMotelyRuntime,
  type MotelyRuntimeStatus,
} from "../motelyBoot.js";

export interface UseMotelyRuntimeState {
  status: MotelyRuntimeStatus;
  ready: boolean;
  error: string | null;
  fsReady: boolean;
  fsError: string | null;
  ensureReady: () => Promise<void>;
}

function formatError(error: unknown): string | null {
  if (!error) return null;
  return error instanceof Error ? error.message : String(error);
}

export function useMotelyRuntime(): UseMotelyRuntimeState {
  const snapshot = useSyncExternalStore(subscribeMotelyRuntime, getMotelyRuntimeSnapshot, getMotelyRuntimeSnapshot);
  const ensureReady = useCallback(() => ensureMotelyReady(), []);

  return useMemo(
    () => ({
      status: snapshot.status,
      ready: snapshot.status === "ready",
      error: formatError(snapshot.error),
      fsReady: snapshot.isFileSystemReady,
      fsError: formatError(snapshot.fileSystemError),
      ensureReady,
    }),
    [snapshot, ensureReady],
  );
}

export function useMotelyRuntimeOwner(): void {
  const ensureReady = useCallback(() => ensureMotelyReady(), []);

  useEffect(() => {
    void ensureReady();
  }, [ensureReady]);
}
