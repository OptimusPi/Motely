"use client";

import { useState, useCallback, useRef, useEffect } from "react";

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

const INITIAL_STATE: UseSearchState = {
  results: [],
  totalSearched: 0n,
  matchingSeeds: 0n,
  status: "idle",
  error: null,
  seedsPerSecond: 0,
  tallyLabels: [],
};

const SEARCH_WORKER_CODE = `
let MotelyWasm = null;
let MotelyWasmEvents = null;
let activeSearch = null;

self.addEventListener('message', async function(e) {
  const msg = e.data;

  if (msg.type === 'init') {
    try {
      const mod = await import(msg.url);
      await mod.default.boot();
      MotelyWasm = mod.MotelyWasm;
      MotelyWasmEvents = mod.MotelyWasmEvents;
      self.postMessage({ type: 'ready' });
    } catch (err) {
      self.postMessage({ type: 'error', message: String(err) });
    }
    return;
  }

  if (msg.type === 'start') {
    if (!MotelyWasm) { self.postMessage({ type: 'error', message: 'Not initialized' }); return; }
    const validation = MotelyWasm.validateJaml(msg.jaml);
    if (validation !== 'valid') { self.postMessage({ type: 'error', message: validation }); return; }

    function cleanup() {
      MotelyWasmEvents.notifyResult = () => {};
      MotelyWasmEvents.notifyProgress = () => {};
      MotelyWasmEvents.notifyComplete = () => {};
      activeSearch = null;
    }

    MotelyWasmEvents.notifyResult = function(seed, score, tallyColumns) {
      self.postMessage({ type: 'result', seed, score, tallyColumns: Array.from(tallyColumns) });
    };
    MotelyWasmEvents.notifyProgress = function(searched, matching) {
      self.postMessage({ type: 'progress', searched: searched.toString(), matching: matching.toString() });
    };
    MotelyWasmEvents.notifyComplete = function(status, searched, matched) {
      cleanup();
      self.postMessage({ type: 'complete', status, searched: searched.toString(), matched: matched.toString() });
    };

    try {
      const mode = msg.mode || 'random';

      if (mode === 'random') {
        activeSearch = MotelyWasm.startRandomSearch(msg.jaml, msg.count);
      } else if (mode === 'aesthetic') {
        activeSearch = MotelyWasm.startAestheticSearch(msg.jaml, msg.aesthetic);
      } else if (mode === 'seedList') {
        activeSearch = MotelyWasm.startSeedListSearch(msg.jaml, msg.seeds);
      } else if (mode === 'keyword') {
        activeSearch = MotelyWasm.startKeywordSearch(msg.jaml, msg.keywords, msg.padding || '');
      } else if (mode === 'sequential') {
        activeSearch = MotelyWasm.startSequentialSearch(msg.jaml, msg.batchCharCount, BigInt(msg.startBatch), BigInt(msg.endBatch));
      } else {
        self.postMessage({ type: 'error', message: 'Unknown search mode: ' + mode });
        cleanup();
        return;
      }
    } catch (err) {
      cleanup();
      self.postMessage({ type: 'error', message: String(err) });
    }
    return;
  }

  if (msg.type === 'stop') {
    if (activeSearch) { activeSearch.cancel(); activeSearch = null; }
    self.postMessage({ type: 'cancelled' });
  }

  if (msg.type === 'get_tally_labels') {
    if (!MotelyWasm) { self.postMessage({ type: 'error', message: 'Not initialized' }); return; }
    try {
      const labels = MotelyWasm.getTallyLabels(msg.jaml);
      self.postMessage({ type: 'tally_labels', labels: Array.from(labels) });
    } catch (err) {
      self.postMessage({ type: 'error', message: String(err) });
    }
  }
});
`;

function createWorker(): Worker {
  const blob = new Blob([SEARCH_WORKER_CODE], { type: "application/javascript" });
  return new Worker(URL.createObjectURL(blob), { type: "module" });
}

export function useSearch(motelyWasmUrl?: string) {
  const [state, setState] = useState<UseSearchState>(INITIAL_STATE);

  const workerRef = useRef<Worker | null>(null);
  const readyRef = useRef(false); // Worker is NOT implicitly ready, must wait for 'ready' message
  const speedRef = useRef({ lastSearched: 0n, lastTime: 0, ema: 0 });

  useEffect(() => {
    setState((s) => ({ ...s, status: "idle" }));
    const worker = createWorker();
    workerRef.current = worker;
    
    if (motelyWasmUrl) {
      worker.postMessage({ type: 'init', url: motelyWasmUrl });
    }

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
  }, [motelyWasmUrl]);

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

  const startSequential = useCallback((jaml: string, startSeed: string, endSeed?: string) => {
    // Sequential search: single-threaded, deterministic order.
    // batchCharCount = length of start seed, startBatch/endBatch = numeric range.
    const charCount = startSeed.length || 1;
    const startNum = parseInt(startSeed, 36) || 0;
    const endNum = endSeed ? parseInt(endSeed, 36) : startNum + 10_000_000;
    sendStart({
      type: "start",
      mode: "sequential",
      jaml,
      batchCharCount: charCount,
      startBatch: startNum.toString(),
      endBatch: endNum.toString(),
    });
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

  return { ...state, start, startAesthetic, startSeedList, startKeyword, startSequential, cancel, clearError, fetchTallyLabels };
}
