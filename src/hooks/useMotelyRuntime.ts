"use client";

import { useCallback, useMemo, useState } from "react";
import bootsharp from "motely-wasm";
import { ensureMotelyReady, type MotelyRuntimeStatus } from "../lib/motely/runtime.js";
import { useMotelyContext } from "../providers/MotelyProvider.js";

export type { MotelyRuntimeStatus };

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
        case bootsharp.BootStatus.Booted: return "ready";
        case bootsharp.BootStatus.Booting: return "booting";
        default: return "idle";
    }
}

export function useMotelyRuntime(): UseMotelyRuntimeState {
    const ctx = useMotelyContext();
    const [localStatus, setLocalStatus] = useState<MotelyRuntimeStatus>(currentStatus);
    const [localError, setLocalError] = useState<string | null>(null);

    const status = ctx?.status ?? localStatus;
    const error = ctx?.error ?? localError;

    const ensureReady = useCallback(async () => {
        try {
            if (!ctx) {
                setLocalStatus(currentStatus());
                if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
                    setLocalStatus("booting");
                    await ensureMotelyReady();
                }
                setLocalStatus(currentStatus());
            } else {
                await ensureMotelyReady();
            }
        } catch (err) {
            if (!ctx) {
                setLocalStatus("error");
                setLocalError(err instanceof Error ? err.message : String(err));
            }
            throw err;
        }
    }, [ctx]);

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
