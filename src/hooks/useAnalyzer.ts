"use client";

import { useState, useCallback } from "react";
import bootsharp, { Motely } from "motely-wasm";
import type { MotelyJamlyzerResult, MotelySeedAnalysis } from "motely-wasm/motely/analysis";
import type { AnalyzerAnteView } from "../components/AnalyzerExplorer.js";

export type AnalyzerStatus = "idle" | "running" | "done" | "error";

export interface UseAnalyzerState {
    antes: AnalyzerAnteView[];
    score: number | null;
    status: AnalyzerStatus;
    error: string | null;
    tallyLabels: string[];
    rawAnalysis: MotelySeedAnalysis | null;
}

async function ensureMotelyReady(): Promise<void> {
    if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
        await bootsharp.boot("/motely-wasm/bin");
    }
}

export function useAnalyzer() {
    const [antes, setAntes] = useState<AnalyzerAnteView[]>([]);
    const [score, setScore] = useState<number | null>(null);
    const [status, setStatus] = useState<AnalyzerStatus>("idle");
    const [error, setError] = useState<string | null>(null);
    const [tallyLabels, setTallyLabels] = useState<string[]>([]);
    const [rawAnalysis, setRawAnalysis] = useState<MotelySeedAnalysis | null>(null);

    const analyze = useCallback((seed: string, jaml: string) => {
        setAntes([]);
        setScore(null);
        setTallyLabels([]);
        setRawAnalysis(null);
        setStatus("running");
        setError(null);

        void (async () => {
            try {
                await ensureMotelyReady();
                const validation = Motely.validateJaml(jaml);
                if (validation !== "valid") {
                    throw new Error(validation || "Invalid JAML.");
                }
                const result: MotelyJamlyzerResult = Motely.analyzeJamlSeeds(jaml, [seed]);
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

    return { antes, score, status, error, analyze, clearError, tallyLabels, rawAnalysis };
}
