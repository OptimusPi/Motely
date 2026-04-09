import bootsharp, {
  MotelyWasmHost,
  SearchEvents,
} from "motely-wasm-compat";
import type { BrowserWasm } from "motely-wasm-compat";

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

let bootPromise: Promise<void> | null = null;
let activeSearch: BrowserWasm.IMotelySearchSession | null = null;

async function ensureBooted(): Promise<void> {
  bootPromise ??= bootsharp.boot().then(() => {}).catch((err) => {
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

  const config = MotelyWasmHost.loadJaml(jaml);
  const results: SearchResult[] = [];
  const startMs = Date.now();

  return await new Promise<void>((resolve, reject) => {
    const onResultHandler = (seed: string, score: number, tally: Int32Array) => {
      results.push({ seed, score, tally });
      onResult(seed, score, tally);
    };

    const onProgressHandler = (searched: bigint, matching: bigint) => {
      onProgress(searched, matching);
    };

    const onCompleteHandler = (
      status: string,
      searched: bigint,
      matched: bigint
    ) => {
      cleanup();
      onComplete({
        status,
        searched: searched.toString(),
        matched: matched.toString(),
        results: results.sort((a, b) => b.score - a.score).slice(0, 500),
        elapsedMs: Date.now() - startMs,
      });
      resolve();
    };

    const cleanup = () => {
      SearchEvents.onResult.unsubscribe(onResultHandler);
      SearchEvents.onProgress.unsubscribe(onProgressHandler);
      SearchEvents.onComplete.unsubscribe(onCompleteHandler);
      activeSearch = null;
    };

    SearchEvents.onResult.subscribe(onResultHandler);
    SearchEvents.onProgress.subscribe(onProgressHandler);
    SearchEvents.onComplete.subscribe(onCompleteHandler);

    try {
      activeSearch = MotelyWasmHost.startRandomSearch(config, seedCount);
    } catch (err) {
      cleanup();
      reject(err);
    }
  });
}

export function stopSearch(): void {
  if (!activeSearch) return;
  const session = activeSearch;
  activeSearch = null;
  try {
    session.cancel();
  } catch {
    // ignore cancellation failures
  }
}
