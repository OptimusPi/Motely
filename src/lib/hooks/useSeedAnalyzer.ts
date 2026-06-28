"use client";

import { useState, useEffect } from "react";
import type { MotelyJamlyzerSeedResult } from "motely-wasm";
import { ensureMotelyReady, parseJaml, analyzeSeeds } from "../motely/runtime.js";

// motely-wasm@23 removed the `Program` namespace, so this no longer takes an
// engine handle — it boots lazily via ensureMotelyReady() and uses the shared
// analyzer adapter directly.
export function useSeedAnalyzer(seed: string | null, jaml?: string) {
    const [data, setData] = useState<MotelyJamlyzerSeedResult | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!seed || seed === "LOCKED") {
            // eslint-disable-next-line react-hooks/set-state-in-effect -- clearing async-derived data when inputs invalidate
            setData(null);
            return;
        }

        const abortController = new AbortController();

        (async () => {
            setLoading(true);
            setError(null);
            try {
                await ensureMotelyReady();
                if (abortController.signal.aborted) return;

                const source = jaml ?? `version: 1\nconfig:\n  deck: Erratic\n  stake: White\n`;
                const config = parseJaml(source);
                config.seeds = [seed];
                const rows = analyzeSeeds(config);
                if (abortController.signal.aborted) return;
                setData(rows[0] ?? null);
            } catch (err) {
                if (abortController.signal.aborted) return;
                console.error("[useSeedAnalyzer] Analysis error:", err);
                setError(err instanceof Error ? err.message : String(err));
            } finally {
                if (!abortController.signal.aborted) setLoading(false);
            }
        })();

        return () => abortController.abort();
    }, [seed, jaml]);

    return { data, loading, error };
}
