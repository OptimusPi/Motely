"use client";

import { useState, useCallback, useRef, useEffect } from "react";
import { SEARCH_WORKER_CODE } from "./searchWorkerCode.js";

export interface SearchResult {
  seed: string;
  score: number;
}

export type SearchStatus = "idle" | "booting" | "running" | "completed" | "cancelled" | "error";

export interface UseSearchState {
  results: SearchResult[];
  totalSearched: bigint;
  matchingSeeds: bigint;
  status: SearchStatus;
  error: string | null;
}

function createWorker(motelyWasmUrl: string): Worker {
  const blob = new Blob([SEARCH_WORKER_CODE], { type: "text/javascript" });
  const blobUrl = URL.createObjectURL(blob);
  const worker = new Worker(blobUrl);
  worker.postMessage({ type: "init", url: motelyWasmUrl });
  return worker;
}

export function useSearch(motelyWasmUrl: string) {
  const [state, setState] = useState<UseSearchState>({
    results: [],
    totalSearched: 0n,
    matchingSeeds: 0n,
    status: "idle",
    error: null,
  });

  const workerRef = useRef<Worker | null>(null);
  const readyRef = useRef(false);

  useEffect(() => {
    setState((s) => ({ ...s, status: "booting" }));
    const worker = createWorker(motelyWasmUrl);
    workerRef.current = worker;
    readyRef.current = false;

    worker.onmessage = (e: MessageEvent) => {
      const msg = e.data as { type: string; [k: string]: unknown };
      if (msg.type === "ready") {
        readyRef.current = true;
        setState((s) => s.status === "booting" ? { ...s, status: "idle" } : s);
      } else if (msg.type === "result") {
        setState((s) => ({ ...s, results: [...s.results, { seed: msg.seed as string, score: msg.score as number }] }));
      } else if (msg.type === "progress") {
        setState((s) => ({ ...s, totalSearched: BigInt(msg.searched as string), matchingSeeds: BigInt(msg.matching as string) }));
      } else if (msg.type === "complete") {
        setState((s) => ({ ...s, status: msg.status === "Completed" ? "completed" : "error", error: msg.status !== "Completed" ? msg.status as string : null, totalSearched: BigInt(msg.searched as string), matchingSeeds: BigInt(msg.matched as string) }));
      } else if (msg.type === "cancelled") {
        setState((s) => ({ ...s, status: "cancelled" }));
      } else if (msg.type === "error") {
        setState((s) => ({ ...s, status: "error", error: msg.message as string }));
      }
    };

    return () => {
      worker.terminate();
      workerRef.current = null;
    };
  }, [motelyWasmUrl]);

  const start = useCallback((jaml: string, count: number) => {
    const worker = workerRef.current;
    if (!worker) return;
    setState({ results: [], totalSearched: 0n, matchingSeeds: 0n, status: "running", error: null });

    if (readyRef.current) {
      worker.postMessage({ type: "start", jaml, count });
    } else {
      const orig = worker.onmessage;
      worker.onmessage = (e: MessageEvent) => {
        orig?.call(worker, e);
        if ((e.data as { type: string }).type === "ready") {
          worker.onmessage = orig;
          worker.postMessage({ type: "start", jaml, count });
        }
      };
    }
  }, []);

  const cancel = useCallback(() => {
    workerRef.current?.postMessage({ type: "stop" });
  }, []);

  const clearError = useCallback(() => {
    setState((s) => (s.error || s.status === "error" ? { ...s, error: null, status: "idle" } : s));
  }, []);

  return { ...state, start, cancel, clearError };
}
