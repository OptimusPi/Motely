/**
 * Bootsharp interop: do not pass arrow functions (`=>`) into generated WASM delegates
 * (e.g. SearchEvents.subscribe). Use `function` declarations until Bootsharp supports it.
 */
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
/** Set while a random search is running; cleared on complete or {@link stopSearch}. */
let activeSearch: BrowserWasm.IMotelySearchSession | null = null;

async function ensureBooted(): Promise<void> {
  bootPromise ??= bootsharp
    .boot()
    .then(function () {
      /* booted */
    })
    .catch(function (err: unknown) {
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

  // Random search must use FromJaml: JamlConfig from loadJaml() does not round-trip through JS interop for startRandomSearch.
  const results: SearchResult[] = [];
  const startMs = Date.now();

  return await new Promise<void>(function (resolve, reject) {
    function cleanup(): void {
      SearchEvents.onResult.unsubscribe(onResultHandler);
      SearchEvents.onProgress.unsubscribe(onProgressHandler);
      SearchEvents.onComplete.unsubscribe(onCompleteHandler);
      activeSearch = null;
    }

    function onResultHandler(seed: string, score: number, tally: Int32Array): void {
      results.push({ seed, score, tally });
      onResult(seed, score, tally);
    }

    function onProgressHandler(searched: bigint, matching: bigint): void {
      onProgress(searched, matching);
    }

    function onCompleteHandler(
      status: string,
      searched: bigint,
      matched: bigint
    ): void {
      cleanup();
      onComplete({
        status,
        searched: searched.toString(),
        matched: matched.toString(),
        results: results
          .sort(function (a, b) {
            return b.score - a.score;
          })
          .slice(0, 500),
        elapsedMs: Date.now() - startMs,
      });
      resolve();
    }

    SearchEvents.onResult.subscribe(onResultHandler);
    SearchEvents.onProgress.subscribe(onProgressHandler);
    SearchEvents.onComplete.subscribe(onCompleteHandler);

    try {
      activeSearch = MotelyWasmHost.startRandomSearchFromJaml(jaml, seedCount);
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
