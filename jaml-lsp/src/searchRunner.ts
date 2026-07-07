import * as vscode from "vscode";
import * as path from "node:path";
import * as fs from "node:fs";
import { pathToFileURL } from "node:url";

// Minimal shape of the bits of motely-wasm@23.x this file actually touches. Declared locally
// (not imported) so jaml-lsp needs no hard build-time dependency on the multi-MB WASM package —
// it's resolved and dynamically imported at runtime from wherever it's actually installed.
interface MotelyScoredSeedResult {
  seed: string;
  score: number;
  tallies: Int32Array;
}
interface MotelyProgress {
  seedsSearched: bigint;
  matchingSeeds: bigint;
  elapsedMilliseconds: bigint;
}
interface WasmEvent<T> {
  subscribe(handler: (arg: T) => void): string;
  unsubscribe(handler: (arg: T) => void): void;
}
interface MotelyWasm {
  default: { boot: () => Promise<void> };
  MotelyJaml: { fromYaml: (content: string) => unknown };
  MotelySearch: {
    onProgress: WasmEvent<MotelyProgress>;
    onScoredResult: WasmEvent<MotelyScoredSeedResult>;
    searchRandom: (config: unknown, count: number) => Promise<MotelyScoredSeedResult[]>;
  };
}

let ready: Promise<MotelyWasm> | null = null;
let _extPath = "";

// MotelySearch.onProgress/onScoredResult are global singletons on the WASM namespace — a broadcast
// carries no search identity, so two concurrent searches' handlers would receive each other's
// events. Enforce single-flight (one search at a time) and give the in-flight call its own cancel
// token so stopSearch targets exactly it, not a later search that reused a shared flag.
let inFlight = false;
let activeToken: { cancelled: boolean } | null = null;

/** Must be called once with the extension path before any search. */
export function init(extensionPath: string): void {
  _extPath = extensionPath;
}

// Resolution order (motely-wasm@23.x, ESM, entry dist/index.mjs):
//   1. dist/motely-wasm/dist/index.mjs — staged into the VSIX
//   2. an open workspace's node_modules/motely-wasm — e.g. jaml-ui
function resolveWasmEntry(): string {
  const bundled = path.join(_extPath, "dist", "motely-wasm", "dist", "index.mjs");
  if (fs.existsSync(bundled)) return bundled;

  // Loading motely-wasm from an open folder's node_modules means executing that folder's code.
  // Gate it on workspace trust so an untrusted folder can't get its own module run via search.
  if (!vscode.workspace.isTrusted) {
    throw new Error(
      "Seed search needs a trusted workspace to load motely-wasm from node_modules. " +
        "Trust this workspace, or ship the WASM bundled in the extension.",
    );
  }

  for (const folder of vscode.workspace.workspaceFolders ?? []) {
    const entry = path.join(folder.uri.fsPath, "node_modules", "motely-wasm", "dist", "index.mjs");
    if (fs.existsSync(entry)) return entry;
  }

  throw new Error(
    "Motely WASM engine not found.\n\n" +
      "Install it in your workspace:\n" +
      "  npm install motely-wasm\n\n" +
      "or build the extension with the WASM assets staged into dist/motely-wasm/.",
  );
}

async function getWasm(): Promise<MotelyWasm> {
  if (!ready) {
    ready = (async () => {
      const entry = resolveWasmEntry();
      // Function()-wrapped import so tsc/bundlers don't try to resolve the specifier at build time.
      const url = pathToFileURL(entry).href;
      const mod = (await (Function("u", "return import(u)")(url) as Promise<MotelyWasm>));
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

// Public signature is shared with the notebook kernel and results panel. Binds to
// MotelyJaml.fromYaml + MotelySearch.searchRandom and the onProgress/onScoredResult event bus.
export async function runSearch(
  jaml: string,
  seedCount: number,
  onProgress: OnProgress,
  onResult: OnResult,
  onComplete: OnComplete,
): Promise<void> {
  if (inFlight) {
    throw new Error("A JAML search is already running. Stop it before starting another.");
  }
  const { MotelyJaml, MotelySearch } = await getWasm();

  inFlight = true;
  const token = { cancelled: false };
  activeToken = token;

  const results: SearchResult[] = [];
  const startMs = Date.now();

  const progressHandler = (p: MotelyProgress) => {
    if (!token.cancelled) onProgress(p.seedsSearched, p.matchingSeeds);
  };
  const resultHandler = (r: MotelyScoredSeedResult) => {
    if (token.cancelled) return;
    results.push({ seed: r.seed, score: r.score });
    onResult(r.seed, r.score);
  };

  MotelySearch.onProgress.subscribe(progressHandler);
  MotelySearch.onScoredResult.subscribe(resultHandler);

  try {
    const config = MotelyJaml.fromYaml(jaml);
    const finalResults = await MotelySearch.searchRandom(config, seedCount);
    // searchRandom resolves with the full result set; prefer it over the streamed accumulation
    // so the summary is authoritative even if an event was missed.
    const authoritative = (finalResults.length > 0 ? finalResults : undefined)?.map((r) => ({
      seed: r.seed,
      score: r.score,
    })) ?? results;
    onComplete({
      status: token.cancelled ? "stopped" : "done",
      searched: seedCount.toString(),
      matched: authoritative.length.toString(),
      results: authoritative.sort((a, b) => b.score - a.score).slice(0, 500),
      elapsedMs: Date.now() - startMs,
    });
  } finally {
    MotelySearch.onProgress.unsubscribe(progressHandler);
    MotelySearch.onScoredResult.unsubscribe(resultHandler);
    inFlight = false;
    if (activeToken === token) activeToken = null;
  }
}

// motely-wasm@23.x's search surface has no cancellation entry point — searchRandom is a single
// awaited call. Best effort: flip the active call's token so its callbacks stop reaching the UI.
// The engine keeps running to completion under the hood.
export function stopSearch(): void {
  if (activeToken) activeToken.cancelled = true;
}
