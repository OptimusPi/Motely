/**
 * Loads the published npm package `motely-wasm-compat` at runtime (ESM).
 * The extension bundle is CJS; dynamic import resolves `node_modules/motely-wasm-compat` from the
 * extension install directory (shipped in the VSIX — see .vscodeignore).
 */
type MotelyWasm = typeof import("motely-wasm-compat");

let ready: Promise<MotelyWasm> | null = null;
let stopRequested = false;

async function getWasm(): Promise<MotelyWasm> {
  if (!ready) {
    ready = (async () => {
      const mod = await import("motely-wasm-compat");
      await mod.default.boot();
      return mod;
    })();
  }
  return ready;
}

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

export async function runSearch(
  jaml: string,
  seedCount: number,
  onProgress: OnProgress,
  onResult: OnResult,
  onComplete: OnComplete
): Promise<void> {
  const { MotelyProgram, SearchEvents } = await getWasm();
  stopRequested = false;

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
    MotelyProgram.startRandomSearch(config, seedCount, 1);
  } catch (err) {
    SearchEvents.onResult.unsubscribe(onResultHandler);
    SearchEvents.onProgress.unsubscribe(onProgressHandler);
    SearchEvents.onComplete.unsubscribe(onCompleteHandler);
    throw err;
  }
}

export function stopSearch(): void {
  stopRequested = true;
  if (!ready) return;
  void ready.then((mod) => {
    try {
      mod.MotelyProgram.stopSearch();
    } catch {
      /* ignore */
    }
  });
}
