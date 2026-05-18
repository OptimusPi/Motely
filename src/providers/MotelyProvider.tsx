"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import bootsharp from "motely-wasm";
import { ensureMotelyReady, type MotelyRuntimeStatus } from "../lib/motely/runtime.js";

export interface MotelyContextValue {
    status: MotelyRuntimeStatus;
    error: string | null;
}

export const MotelyContext = createContext<MotelyContextValue | null>(null);

function currentStatus(): MotelyRuntimeStatus {
    switch (bootsharp.getStatus()) {
        case bootsharp.BootStatus.Booted: return "ready";
        case bootsharp.BootStatus.Booting: return "booting";
        default: return "idle";
    }
}

export function MotelyProvider({ children }: { children: ReactNode }) {
    const [status, setStatus] = useState<MotelyRuntimeStatus>(currentStatus);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (status === "ready") return;
        setStatus("booting");
        ensureMotelyReady()
            .then(() => setStatus("ready"))
            .catch((err) => {
                setStatus("error");
                setError(err instanceof Error ? err.message : String(err));
            });
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    return (
        <MotelyContext.Provider value={{ status, error }}>
            {children}
        </MotelyContext.Provider>
    );
}

export function useMotelyContext(): MotelyContextValue | null {
    return useContext(MotelyContext);
}
