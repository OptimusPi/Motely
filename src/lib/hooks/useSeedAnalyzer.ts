"use client";

import { useState, useEffect } from "react";
import type { MotelyWasm as MotelyWasmType, Motely as MotelyEnumsType, Analysis } from "motely-wasm";

export interface MotelyRuntime {
  MotelyWasm: typeof MotelyWasmType;
  Motely: typeof MotelyEnumsType;
}

export function useSeedAnalyzer(runtime: MotelyRuntime, seed: string | null) {
  const [data, setData] = useState<Analysis.MotelyLegacyTextAnalyzer | null | undefined>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!seed || seed === "LOCKED") {
      setData(null);
      return;
    }

    const abortController = new AbortController();
    const runAnalysis = async () => {
      setLoading(true);
      setError(null);
      try {
        const jaml = `version: 1\nconfig:\n  deck: Erratic\n  stake: White\n`;
        const rawResult = runtime.MotelyWasm.analyzeJamlSeeds(jaml, [seed]);
        if (abortController.signal.aborted) return;

        if (rawResult && rawResult.seeds.length > 0) {
          const result = rawResult.seeds[0];
          setData(result.analysis || null);
        } else {
          throw new Error("No analysis result returned");
        }
      } catch (err) {
        if (abortController.signal.aborted) return;
        console.error("[useSeedAnalyzer] Analysis error:", err);
        setError(err instanceof Error ? err.message : String(err));
      } finally {
        if (!abortController.signal.aborted) setLoading(false);
      }
    };

    runAnalysis();
    return () => abortController.abort();
  }, [runtime, seed]);

  return { data, loading, error };
}
