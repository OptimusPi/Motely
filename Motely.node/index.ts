// Motely Node.js Package Entry Point
// Prefer node-api-dotnet addon when addonPath is set; otherwise use browser WASM build with Node.js runtime.
// Per https://microsoft.github.io/node-api-dotnet/reference/js/ and scenarios/js-dotnet-module.html

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

export interface SearchProgressInfo {
  type: 'progress';
  seedsSearched: number;
  matchingSeeds: number;
  elapsedMs: number;
  resultCount: number;
}

export interface SearchCompleteInfo {
  type: 'done';
  searchId: string;
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
  onProgress?: (seedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
  onResult?: (seed: string, score: number) => void;
}

export interface ErrorResult {
  error: string;
}

// ──────────────────────────────── Public API ────────────────────────────────

export interface MotelyNodeApi {
  /** Get runtime capabilities (SIMD, threads, etc.) */
  getCapabilities(): Promise<CapabilitiesInfo>;

  /** Analyze a single seed. Returns full ante-by-ante breakdown. */
  analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;

  /** Validate a JAML filter string. */
  validateJaml(jamlContent: string): Promise<ValidateResult>;

  /**
   * Start a JAML search. Returns a Promise that resolves when search completes.
   * Progress is pushed to onProgress/onResult callbacks.
   */
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<void>;

  /** Dispose and cleanup */
  dispose(): void;
}

// ──────────────────────────────── Raw .NET exports (addon or WASM) ────────────────────────────────

// Raw exports from .NET (WASM or node-api-dotnet addon)
interface RawExports {
  GetVersionAsync(): Promise<string>;
  GetCapabilitiesAsync(): Promise<string>;
  AnalyzeSeedAsync(seed: string, deck: string, stake: string): Promise<string>;
  ValidateJamlAsync(jamlContent: string): Promise<string>;
  StartJamlSearch(jamlContent: string, optionsJson: string): Promise<string>;
  GetSearchStatus(): Promise<string>;
  StopSearch(): void;
  DisposeSearch(): Promise<void>;
}

// Global callbacks for .NET [JSImport]
declare global {
  var __motelyOnProgress: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
  var __motelyOnResult: (seed: string, score: number) => void;
}

/** ESM: use import.meta.url. CJS (esbuild output): use __dirname. */
declare const __dirname: string;
function _dir(): string {
  if (typeof import.meta !== 'undefined' && (import.meta as { url?: string }).url) {
    return dirname(fileURLToPath((import.meta as { url: string }).url));
  }
  return typeof __dirname !== 'undefined' ? __dirname : '.';
}

export interface LoadMotelyOptions {
  /** When set, load the .NET addon via node-api-dotnet (in-process). Requires dependency "node-api-dotnet" and a built Motely.NodeAddon.dll. */
  addonPath?: string;
  /** When using WASM, path to the folder containing dotnet.js (default: package _framework). */
  frameworkPath?: string;
}

/**
 * Build the unified API from raw .NET exports (WASM or addon).
 */
function buildApi(raw: RawExports, cachedCapabilities: CapabilitiesInfo): MotelyNodeApi {
  return {
    async getCapabilities(): Promise<CapabilitiesInfo> {
      return cachedCapabilities;
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

    async startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<void> {
      const { onProgress, onResult, ...searchParams } = options ?? {};
      const withDefaults = {
        threadCount: 1,
        batchCharCount: 4,
        ...searchParams,
      };
      const optionsJson = JSON.stringify(withDefaults);

      const isAddon = typeof (raw as RawExports & { GetSearchStatus?: () => Promise<string> }).GetSearchStatus === 'function';

      if (isAddon && (onProgress || onResult)) {
        // Addon: poll GetSearchStatus at 1s. Batches are 35^batchCharCount seeds; keep marshalling non-chatty.
        const startPromise = raw.StartJamlSearch(jamlContent, optionsJson);
        const POLL_MS = 1000;
        const interval = setInterval(async () => {
          try {
            const statusJson = await (raw as RawExports).GetSearchStatus!();
            const status = JSON.parse(statusJson) as {
              isRunning?: boolean;
              totalSeedsSearched?: number;
              matchingSeeds?: number;
              elapsedMs?: number;
              results?: { seed: string; score: number }[];
              error?: string;
            };
            if (status.error) return;
            if (onProgress)
              onProgress(
                status.totalSeedsSearched ?? 0,
                status.matchingSeeds ?? 0,
                status.elapsedMs ?? 0,
                status.results?.length ?? 0
              );
            if (onResult && status.results)
              for (const r of status.results) onResult(r.seed, r.score);
            if (!status.isRunning) clearInterval(interval);
          } catch {
            /* ignore */
          }
        }, POLL_MS);
        try {
          await startPromise;
        } finally {
          clearInterval(interval);
        }
      } else if (isAddon) {
        const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);
        const result = JSON.parse(resultJson);
        if (result.error) throw new Error(result.error);
      } else {
        // WASM only: .NET calls back via global __motelyOnProgress / __motelyOnResult ([JSImport]).
        globalThis.__motelyOnProgress = onProgress ?? (() => {});
        globalThis.__motelyOnResult = onResult ?? (() => {});
        try {
          const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);
          const result = JSON.parse(resultJson);
          if (result.error) throw new Error(result.error);
        } finally {
          globalThis.__motelyOnProgress = () => {};
          globalThis.__motelyOnResult = () => {};
        }
      }
    },

    dispose: () => {
      void raw.DisposeSearch();
    },
  };
}

/**
 * Load Motely for Node.js. Prefer node-api-dotnet addon when addonPath is set; otherwise use WASM.
 * Call once at app startup; the returned object is reusable.
 * @see https://microsoft.github.io/node-api-dotnet/reference/js/
 */
export async function loadMotely(options?: LoadMotelyOptions): Promise<MotelyNodeApi> {
  if (options?.addonPath) {
    const dotnet = await import('node-api-dotnet');
    const m = dotnet.require(options.addonPath) as Record<string, RawExports> | RawExports;
    const raw: RawExports = (m as Record<string, RawExports>).MotelyNodeExports ?? (m as RawExports);
    const [versionJson, capabilitiesJson] = await Promise.all([
      raw.GetVersionAsync(),
      raw.GetCapabilitiesAsync(),
    ]);
    const cachedCapabilities = JSON.parse(capabilitiesJson) as CapabilitiesInfo;
    return buildApi(raw, cachedCapabilities);
  }

  // WASM path
  globalThis.__motelyOnProgress = () => {};
  globalThis.__motelyOnResult = () => {};

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

  const [versionJson, capabilitiesJson] = await Promise.all([
    raw.GetVersionAsync(),
    raw.GetCapabilitiesAsync(),
  ]);
  const cachedCapabilities = JSON.parse(capabilitiesJson) as CapabilitiesInfo;
  return buildApi(raw, cachedCapabilities);
}
