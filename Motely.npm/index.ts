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
  availableThreadCount: number;
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
  error?: string | null;
  antes: AnteAnalysisInfo[];
}

export interface SearchResultInfo {
  seed: string;
  score: number;
  tallies?: string[] | null;
}

export interface SearchStatusInfo {
  filterId: string;
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
  batchCharCount?: number;
  startBatch?: number;
  endBatch?: number;
  cutoff?: string;
  specificSeed?: string;
  seeds?: string[];
  keyword?: string;
  keywords?: string[];
  padding?: string;
  randomSeeds?: number;
  palindrome?: boolean;
  /** Called with native primitives every ~15ms during search. No JSON overhead. */
  onProgress?: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number) => void;
  /** Called with native primitives for each new result found. No JSON overhead. */
  onResult?: (seed: string, score: number) => void;
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

  /** Get the number of threads the runtime can actually use */
  getAvailableThreadCount(): number;

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
   * Only ONE search can run at a time. Starting a new search cancels any existing one.
   * @param jamlContent - The JAML filter content
   * @param options - Search parameters + onProgress/onResult callbacks
   * @returns Promise resolving to final search status with results
   */
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchStatusInfo>;

  /** Stop the current running search (non-blocking, sets cancellation flag) */
  stopSearch(): void;

  /** Dispose the current search and free memory. Returns Promise. */
  disposeSearch(): Promise<void>;
}

export interface LoadMotelyOptions {
  /** Base URL for runtime assets. Defaults to this package's bundled framework folder. */
  baseUrl?: string;
  /**
   * Threading mode.
   * - "auto": use threads when cross-origin isolation is available, otherwise fall back to the single-thread bundle
   * - "on": require the threaded bundle
   * - "off": force the single-thread bundle
   */
  threads?: "auto" | "on" | "off";
}

// ──────────────────────────────── Raw Export Shape ────────────────────────────────

/**
 * Shape of the raw [JSExport] methods from .NET.
 * onProgress receives primitives directly via JSMarshalAs - no JSON.
 * See: https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app
 */
interface RawExports {
  GetVersionAsync(): Promise<string>;
  GetCapabilitiesAsync(): Promise<string>;
  AnalyzeSeedAsync(seed: string, deck: string, stake: string): Promise<string>;
  ValidateJamlAsync(jamlContent: string): Promise<string>;
  StartJamlSearch(
    jamlContent: string,
    optionsJson: string,
    onProgress: (seedsSearched: number, matchingSeeds: number, elapsedMs: number) => void,
    onResult: (seed: string, score: number) => void,
  ): Promise<string>;
  StopSearch(): void;
  DisposeSearch(): Promise<void>;
}

function resolveFrameworkUrl(baseUrl: string | undefined, frameworkFolder: "_framework" | "_framework_st"): string {
  if (baseUrl) {
    return (baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl) || baseUrl;
  }

  if (frameworkFolder === "_framework_st") {
    return new URL("./_framework_st/dotnet.js", import.meta.url).href.replace(/\/dotnet\.js$/, "");
  }

  return new URL("./_framework/dotnet.js", import.meta.url).href.replace(/\/dotnet\.js$/, "");
}

// ──────────────────────────────── Loader ────────────────────────────────
/**
 * Load the Motely WASM runtime and return the API.
 * Call once at app startup; the returned object is reusable.
 *
 * The _framework/dotnet.js entry point is the standard .NET WASM boot file.
 * See: https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md
 */
export async function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi> {
  // Diagnostic: warn if cross-origin isolation is missing (threads + SharedArrayBuffer require it)
  const supportsIsolation = typeof globalThis.crossOriginIsolated === "undefined" || globalThis.crossOriginIsolated;
  const threadingMode = options?.threads ?? "auto";

  if (!supportsIsolation && threadingMode !== "off") {
    console.warn(
      "[motely-wasm] crossOriginIsolated is false. " +
      (threadingMode === "on"
        ? "Multi-threading and SharedArrayBuffer are REQUIRED by threads: \"on\" and will likely fail to initialize. "
        : "Multi-threading and SharedArrayBuffer are DISABLED. Falling back to the single-thread runtime bundle. ") +
      "Your server must send these headers on ALL responses:\n" +
      "  Cross-Origin-Opener-Policy: same-origin\n" +
      "  Cross-Origin-Embedder-Policy: require-corp\n" +
      "See: https://web.dev/articles/coop-coep"
    );
  }

  const frameworkFolder = threadingMode === "off" || (!supportsIsolation && threadingMode === "auto")
    ? "_framework_st"
    : "_framework";
  const url = resolveFrameworkUrl(options?.baseUrl, frameworkFolder);
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
    getAvailableThreadCount: () => cachedCapabilities.availableThreadCount,
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

      const withDefaults = {
        threadCount: Math.max(1, cachedCapabilities.availableThreadCount),
        batchCharCount: 4,
        ...searchParams,
      };
      const optionsJson = JSON.stringify(withDefaults);

      const progressCb = onProgress ?? (() => { });
      const resultCb = onResult ?? (() => { });

      const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson, progressCb, resultCb);

      const result = JSON.parse(resultJson);
      if (result.error) throw new Error(result.error);
      return result as SearchStatusInfo;
    },

    stopSearch: () => { raw.StopSearch(); },
    disposeSearch: () => raw.DisposeSearch(),
  };

  return api;
}

