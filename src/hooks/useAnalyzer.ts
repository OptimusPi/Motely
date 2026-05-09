"use client";

import { useState, useCallback } from "react";
import { Motely } from "../motelyBoot.js";
import type { AnalyzerAnteView, AnalyzerItem } from "../components/AnalyzerExplorer.js";

export type AnalyzerStatus = "idle" | "running" | "done" | "error";

export function useAnalyzer() {
  const [antes, setAntes] = useState<AnalyzerAnteView[]>([]);
  const [status, setStatus] = useState<AnalyzerStatus>("idle");
  const [error, setError] = useState<string | null>(null);
  const [tallyLabels, setTallyLabels] = useState<string[]>([]);

  const analyze = useCallback((seed: string, jaml: string) => {
    setAntes([]);
    setTallyLabels([]);
    setStatus("running");
    setError(null);

    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const result = (Motely.MotelyWasm as any).analyzeJamlSeeds(jaml, [seed]);
      if (result.error) throw new Error(result.error);

      if (result.tallyLabels) setTallyLabels(Array.from(result.tallyLabels as string[]));

      const seedResult = result.seeds?.[0];
      const analysis = seedResult?.analysis;

      if (!analysis?.antes) {
        setAntes([]);
        setStatus("done");
        return;
      }

      const mapped: AnalyzerAnteView[] = Array.from(analysis.antes as unknown[]).map((a: unknown) => {
        const ante = a as {
          ante: number;
          boss: string;
          voucher: string;
          smallBlindTag: string;
          bigBlindTag: string;
          packs: { type: string }[];
          shopQueue: { id: string; name: string; value: number; matched: boolean }[];
        };
        return {
          ante: ante.ante,
          boss: ante.boss,
          voucher: ante.voucher,
          smallBlindTag: ante.smallBlindTag,
          bigBlindTag: ante.bigBlindTag,
          packs: ante.packs.map((p) => p.type),
          shop: ante.shopQueue.map((item): AnalyzerItem => ({
            id: item.id,
            name: item.name,
            value: item.value,
            desired: item.matched,
          })),
        };
      });

      setAntes(mapped);
      setStatus("done");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setStatus("error");
    }
  }, []);

  const clearError = useCallback(() => {
    setError(null);
    setStatus((s) => (s === "error" ? "idle" : s));
  }, []);

  return { antes, status, error, analyze, clearError, tallyLabels };
}
