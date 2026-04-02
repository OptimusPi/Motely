import * as path from "node:path";
import { createRequire } from "node:module";

export interface SearchResult {
  seed: string;
  score: number;
}

export interface SearchSummary {
  status: string;
  searched: string;
  matched: string;
  results: SearchResult[];
  elapsedMs: number;
}

type OnProgress = (searched: bigint, matching: bigint) => void;
type OnResult = (seed: string, score: number) => void;
type OnComplete = (summary: SearchSummary) => void;

let Motely: any = null;
let bootPromise: Promise<void> | null = null;

async function ensureBooted(extensionPath: string): Promise<void> {
  if (bootPromise) return bootPromise;
  bootPromise = (async () => {
    const wasmPath = path.join(extensionPath, "dist", "motely-wasm.mjs");
    const mod = await import(/* webpackIgnore: true */ `${wasmPath}`);
    await mod.default.boot();
    Motely = mod.Motely;
  })();
  return bootPromise;
}

let stopRequested = false;

export async function runSearch(
  extensionPath: string,
  jaml: string,
  seedCount: number,
  onProgress: OnProgress,
  onResult: OnResult,
  onComplete: OnComplete
): Promise<void> {
  await ensureBooted(extensionPath);
  stopRequested = false;

  const { MotelyProgram, SearchEvents } = Motely.BrowserWasm;
  const results: SearchResult[] = [];
  const startMs = Date.now();

  const onResultHandler = (seed: string, score: number, _tally: Int32Array) => {
    results.push({ seed, score });
    onResult(seed, score);
  };

  const onProgressHandler = (searched: bigint, matching: bigint, _elapsed: bigint) => {
    onProgress(searched, matching);
  };

  const onCompleteHandler = (status: string, searched: bigint, matched: bigint) => {
    SearchEvents.onResult.unsubscribe(onResultHandler);
    SearchEvents.onProgress.unsubscribe(onProgressHandler);
    SearchEvents.onComplete.unsubscribe(onCompleteHandler);
    onComplete({
      status,
      searched: searched.toString(),
      matched: matched.toString(),
      results: results.sort((a, b) => b.score - a.score).slice(0, 500),
      elapsedMs: Date.now() - startMs,
    });
  };

  SearchEvents.onResult.subscribe(onResultHandler);
  SearchEvents.onProgress.subscribe(onProgressHandler);
  SearchEvents.onComplete.subscribe(onCompleteHandler);

  try {
    const config = MotelyProgram.loadJaml(jaml);
    MotelyProgram.startRandomSearch(config, seedCount, 8);
  } catch (err) {
    SearchEvents.onResult.unsubscribe(onResultHandler);
    SearchEvents.onProgress.unsubscribe(onProgressHandler);
    SearchEvents.onComplete.unsubscribe(onCompleteHandler);
    throw err;
  }
}

export function stopSearch(extensionPath: string): void {
  stopRequested = true;
  if (!Motely) return;
  try {
    Motely.BrowserWasm.MotelyProgram.stopSearch();
  } catch {}
}
