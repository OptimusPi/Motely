// Motely WASM Package Entry Point
// Thin loader that calls [JSExport] methods and parses JSON responses.
// The .NET WASM runtime is the source of truth; this is just the JS bridge.

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
  timestamp: string;
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
  twos: number;
  error?: string | null;
  antes: AnteAnalysisInfo[];
}

export interface SearchResultInfo {
  seed: string;
  score: number;
  tallies?: number[] | null;
}

export interface SearchStatusInfo {
  searchId: string;
  status: string;
  isRunning: boolean;
  totalSeedsSearched: number;
  matchingSeeds: number;
  resultCount: number;
  elapsedMs: number;
  results: SearchResultInfo[];
  error?: string | null;
}

export interface ValidateResult {
  valid: boolean;
  error?: string | null;
  name?: string | null;
  deck?: string | null;
  stake?: string | null;
}

export interface SearchOptions {
  threadCount?: number;
  batchSize?: number;
  startBatch?: number;
  endBatch?: number;
  cutoff?: string;
  specificSeed?: string;
  randomSeeds?: number;
  palindrome?: boolean;
  /** Called with native primitives every ~500ms during search. No JSON overhead. */
  onProgress?: (searchId: string, totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
  /** Called with native primitives for each new result found. No JSON overhead. */
  onResult?: (searchId: string, seed: string, score: number) => void;
}

export interface ErrorResult {
  error: string;
}

// ──────────────────────────────── Public API ────────────────────────────────

export interface MotelyWasmApi {
  /** Get version and feature info */
  getVersion(): VersionInfo;

  /** Get runtime capabilities (SIMD, threads, etc.) */
  getCapabilities(): CapabilitiesInfo;

  /** Check if SIMD is hardware-accelerated */
  isSimdEnabled(): boolean;

  /** Check if threading is enabled */
  isThreadingEnabled(): boolean;

  /** Get available processor count */
  getProcessorCount(): number;

  /**
   * Analyze a single seed. Returns full ante-by-ante breakdown.
   * @throws If the result contains an `error` field
   */
  analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;

  /**
   * Validate a JAML filter string.
   */
  validateJaml(jamlContent: string): Promise<ValidateResult>;

  /**
   * Start a JAML search. Returns a Promise that resolves with final SearchStatusInfo.
   * Progress is pushed to onProgress/onResult callbacks (native primitives, no JSON).
   * @param jamlContent - The JAML filter content
   * @param options - Search parameters + onProgress/onResult callbacks
   * @returns Promise resolving to final search status with results
   */
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchStatusInfo>;

  /**
   * Get status and top results for an active search (on-demand query).
   * @param searchId - The search ID
   * @param resultLimit - Max results to include (default 50)
   */
  getSearchStatus(searchId: string, resultLimit?: number): Promise<SearchStatusInfo>;

  /** Stop a running search (non-blocking, sets cancellation flag) */
  stopSearch(searchId: string): void;

  /** Dispose a completed/stopped search and free memory. Returns Promise. */
  disposeSearch(searchId: string): Promise<void>;
}

export interface LoadMotelyOptions {
  /** Base URL for _framework (e.g. "/_framework" or "https://cdn.example/assets"). Default "/_framework". */
  baseUrl?: string;
  /**
   * Threading mode. Only the threaded build (_framework) is shipped.
   * - "auto": use _framework (default)
   * - "on": use _framework
   */
  threads?: "auto" | "on";
}

// ──────────────────────────────── Raw Export Shape ────────────────────────────────

/** Shape of the raw [JSExport] methods from .NET (async where required for threaded WASM) */
interface RawExports {
  GetVersionAsync(): Promise<string>;
  GetCapabilitiesAsync(): Promise<string>;
  AnalyzeSeedAsync(seed: string, deck: string, stake: string): Promise<string>;
  ValidateJamlAsync(jamlContent: string): Promise<string>;
  StartJamlSearch(jamlContent: string, optionsJson: string): Promise<string>;
  GetSearchStatusAsync(searchId: string, resultLimit: number): Promise<string>;
  StopSearchAsync(searchId: string): Promise<void>;
  DisposeSearch(searchId: string): Promise<void>;
}

// ──────────────────────────────── Loader ────────────────────────────────

// Default no-op callbacks. C# [JSImport] calls these via globalThis.
declare global {
  var __motelyOnProgress: (searchId: string, totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
  var __motelyOnResult: (searchId: string, seed: string, score: number) => void;
}
/**
 * Load the Motely WASM runtime and return the API.
 * Call once at app startup; the returned object is reusable.
 *
 * The _framework/dotnet.js entry point is the standard .NET WASM boot file.
 * See: https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md
 */
export async function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi> {
  // Install no-op callbacks before the runtime boots so [JSImport] bindings resolve
  globalThis.__motelyOnProgress = () => {};
  globalThis.__motelyOnResult = () => {};

  const defaultBase = "/_framework";
  const base = (options?.baseUrl ?? defaultBase).replace(/\/$/, "") || defaultBase;
  const origin = typeof window !== "undefined" ? window.location.origin : "https://localhost";
  const url = base.startsWith("http") ? base : new URL(base, origin).href;
  const dotnetUrl = `${url}/dotnet.js`;

  // Dynamic import of the .NET WASM entry point.
  // @vite-ignore / webpackIgnore prevent bundlers from analyzing the URL.
  // With standard boot config (no WasmBundlerFriendlyBootConfig), dotnet.js uses fetch() for .wasm/.dat at runtime.
  const { dotnet } = await import(
    /* @vite-ignore */ /* webpackIgnore: true */ dotnetUrl
  ) as {
    dotnet: {
      withDiagnosticTracing(enabled: boolean): any;
      create(): Promise<{
        getAssemblyExports(assemblyName: string): Promise<any>;
        getConfig(): { mainAssemblyName: string };
        runMain(): Promise<void>;
      }>;
    };
  };

  const runtime = await dotnet.create();
  const config = runtime.getConfig();
  const allExports = await runtime.getAssemblyExports(config.mainAssemblyName);

  const raw: RawExports = allExports.Motely.BrowserWasm.MotelyWasmExports;

  runtime.runMain().catch(err => console.error("[motely-wasm] runMain failed:", err));

  const [versionJson, capabilitiesJson] = await Promise.all([
    raw.GetVersionAsync(),
    raw.GetCapabilitiesAsync(),
  ]);
  const cachedVersion = JSON.parse(versionJson) as VersionInfo;
  const cachedCapabilities = JSON.parse(capabilitiesJson) as CapabilitiesInfo;

  const api: MotelyWasmApi = {
    getVersion: () => cachedVersion,
    getCapabilities: () => cachedCapabilities,
    isSimdEnabled: () => cachedCapabilities.simd,
    isThreadingEnabled: () => cachedCapabilities.threads,
    getProcessorCount: () => cachedCapabilities.processorCount,

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

    async startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchStatusInfo> {
      const { onProgress, onResult, ...searchParams } = options ?? {};
      globalThis.__motelyOnProgress = onProgress ?? (() => {});
      globalThis.__motelyOnResult = onResult ?? (() => {});

      const optionsJson = Object.keys(searchParams).length > 0
        ? JSON.stringify(searchParams) : "{}";

      const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);

      globalThis.__motelyOnProgress = () => {};
      globalThis.__motelyOnResult = () => {};

      const result = JSON.parse(resultJson);
      if (result.error && !result.searchId) throw new Error(result.error);
      return result as SearchStatusInfo;
    },

    async getSearchStatus(searchId: string, resultLimit?: number): Promise<SearchStatusInfo> {
      const json = await raw.GetSearchStatusAsync(searchId, resultLimit ?? 50);
      const result = JSON.parse(json);
      if (result.error && !result.searchId) throw new Error(result.error);
      return result as SearchStatusInfo;
    },

    stopSearch: (searchId: string) => { raw.StopSearchAsync(searchId).catch(() => {}); },
    disposeSearch: (searchId: string) => raw.DisposeSearch(searchId),
  };

  return api;
}
