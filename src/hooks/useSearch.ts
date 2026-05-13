"use client";

import { useState, useCallback, useRef, useEffect } from "react";
import { Motely } from "../motelyBoot.js";

const { MotelyWasm, MotelyWasmEvents } = Motely;

export interface SearchResult {
  seed: string;
  score: number;
  tallyColumns?: number[];
}

export type SearchStatus = "idle" | "running" | "completed" | "cancelled" | "error";

export interface UseSearchState {
  results: SearchResult[];
  totalSearched: bigint;
  matchingSeeds: bigint;
  status: SearchStatus;
  error: string | null;
  seedsPerSecond: number;
  tallyLabels: string[];
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

// Module-level: only ONE search can run at a time across all useSearch
// instances because MotelyWasmEvents handlers are shared global state.
// runId guards against late events from a prior search bleeding into the
// next one.
let runIdCounter = 0;

export function useSearch() {
  const [state, setState] = useState<UseSearchState>(INITIAL_STATE);
  const activeSearchRef = useRef<{ cancel(): void } | null>(null);
  const myRunIdRef = useRef(0);
  const speedRef = useRef({ lastSearched: 0n, lastTime: 0, ema: 0 });
  const pendingResultsRef = useRef<SearchResult[]>([]);
  const flushScheduledRef = useRef(false);

  const flushResults = useCallback(() => {
    flushScheduledRef.current = false;
    if (pendingResultsRef.current.length === 0) return;
    const batch = pendingResultsRef.current;
    pendingResultsRef.current = [];
    setState((s) => ({ ...s, results: [...s.results, ...batch] }));
  }, []);

  useEffect(() => {
    return () => {
      // On unmount, cancel any active search and zero the runId so events
      // from in-flight searches stop mutating state.
      activeSearchRef.current?.cancel();
      myRunIdRef.current = 0;
    };
  }, []);

  const wireSearch = useCallback((startFn: () => { cancel(): void }) => {
    const runId = ++runIdCounter;
    myRunIdRef.current = runId;
    speedRef.current = { lastSearched: 0n, lastTime: 0, ema: 0 };
    pendingResultsRef.current = [];
    setState((s) => ({ ...INITIAL_STATE, status: "running", tallyLabels: s.tallyLabels }));

    MotelyWasmEvents.notifyResult = (seed: string, score: number, tallyColumns: Iterable<number> | ArrayLike<number>) => {
      if (runId !== runIdCounter) return;
      pendingResultsRef.current.push({ seed, score, tallyColumns: Array.from(tallyColumns) });
      if (!flushScheduledRef.current) {
        flushScheduledRef.current = true;
        requestAnimationFrame(flushResults);
      }
    };

    MotelyWasmEvents.notifyProgress = (searched: bigint, matching: bigint) => {
      if (runId !== runIdCounter) return;
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
    };

    MotelyWasmEvents.notifyComplete = (status: string, searched: bigint, matched: bigint) => {
      if (runId !== runIdCounter) return;
      activeSearchRef.current = null;
      // Final flush of any pending results before marking complete.
      flushResults();
      setState((s) => ({
        ...s,
        status: status === "Completed" ? "completed" : status === "Cancelled" ? "cancelled" : "error",
        error: status === "Completed" || status === "Cancelled" ? null : status,
        totalSearched: searched,
        matchingSeeds: matched,
        seedsPerSecond: 0,
      }));
    };

    try {
      activeSearchRef.current = startFn();
    } catch (err) {
      activeSearchRef.current = null;
      setState((s) => ({ ...s, status: "error", error: String(err) }));
    }
  }, [flushResults]);

  const start = useCallback((jaml: string, count: number) => {
    const validation = MotelyWasm.validateJaml(jaml);
    if (validation !== "valid") {
      setState((s) => ({ ...s, status: "error", error: validation }));
      return;
    }
    wireSearch(() => MotelyWasm.startRandomSearch(jaml, count));
  }, [wireSearch]);

  const startAesthetic = useCallback((jaml: string, aesthetic: number) => {
    wireSearch(() => MotelyWasm.startAestheticSearch(jaml, aesthetic));
  }, [wireSearch]);

  const startSeedList = useCallback((jaml: string, seeds: string[]) => {
    wireSearch(() => MotelyWasm.startSeedListSearch(jaml, seeds));
  }, [wireSearch]);

  const startKeyword = useCallback((jaml: string, keywords: string, padding?: string) => {
    wireSearch(() => MotelyWasm.startKeywordSearch(jaml, keywords, padding ?? ""));
  }, [wireSearch]);

  const startSequential = useCallback((jaml: string, startSeed: string, endSeed?: string) => {
    const charCount = startSeed.length || 1;
    const startNum = parseInt(startSeed, 36) || 0;
    const endNum = endSeed ? parseInt(endSeed, 36) : startNum + 10_000_000;
    wireSearch(() => MotelyWasm.startSequentialSearch(jaml, charCount, BigInt(startNum), BigInt(endNum)));
  }, [wireSearch]);

  const cancel = useCallback(() => {
    activeSearchRef.current?.cancel();
    activeSearchRef.current = null;
    runIdCounter++; // invalidate any late events
    setState((s) => ({ ...s, status: "cancelled", seedsPerSecond: 0 }));
  }, []);

  const clearError = useCallback(() => {
    setState((s) => (s.error || s.status === "error" ? { ...s, error: null, status: "idle" } : s));
  }, []);

  const fetchTallyLabels = useCallback((jaml: string) => {
    try {
      const labels = MotelyWasm.getTallyLabels(jaml);
      setState((s) => ({ ...s, tallyLabels: Array.from(labels) }));
    } catch (err) {
      setState((s) => ({ ...s, status: "error", error: String(err) }));
    }
  }, []);

  return { ...state, start, startAesthetic, startSeedList, startKeyword, startSequential, cancel, clearError, fetchTallyLabels };
}
