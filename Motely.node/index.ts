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
  shopQueue: ShopItemInfo[];
  packs: PackInfo[];
}

export interface SeedAnalysisInfo {
  seed: string;
  deck: string;
  stake: string;
  error?: string | null;
  antes: AnteAnalysisInfo[];
}

export interface SearchResultInfo {
  seed: string;
  score: number;
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
}

// ──────────────────────────────── Internals ────────────────────────────────

interface RawExports {
  GetVersionAsync(): Promise<string>;
  GetCapabilitiesAsync(): Promise<string>;
  AnalyzeSeedAsync(seed: string, deck: string, stake: string): Promise<string>;
  ValidateJamlAsync(jamlContent: string): Promise<string>;
  StartJamlSearch(
    jamlContent: string,
    optionsJson: string,
    onProgress: (progressJson: string) => void,
    onResult: (seed: string, score: number) => void,
  ): Promise<string>;
  StopSearch(): void;
  DisposeSearch(): Promise<void>;
}

function _dir(): string {
  return dirname(fileURLToPath(import.meta.url));
}

function buildApi(raw: RawExports, cachedCapabilities: CapabilitiesInfo): MotelyNodeApi {
  return {
    async getCapabilities(): Promise<CapabilitiesInfo> {
      return cachedCapabilities;
    },

    getAvailableThreadCount(): number {
      return cachedCapabilities.availableThreadCount;
    },

    async analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo> {
      const json = await raw.AnalyzeSeedAsync(seed, deck, stake);
      const result = JSON.parse(json);
      if (result.error && !result.seed) throw new Error(result.error);
      return result as SeedAnalysisInfo;
    },

    async validateJaml(jaml: string): Promise<ValidateResult> {
      const json = await raw.ValidateJamlAsync(jaml);
      return JSON.parse(json) as ValidateResult;
    },

    async startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]> {
      const { onProgress, onResult, ...searchParams } = options ?? {};
      const optionsJson = JSON.stringify({
        threadCount: 1,
        batchCharCount: 4,
        ...searchParams,
      });

      const results: SearchResultInfo[] = [];

      const progressCb = onProgress
        ? (json: string) => {
          const p = JSON.parse(json) as { seedsSearched: number; matchingSeeds: number; elapsedMs: number; resultCount: number };
          onProgress(p.seedsSearched, p.matchingSeeds, p.elapsedMs, p.resultCount);
        }
        : () => { };
      const resultCb = (seed: string, score: number) => {
        results.push({ seed, score });
        onResult?.(seed, score);
      };

      const response = JSON.parse(
        await raw.StartJamlSearch(jamlContent, optionsJson, progressCb, resultCb)
      ) as { error?: string };
      if (response.error) throw new Error(response.error);
      return results;
    },

    dispose(): void {
      void raw.DisposeSearch();
    },
  };
}

/**
 * Load the Motely WASM engine for Node.js. Call once at startup; reuse the returned API.
 */
export async function loadMotely(options?: LoadMotelyOptions): Promise<MotelyNodeApi> {
  const frameworkPath = options?.frameworkPath ?? join(_dir(), '_framework');
  const dotnetJsPath = join(frameworkPath, 'dotnet.js');
  const dotnetUrl = pathToFileURL(dotnetJsPath).href;
  const mod = await import(dotnetUrl) as { dotnet: { withDiagnosticTracing: (v: boolean) => { create: () => Promise<unknown> }; create?: () => Promise<unknown> } };
  const dotnet = mod.dotnet;
  const runtime = await dotnet.withDiagnosticTracing(false).create();
  const config = (runtime as { getConfig: () => { mainAssemblyName: string } }).getConfig();
  const allExports = (await (runtime as { getAssemblyExports: (name: string) => Promise<unknown> }).getAssemblyExports(config.mainAssemblyName)) as {
    Motely: { BrowserWasm: { MotelyWasmExports: RawExports } };
  };
  const raw = allExports.Motely.BrowserWasm.MotelyWasmExports;

  (runtime as { runMain: () => Promise<unknown> }).runMain?.().catch((err: unknown) => console.error('[motely-node] runMain failed:', err));

  const capabilitiesJson = await raw.GetCapabilitiesAsync();
  const cachedCapabilities = JSON.parse(capabilitiesJson) as CapabilitiesInfo;
  return buildApi(raw, cachedCapabilities);
}
