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
    /** Called with native primitives every ~15ms during search. No JSON overhead. */
    onProgress?: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
    /** Called with native primitives for each new result found. No JSON overhead. */
    onResult?: (seed: string, score: number) => void;
}
export interface ErrorResult {
    error: string;
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
    /** Base URL for _framework (e.g. "/_framework" or "https://cdn.example/assets"). Default "/_framework". */
    baseUrl?: string;
    /**
     * Threading mode. Only the threaded build (_framework) is shipped.
     * - "auto": use _framework (default)
     * - "on": use _framework
     */
    threads?: "auto" | "on";
}
declare global {
    var __motelyOnProgress: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
    var __motelyOnResult: (seed: string, score: number) => void;
}
/**
 * Load the Motely WASM runtime and return the API.
 * Call once at app startup; the returned object is reusable.
 *
 * The _framework/dotnet.js entry point is the standard .NET WASM boot file.
 * See: https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md
 */
export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi>;
