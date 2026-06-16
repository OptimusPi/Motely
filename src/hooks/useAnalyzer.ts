"use client";

import { useState, useCallback } from "react";
<<<<<<< HEAD
import { Motely, type MotelyJamlyzerResult, type MotelySeedAnalysis } from "motely-wasm";
=======
import { Program as Motely } from "motely-wasm/motely/wasm";
import type { MotelyJamlyzerResult, MotelySeedAnalysis } from "motely-wasm/motely/analysis";
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
import { ensureMotelyReady } from "../lib/motely/runtime.js";

export type AnalyzerStatus = "idle" | "running" | "done" | "error";


export function useAnalyzer() {
    const [score, setScore] = useState<number | null>(null);
    const [status, setStatus] = useState<AnalyzerStatus>("idle");
    const [error, setError] = useState<string | null>(null);
    const [tallyLabels, setTallyLabels] = useState<string[]>([]);
    const [rawAnalysis, setRawAnalysis] = useState<MotelySeedAnalysis | null>(null);

    const analyze = useCallback((seed: string, jaml: string) => {
        setScore(null);
        setTallyLabels([]);
        setRawAnalysis(null);
        setStatus("running");
        setError(null);

        void (async () => {
            try {
                await ensureMotelyReady();
<<<<<<< HEAD
                const validation = Motely.validateJaml(jaml);
                if (validation !== "valid") {
                    throw new Error(validation || "Invalid JAML.");
                }
                const result: MotelyJamlyzerResult = Motely.analyzeJamlSeeds(jaml, [seed]);
=======
                let validation = "valid";
                try { Motely.parseJaml(jaml); } catch (e) { validation = e instanceof Error ? e.message : "Invalid JAML"; }
                if (validation !== "valid") {
                    throw new Error(validation || "Invalid JAML.");
                }
                const analyzeConfig = Motely.parseJaml(jaml);
                analyzeConfig.seeds = [seed];
                const result: MotelyJamlyzerResult = Motely.jamlyzer(analyzeConfig);
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
                if (result.error) {
                    throw new Error(result.error);
                }
                if (result.tallyLabels) setTallyLabels(result.tallyLabels);
                const seedResult = result.seeds[0];
                if (seedResult?.analysis) {
                    setRawAnalysis(seedResult.analysis);
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
