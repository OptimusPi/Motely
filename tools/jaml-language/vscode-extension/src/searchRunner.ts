import bootsharp, { MotelyWasm, MotelyWasmEvents } from "motely-wasm";
import type { Motely } from "motely-wasm";

export interface SearchResult {
  seed: string;
  score: number;
  tally: Int32Array;
}

export interface SearchSummary {
  status: string;
  searched: string;
  matched: string;
  results: SearchResult[];
  elapsedMs: number;
}

export type OnProgress = (searched: bigint, matching: bigint) => void;
export type OnResult = (seed: string, score: number, tally: Int32Array) => void;
export type OnComplete = (summary: SearchSummary) => void;

let bootPromise: Promise<unknown> | null = null;
let activeSearch: Motely.IMotelyWasmSearch | null = null;

async function ensureBooted(): Promise<void> {
  bootPromise ??= bootsharp.boot().catch(function (err: unknown) {
    bootPromise = null;
    throw err;
  });
  await bootPromise;
}

export async function runSearch(
  jaml: string,
  seedCount: number,
  onProgress: OnProgress,
  onResult: OnResult,
  onComplete: OnComplete
): Promise<void> {
  if (activeSearch) {
    throw new Error("A JAML search is already running. Stop it before starting another.");
  }

  await ensureBooted();

  const validation = MotelyWasm.validateJaml(jaml);
  if (validation !== "valid") {
    throw new Error(validation);
  }

  const results: SearchResult[] = [];
  const startMs = Date.now();

  return new Promise<void>(function (resolve, reject) {
    let resultId: string;
    let progressId: string;
    let completeId: string;

    function cleanup(): void {
      MotelyWasmEvents.onResult.unsubscribeById(resultId);
      MotelyWasmEvents.onProgress.unsubscribeById(progressId);
      MotelyWasmEvents.onComplete.unsubscribeById(completeId);
      activeSearch = null;
    }

    resultId = MotelyWasmEvents.onResult.subscribe(function (seed, score, tally) {
      results.push({ seed, score, tally });
      onResult(seed, score, tally);
    });

    progressId = MotelyWasmEvents.onProgress.subscribe(function (searched, matching) {
      onProgress(searched, matching);
    });

    completeId = MotelyWasmEvents.onComplete.subscribe(function (status, searched, matched) {
      cleanup();
      onComplete({
        status,
        searched: searched.toString(),
        matched: matched.toString(),
        results: results.sort(function (a, b) { return b.score - a.score; }).slice(0, 500),
        elapsedMs: Date.now() - startMs,
      });
      resolve();
    });

    try {
      activeSearch = MotelyWasm.startRandomSearch(jaml, seedCount);
    } catch (err) {
      cleanup();
      reject(err);
    }
  });
}

export function stopSearch(): void {
  activeSearch?.cancel();
}
