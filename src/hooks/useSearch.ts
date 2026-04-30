"use client";

import { useState, useCallback, useRef, useEffect } from "react";
import SearchWorker from "./searchWorker.js?worker&inline";

export interface SearchResult {
  seed: string;
  score: number;
  tallyColumns?: number[];
}

export type SearchStatus = "idle" | "booting" | "running" | "completed" | "cancelled" | "error";

export interface UseSearchState {
  results: SearchResult[];
  totalSearched: bigint;
  matchingSeeds: bigint;
  status: SearchStatus;
  error: string | null;
  seedsPerSecond: number;
  tallyLabels: string[];
}

function createWorker(): Worker {
  return new SearchWorker();
}

const INITIAL_STATE: UseSearchState = {
  results: [],
  totalSearched: 0n,
  matchingSeeds: 0n,
  status: "idle",
  error: null,
  seedsPerSecond: 0,
  tallyLabels: [],
};

export function useSearch() {
  const [state, setState] = useState<UseSearchState>(INITIAL_STATE);

  const workerRef = useRef<Worker | null>(null);
  const readyRef = useRef(true); // Worker is ready implicitly since boot is handled by import
  const speedRef = useRef({ lastSearched: 0n, lastTime: 0, ema: 0 });

  useEffect(() => {
    setState((s) => ({ ...s, status: "idle" }));
    const worker = createWorker();
    workerRef.current = worker;

    worker.onmessage = (e: MessageEvent) => {
      const msg = e.data as { type: string; [k: string]: unknown };
      if (msg.type === "ready") {
        readyRef.current = true;
        setState((s) => s.status === "booting" ? { ...s, status: "idle" } : s);
      } else if (msg.type === "result") {
        setState((s) => ({
          ...s,
          results: [...s.results, {
            seed: msg.seed as string,
            score: msg.score as number,
            tallyColumns: msg.tallyColumns as number[] | undefined,
          }],
        }));
      } else if (msg.type === "progress") {
        const searched = BigInt(msg.searched as string);
        const matching = BigInt(msg.matching as string);
        const now = performance.now();
        const ref = speedRef.current;
        let sps = ref.ema;

        if (ref.lastTime > 0) {
          const dtMs = now - ref.lastTime;
          if (dtMs > 0) {
            const delta = Number(searched - ref.lastSearched);
            const instantSps = delta / (dtMs / 1000);
            sps = ref.ema === 0 ? instantSps : ref.ema * 0.7 + instantSps * 0.3;
          }
        }
        ref.lastSearched = searched;
        ref.lastTime = now;
        ref.ema = sps;

        setState((s) => ({ ...s, totalSearched: searched, matchingSeeds: matching, seedsPerSecond: Math.round(sps) }));
      } else if (msg.type === "complete") {
        speedRef.current = { lastSearched: 0n, lastTime: 0, ema: 0 };
        setState((s) => ({
          ...s,
          status: msg.status === "Completed" ? "completed" : "error",
          error: msg.status !== "Completed" ? msg.status as string : null,
          totalSearched: BigInt(msg.searched as string),
          matchingSeeds: BigInt(msg.matched as string),
          seedsPerSecond: 0,
        }));
      } else if (msg.type === "cancelled") {
        speedRef.current = { lastSearched: 0n, lastTime: 0, ema: 0 };
        setState((s) => ({ ...s, status: "cancelled", seedsPerSecond: 0 }));
      } else if (msg.type === "tally_labels") {
        setState((s) => ({ ...s, tallyLabels: msg.labels as string[] }));
      } else if (msg.type === "error") {
        setState((s) => ({ ...s, status: "error", error: msg.message as string }));
      }
    };

    return () => {
      worker.terminate();
      workerRef.current = null;
    };
  }, []);

  const sendStart = useCallback((payload: Record<string, unknown>) => {
    const worker = workerRef.current;
    if (!worker) return;
    speedRef.current = { lastSearched: 0n, lastTime: 0, ema: 0 };
    setState({ ...INITIAL_STATE, status: "running", tallyLabels: state.tallyLabels });

    const send = () => worker.postMessage(payload);

    if (readyRef.current) {
      send();
    } else {
      const orig = worker.onmessage;
      worker.onmessage = (e: MessageEvent) => {
        orig?.call(worker, e);
        if ((e.data as { type: string }).type === "ready") {
          worker.onmessage = orig;
          send();
        }
      };
    }
  }, [state.tallyLabels]);

  const start = useCallback((jaml: string, count: number) => {
    sendStart({ type: "start", mode: "random", jaml, count });
  }, [sendStart]);

  const startAesthetic = useCallback((jaml: string, aesthetic: number) => {
    sendStart({ type: "start", mode: "aesthetic", jaml, aesthetic });
  }, [sendStart]);

  const startSeedList = useCallback((jaml: string, seeds: string[]) => {
    sendStart({ type: "start", mode: "seedList", jaml, seeds });
  }, [sendStart]);

  const startKeyword = useCallback((jaml: string, keywords: string, padding?: string) => {
    sendStart({ type: "start", mode: "keyword", jaml, keywords, padding });
  }, [sendStart]);

  const cancel = useCallback(() => {
    workerRef.current?.postMessage({ type: "stop" });
  }, []);

  const clearError = useCallback(() => {
    setState((s) => (s.error || s.status === "error" ? { ...s, error: null, status: "idle" } : s));
  }, []);

  const fetchTallyLabels = useCallback((jaml: string) => {
    workerRef.current?.postMessage({ type: "get_tally_labels", jaml });
  }, []);

  return { ...state, start, startAesthetic, startSeedList, startKeyword, cancel, clearError, fetchTallyLabels };
}
