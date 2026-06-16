"use client";

import { useState, useEffect } from "react";
<<<<<<< HEAD
import { type Motely as MotelyNamespace } from "motely-wasm";
import type { MotelyJamlyzerSeedResult } from "motely-wasm";
=======
import { type Program as MotelyNamespace } from "motely-wasm/motely/wasm";
import type { MotelyJamlyzerSeedResult } from "motely-wasm/motely/analysis";
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45

type MotelyApi = typeof MotelyNamespace;

export function useSeedAnalyzer(motely: MotelyApi | null, seed: string | null, jaml?: string) {
    const [data, setData] = useState<MotelyJamlyzerSeedResult | null>(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (!seed || seed === "LOCKED" || !motely) {
            // eslint-disable-next-line react-hooks/set-state-in-effect -- clearing async-derived data when inputs invalidate
            setData(null);
            return;
        }

        const abortController = new AbortController();

        (async () => {
            setLoading(true);
            setError(null);
            try {
                const config = jaml ?? `version: 1\nconfig:\n  deck: Erratic\n  stake: White\n`;
<<<<<<< HEAD
                const validation = motely.validateJaml(config);
=======
                let validation = "valid";
                try { motely.parseJaml(config); } catch (e) { validation = e instanceof Error ? e.message : "Invalid JAML."; }
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
                if (abortController.signal.aborted) return;
                if (validation !== "valid") {
                    throw new Error(validation || "Invalid JAML.");
                }

<<<<<<< HEAD
                const result = motely.analyzeJamlSeeds(config, [seed]);
=======
                const analyzeConfig = motely.parseJaml(config);
                analyzeConfig.seeds = [seed];
                const result = motely.jamlyzer(analyzeConfig);
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
                if (abortController.signal.aborted) return;
                if (result.error) {
                    throw new Error(result.error);
                }
                setData(result.seeds[0] ?? null);
            } catch (err) {
                if (abortController.signal.aborted) return;
                console.error("[useSeedAnalyzer] Analysis error:", err);
                setError(err instanceof Error ? err.message : String(err));
            } finally {
                if (!abortController.signal.aborted) setLoading(false);
            }
        })();

        return () => abortController.abort();
    }, [motely, seed, jaml]);

    return { data, loading, error };
}
