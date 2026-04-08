import * as vscode from "vscode";
import * as path from "node:path";
import * as fs from "node:fs";

/**
 * Loads the WASM search engine.  Resolution order:
 *   1. dist/motely-wasm.mjs  — bundled in the VSIX (self-contained, just works)
 *   2. workspace node_modules — fallback for dev / monorepo setups
 */
type MotelyWasm = typeof import("motely-wasm");
type SearchSession = { cancel(): void };
type SearchEventsApi = {
  onResult: {
    subscribe(handler: (seed: string, score: number, tally: Int32Array) => void): void;
    unsubscribe(handler: (seed: string, score: number, tally: Int32Array) => void): void;
  };
  onProgress: {
    subscribe(handler: (searched: bigint, matching: bigint) => void): void;
    unsubscribe(handler: (searched: bigint, matching: bigint) => void): void;
  };
  onComplete: {
    subscribe(handler: (status: string, searched: bigint, matched: bigint) => void): void;
    unsubscribe(handler: (status: string, searched: bigint, matched: bigint) => void): void;
  };
};
type MotelyWasmCompat = MotelyWasm & {
  MotelyWasmHost: {
    loadJaml(jaml: string): unknown;
    startRandomSearch(config: unknown, seedCount: number): SearchSession;
  };
  SearchEvents: SearchEventsApi;
};

let ready: Promise<MotelyWasm> | null = null;
let activeSearch: SearchSession | null = null;

function resolveWasmPath(extPath: string): string {
  // 1. Bundled in VSIX (placed by esbuild stage step)
  const bundled = path.join(extPath, "dist", "motely-wasm.mjs");
  if (fs.existsSync(bundled)) return bundled;

  // 2. Workspace node_modules
  const candidates = ["motely-wasm-compat", "motely-wasm"];
  for (const folder of vscode.workspace.workspaceFolders ?? []) {
    for (const pkg of candidates) {
      const entry = path.join(folder.uri.fsPath, "node_modules", pkg, "index.mjs");
      if (fs.existsSync(entry)) return entry;
    }
  }

  throw new Error(
    "Motely WASM engine not found.\n\n" +
    "If you built the extension yourself, run:\n" +
    "  dotnet publish Motely.BrowserWasm -c Release\n" +
    "then rebuild the extension.\n\n" +
    "Or install the npm package in your workspace:\n" +
    "  npm install motely-wasm-compat"
  );
}

/** Must be called once with the extension path before any search. */
let _extPath = "";
export function init(extensionPath: string): void {
  _extPath = extensionPath;
}

async function getWasm(): Promise<MotelyWasm> {
  if (!ready) {
    ready = (async () => {
      const wasmPath = resolveWasmPath(_extPath);
      // Dynamic import via Function() so esbuild doesn't resolve it at bundle time.
      const mod = await (Function("p", "return import(p)")(wasmPath) as Promise<MotelyWasm>);
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
  if (activeSearch) {
    throw new Error("A JAML search is already running. Stop it before starting another.");
  }

  const { MotelyWasmHost, SearchEvents } = await getWasm() as MotelyWasmCompat;
  const config = MotelyWasmHost.loadJaml(jaml);
  const results: SearchResult[] = [];
  const startMs = Date.now();

  return await new Promise<void>((resolve, reject) => {
    const onResultHandler = (seed: string, score: number, _tally: Int32Array) => {
      results.push({ seed, score });
      onResult(seed, score);
    };

    const onProgressHandler = (searched: bigint, matching: bigint) => {
      onProgress(searched, matching);
    };

    let session: SearchSession | null = null;
    const cleanup = () => {
      SearchEvents.onResult.unsubscribe(onResultHandler);
      SearchEvents.onProgress.unsubscribe(onProgressHandler);
      SearchEvents.onComplete.unsubscribe(onCompleteHandler);
      if (session && activeSearch === session) activeSearch = null;
    };

    const onCompleteHandler = (status: string, searched: bigint, matched: bigint) => {
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

    SearchEvents.onResult.subscribe(onResultHandler);
    SearchEvents.onProgress.subscribe(onProgressHandler);
    SearchEvents.onComplete.subscribe(onCompleteHandler);

    try {
      session = MotelyWasmHost.startRandomSearch(config, seedCount);
      activeSearch = session;
    } catch (err) {
      cleanup();
      reject(err);
    }
  });
}

export function stopSearch(): void {
  if (activeSearch) {
    try { activeSearch.cancel(); } catch { }
  }
}
