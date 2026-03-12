// Motely Node.js Package — WASM-based Balatro seed engine
// Wraps .NET WASM exports (Motely.SingleThread / Motely.BrowserWasm)

import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { pathToFileURL } from 'node:url';

// ──────────────────────────────── Public Types ────────────────────────────────

export interface VersionInfo {
  version: string;
  runtime: string;
  features: string[];
}

export interface CapabilitiesInfo {
  simd: boolean;
  threads: boolean;
  availableThreadCount: number;
  processorCount: number;
  runtime: string;
  version: string;
}

export interface ShopItemInfo {
  id: string;
  name: string;
}

export interface PackInfo {
  type: string;
  items: string[];
}

export interface AnteAnalysisInfo {
  ante: number;
  boss: string;
  voucher: string;
  smallBlindTag: string;
  bigBlindTag: string;
  drawOrder: string;
  shopQueue: ShopItemInfo[];
  packs: PackInfo[];
}

export interface SeedAnalysisInfo {
  seed: string;
  deck: string;
  stake: string;
  erraticDeckComposition: string[];
  error?: string | null;
  antes: AnteAnalysisInfo[];
}

export interface SearchResultInfo {
  seed: string;
  score: number;
  tallies?: string[] | null;
}

export interface ValidateResult {
  valid: boolean;
  error?: string | null;
  name?: string | null;
  deck?: string | null;
  stake?: string | null;
}

export interface SearchOptions {
  /** Thread count (default 1 — Node.js WASM is single-threaded) */
  threadCount?: number;
  /** Batch character count 1-7 (default 4) */
  batchCharCount?: number;
  randomSeeds?: number;
  cutoff?: string;
  startBatch?: number;
  endBatch?: number;
  specificSeed?: string;
  palindrome?: boolean;
  /** Called every ~2 seconds by the .NET search engine with current progress. */
  onProgress?: (seedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
  /** Called each time a matching seed is found during search. */
  onResult?: (seed: string, score: number) => void;
}

// ──────────────────────────────── Public API ────────────────────────────────

export interface MotelyNodeApi {
  /** Get runtime capabilities (SIMD, threads, etc.) */
  getCapabilities(): Promise<CapabilitiesInfo>;

  /** Get the number of threads the runtime can actually use */
  getAvailableThreadCount(): number;

  /** Analyze a single seed. Returns full ante-by-ante breakdown. */
  analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;

  /** Validate a JAML filter string. */
  validateJaml(jamlContent: string): Promise<ValidateResult>;

  /**
   * Run a JAML search. Resolves with all matching seeds when search completes.
   * onProgress is called ~every 2s with stats; onResult is called per match as found.
   */
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]>;

  /** Stop any running search and release resources. */
  dispose(): void;
}

export interface LoadMotelyOptions {
  /** Path to the folder containing dotnet.js (default: package _framework). */
  frameworkPath?: string;
  addonPath?: string;
  pollIntervalMs?: number;
}

// ──────────────────────────────── Internals ────────────────────────────────

interface RawExports {
  getVersionAsync(): Promise<string>;
  getCapabilitiesAsync(): Promise<string>;
  analyzeSeedAsync(seed: string, deck: string, stake: string): Promise<string>;
  validateJamlAsync(jamlContent: string): Promise<string>;
  startJamlSearch(
    jamlContent: string,
    optionsJson: string,
  ): Promise<string>;
  getSearchStatus(): Promise<string>;
  stopSearch(): void;
  disposeSearch(): Promise<void>;
}

interface SearchStatusInfo {
  error?: string | null;
  status: string;
  isRunning: boolean;
  totalSeedsSearched: number;
  matchingSeeds: number;
  resultCount: number;
  elapsedMs: number;
  results: SearchResultInfo[];
}

function _dir(): string {
  return dirname(fileURLToPath(import.meta.url));
}

function buildApi(raw: RawExports, cachedCapabilities: CapabilitiesInfo, pollIntervalMs: number): MotelyNodeApi {
  let disposed = false

  return {
    async getCapabilities(): Promise<CapabilitiesInfo> {
      return cachedCapabilities;
    },

    getAvailableThreadCount(): number {
      return cachedCapabilities.availableThreadCount;
    },

    async analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo> {
      const json = await raw.analyzeSeedAsync(seed, deck, stake);
      const result = JSON.parse(json);
      if (result.error && !result.seed) throw new Error(result.error);
      return result as SeedAnalysisInfo;
    },

    async validateJaml(jaml: string): Promise<ValidateResult> {
      const json = await raw.validateJamlAsync(jaml);
      return JSON.parse(json) as ValidateResult;
    },

    async startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]> {
      if (disposed) {
        throw new Error('Motely instance has been disposed');
      }

      const { onProgress, onResult, ...searchParams } = options ?? {};
      const optionsJson = JSON.stringify({
        threadCount: Math.max(1, searchParams.threadCount ?? cachedCapabilities.availableThreadCount ?? 1),
        batchCharCount: 4,
        palindrome: searchParams.palindrome ?? !(searchParams.specificSeed || searchParams.randomSeeds),
        ...searchParams,
      });

      const results: SearchResultInfo[] = [];

      const seen = new Set<string>()

      const applyStatus = (status: SearchStatusInfo) => {
        onProgress?.(status.totalSeedsSearched, status.matchingSeeds, status.elapsedMs, status.resultCount)

        for (const result of status.results ?? []) {
          const key = `${result.seed}:${result.score}`
          if (seen.has(key)) continue
          seen.add(key)
          results.push(result)
          onResult?.(result.seed, result.score)
        }
      }

      const completionPromise = raw.startJamlSearch(jamlContent, optionsJson)
        .then(json => ({ kind: 'done' as const, json }))
        .catch(error => ({ kind: 'error' as const, error }))

      while (true) {
        const next = await Promise.race([
          completionPromise,
          new Promise<{ kind: 'tick' }>(resolve => setTimeout(() => resolve({ kind: 'tick' }), pollIntervalMs)),
        ])

        if (next.kind === 'error') {
          throw next.error
        }

        if (next.kind === 'done') {
          const status = JSON.parse(next.json) as SearchStatusInfo
          if (status.error) throw new Error(status.error)
          applyStatus(status)
          return results
        }

        const statusJson = await raw.getSearchStatus()
        const status = JSON.parse(statusJson) as SearchStatusInfo
        if (status.error) {
          if (status.error === 'No active search') continue
          throw new Error(status.error)
        }

        applyStatus(status)
      }
    },

    dispose(): void {
      disposed = true
      try {
        raw.stopSearch()
      } catch {
      }
      void raw.disposeSearch()
    },
  };
}

/**
 * Load the Motely WASM engine for Node.js. Call once at startup; reuse the returned API.
 */
export async function loadMotely(options?: LoadMotelyOptions): Promise<MotelyNodeApi> {
  const addonModulePath = options?.addonPath
    ?? (options?.frameworkPath
      ? join(options.frameworkPath, 'Motely.NodeAddon.mjs')
      : join(_dir(), 'addon', 'Motely.NodeAddon.mjs'))

  const addonUrl = pathToFileURL(addonModulePath).href
  const mod = await import(addonUrl) as { MotelyNodeExports: RawExports }
  const raw = mod.MotelyNodeExports

  const capabilitiesJson = await raw.getCapabilitiesAsync()
  const cachedCapabilities = JSON.parse(capabilitiesJson) as CapabilitiesInfo
  return buildApi(raw, cachedCapabilities, Math.max(25, options?.pollIntervalMs ?? 100))
}
