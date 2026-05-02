"use client";

import { useState, useEffect } from "react";
import { type Motely } from "motely-wasm";

export function useSeedAnalyzer(motely: typeof Motely | null, seed: string | null) {
  const [data, setData] = useState<Motely.Analysis.MotelyLegacyTextAnalyzer | null | undefined>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!seed || seed === "LOCKED" || !motely) {
      setData(null);
      return;
    }

    const abortController = new AbortController();
    const runAnalysis = async () => {
      setLoading(true);
      setError(null);
      try {
        const jaml = `version: 1\nconfig:\n  deck: Erratic\n  stake: White\n`;
        const rawResult = motely.MotelyWasm.analyzeJamlSeeds(jaml, [seed]);
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
  }, [motely, seed]);

  return { data, loading, error };
}
