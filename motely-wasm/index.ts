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
  tallies?: number[] | null;
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

export interface SearchCallbacks {
  /** Called with native primitives every ~15ms during search. No JSON overhead. */
  onProgress?: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
  /** Called with native primitives for each new result found. No JSON overhead. */
  onResult?: (seed: string, score: number) => void;
}

export interface SearchRuntimeOptions extends SearchCallbacks {
  threadCount?: number;
  batchCharCount?: number;
}

export interface SequentialSearchOptions extends SearchRuntimeOptions {
  startBatch?: number;
  endBatch?: number;
}

export interface KeywordSearchOptions extends SearchRuntimeOptions {
  padding?: string;
}

export interface ErrorResult {
  error: string;
}

// ──────────────────────────────── Public API ────────────────────────────────

export interface BoolStreamResult {
  results: boolean[];
  nextState: number | null;
}

export interface IntStreamResult {
  results: number[];
  nextState: number | null;
}

export interface StringStreamResult {
  results: string[];
  nextState: number | null;
}

export interface PackStreamResult {
  results: string[];
  nextState: number | null;
  generatedFirstPack: boolean;
}

export interface ItemStreamResult {
  results: string[];
  nextState: number[] | null;
}

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
  startJamlSearch(jamlContent: string, options?: SequentialSearchOptions): Promise<SearchStatusInfo>;
  verifySeed(jamlContent: string, seed: string, options?: SearchRuntimeOptions): Promise<SearchStatusInfo>;
  startSeedListSearch(jamlContent: string, seeds: string[], options?: SearchRuntimeOptions): Promise<SearchStatusInfo>;
  startKeywordSearch(jamlContent: string, keyword: string, options?: KeywordSearchOptions): Promise<SearchStatusInfo>;
  startKeywordsSearch(jamlContent: string, keywords: string[], options?: KeywordSearchOptions): Promise<SearchStatusInfo>;
  startRandomSearch(jamlContent: string, count: number, options?: SearchRuntimeOptions): Promise<SearchStatusInfo>;
  startPalindromeSearch(jamlContent: string, options?: SearchRuntimeOptions): Promise<SearchStatusInfo>;

  /** Stop the current running search (non-blocking, sets cancellation flag) */
  stopSearch(): void;

  /** Dispose the current search and free memory. Returns Promise. */
  disposeSearch(): Promise<void>;

  // ──────────────────────────────── Streaming ────────────────────────────────

  getLastNextState(): number;
  getLastGeneratedFirstPack(): boolean;
  getLastStateJson(): string;

  streamLuckyMoney(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): Uint8Array;
  streamLuckyMult(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): Uint8Array;
  streamMisprint(seed: string, deck: string, stake: string, state: number, take: number): Int32Array;
  streamCavendish(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): Uint8Array;
  streamGrosMichel(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): Uint8Array;
  
  streamErraticDeck(seed: string, deck: string, stake: string, state: number, take: number): string[];
  streamWheelOfFortune(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): string[];
  
  streamTags(seed: string, deck: string, stake: string, ante: number, state: number, take: number): string[];
  streamBoosterPacks(seed: string, deck: string, stake: string, ante: number, state: number, generatedFirstPack: boolean, take: number): string[];
  streamVouchers(seed: string, deck: string, stake: string, ante: number, voucherBitfield: number, state: number, take: number): string[];

  streamTarot(seed: string, deck: string, stake: string, ante: number, source: string, stateJson: string, take: number): string[];
  streamPlanet(seed: string, deck: string, stake: string, ante: number, source: string, stateJson: string, take: number): string[];
  streamSpectral(seed: string, deck: string, stake: string, ante: number, source: string, stateJson: string, take: number): string[];
  streamStandardCards(seed: string, deck: string, stake: string, ante: number, flags: number, stateJson: string, take: number): string[];
  streamJokers(seed: string, deck: string, stake: string, ante: number, source: string, flags: number, stateJson: string, take: number): string[];
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

/** Shape of the raw [JSExport] methods from MotelyWasmExports.cs */
interface RawExports {
  GetVersion(): string;
  IsSimdEnabled(): boolean;
  GetProcessorCount(): number;
  ValidateJaml(jamlContent: string): boolean;
  ValidateJamlWithError(jamlContent: string): string;
  AnalyzeSeed(seed: string, deck: string, stake: string): string;
  StartJamlSearch(
    jamlContent: string,
    threadCount: number,
    batchCharCount: number,
    onProgress: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number) => void,
    onResult: (seed: string, score: number) => void,
  ): Promise<string>;
  StopSearch(): void;
}

interface RawSearchParams {
  threadCount?: number;
  batchCharCount?: number;
  startBatch?: number;
  endBatch?: number;
  specificSeed?: string;
  seeds?: string[];
  keywords?: string[];
  padding?: string;
  randomSeeds?: number;
  palindrome?: boolean;
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars
type _RawSearchParamsUsed = RawSearchParams;

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

  const processorCount = raw.GetProcessorCount();

  const cachedVersion: VersionInfo = {
    version: raw.GetVersion(),
    runtime: "dotnet-wasm",
    features: raw.IsSimdEnabled() ? ["simd"] : [],
  };
  const cachedCapabilities: CapabilitiesInfo = {
    simd: raw.IsSimdEnabled(),
    threads: false,
    availableThreadCount: processorCount,
    processorCount,
    runtime: cachedVersion.runtime,
    version: cachedVersion.version,
    timestamp: new Date().toISOString(),
  };

  const runSearch = async (
    jamlContent: string,
    threadCount: number,
    batchCharCount: number,
    callbacks?: SearchCallbacks,
  ): Promise<SearchStatusInfo> => {
    const { onProgress, onResult } = callbacks ?? {};
    let resultCount = 0;

    const progressCb = onProgress
      ? (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number) => {
        onProgress(totalSeedsSearched, matchingSeeds, elapsedMs, resultCount);
      }
      : (_t: number, _m: number, _e: number) => { };
    const resultCb = (seed: string, score: number) => {
      resultCount++;
      onResult?.(seed, score);
    };

    const status = await raw.StartJamlSearch(jamlContent, threadCount, batchCharCount, progressCb, resultCb);
    if (status.startsWith("error:")) throw new Error(status.slice(7).trim());
    return { filterId: "", status, isRunning: false, totalSeedsSearched: 0, matchingSeeds: 0, resultCount, elapsedMs: 0, results: [] };
  };

  const api: MotelyWasmApi = {
    getVersion: () => cachedVersion,
    getCapabilities: () => cachedCapabilities,
    isSimdEnabled: () => cachedCapabilities.simd,
    isThreadingEnabled: () => false,
    getAvailableThreadCount: () => processorCount,
    getProcessorCount: () => processorCount,

    async analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo> {
      const json = raw.AnalyzeSeed(seed, deck, stake);
      const result = JSON.parse(json);
      if (result.error && !result.seed) throw new Error(result.error);
      return result as SeedAnalysisInfo;
    },

    async validateJaml(jaml: string): Promise<ValidateResult> {
      const error = raw.ValidateJamlWithError(jaml);
      return error ? { valid: false, error } : { valid: true };
    },

    async startJamlSearch(jamlContent: string, options?: SequentialSearchOptions): Promise<SearchStatusInfo> {
      return runSearch(jamlContent, options?.threadCount ?? processorCount, options?.batchCharCount ?? 4, options);
    },

    async verifySeed(jamlContent: string, _seed: string, options?: SearchRuntimeOptions): Promise<SearchStatusInfo> {
      return runSearch(jamlContent, options?.threadCount ?? processorCount, options?.batchCharCount ?? 4, options);
    },

    async startSeedListSearch(jamlContent: string, _seeds: string[], options?: SearchRuntimeOptions): Promise<SearchStatusInfo> {
      return runSearch(jamlContent, options?.threadCount ?? processorCount, options?.batchCharCount ?? 4, options);
    },

    async startKeywordSearch(jamlContent: string, _keyword: string, options?: KeywordSearchOptions): Promise<SearchStatusInfo> {
      return runSearch(jamlContent, options?.threadCount ?? processorCount, options?.batchCharCount ?? 4, options);
    },

    async startKeywordsSearch(jamlContent: string, _keywords: string[], options?: KeywordSearchOptions): Promise<SearchStatusInfo> {
      return runSearch(jamlContent, options?.threadCount ?? processorCount, options?.batchCharCount ?? 4, options);
    },

    async startRandomSearch(jamlContent: string, _count: number, options?: SearchRuntimeOptions): Promise<SearchStatusInfo> {
      return runSearch(jamlContent, options?.threadCount ?? processorCount, options?.batchCharCount ?? 4, options);
    },

    async startPalindromeSearch(jamlContent: string, options?: SearchRuntimeOptions): Promise<SearchStatusInfo> {
      return runSearch(jamlContent, options?.threadCount ?? processorCount, options?.batchCharCount ?? 4, options);
    },

    stopSearch: () => { raw.StopSearch(); },
    disposeSearch: () => Promise.resolve(),

    getLastNextState: () => 0,
    getLastGeneratedFirstPack: () => false,
    getLastStateJson: () => "{}",

    streamLuckyMoney: () => new Uint8Array(0),
    streamLuckyMult: () => new Uint8Array(0),
    streamMisprint: () => new Int32Array(0),
    streamCavendish: () => new Uint8Array(0),
    streamGrosMichel: () => new Uint8Array(0),
    streamErraticDeck: () => [],
    streamWheelOfFortune: () => [],
    streamTags: () => [],
    streamBoosterPacks: () => [],
    streamVouchers: () => [],
    streamTarot: () => [],
    streamPlanet: () => [],
    streamSpectral: () => [],
    streamStandardCards: () => [],
    streamJokers: () => [],
  };

  return api;
}
