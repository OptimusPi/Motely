"use client";

import { useState, useCallback } from "react";
import type { MotelyJamlyzerSeedResult } from "motely-wasm";
import { ensureMotelyReady, parseJaml, tallyLabelsFor, analyzeSeeds } from "../lib/motely/runtime.js";

export type AnalyzerStatus = "idle" | "running" | "done" | "error";


export function useAnalyzer() {
    const [score, setScore] = useState<number | null>(null);
    const [status, setStatus] = useState<AnalyzerStatus>("idle");
    const [error, setError] = useState<string | null>(null);
    const [tallyLabels, setTallyLabels] = useState<string[]>([]);
    // motely-wasm@23's analyzer returns the seed result directly (was the nested
    // `MotelySeedAnalysis` under `.analysis`).
    const [rawAnalysis, setRawAnalysis] = useState<MotelyJamlyzerSeedResult | null>(null);

    const analyze = useCallback((seed: string, jaml: string) => {
        setScore(null);
        setTallyLabels([]);
        setRawAnalysis(null);
        setStatus("running");
        setError(null);

        void (async () => {
            try {
                await ensureMotelyReady();
                const config = parseJaml(jaml);
                config.seeds = [seed];
                setTallyLabels(tallyLabelsFor(config));
                const rows = analyzeSeeds(config);
                const seedResult = rows[0];
                if (seedResult) {
                    setRawAnalysis(seedResult);
                    setScore(seedResult.score);
                }
                setStatus("done");
            } catch (e) {
                setError(e instanceof Error ? e.message : String(e));
                setStatus("error");
            }
        })();
    }, []);

    const clearError = useCallback(() => {
        setError(null);
        setStatus((s) => (s === "error" ? "idle" : s));
    }, []);

    return { score, status, error, analyze, clearError, tallyLabels, rawAnalysis };
}
